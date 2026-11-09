using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace OperaEtDies.Data;

/// <summary>
/// Text from the data files with placeholders that are filled in when the text is shown. A placeholder is
/// "{id}" or "{id.field}", optionally followed by a format, as in "{id.field:first_upper}". The only format is
/// "first_upper", which capitalizes the first character. Literal braces are written "{{" and "}}".
/// </summary>
public sealed class TextTemplate
{
	public static bool TryParse(string text, [NotNullWhen(true)] out TextTemplate? template)
	{
		var segments = new List<Segment>();
		var literal = new StringBuilder();
		var isValid = true;
		var index = 0;
		while (isValid && index < text.Length)
		{
			var ch = text[index];
			var next = index + 1 < text.Length ? text[index + 1] : '\0';
			if ((ch == '{' && next == '{') || (ch == '}' && next == '}'))
			{
				literal.Append(ch);
				index += 2;
			}
			else if (ch == '{')
			{
				var end = text.IndexOf('}', index + 1);
				var placeholder = end < 0 ? null : ParsePlaceholder(text[(index + 1)..end]);
				if (placeholder is null)
				{
					isValid = false;
				}
				else
				{
					if (literal.Length != 0)
						segments.Add(new Segment(literal.ToString(), null));
					literal.Clear();
					segments.Add(new Segment(null, placeholder));
					index = end + 1;
				}
			}
			else if (ch == '}')
			{
				isValid = false;
			}
			else
			{
				literal.Append(ch);
				index++;
			}
		}

		if (literal.Length != 0)
			segments.Add(new Segment(literal.ToString(), null));

		template = isValid ? new TextTemplate(segments) : null;
		return isValid;
	}

	/// <summary>
	/// Parses a template that was already checked when the data was loaded.
	/// </summary>
	public static TextTemplate Parse(string text) =>
		TryParse(text, out var template) ? template : throw new FormatException($"The template \"{text}\" is not valid.");

	public IReadOnlyList<TextTemplatePlaceholder> Placeholders { get; }

	/// <summary>
	/// Creates the text, using the given function to get the text for each placeholder.
	/// </summary>
	public string Format(Func<TextTemplatePlaceholder, string> resolve)
	{
		var builder = new StringBuilder();
		foreach (var segment in m_segments)
			builder.Append(segment.Placeholder is { } placeholder ? ApplyFormat(resolve(placeholder), placeholder.Format) : segment.Literal);
		return builder.ToString();
	}

	/// <summary>
	/// Whether a placeholder's format is one that templates support.
	/// </summary>
	public static bool IsKnownFormat(string format) => format == FirstUpperFormat;

	private TextTemplate(IReadOnlyList<Segment> segments)
	{
		m_segments = segments;
		Placeholders = segments.Select(x => x.Placeholder).OfType<TextTemplatePlaceholder>().ToList();
	}

	private static TextTemplatePlaceholder? ParsePlaceholder(string content)
	{
		if (content.Contains('{', StringComparison.Ordinal))
			return null;

		var formatParts = content.Split(':');
		var parts = formatParts[0].Split('.');
		if (formatParts.Length > 2 || formatParts.Any(x => x.Length == 0) || parts.Length > 2 || parts.Any(x => x.Length == 0))
			return null;

		return new TextTemplatePlaceholder(parts[0], parts.Length == 2 ? parts[1] : null, formatParts.Length == 2 ? formatParts[1] : null);
	}

	private static string ApplyFormat(string text, string? format) => format switch
	{
		FirstUpperFormat when text.Length != 0 => text[..1].ToUpper(CultureInfo.CurrentCulture) + text[1..],
		_ => text,
	};

	private const string FirstUpperFormat = "first_upper";

	private sealed record Segment(string? Literal, TextTemplatePlaceholder? Placeholder);

	private readonly IReadOnlyList<Segment> m_segments;
}

/// <summary>
/// A placeholder in a <see cref="TextTemplate"/>: the id of what to show, optionally a named field of it, and
/// optionally a format applied to the text.
/// </summary>
public sealed record TextTemplatePlaceholder(string Id, string? Field, string? Format = null);
