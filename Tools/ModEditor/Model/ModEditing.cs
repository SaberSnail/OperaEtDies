using System.Text.Json.Nodes;
using OperaEtDies.Data;

namespace ModEditor.Model;

/// <summary>
/// Reads and changes the pieces of a mod's files by their locations. Changes that can't be made return the reason,
/// and leave the mod as it was.
/// </summary>
public static class ModEditing
{
	/// <summary>
	/// Gets the JSON at a location in a file, or null if there's nothing there. For a whole file, it's the file's JSON.
	/// </summary>
	public static JsonNode? Get(ModProject project, ModLocation location)
	{
		var root = location.FilePath is { } path ? project.FindFile(path)?.Root : null;
		return location switch
		{
			FileLocation => root,
			SectionLocation x => GetSection(root, x.Section),
			ItemLocation x => GetSection(root, x.Section) is JsonArray list && x.Index < list.Count ? list[x.Index] : null,
			SettingLocation x => GetSection(root, x.Section) is JsonObject obj && ModJson.FindName(obj, x.Name) is { } name ? obj[name] : null,
			OperationLocation x => GetOperationList(root, x.Group) is { } list && x.Index < list.Count ? list[x.Index] : null,
			GroupLocation x => root is JsonArray list && x.Index < list.Count && ModPatcher.IsGroup(list[x.Index]) ? list[x.Index] : null,
			_ => null,
		};
	}

	/// <summary>
	/// Whether there's a folder, file, or piece at the location.
	/// </summary>
	public static bool Exists(ModProject project, ModLocation location) => location switch
	{
		FolderLocation x => project.FolderExists(x.Path),
		FileLocation x => project.FindFile(x.Path) is not null,
		SettingLocation x => GetSection(project.FindFile(x.File)?.Root, x.Section) is JsonObject obj && ModJson.FindName(obj, x.Name) is not null,
		_ => Get(project, location) is not null,
	};

	/// <summary>
	/// Takes the piece at a location, as a copy, or returns null if the location isn't a piece of a file.
	/// </summary>
	public static ModPiece? GetPiece(ModProject project, ModLocation location)
	{
		if (!Exists(project, location))
			return null;
		var value = ModJson.Clone(Get(project, location));
		return location switch
		{
			SectionLocation x => ModPiece.WholeSection(GetSectionName(project, x), value),
			ItemLocation x => ModPiece.Item(GetSectionName(project, x.File, x.Section), value),
			SettingLocation x => ModPiece.Setting(GetSectionName(project, x.File, x.Section), x.Name, value),
			OperationLocation => ModPiece.Operation(value),
			GroupLocation => ModPiece.Group(value),
			_ => null,
		};
	}

	/// <summary>
	/// Replaces the JSON at a piece's location.
	/// </summary>
	public static string? Replace(ModProject project, ModLocation location, JsonNode? value)
	{
		var file = location.FilePath is { } path ? project.FindFile(path) : null;
		if (file?.Root is not { } root || !Exists(project, location))
			return "There's nothing there to change.";

		switch (location)
		{
		case SectionLocation x:
			((JsonObject) root)[ModJson.FindName((JsonObject) root, x.Section)!] = value;
			break;
		case ItemLocation x:
			((JsonArray) GetSection(root, x.Section)!)[x.Index] = value;
			break;
		case SettingLocation x:
			var settings = (JsonObject) GetSection(root, x.Section)!;
			settings[ModJson.FindName(settings, x.Name)!] = value;
			break;
		case OperationLocation x:
			GetOperationList(root, x.Group)![x.Index] = value;
			break;
		case GroupLocation x:
			((JsonArray) root)[x.Index] = value;
			break;
		default:
			return "Only a section, item, setting, operation, or group can be changed this way.";
		}

		file.UpdateText();
		return null;
	}

