using System.Text.Json.Nodes;

namespace ModEditor.Model;

/// <summary>
/// Where something is in a mod: a folder, a file, or a piece of a file's JSON.
/// </summary>
public abstract record ModLocation
{
	/// <summary>
	/// The path of the file this is in, or null for a folder.
	/// </summary>
	public virtual string? FilePath => null;
}

public sealed record FolderLocation(string Path) : ModLocation;

public sealed record FileLocation(string Path) : ModLocation
{
	public override string FilePath => Path;
}

/// <summary>
/// A top-level section of a data file.
/// </summary>
public sealed record SectionLocation(string File, string Section) : ModLocation
{
	public override string FilePath => File;
}

/// <summary>
/// An item in a list section of a data file.
/// </summary>
public sealed record ItemLocation(string File, string Section, int Index) : ModLocation
{
	public override string FilePath => File;
}

/// <summary>
/// A setting in a settings section of a data file.
/// </summary>
public sealed record SettingLocation(string File, string Section, string Name) : ModLocation
{
	public override string FilePath => File;
}

/// <summary>
/// An operation in an operations file: at an index in the file's list, or, if <see cref="Group"/> isn't null, at an
/// index in the operations of the group at that index in the file's list.
/// </summary>
public sealed record OperationLocation(string File, int Index, int? Group = null) : ModLocation
{
	public override string FilePath => File;
}

/// <summary>
/// A group of operations, at an index in an operations file's list.
/// </summary>
public sealed record GroupLocation(string File, int Index) : ModLocation
{
	public override string FilePath => File;
}

/// <summary>
/// What kind of piece of a mod file a <see cref="ModPiece"/> is.
/// </summary>
public enum ModPieceKind
{
	Item,
	Setting,
	Section,
	Operation,
	Group,
}

/// <summary>
/// A piece of a mod file, taken out so it can be put somewhere else: an item or setting with the section it belongs
/// in, a whole section, an operation, or a group of operations.
/// </summary>
public sealed record ModPiece(ModPieceKind Kind, string? Section, string? Name, JsonNode? Value)
{
	public static ModPiece Item(string section, JsonNode? value) => new(ModPieceKind.Item, section, null, value);

	public static ModPiece Setting(string section, string name, JsonNode? value) => new(ModPieceKind.Setting, section, name, value);

	public static ModPiece WholeSection(string section, JsonNode? value) => new(ModPieceKind.Section, section, null, value);

	public static ModPiece Operation(JsonNode? value) => new(ModPieceKind.Operation, null, null, value);

	public static ModPiece Group(JsonNode? value) => new(ModPieceKind.Group, null, null, value);

	/// <summary>
	/// Gets the piece as JSON, to put on the clipboard.
	/// </summary>
	public JsonObject ToJson() => new()
	{
		["kind"] = Kind.ToString(),
		["section"] = Section,
		["name"] = Name,
		["value"] = Value?.DeepClone(),
	};

	/// <summary>
	/// Reads a piece written by <see cref="ToJson"/>, or returns null if it isn't one.
	/// </summary>
	public static ModPiece? FromJson(JsonNode? node)
	{
		static string? GetString(JsonObject obj, string name) =>
			obj[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

		if (node is not JsonObject obj || GetString(obj, "kind") is not { } kindText || !Enum.TryParse<ModPieceKind>(kindText, out var kind))
			return null;
		var section = GetString(obj, "section");
		var name = GetString(obj, "name");
		var isValid = kind switch
		{
			ModPieceKind.Item or ModPieceKind.Section => section is not null,
			ModPieceKind.Setting => section is not null && name is not null,
			_ => true,
		};
		return isValid ? new ModPiece(kind, section, name, obj["value"]?.DeepClone()) : null;
	}
}
