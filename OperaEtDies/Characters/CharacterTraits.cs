using System.Globalization;
using OperaEtDies.Data;

namespace OperaEtDies.Characters;

/// <summary>
/// The values of the properties that describe who a character is, used to create a person in the game world.
/// Values are keyed by property id: a string for string properties, an int for integer properties, and a
/// <see cref="CharacterPropertyOption"/> for value list properties. Properties whose conditions aren't met have no value.
/// </summary>
public sealed class CharacterTraits
{
	public CharacterTraits(IReadOnlyDictionary<string, object> values)
	{
		m_values = new Dictionary<string, object>(values, StringComparer.Ordinal);
	}

	public IReadOnlyDictionary<string, object> Values => m_values;

	/// <summary>
	/// Creates a copy of the traits with one property's value changed.
	/// </summary>
	public CharacterTraits With(string propertyId, object value) =>
		new(new Dictionary<string, object>(m_values, StringComparer.Ordinal) { [propertyId] = value });

	public string GetString(string propertyId) => (string) m_values[propertyId];

	public int GetInteger(string propertyId) => (int) m_values[propertyId];

	public CharacterPropertyOption GetOption(string propertyId) => (CharacterPropertyOption) m_values[propertyId];

	public bool HasValue(string propertyId) => m_values.ContainsKey(propertyId);

	/// <summary>
	/// Gets the value id of a value list property, or null if it has no value.
	/// </summary>
	public string? GetValueId(string propertyId) => (m_values.GetValueOrDefault(propertyId) as CharacterPropertyOption)?.Id;

	/// <summary>
	/// Creates text from a template, filling in each placeholder with a property's value. A value list property
	/// shows its value's title, or the named text of its value when the placeholder has a field.
	/// </summary>
	public string Format(TextTemplate template) => template.Format(GetText);

	/// <summary>
	/// Creates text from the template in the set with the given id, using the variant whose conditions this
	/// character meets. Placeholders can also name other templates in the set. Values hidden by the obscuring are
	/// treated as if the character didn't have them.
	/// </summary>
	public string Format(TextTemplateSet templates, string templateId, CharacterObscuring? obscuring = null)
	{
		object? GetVisibleValue(string propertyId) =>
			m_values.GetValueOrDefault(propertyId) is { } value && !(obscuring?.Hides(propertyId, value) ?? false) ? value : null;

		return templates.Format(templateId, x => GetText(GetVisibleValue(x.Id), x.Field), GetVisibleValue);
	}

	public string GetFullName(TextTemplateSet templates, CharacterObscuring? obscuring = null) => Format(templates, CharacterData.FullNameTemplateId, obscuring);

	public string GetDescription(TextTemplateSet templates, CharacterObscuring? obscuring = null) => Format(templates, CharacterData.DescriptionTemplateId, obscuring);

	private string GetText(TextTemplatePlaceholder placeholder) => GetText(m_values.GetValueOrDefault(placeholder.Id), placeholder.Field);

	private static string GetText(object? value, string? fieldName) => (value, fieldName) switch
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