	/// <summary>
	/// Removes the folder, file, or piece at a location. A section left empty by removing its last item or setting is
	/// removed too.
	/// </summary>
	public static void Remove(ModProject project, ModLocation location)
	{
		var file = location.FilePath is { } path ? project.FindFile(path) : null;
		switch (location)
		{
		case FolderLocation x:
			project.RemoveFolder(x.Path);
			return;
		case FileLocation:
			if (file is not null)
				project.RemoveFile(file);
			return;
		}

		if (file?.Root is not { } root || !Exists(project, location))
			return;

		switch (location)
		{
		case SectionLocation x:
			((JsonObject) root).Remove(ModJson.FindName((JsonObject) root, x.Section)!);
			break;
		case ItemLocation x:
			var list = (JsonArray) GetSection(root, x.Section)!;
			list.RemoveAt(x.Index);
			if (list.Count == 0)
				((JsonObject) root).Remove(ModJson.FindName((JsonObject) root, x.Section)!);
			break;
		case SettingLocation x:
			var settings = (JsonObject) GetSection(root, x.Section)!;
			settings.Remove(ModJson.FindName(settings, x.Name)!);
			if (settings.Count == 0)
				((JsonObject) root).Remove(ModJson.FindName((JsonObject) root, x.Section)!);
			break;
		case OperationLocation x:
			GetOperationList(root, x.Group)!.RemoveAt(x.Index);
			break;
		case GroupLocation x:
			((JsonArray) root).RemoveAt(x.Index);
			break;
		}

		file.UpdateText();
	}

	/// <summary>
	/// Puts a piece at a target: into a file or section (at the end), or next to an item, setting, or operation. Items
	/// and settings go into the section they belong to, which is added to a data file that doesn't have it. A section
	/// is joined with the file's section of the same name, if it has one. An operation goes into a group it's put on
	/// (at the end), unless <paramref name="intoGroup"/> is false, when it goes next to the group; a group only goes in
	/// the file's list, next to the group or operation it's put on (or the group that operation is in). Returns where the
	/// piece went, or the reason it couldn't go there.
	/// </summary>
	public static (ModLocation? Location, string? Error) Insert(ModProject project, ModPiece piece, ModLocation target, bool after, bool intoGroup = true)
	{
		var file = target.FilePath is { } path ? project.FindFile(path) : null;
		if (file is null)
			return (null, "Pieces of files can only go into a file.");

		var root = file.Root as JsonObject;
		var result = piece.Kind switch
		{
			ModPieceKind.Operation or ModPieceKind.Group => InsertOperation(file, piece, target, after, intoGroup),
			_ when root is null => (null, file.Kind == ModFileKind.Operations ? $"Only operations can go into an operations file, and this is {Describe(piece)}. To change a piece of the game data, use Replace or Remove in its menu." : "The file can't be read."),
			ModPieceKind.Section => InsertSection(file, root, piece),
			_ => InsertIntoSection(file, root, piece, target, after),
		};
		if (result.Item1 is not null)
			file.UpdateText();
		return result;
	}

	/// <summary>
	/// Moves (or copies) the piece at one location to a target, as <see cref="Insert"/> does. A copied item gets a new id
	/// if its id is already used in the section it goes into. Returns where it went, or the reason it couldn't go there.
	/// </summary>
	public static (ModLocation? Location, string? Error) Move(ModProject project, ModLocation source, ModLocation target, bool after, bool isCopy, bool intoGroup = true)
	{
		if (GetPiece(project, source) is not { } piece)
			return (null, "There's nothing there to move.");

		// A group put on an operation in a group goes next to that group, which may be the group itself.
		if (piece.Kind == ModPieceKind.Group && target is OperationLocation { Group: { } targetGroup } operationTarget)
			target = new GroupLocation(operationTarget.File, targetGroup);
		if (!isCopy && source == target)
			return (source, null);

		var snapshot = project.TakeSnapshot();
		if (isCopy)
		{
			piece = MakeIdUnique(project, piece, target);
		}
		else
		{
			Remove(project, source);
			target = AdjustForRemoval(project, target, source);
		}

		var result = Insert(project, piece, target, after, intoGroup);
		if (result.Location is null)
			project.Restore(snapshot);
		return result;
	}

