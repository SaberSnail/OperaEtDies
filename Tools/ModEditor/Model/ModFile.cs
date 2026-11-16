using System.Text.Json;
using System.Text.Json.Nodes;
using OperaEtDies.Data;

namespace ModEditor.Model;

/// <summary>
/// What a mod file holds, going by its JSON.
/// </summary>
public enum ModFileKind
{
	/// <summary>
	/// An object of sections (and optional comments), merged like a data file.
	/// </summary>
	Data,

	/// <summary>
	/// A list of operations and groups of operations.
	/// </summary>
	Operations,

	/// <summary>
	/// Text that isn't JSON, JSON that's neither an object nor a list, or a data file whose comments can't
	/// be read.
	/// </summary>
	Invalid,
}

/// <summary>
/// One JSON file in a mod folder. Its text is kept exactly as it was loaded or typed, and is only written again (in
/// the style of the data files, without its JSON comments) when its JSON or its comments are changed piece by piece.
/// </summary>
public sealed class ModFile
{
	public ModFile(string path, string text)
	{
		Path = path;
		m_text = "";
		SetText(text);
	}

	/// <summary>
	/// The file's path from the mod folder, with forward slashes, such as "Portraits/Hair.json".
	/// </summary>
	public string Path { get; set; }

	public string Name => ModFilePath.GetName(Path);

	public string Text => m_text;

	/// <summary>
	/// The file's sections (an object, without its comments) or operations (a list), or null if its text can't be read.
	/// It can be changed in place, followed by a call to <see cref="UpdateText"/>.
	/// </summary>
	public JsonNode? Root { get; private set; }

	/// <summary>
	/// A data file's "comments" property, for the people editing the mod, or null if it has none. A list of operations
	/// has comments in its groups instead.
	/// </summary>
	public string? Comments { get; private set; }

	/// <summary>
	/// Why the text can't be read, or null if it can.
	/// </summary>
	public string? ParseError { get; private set; }

	public ModFileKind Kind => Root switch
	{
		JsonObject => ModFileKind.Data,
		JsonArray => ModFileKind.Operations,
		_ => ModFileKind.Invalid,
	};

	/// <summary>
	/// Replaces the text, and reads its JSON.
	/// </summary>
	public void SetText(string text)
	{
		m_text = text;
		var (root, error) = ModJson.Parse(text);
		var (parts, errors) = error is null ? DataFileParts.Split(root, allowOperations: true) : (null, []);
		Root = (JsonNode?) parts?.Sections ?? parts?.Operations;
		Comments = parts?.Comments;
		ParseError = error ?? (errors.Count != 0 ? string.Join(" ", errors) : null);
	}

	/// <summary>
	/// Replaces the JSON (sections without comments, or operations), and writes the text from it.
	/// </summary>
	public void SetRoot(JsonNode root)
	{
		Root = root;
		UpdateText();
	}

	/// <summary>
	/// Replaces a data file's comments (leaving them out if they're empty), and writes the text again.
	/// </summary>
	public void SetComments(string? comments)
	{
		if (Root is JsonObject)
		{
			Comments = string.IsNullOrWhiteSpace(comments) ? null : comments;
			UpdateText();
		}
	}

	/// <summary>
	/// Writes the text again from the JSON and the comments, after they've been changed. The comments come first.
	/// </summary>
	public void UpdateText()
	{
		if (Root is not null)
		{
			m_text = JsonFormatter.Format(Combine()) + "\r\n";
			ParseError = null;
		}
	}

	private JsonNode Combine()
	{
		if (Root is not JsonObject sections || Comments is null)
			return Root!;

		var file = new JsonObject { [DataFileParts.CommentsName] = Comments };
		foreach (var (name, value) in sections)
			file[name] = value?.DeepClone();
		return file;
	}

	private string m_text;
}

/// <summary>
/// Reading and copying JSON the way the data files are read.
/// </summary>
public static class ModJson
{
	/// <summary>
	/// Reads JSON, allowing comments and trailing commas. Returns the JSON (which can be null for "null"), or the reason
	/// it can't be read.
	/// </summary>
	public static (JsonNode? Node, string? Error) Parse(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return (null, "The text is empty.");

		try
		{
			return (JsonNode.Parse(text, null, s_options), null);
		}
		catch (JsonException e)
		{
			return (null, e.Message);
		}
	}

	public static JsonNode? Clone(JsonNode? node) => node?.DeepClone();

	public static string Format(JsonNode? node) => JsonFormatter.Format(node);

	/// <summary>
	/// Gets an item's id, or null if it doesn't have one.
	/// </summary>
	public static string? GetId(JsonNode? node) =>
		node is JsonObject obj && obj.TryGetPropertyValue("id", out var id) && id is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

	/// <summary>
	/// Gets an object property, matching the name exactly if possible, or else ignoring case, as the data files are read.
	/// </summary>
	public static string? FindName(JsonObject obj, string name) =>
		obj.ContainsKey(name) ? name : obj.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Whether a property of an object is its comments (text, for the people editing the data), which the game ignores.
	/// </summary>
	public static bool IsComments(string name, JsonNode? value) =>
		string.Equals(name, DataFileParts.CommentsName, StringComparison.OrdinalIgnoreCase) && value is JsonValue text && text.GetValueKind() == JsonValueKind.String;

	private static readonly JsonDocumentOptions s_options = new()
	{
		CommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};
}
