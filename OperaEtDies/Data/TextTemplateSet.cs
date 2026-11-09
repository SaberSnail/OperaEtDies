namespace OperaEtDies.Data;

/// <summary>
/// Templates keyed by id. A placeholder that names another template in the set is replaced with that template's text.
/// </summary>
public sealed class TextTemplateSet
{
	public TextTemplateSet(IEnumerable<KeyValuePair<string, TextTemplate>> templates)
	{
		m_templates = new Dictionary<string, TextTemplate>(StringComparer.Ordinal);
		foreach (var (id, template) in templates)
			m_templates.TryAdd(id, template);
	}

	public bool Contains(string id) => m_templates.ContainsKey(id);

	/// <summary>
	/// Creates the text of the template with the given id. Placeholders that name a template are filled in with
	/// that template's text, and the given function fills in the rest. Templates must not refer to themselves,
	/// which validation checks when the data is loaded.
	/// </summary>
	public string Format(string id, Func<TextTemplatePlaceholder, string> resolve) =>
		m_templates[id].Format(x => m_templates.ContainsKey(x.Id) ? Format(x.Id, resolve) : resolve(x));

	private readonly Dictionary<string, TextTemplate> m_templates;
}