	/// <summary>
	/// Copies the piece at a location to just after it, with a new id if it has one.
	/// </summary>
	public static ModLocation? Duplicate(ModProject project, ModLocation location) =>
		location is SettingLocation ? null : Move(project, location, location, after: true, isCopy: true).Location;

	/// <summary>
	/// Gives an item a new id if the section it would go into at the target already has one like it.
	/// </summary>
	public static ModPiece MakeIdUnique(ModProject project, ModPiece piece, ModLocation target)
	{
		if (piece.Kind != ModPieceKind.Item || ModJson.GetId(piece.Value) is not { } id || piece.Value is not JsonObject item)
			return piece;
		var ids = (target.FilePath is { } path ? GetSection(project.FindFile(path)?.Root, piece.Section!) as JsonArray : null)?
			.Select(ModJson.GetId).OfType<string>().ToHashSet(StringComparer.Ordinal) ?? [];
		if (!ids.Contains(id))
			return piece;

		var copy = (JsonObject) item.DeepClone();
		copy["id"] = MakeUnique(id + "Copy", ids.Contains);
		return piece with { Value = copy };
	}

	/// <summary>
	/// Gets the path that finds a piece in the merged data from a mod operation, or null if it can't be found by path
	/// (an operation, or an item without an id).
	/// </summary>
	public static string? GetDataPath(ModPiece piece) => piece.Kind switch
	{
		ModPieceKind.Section => piece.Section,
		ModPieceKind.Item when ModJson.GetId(piece.Value) is { } id => piece.Section + FormatStep(id),
		ModPieceKind.Setting => piece.Section + (IsPlainName(piece.Name!) ? "." + piece.Name : FormatStep(piece.Name!)),
		_ => null,
	};

	/// <summary>
	/// Adds a step to a path for a property (or map key), or returns null if there's no path to add to.
	/// </summary>
	public static string? AppendName(string? path, string name) =>
		path is null ? null : path + (IsPlainName(name) ? "." + name : FormatStep(name));

	/// <summary>
	/// Adds a step to a path for a list item, by its id if it has one or else by its index, or returns null if there's
	/// no path to add to.
	/// </summary>
	public static string? AppendItem(string? path, JsonNode? item, int index) =>
		path is null ? null : path + (ModJson.GetId(item) is { } id ? FormatStep(id) : $"[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]");

	/// <summary>
	/// Gets the first of "text", "text2", "text3", and so on that isn't taken.
	/// </summary>
	public static string MakeUnique(string text, Func<string, bool> isTaken)
	{
		var result = text;
		for (var number = 2; isTaken(result); number++)
			result = text + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
		return result;
	}

	/// <summary>
	/// Gets a section of a data file's JSON, matching its name exactly if possible, or else ignoring case.
	/// </summary>
	public static JsonNode? GetSection(JsonNode? root, string section) =>
		root is JsonObject obj && ModJson.FindName(obj, section) is { } name ? obj[name] : null;

	/// <summary>
	/// Gets the list an operation is in: the file's list, or the operations of the group at an index in it. Returns null
	/// if there's no such list.
	/// </summary>
	public static JsonArray? GetOperationList(JsonNode? root, int? group)
	{
		if (root is not JsonArray items || group is not { } index)
			return root as JsonArray;
		return index < items.Count && ModPatcher.IsGroup(items[index]) && items[index] is JsonObject groupObject &&
			ModJson.FindName(groupObject, ModPatcher.GroupOperationsName) is { } name ? groupObject[name] as JsonArray : null;
	}

