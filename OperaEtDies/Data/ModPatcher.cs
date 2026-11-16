using System.Text.Json.Nodes;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// Applies the operations in a mod file to the contents of a data file, before the data file is read.
/// </summary>
public static class ModPatcher
{
	public const string InsertOperation = "insert";
	public const string ReplaceOperation = "replace";
	public const string SetOperation = "set";
	public const string RemoveOperation = "remove";
	public const string MoveOperation = "move";
	public const string CopyOperation = "copy";

	/// <summary>
	/// The property of a group that holds its operations.
	/// </summary>
	public const string GroupOperationsName = "operations";

	/// <summary>
	/// Applies each operation (and group of operations) in a mod file to the root of a data file, in order. Operations
	/// can't change the protected paths. Returns a user-facing message for each problem found; operations that have
	/// problems are skipped.
	/// </summary>
	public static IReadOnlyList<string> Apply(JsonNode root, JsonNode? modFile, IReadOnlyCollection<string> protectedPaths)
	{
		var errors = new List<string>();
		if (modFile is JsonArray operations)
			ApplyOperations(root, operations, null, protectedPaths, errors);
		else
			errors.Add(OurResources.ModFileNotList);

		return errors;
	}

	/// <summary>
	/// Applies a list of operations to a root, in order, adding a message for each problem found. Operations in the
	/// changes of a copy, or in a group, are numbered after the item that holds them, as in "3.2". Groups are only
	/// allowed at the top of a mod file.
	/// </summary>
	private static void ApplyOperations(JsonNode? root, JsonArray operations, string? parentNumber, IReadOnlyCollection<string> protectedPaths, ICollection<string> errors)
	{
		for (var index = 0; index < operations.Count; index++)
		{
			var number = parentNumber is null ? $"{index + 1}" : $"{parentNumber}.{index + 1}";
			var item = operations[index];
			if (!IsGroup(item))
				ApplyOperation(root, item, number, protectedPaths, errors);
			else if (parentNumber is null)
				ApplyGroup(root, (JsonObject) item!, number, protectedPaths, errors);
			else
				errors.Add(OurResources.ModGroupNotAllowed.FormatCurrentCulture(number));
		}
	}

	/// <summary>
	/// Whether an item in a mod file is a group of operations: an object with "operations" or "comments", and no "op".
	/// </summary>
	public static bool IsGroup(JsonNode? item) =>
		item is JsonObject obj && FindKey(obj, "op") is null && (FindKey(obj, GroupOperationsName) is not null || FindKey(obj, DataFileParts.CommentsName) is not null);

	/// <summary>
	/// Checks a group's properties, and applies its operations in order. A group without "operations" has none.
	/// </summary>
	private static void ApplyGroup(JsonNode? root, JsonObject group, string number, IReadOnlyCollection<string> protectedPaths, ICollection<string> errors)
	{
		foreach (var (name, value) in group)
		{
			if (string.Equals(name, DataFileParts.CommentsName, StringComparison.OrdinalIgnoreCase))
			{
				if (value is not JsonValue comments || comments.GetValueKind() != System.Text.Json.JsonValueKind.String)
					errors.Add(OurResources.ModGroupCommentsInvalid.FormatCurrentCulture(number, name));
			}
			else if (string.Equals(name, GroupOperationsName, StringComparison.OrdinalIgnoreCase))
			{
				if (value is not JsonArray)
					errors.Add(OurResources.ModGroupOperationsInvalid.FormatCurrentCulture(number, name));
			}
			else
			{
				errors.Add(OurResources.ModGroupPropertyInvalid.FormatCurrentCulture(number, name));
			}
		}

		if (GetProperty(group, GroupOperationsName) is JsonArray operations)
			ApplyOperations(root, operations, number, protectedPaths, errors);
	}

