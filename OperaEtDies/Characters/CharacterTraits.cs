using System.Globalization;
using OperaEtDies.Data;

namespace OperaEtDies.Characters;

/// <summary>
/// The values of the properties that describe who a character is, used to create a person in the game world.
/// Values are keyed by property id: a string for string properties, an int for integer properties, and a
/// <see cref="CharacterPropertyOption"/> for value list properties.
/// </summary>
public sealed class CharacterTraits
{
	public CharacterTraits(IReadOnlyDictionary<string, object> values)
	{
		m_values = new Dictionary<string, object>(values, StringComparer.Ordinal);
	}

	public IReadOnlyDictionary<string, object> Values => m_values;

	public string GetString(string propertyId) => (string) m_values[propertyId];

	public int GetInteger(string propertyId) => (int) m_values[propertyId];

	public CharacterPropertyOption GetOption(string propertyId) => (CharacterPropertyOption) m_values[propertyId];

	/// <summary>
	/// Creates text from a template, filling in each placeholder with a property's value. A value list property
	/// shows its value's title, or the named text of its value when the placeholder has a field.
	/// </summary>
	public string Format(TextTemplate template) => template.Format(GetText);

	/// <summary>
	/// Creates text from the template in the set with the given id. Placeholders can also name other templates in the set.
	/// </summary>
	public string Format(TextTemplateSet templates, string templateId) => templates.Format(templateId, GetText);

	public string GetFullName(TextTemplateSet templates) => Format(templates, CharacterData.FullNameTemplateId);

	public string GetDescription(TextTemplateSet templates) => Format(templates, CharacterData.DescriptionTemplateId);

	private string GetText(TextTemplatePlaceholder placeholder) => (m_values.GetValueOrDefault(placeholder.Id), placeholder.Field) switch
	{
		(CharacterPropertyOption option, { } field) => option.Text.GetValueOrDefault(field) ?? "",
		(_, not null) => "",
		(string text, null) => text,
		(int number, null) => number.ToString(CultureInfo.CurrentCulture),
		(CharacterPropertyOption option, null) => option.Title,
		_ => "",
	};

	private readonly Dictionary<string, object> m_values;
}
