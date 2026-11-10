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

	/// <summary>
	/// Applies each operation in a mod file to the root of a data file, in order. Operations can't change the
	/// protected top level properties. Returns a user-facing message for each problem found; operations that have
	/// problems are skipped.
	/// </summary>
	public static IReadOnlyList<string> Apply(JsonNode root, JsonNode? modFile, IReadOnlyCollection<string> protectedProperties)
	{
		var errors = new List<string>();
		if (modFile is JsonArray operations)
		{
			for (var index = 0; index < operations.Count; index++)
			{
				if (ApplyOperation(root, operations[index], index + 1, protectedProperties) is { } error)
					errors.Add(error);
			}
		}
		else
		{
			errors.Add(OurResources.ModFileNotList);
		}

		return errors;
	}

	private static string? ApplyOperation(JsonNode root, JsonNode? node, int number, IReadOnlyCollection<string> protectedProperties)
	{
		if (node is not JsonObject operation)
			return OurResources.ModOperationNotObject.FormatCurrentCulture(number);

		var type = GetString(operation, "op");
		var path = GetString(operation, "path");
		string? error = null;
		if (type is null)
		{
			error = OurResources.ModOperationTypeMissing.FormatCurrentCulture(number);
		}
		else if (!s_operations.Contains(type))
		{
			error = OurResources.ModOperationTypeInvalid.FormatCurrentCulture(number, type);
		}
		else if (path is null)
		{
			error = OurResources.ModOperationPathMissing.FormatCurrentCulture(number);
		}
		else if (ApplyOperation(root, operation, type.ToLowerInvariant(), path, protectedProperties) is { } message)
		{
			error = OurResources.ModOperationFailed.FormatCurrentCulture(number, type, path, message);
		}

		return error;
	}

	private static string? ApplyOperation(JsonNode root, JsonObject operation, string type, string path, IReadOnlyCollection<string> protectedProperties)
	{
		if (!ModPath.TryParse(path, out var segments, out var pathError))
			return pathError;
		if (protectedProperties.Contains(segments[0].Text, StringComparer.OrdinalIgnoreCase))
			return OurResources.ModPathProtected.FormatCurrentCulture(segments[0].Text);
		if (type is InsertOperation or ReplaceOperation or SetOperation && FindKey(operation, "value") is null)
			return OurResources.ModValueMissing;

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
				list.Insert(position ?? list.Count, value);
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
			return OurResources.ModMoveNotListItem.FormatCurrentCulture(path);

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
	private static string? TryResolve(JsonNode root, IReadOnlyList<ModPathSegment> segments, int count, out JsonNode? node)
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

	private static readonly HashSet<string> s_operations = new(StringComparer.OrdinalIgnoreCase) { InsertOperation, ReplaceOperation, SetOperation, RemoveOperation, MoveOperation };
}