	private static (ModLocation?, string?) InsertOperation(ModFile file, ModPiece piece, ModLocation target, bool after, bool intoGroup)
	{
		if (file.Root is not JsonArray items)
			return (null, piece.Kind == ModPieceKind.Group ? "Groups of operations can only go into an operations file." : "Operations can only go into an operations file.");

		var value = ModJson.Clone(piece.Value);
		if (piece.Kind == ModPieceKind.Operation)
		{
			if (intoGroup && target is GroupLocation group && group.Index < items.Count && items[group.Index] is JsonObject groupObject)
			{
				var name = ModJson.FindName(groupObject, ModPatcher.GroupOperationsName);
				if (name is null)
					groupObject[name = ModPatcher.GroupOperationsName] = new JsonArray();
				if (groupObject[name] is not JsonArray groupOperations)
					return (null, $"The group's \"{name}\" isn't a list.");
				groupOperations.Add(value);
				return (new OperationLocation(file.Path, groupOperations.Count - 1, group.Index), null);
			}

			if (target is OperationLocation { Group: { } groupIndex } inGroup && GetOperationList(items, groupIndex) is { } list)
			{
				var position = Math.Min(inGroup.Index + (after ? 1 : 0), list.Count);
				list.Insert(position, value);
				return (new OperationLocation(file.Path, position, groupIndex), null);
			}
		}

		var targetIndex = target switch
		{
			GroupLocation x => x.Index,
			OperationLocation { Group: { } x } => x,
			OperationLocation x => x.Index,
			_ => (int?) null,
		};
		var index = targetIndex is { } t ? Math.Min(t + (after ? 1 : 0), items.Count) : items.Count;
		items.Insert(index, value);
		return (ModPatcher.IsGroup(value) ? new GroupLocation(file.Path, index) : new OperationLocation(file.Path, index), null);
	}

	private static (ModLocation?, string?) InsertSection(ModFile file, JsonObject root, ModPiece piece)
	{
		var name = ModJson.FindName(root, piece.Section!);
		var existing = name is null ? null : root[name];
		switch (existing, piece.Value)
		{
		case (null, _) when name is null:
			root[piece.Section!] = ModJson.Clone(piece.Value);
			return (new SectionLocation(file.Path, piece.Section!), null);
		case (JsonArray list, JsonArray items):
			foreach (var item in items)
				list.Add(ModJson.Clone(item));
			return (new SectionLocation(file.Path, name!), null);
		case (JsonObject settings, JsonObject values):
			if (values.Select(x => x.Key).FirstOrDefault(x => ModJson.FindName(settings, x) is not null) is { } taken)
				return (null, $"The file's \"{name}\" section already has \"{taken}\".");
			foreach (var (key, value) in values)
				settings[key] = ModJson.Clone(value);
			return (new SectionLocation(file.Path, name!), null);
		default:
			return (null, $"The file's \"{name}\" section is a different kind of section.");
		}
	}

	private static (ModLocation?, string?) InsertIntoSection(ModFile file, JsonObject root, ModPiece piece, ModLocation target, bool after)
	{
		var targetSection = target switch
		{
			SectionLocation x => x.Section,
			ItemLocation x => x.Section,
			SettingLocation x => x.Section,
			_ => null,
		};
		if (targetSection is not null && !string.Equals(targetSection, piece.Section, StringComparison.OrdinalIgnoreCase))
			return (null, $"This belongs in a \"{piece.Section}\" section.");

		var name = ModJson.FindName(root, piece.Section!) ?? piece.Section!;
		var section = root[name];
		if (piece.Kind == ModPieceKind.Item)
		{
			if (section is null)
				root[name] = section = new JsonArray();
			if (section is not JsonArray list)
				return (null, $"The file's \"{name}\" section is a settings section, not a list.");

			var index = target is ItemLocation x ? Math.Min(x.Index + (after ? 1 : 0), list.Count) : list.Count;
			list.Insert(index, ModJson.Clone(piece.Value));
			return (new ItemLocation(file.Path, name, index), null);
		}
		else
		{
			if (section is null)
				root[name] = section = new JsonObject();
			if (section is not JsonObject settings)
				return (null, $"The file's \"{name}\" section is a list, not a settings section.");
			if (ModJson.FindName(settings, piece.Name!) is { } taken)
				return (null, $"The file's \"{name}\" section already has \"{taken}\".");

			// Objects can't insert in place, so the settings are put back in order with the new one where it goes.
			var entries = settings.ToList();
			settings.Clear();
			var targetIndex = target is SettingLocation y ? entries.FindIndex(e => string.Equals(e.Key, y.Name, StringComparison.OrdinalIgnoreCase)) : -1;
			var insertAt = targetIndex < 0 ? entries.Count : targetIndex + (after ? 1 : 0);
			entries.Insert(insertAt, new KeyValuePair<string, JsonNode?>(piece.Name!, ModJson.Clone(piece.Value)));
			foreach (var (key, value) in entries)
				settings[key] = value?.Parent is null ? value : value.DeepClone();
			return (new SettingLocation(file.Path, name, piece.Name!), null);
		}
	}

