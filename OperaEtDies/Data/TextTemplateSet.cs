using System.Text;

namespace OperaEtDies.Data;

/// <summary>
/// Templates keyed by id, each with one or more variants chosen by a condition. A placeholder that names another
/// template in the set is replaced with that template's text.
/// </summary>
public sealed class TextTemplateSet
{
	public TextTemplateSet(IEnumerable<KeyValuePair<string, IReadOnlyList<TextTemplateVariant>>> templates)
	{
		m_templates = new Dictionary<string, IReadOnlyList<TextTemplateVariant>>(StringComparer.Ordinal);
		foreach (var (id, variants) in templates)
			m_templates.TryAdd(id, variants);
	}

	public bool Contains(string id) => m_templates.ContainsKey(id);

	/// <summary>
	/// Creates the text of the template with the given id, using its first variant whose conditions are met (or empty
	/// text if none is). Placeholders that name a template are filled in with that template's text, and the given
	/// function fills in the rest. Runs of spaces are collapsed to one, and spaces at the start and end or before
	/// punctuation are removed, so templates can leave out parts of the text. Templates must not refer to themselves,
	/// which validation checks when the data is loaded.
	/// </summary>
	/// <param name="id">The id of the template.</param>
	/// <param name="resolve">Gets the text for a placeholder that doesn't name a template.</param>
	/// <param name="getValue">Gets the visible value of a property, or null if it has none, for checking conditions.</param>
	public string Format(string id, Func<TextTemplatePlaceholder, string> resolve, Func<string, object?> getValue)
	{
		string? GetValueId(string propertyId) => (getValue(propertyId) as CharacterPropertyOption)?.Id;

		var variant = m_templates[id].FirstOrDefault(x => CharacterCondition.IsMet(x.When, GetValueId, h => getValue(h) is not null));
		var text = variant?.Template.Format(x => m_templates.ContainsKey(x.Id) ? Format(x.Id, resolve, getValue) : resolve(x)) ?? "";
		return CollapseSpaces(text);
	}

	private static string CollapseSpaces(string text)
	{
		var builder = new StringBuilder(text.Length);
		foreach (var ch in text)
		{
			// A space left before punctuation by an empty part of the text is removed.
			if (s_punctuation.Contains(ch) && builder.Length != 0 && builder[^1] == ' ')
				builder.Length--;

			if (ch != ' ' || (builder.Length != 0 && builder[^1] != ' '))
				builder.Append(ch);
		}

		if (builder.Length != 0 && builder[^1] == ' ')
			builder.Length--;

		return builder.ToString();
	}

	private static readonly char[] s_punctuation = ['.', ',', ';', ':', '!', '?'];

	private readonly Dictionary<string, IReadOnlyList<TextTemplateVariant>> m_templates;
}

/// <summary>
/// One of the variants of a template in a <see cref="TextTemplateSet"/>, used when its condition is met by what can
/// be seen of the character.
/// </summary>
public sealed record TextTemplateVariant(CharacterCondition? When, TextTemplate Template);