	private static void ApplyOperation(JsonNode? root, JsonNode? node, string number, IReadOnlyCollection<string> protectedPaths, ICollection<string> errors)
	{
		string? error = null;
		if (node is not JsonObject operation)
		{
			error = OurResources.ModOperationNotObject.FormatCurrentCulture(number);
		}
		else
		{
			var type = GetString(operation, "op");
			var path = GetString(operation, "path");
			if (type is null)
				error = OurResources.ModOperationTypeMissing.FormatCurrentCulture(number);
			else if (!s_operations.Contains(type))
				error = OurResources.ModOperationTypeInvalid.FormatCurrentCulture(number, type);
			else if (path is null)
				error = OurResources.ModOperationPathMissing.FormatCurrentCulture(number);
			else if (ApplyOperation(root, operation, type.ToLowerInvariant(), path, number, protectedPaths, errors) is { } message)
				error = OurResources.ModOperationFailed.FormatCurrentCulture(number, type, path, message);
		}

		if (error is not null)
			errors.Add(error);
	}

	private static string? ApplyOperation(JsonNode? root, JsonObject operation, string type, string path, string number, IReadOnlyCollection<string> protectedPaths, ICollection<string> errors)
	{
		if (!ModPath.TryParse(path, out var segments, out var pathError))
			return pathError;
		if (FindProtectedPath(segments, protectedPaths) is { } protectedPath)
			return OurResources.ModPathProtected.FormatCurrentCulture(protectedPath);
		var hasValue = FindKey(operation, "value") is not null;
		var valuesNode = GetProperty(operation, "values");
		var hasValues = FindKey(operation, "values") is not null;
		var hasChanges = FindKey(operation, "changes") is not null;
		if (hasValues && type != InsertOperation)
			return OurResources.ModValuesNotInsert;
		if (hasValue && hasValues)
			return OurResources.ModValueConflict;
		if (type is ReplaceOperation or SetOperation && !hasValue)
			return OurResources.ModValueMissing;
		if (type is InsertOperation && !hasValue && !hasValues)
			return OurResources.ModInsertValueMissing;
		if (hasValues && valuesNode is not JsonArray)
			return OurResources.ModValuesNotList;
		if (hasChanges && type != CopyOperation)
			return OurResources.ModChangesNotCopy;
		if (hasChanges && GetProperty(operation, "changes") is not JsonArray)
			return OurResources.ModChangesNotList;

		var error = TryResolve(root, segments, segments.Count - 1, out var container);
		if (error is null)
		{
			error = TryFindSlot(container!, segments, segments.Count - 1, out var slot);
			if (error is null)
			{
				var value = GetProperty(operation, "value")?.DeepClone();
				error = type switch
				{
					InsertOperation => Insert(slot, operation, value, path),
					ReplaceOperation => slot.Exists ? Replace(slot, value) : OurResources.ModPathNotFound.FormatCurrentCulture(path),
					SetOperation => Set(slot, value),
					RemoveOperation => slot.Exists ? Remove(slot) : OurResources.ModPathNotFound.FormatCurrentCulture(path),
					MoveOperation => slot.Exists ? Move(slot, operation, path) : OurResources.ModPathNotFound.FormatCurrentCulture(path),
					CopyOperation => slot.Exists ? Copy(slot, operation, path, number, errors) : OurResources.ModPathNotFound.FormatCurrentCulture(path),
					_ => throw new InvalidOperationException($"Unknown mod operation \"{type}\"."),
				};
			}
		}

		return error;
	}

	private static string? Insert(Slot slot, JsonObject operation, JsonNode? value, string path)
	{
		if (!slot.Exists && slot.Container is JsonObject newParent)
			newParent[slot.Key!] = new JsonArray();

		string? error;
		if (!slot.Exists && slot.Container is JsonArray)
		{
			error = OurResources.ModPathNotFound.FormatCurrentCulture(path);
		}
		else if (slot.Get() is not JsonArray list)
		{
			error = OurResources.ModTargetNotList.FormatCurrentCulture(path);
		}
		else
		{
			error = TryGetPosition(operation, list, out var position);
			if (error is null)
			{
				var items = GetProperty(operation, "values") is JsonArray values ? values.Select(x => x?.DeepClone()).ToList() : [value];
				var start = position ?? list.Count;
				for (var offset = 0; offset < items.Count; offset++)
					list.Insert(start + offset, items[offset]);
			}
		}

		return error;
	}

