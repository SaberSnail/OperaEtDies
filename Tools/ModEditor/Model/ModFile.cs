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
	/// An object of sections, merged like a data file.
	/// </summary>
	Data,

	/// <summary>
	/// A list of operations.
	/// </summary>
	Operations,

	/// <summary>
	/// Text that isn't JSON, or JSON that's neither an object nor a list.
	/// </summary>
	Invalid,
}

/// <summary>
/// One JSON file in a mod folder. Its text is kept exactly as it was loaded or typed, and is only written again (in
/// the style of the data files, without comments) when its JSON is changed piece by piece.
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
	/// The file's JSON, or null if its text can't be read. It can be changed in place, followed by a call to
	/// <see cref="UpdateText"/>.
	/// </summary>
	public JsonNode? Root { get; private set; }

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
		Root = root is JsonObject or JsonArray ? root : null;
		ParseError = error ?? (Root is null ? "A mod file must be an object of sections or a list of operations." : null);
	}

	/// <summary>
	/// Replaces the JSON, and writes the text from it.
	/// </summary>
	public void SetRoot(JsonNode root)
	{
		Root = root;
		UpdateText();
	}

	/// <summary>
	/// Writes the text again from the JSON, after it's been changed in place.
	/// </summary>
	public void UpdateText()
	{
		if (Root is not null)
		{
			m_text = JsonFormatter.Format(Root) + "\r\n";
			ParseError = null;
		}
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

	private static readonly JsonDocumentOptions s_options = new()
	{
		CommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};
}
