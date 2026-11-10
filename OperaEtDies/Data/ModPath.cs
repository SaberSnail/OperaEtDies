using System.Diagnostics.CodeAnalysis;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// One step of a mod operation's path. A name step (after a dot, or first) picks a property of an object. A bracket
/// step picks a property of an object, or an item of a list by its id, or by its index if the text is a whole number
/// that wasn't quoted.
/// </summary>
public sealed record ModPathSegment(string Text, bool IsBracket, bool IsQuoted)
{
	/// <summary>
	/// Gets the index this step picks from a list, or null if it picks an item by id.
	/// </summary>
	public int? GetIndex() => IsBracket && !IsQuoted && Text.All(char.IsAsciiDigit) && int.TryParse(Text, out var index) ? index : null;

	public override string ToString() => IsBracket ? (IsQuoted ? $"['{Text}']" : $"[{Text}]") : Text;
}

/// <summary>
/// Parses the paths that mod operations use to find the data they change, such as <c>properties[hair].values[red].frequencyWhen[0]</c>.
/// </summary>
public static class ModPath
{
	public static bool TryParse(string path, [NotNullWhen(true)] out IReadOnlyList<ModPathSegment>? segments, [NotNullWhen(false)] out string? error)
	{
		var parsed = new List<ModPathSegment>();
		error = path.Length == 0 ? OurResources.ModPathEmpty : null;
		var index = 0;
		while (error is null && index < path.Length)
		{
			var c = path[index];
			if (c == '[')
			{
				if (TryReadBracket(path, ref index) is { } segment)
					parsed.Add(segment);
				else
					error = OurResources.ModPathMalformed.FormatCurrentCulture(index + 1);
			}
			else if (c == '.' && parsed.Count != 0)
			{
				index++;
				if (TryReadName(path, ref index) is { } name)
					parsed.Add(name);
				else
					error = OurResources.ModPathMalformed.FormatCurrentCulture(index + 1);
			}
			else if (parsed.Count == 0 && TryReadName(path, ref index) is { } name)
			{
				parsed.Add(name);
			}
			else
			{
				error = OurResources.ModPathMalformed.FormatCurrentCulture(index + 1);
			}
		}

		segments = error is null ? parsed : null;
		return error is null;
	}

	/// <summary>
	/// Gets the text of a path made of the given steps.
	/// </summary>
	public static string Format(IEnumerable<ModPathSegment> segments)
	{
		var text = "";
		foreach (var segment in segments)
			text += segment.IsBracket || text.Length == 0 ? segment.ToString() : $".{segment}";
		return text;
	}

	/// <summary>
	/// Reads a name that ends at a dot, an opening bracket, or the end of the path. Returns null if it's empty or
	/// contains a closing bracket.
	/// </summary>
	private static ModPathSegment? TryReadName(string path, ref int index)
	{
		var start = index;
		while (index < path.Length && path[index] is not ('.' or '[' or ']'))
			index++;

		var isValid = index > start && (index == path.Length || path[index] != ']');
		return isValid ? new ModPathSegment(path[start..index], IsBracket: false, IsQuoted: false) : null;
	}

	/// <summary>
	/// Reads a bracket step, starting at its opening bracket: either <c>[text]</c> or <c>['text']</c>, where only the
	/// quoted form can contain a closing bracket. Returns null if it isn't closed or is empty.
	/// </summary>
	private static ModPathSegment? TryReadBracket(string path, ref int index)
	{
		ModPathSegment? segment = null;
		var isQuoted = index + 1 < path.Length && path[index + 1] == '\'';
		var start = index + (isQuoted ? 2 : 1);
		var end = isQuoted ? path.IndexOf("']", start, StringComparison.Ordinal) : path.IndexOf(']', start);
		if (end > start)
		{
			segment = new ModPathSegment(path[start..end], IsBracket: true, isQuoted);
			index = end + (isQuoted ? 2 : 1);
		}

		return segment;
	}
}