	/// <summary>
	/// Adjusts a target in the same list as a piece that was just removed, since the items after it moved up.
	/// </summary>
	private static ModLocation AdjustForRemoval(ModProject project, ModLocation target, ModLocation removed) => (target, removed) switch
	{
		(ItemLocation t, ItemLocation r) when t.File == r.File && t.Section == r.Section && t.Index > r.Index => t with { Index = t.Index - 1 },
		(OperationLocation t, OperationLocation r) when t.File == r.File && t.Group == r.Group && t.Index > r.Index => t with { Index = t.Index - 1 },
		// Removing an operation or group from a file's list moves up the groups after it, and the operations in them.
		(GroupLocation t, _) when IsRemovedBefore(removed, t.File, t.Index) => t with { Index = t.Index - 1 },
		(OperationLocation { Group: { } group } t, _) when IsRemovedBefore(removed, t.File, group) => t with { Group = group - 1 },
		(OperationLocation { Group: null } t, GroupLocation) when IsRemovedBefore(removed, t.File, t.Index) => t with { Index = t.Index - 1 },
		// A target section that was removed with its last piece is still a fine place for a piece of that section.
		(ItemLocation t, _) when !Exists(project, t) => new FileLocation(t.File),
		(SettingLocation t, _) when !Exists(project, t) => new FileLocation(t.File),
		(SectionLocation t, _) when !Exists(project, t) => new FileLocation(t.File),
		_ => target,
	};

	/// <summary>
	/// Whether what was removed was in a file's list (an operation or a group) before an index in it.
	/// </summary>
	private static bool IsRemovedBefore(ModLocation removed, string file, int index) => removed switch
	{
		GroupLocation r => r.File == file && r.Index < index,
		OperationLocation { Group: null } r => r.File == file && r.Index < index,
		_ => false,
	};

	private static string GetSectionName(ModProject project, SectionLocation location) => GetSectionName(project, location.File, location.Section);

	private static string GetSectionName(ModProject project, string filePath, string section) =>
		project.FindFile(filePath)?.Root is JsonObject root ? ModJson.FindName(root, section) ?? section : section;

	/// <summary>
	/// Describes a piece for a message, such as: an item of "houses".
	/// </summary>
	private static string Describe(ModPiece piece) => piece.Kind switch
	{
		ModPieceKind.Item => ModJson.GetId(piece.Value) is { } id ? $"\"{id}\", an item of \"{piece.Section}\"" : $"an item of \"{piece.Section}\"",
		ModPieceKind.Setting => $"the \"{piece.Name}\" setting of \"{piece.Section}\"",
		ModPieceKind.Section => $"the \"{piece.Section}\" section",
		ModPieceKind.Group => "a group of operations",
		_ => "an operation",
	};

	private static bool IsPlainName(string name) => name.Length != 0 && name.All(x => char.IsLetterOrDigit(x) || x == '_' || x == '-');

	// A plain step can't hold "]", and a whole number in one would pick an item by its index instead.
	private static string FormatStep(string id) => id.Contains(']', StringComparison.Ordinal) || int.TryParse(id, out _) ? $"['{id}']" : $"[{id}]";
}
