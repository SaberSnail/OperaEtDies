using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PortraitEditor.Model;

/// <summary>
/// Reads and changes the properties of JSON objects the way the data files are read: names match exactly if
/// possible, or else ignoring case.
/// </summary>
public static class JsonObjectUtility
{
	public static string? FindName(this JsonObject obj, string name) =>
		obj.ContainsKey(name) ? name : obj.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));

	public static JsonNode? GetProperty(this JsonObject obj, string name) => obj.FindName(name) is { } key ? obj[key] : null;

	public static string? GetString(this JsonObject obj, string name) =>
		obj.GetProperty(name) is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

	public static double? GetNumber(this JsonObject obj, string name) =>
		obj.GetProperty(name) is JsonValue value && value.GetValueKind() == JsonValueKind.Number ? value.GetValue<double>() : null;

	/// <summary>
	/// Gets a property as text: a string as it is, and anything else as JSON. Empty if the property is missing.
	/// </summary>
	public static string GetText(this JsonObject obj, string name) => obj.GetProperty(name) switch
	{
		null => "",
		JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
		JsonValue value when value.GetValueKind() == JsonValueKind.Number => value.GetValue<double>().ToString("R", CultureInfo.InvariantCulture),
		var node => JsonFormatter.Format(node),
	};

	/// <summary>
	/// Sets a property, keeping its place if the object has it, or removes it if the value is null.
	/// </summary>
	public static void SetProperty(this JsonObject obj, string name, JsonNode? value)
	{
		var key = obj.FindName(name);
		if (value is null)
		{
			if (key is not null)
				obj.Remove(key);
		}
		else
		{
			obj[key ?? name] = value;
		}
	}
}
