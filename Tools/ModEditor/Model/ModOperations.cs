using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ModEditor.Model;

/// <summary>
/// Reads mod operations (see Docs/Mods.md) for the editor.
/// </summary>
public static partial class ModOperations
{
	/// <summary>
	/// The operations, in the order the documentation lists them.
	/// </summary>
	public static IReadOnlyList<string> Names { get; } = ["insert", "replace", "set", "remove", "move", "copy"];

	/// <summary>
	/// Gets a property of an operation as text, matching its name ignoring case, or null if it isn't a string.
	/// </summary>
	public static string? GetString(JsonObject operation, string name) =>
		ModJson.FindName(operation, name) is { } key && operation[key] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

	/// <summary>
	/// Gets the ids of the items an operation works on, to tell operations apart: the ids of the items an insert or
	/// replace adds, the new id a copy gives, or else the id at the end of the path. Empty if there are none.
	/// </summary>
	public static IReadOnlyList<string> GetItemIds(JsonObject operation)
	{
		var op = GetString(operation, "op")?.ToLowerInvariant();
		var path = GetString(operation, "path") ?? "";
		JsonNode? Get(string name) => ModJson.FindName(operation, name) is { } key ? operation[key] : null;

		if (op is "insert" or "replace")
		{
			var values = Get("values") is JsonArray list ? list.ToList() : [Get("value")];
			var ids = values.Select(ModJson.GetId).OfType<string>().ToList();
			if (ids.Count != 0)
				return ids;
		}
		else if (op == "copy" && Get("changes") is JsonArray changes)
		{
			// A copy of an item with an id sets a new one.
			var newId = changes.OfType<JsonObject>()
				.Where(x => string.Equals(GetString(x, "path"), "id", StringComparison.OrdinalIgnoreCase) && GetString(x, "op") is "set" or "replace")
				.Select(x => ModJson.FindName(x, "value") is { } key && x[key] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null)
				.LastOrDefault(x => x is not null);
			if (newId is not null)
				return [newId];
		}

		return op == "insert" || GetLastStepId(path) is not { } stepId ? [] : [stepId];
	}

	/// <summary>
	/// Gets the id in the last step of a path, such as "red" in "properties[hairColor].values[red]", or null if the last
	/// step isn't an id (a name, or an index).
	/// </summary>
	public static string? GetLastStepId(string path)
	{
		var match = LastStepRegex().Match(path);
		if (!match.Success)
			return null;
		if (match.Groups["quoted"].Success)
			return match.Groups["quoted"].Value;
		var text = match.Groups["plain"].Value;
		return int.TryParse(text, out _) ? null : text;
	}

	[GeneratedRegex(@"\[(?:'(?<quoted>(?:(?!'\]).)*)'|(?<plain>[^\]]*))\]$")]
	private static partial Regex LastStepRegex();
}
