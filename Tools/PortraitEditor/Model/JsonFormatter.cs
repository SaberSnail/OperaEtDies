using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PortraitEditor.Model;

/// <summary>
/// Writes JSON in the style of the data files: indented with tabs, with each object or list on one line unless it
/// holds a list of objects.
/// </summary>
public static class JsonFormatter
{
	public static string Format(JsonNode? node)
	{
		var text = new StringBuilder();
		Write(text, node, 0);
		return text.ToString();
	}

	private static void Write(StringBuilder text, JsonNode? node, int depth)
	{
		switch (node)
		{
		case JsonObject { Count: 0 }:
			text.Append("{}");
			break;
		case JsonArray { Count: 0 }:
			text.Append("[]");
			break;
		case JsonObject obj when IsInline(obj):
			text.Append("{ ");
			AppendJoined(text, obj, x =>
			{
				AppendName(text, x.Key);
				Write(text, x.Value, depth);
			});
			text.Append(" }");
			break;
		case JsonArray array when IsInline(array):
			text.Append("[ ");
			AppendJoined(text, array, x => Write(text, x, depth));
			text.Append(" ]");
			break;
		case JsonObject obj:
			text.Append("{\r\n");
			AppendLines(text, obj, depth, x =>
			{
				AppendName(text, x.Key);
				Write(text, x.Value, depth + 1);
			});
			text.Append('\t', depth).Append('}');
			break;
		case JsonArray array:
			text.Append("[\r\n");
			AppendLines(text, array, depth, x => Write(text, x, depth + 1));
			text.Append('\t', depth).Append(']');
			break;
		case null:
			text.Append("null");
			break;
		default:
			text.Append(node.ToJsonString(s_options));
			break;
		}
	}

	private static void AppendName(StringBuilder text, string name) => text.Append(JsonSerializer.Serialize(name, s_options)).Append(": ");

	private static void AppendJoined<T>(StringBuilder text, IEnumerable<T> items, Action<T> write)
	{
		var isFirst = true;
		foreach (var item in items)
		{
			if (!isFirst)
				text.Append(", ");
			write(item);
			isFirst = false;
		}
	}

	private static void AppendLines<T>(StringBuilder text, ICollection<T> items, int depth, Action<T> write)
	{
		var index = 0;
		foreach (var item in items)
		{
			text.Append('\t', depth + 1);
			write(item);
			index++;
			text.Append(index < items.Count ? ",\r\n" : "\r\n");
		}
	}

	private static bool IsInline(JsonNode? node) => node switch
	{
		JsonObject obj => obj.All(x => IsInline(x.Value)),
		JsonArray array => array.All(x => x is not JsonObject && IsInline(x)),
		_ => true,
	};

	private static readonly JsonSerializerOptions s_options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}