	private static string? Replace(Slot slot, JsonNode? value)
	{
		if (slot.Container is JsonObject parent)
		{
			parent[slot.Key!] = value;
		}
		else if (slot.Container is JsonArray list)
		{
			list.RemoveAt(slot.Index);
			list.Insert(slot.Index, value);
		}

		return null;
	}

	private static string? Set(Slot slot, JsonNode? value)
	{
		string? error = null;
		if (slot.Container is JsonObject parent)
			parent[slot.Key!] = value;
		else
			error = OurResources.ModSetNotProperty;
		return error;
	}

	private static string? Remove(Slot slot)
	{
		if (slot.Container is JsonObject parent)
			parent.Remove(slot.Key!);
		else if (slot.Container is JsonArray list)
			list.RemoveAt(slot.Index);
		return null;
	}

	private static string? Move(Slot slot, JsonObject operation, string path)
	{
		if (slot.Container is not JsonArray list)
			return OurResources.ModNotListItem.FormatCurrentCulture(path);

		var item = list[slot.Index];
		var itemId = GetId(item);
		var relativeId = GetString(operation, "before") ?? GetString(operation, "after");

		string? error;
		if (itemId is not null && relativeId == itemId)
		{
			error = OurResources.ModMoveRelativeToSelf;
		}
		else
		{
			// The new position is in the list without the item.
			list.RemoveAt(slot.Index);
			error = TryGetPosition(operation, list, out var position);
			if (error is null && position is null)
				error = OurResources.ModPositionMissing;
			list.Insert(error is null ? position!.Value : slot.Index, item);
		}

		return error;
	}

	/// <summary>
	/// Copies a list item, applies the operation's changes to the copy (with paths starting at the copy), and adds it
	/// to the list, right after the original unless a position is given. Problems in the changes are added to the
	/// errors, and the copy isn't added if there are any.
	/// </summary>
	private static string? Copy(Slot slot, JsonObject operation, string path, string number, ICollection<string> errors)
	{
		if (slot.Container is not JsonArray list)
			return OurResources.ModNotListItem.FormatCurrentCulture(path);

		var error = TryGetPosition(operation, list, out var position);
		if (error is null)
		{
			var copy = list[slot.Index]?.DeepClone();
			var errorCount = errors.Count;
			if (GetProperty(operation, "changes") is JsonArray changes)
				ApplyOperations(copy, changes, number, [], errors);

			if (errors.Count == errorCount)
			{
				if (GetId(copy) is { } id && FindIdIndex(list, id) >= 0)
					error = OurResources.ModCopyIdUsed.FormatCurrentCulture(id);
				else
					list.Insert(position ?? slot.Index + 1, copy);
			}
		}

		return error;
	}

	/// <summary>
	/// Gets where in a list an item goes, from the operation's "index", "before", or "after". The position is null if
	/// none of them are given.
	/// </summary>
	private static string? TryGetPosition(JsonObject operation, JsonArray list, out int? position)
	{
		position = null;
		var index = GetProperty(operation, "index");
		var before = GetProperty(operation, "before");
		var after = GetProperty(operation, "after");

		string? error = null;
		if (new[] { index, before, after }.Count(x => x is not null) > 1)
		{
			error = OurResources.ModPositionConflict;
		}
		else if (index is not null)
		{
			if (index is JsonValue indexValue && indexValue.TryGetValue<int>(out var number) && number >= 0 && number <= list.Count)
				position = number;
			else
				error = OurResources.ModIndexInvalid.FormatCurrentCulture(list.Count);
		}
		else if ((before ?? after) is { } idNode)
		{
			var name = before is not null ? "before" : "after";
			if (idNode is not JsonValue idValue || !idValue.TryGetValue<string>(out var id) || string.IsNullOrWhiteSpace(id))
			{
				error = OurResources.ModPositionIdInvalid.FormatCurrentCulture(name);
			}
			else
			{
				var itemIndex = FindIdIndex(list, id);
				if (itemIndex < 0)
					error = OurResources.ModPositionIdUnknown.FormatCurrentCulture(id);
				else
					position = before is not null ? itemIndex : itemIndex + 1;
			}
		}

		return error;
	}

	/// <summary>
	/// Follows the first steps of a path from the root, which must all exist.
	/// </summary>
	private static string? TryResolve(JsonNode? root, IReadOnlyList<ModPathSegment> segments, int count, out JsonNode? node)
	{
		node = root;
		string? error = null;
		for (var index = 0; index < count && error is null; index++)
		{
			error = TryFindSlot(node!, segments, index, out var slot);
			if (error is null)
			{
				if (slot.Exists)
					node = slot.Get();
				else
					error = OurResources.ModPathNotFound.FormatCurrentCulture(ModPath.Format(segments.Take(index + 1)));
			}
		}

		return error;
	}

	/// <summary>
	/// Finds where one step of a path leads within the node reached by the steps before it. The place may not exist yet.
	/// </summary>
	private static string? TryFindSlot(JsonNode? container, IReadOnlyList<ModPathSegment> segments, int index, out Slot slot)
	{
		var segment = segments[index];
		slot = default;
		string? error = null;
		if (container is JsonObject parent)
		{
			var key = FindKey(parent, segment.Text);
			slot = new Slot(parent, key ?? segment.Text, -1, key is not null);
		}
		else if (container is JsonArray list)
		{
			if (!segment.IsBracket)
			{
				error = OurResources.ModPathNameOnList.FormatCurrentCulture(ModPath.Format(segments.Take(index)));
			}
			else
			{
				var itemIndex = segment.GetIndex() ?? FindIdIndex(list, segment.Text);
				slot = new Slot(list, null, itemIndex, itemIndex >= 0 && itemIndex < list.Count);
			}
		}
		else
		{
			error = OurResources.ModPathNotContainer.FormatCurrentCulture(ModPath.Format(segments.Take(index)));
		}

		return error;
	}

	/// <summary>
	/// Finds the key of an object's property: an exact match if there is one, or else a match that ignores case, like
	/// the names of properties in data files.
	/// </summary>
	private static string? FindKey(JsonObject node, string name) =>
		node.ContainsKey(name) ? name : node.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));

	private static JsonNode? GetProperty(JsonObject node, string name) => FindKey(node, name) is { } key ? node[key] : null;

	/// <summary>
	/// Gets the protected path that a path leads to or into, or that leads into the path (so changing it would change the
	/// protected data), or null if there's none. Protected paths are names joined by dots.
	/// </summary>
	private static string? FindProtectedPath(IReadOnlyList<ModPathSegment> segments, IReadOnlyCollection<string> protectedPaths)
	{
		foreach (var protectedPath in protectedPaths)
		{
			var names = protectedPath.Split('.');
			var count = Math.Min(names.Length, segments.Count);
			if (Enumerable.Range(0, count).All(x => string.Equals(names[x], segments[x].Text, StringComparison.OrdinalIgnoreCase)))
				return protectedPath;
		}

		return null;
	}

	private static string? GetString(JsonObject node, string name) =>
		GetProperty(node, name) is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

	private static string? GetId(JsonNode? node) => node is JsonObject item ? GetString(item, "id") : null;

	private static int FindIdIndex(JsonArray list, string id)
	{
		var found = -1;
		for (var index = 0; index < list.Count && found < 0; index++)
		{
			if (GetId(list[index]) == id)
				found = index;
		}

		return found;
	}

	/// <summary>
	/// A place a path leads to: a property of an object (by key) or an item of a list (by index), which may not exist yet.
	/// </summary>
	private readonly record struct Slot(JsonNode Container, string? Key, int Index, bool Exists)
	{
		public JsonNode? Get() => Container is JsonObject parent ? parent[Key!] : ((JsonArray) Container)[Index];
	}

	private static readonly HashSet<string> s_operations = new(StringComparer.OrdinalIgnoreCase) { InsertOperation, ReplaceOperation, SetOperation, RemoveOperation, MoveOperation, CopyOperation };
}
