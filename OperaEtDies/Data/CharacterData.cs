using System.IO;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// The properties that describe a character, and the templates used to name and describe characters, loaded from Characters.json.
/// </summary>
public sealed class CharacterData : IValidatedData
{
	public const string FileName = "Characters.json";

	public static DataLoadResult<CharacterData> Load(string dataDirectory) =>
		JsonDataLoader.Load<CharacterData>(Path.Combine(dataDirectory, FileName));

	/// <summary>
	/// The id of the template that creates a character's full name.
	/// </summary>
	public const string FullNameTemplateId = "fullName";

	/// <summary>
	/// The id of the template that creates a description of a character.
	/// </summary>
	public const string DescriptionTemplateId = "description";

	/// <summary>
	/// Templates that create text about a character from their properties and from other templates.
	/// </summary>
	public required IReadOnlyList<CharacterTemplate> Templates { get; init; }

	public required IReadOnlyList<CharacterProperty> Properties { get; init; }

	public IEnumerable<string> Validate()
	{
		foreach (var error in DataValidation.ValidateIdentifiedItems("properties", Properties, x => x.Id, x => x.Title))
			yield return error;

		// Items that are empty or missing an id are already reported above.
		var properties = Properties.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();

		// String defaults can only be keyed by properties defined before them, which also rules out cycles.
		var earlierProperties = new Dictionary<string, CharacterProperty>(StringComparer.Ordinal);
		foreach (var property in properties)
		{
			foreach (var error in ValidateProperty(property, earlierProperties).Distinct(StringComparer.Ordinal))
				yield return error;
			earlierProperties.TryAdd(property.Id, property);
		}

		foreach (var error in ValidateTemplates(earlierProperties))
			yield return error;
	}

	private static IEnumerable<string> ValidateProperty(CharacterProperty property, IReadOnlyDictionary<string, CharacterProperty> earlierProperties)
	{
		var id = property.Id;
		var type = property.Type;

		if (type == CharacterPropertyType.ValueList)
		{
			if (property.Values is null)
			{
				yield return OurResources.CharacterSettingMissing.FormatCurrentCulture(id, "values");
			}
			else
			{
				foreach (var error in ValidateOptions(id, property.Values))
					yield return error;
			}
		}
		else if (property.Values is not null)
		{
			yield return OurResources.CharacterSettingNotAllowed.FormatCurrentCulture(id, "values");
		}

		if (type == CharacterPropertyType.Integer)
		{
			if (property.Range is null && (property.Player?.Range is null || property.Npc?.Range is null))
				yield return OurResources.CharacterSettingMissing.FormatCurrentCulture(id, "range");
			if (property.Range is not null)
			{
				foreach (var error in ValidateRange($"{id}.range", property.Range))
					yield return error;
			}
		}
		else if (property.Range is not null)
		{
			yield return OurResources.CharacterSettingNotAllowed.FormatCurrentCulture(id, "range");
		}

		if (type == CharacterPropertyType.String)
		{
			if (property.Defaults is not null)
			{
				foreach (var error in ValidateDefaults($"{id}.defaults", property.Defaults, earlierProperties))
					yield return error;
			}
		}
		else if (property.Defaults is not null)
		{
			yield return OurResources.CharacterSettingNotAllowed.FormatCurrentCulture(id, "defaults");
		}

		foreach (var role in s_roles)
		{
			if (property.GetRoleSettings(role) is { } settings)
			{
				foreach (var error in ValidateRoleSettings(property, role, settings, earlierProperties))
					yield return error;
			}
		}

		if (type == CharacterPropertyType.String)
		{
			foreach (var error in ValidateDefaultsCoverKeys(property, earlierProperties))
				yield return error;
		}
	}

	private static IEnumerable<string> ValidateOptions(string propertyId, IReadOnlyList<CharacterPropertyOption?> options)
	{
		var listName = $"{propertyId}.values";
		foreach (var error in DataValidation.ValidateIdentifiedItems(listName, options, x => x.Id, x => x.Title))
			yield return error;
		foreach (var error in DataValidation.ValidateItemFrequencies(listName, options, x => x.Id, x => x.Frequency))
			yield return error;

		foreach (var option in options.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)))
		{
			foreach (var (name, text) in option!.Text)
			{
				if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(text))
					yield return OurResources.CharacterOptionTextBlank.FormatCurrentCulture(option.Id, propertyId, name);
			}
		}
	}

	private static IEnumerable<string> ValidateRange(string path, IntegerRange range)
	{
		if (range.Maximum < range.Minimum)
			yield return OurResources.IntegerRangeInvalid.FormatCurrentCulture(path, range.Maximum, range.Minimum);
	}

	private static IEnumerable<string> ValidateRoleSettings(CharacterProperty property, CharacterRole role, CharacterRoleSettings settings, IReadOnlyDictionary<string, CharacterProperty> earlierProperties)
	{
		var id = property.Id;
		var path = $"{id}.{GetRoleName(role)}";

		if (settings.Values is not null)
		{
			if (property.Type != CharacterPropertyType.ValueList)
			{
				yield return OurResources.CharacterSettingNotAllowed.FormatCurrentCulture(id, $"{GetRoleName(role)}.values");
			}
			else
			{
				foreach (var error in ValidateRoleValues($"{path}.values", settings.Values, property.Values ?? []))
					yield return error;
			}
		}

		if (settings.Range is not null)
		{
			if (property.Type != CharacterPropertyType.Integer)
			{
				yield return OurResources.CharacterSettingNotAllowed.FormatCurrentCulture(id, $"{GetRoleName(role)}.range");
			}
			else
			{
				foreach (var error in ValidateRange($"{path}.range", settings.Range))
					yield return error;
			}
		}

		if (settings.Defaults is not null)
		{
			if (property.Type != CharacterPropertyType.String)
			{
				yield return OurResources.CharacterSettingNotAllowed.FormatCurrentCulture(id, $"{GetRoleName(role)}.defaults");
			}
			else
			{
				foreach (var error in ValidateDefaults($"{path}.defaults", settings.Defaults, earlierProperties))
					yield return error;
			}
		}
	}

	private static IEnumerable<string> ValidateRoleValues(string listName, IReadOnlyList<string?> valueIds, IReadOnlyList<CharacterPropertyOption?> options)
	{
		if (valueIds.Count == 0)
		{
			yield return OurResources.DataListEmpty.FormatCurrentCulture(listName);
		}
		else
		{
			var optionIds = options.Where(x => x is not null).Select(x => x!.Id).ToHashSet(StringComparer.Ordinal);
			var seenIds = new HashSet<string>(StringComparer.Ordinal);
			foreach (var valueId in valueIds)
			{
				if (string.IsNullOrWhiteSpace(valueId))
					yield return OurResources.DataListItemIdMissing.FormatCurrentCulture(listName);
				else if (!seenIds.Add(valueId))
					yield return OurResources.DataListItemIdDuplicate.FormatCurrentCulture(valueId, listName);
				else if (!optionIds.Contains(valueId))
					yield return OurResources.RoleValueUnknown.FormatCurrentCulture(listName, valueId);
			}
		}
	}

	private static IEnumerable<string> ValidateDefaults(string path, StringDefaults defaults, IReadOnlyDictionary<string, CharacterProperty> earlierProperties)
	{
		var isKeyed = defaults.KeyedBy is not null;
		if (isKeyed ? defaults.ValuesByKey is null || defaults.Values is not null : defaults.Values is null || defaults.ValuesByKey is not null)
		{
			yield return OurResources.StringDefaultsFormInvalid.FormatCurrentCulture(path);
		}
		else if (defaults.Values is not null)
		{
			foreach (var error in DataValidation.ValidateWeightedStrings($"{path}.values", defaults.Values))
				yield return error;
		}
		else if (defaults.KeyedBy is { } keyedBy && defaults.ValuesByKey is { } valuesByKey)
		{
			var keyProperty = GetKeyProperty(keyedBy, earlierProperties);
			if (keyProperty is null)
				yield return OurResources.StringDefaultsKeyedByInvalid.FormatCurrentCulture(path, keyedBy);

			var keyIds = (keyProperty?.Values ?? []).Where(x => x is not null).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
			foreach (var (key, values) in valuesByKey)
			{
				if (keyProperty is not null && !keyIds.Contains(key))
					yield return OurResources.StringDefaultsKeyUnknown.FormatCurrentCulture(path, key, keyedBy);
				foreach (var error in DataValidation.ValidateWeightedStrings($"{path}.valuesByKey.{key}", values))
					yield return error;
			}
		}
	}

	/// <summary>
	/// Checks that keyed defaults have values for every value the key property can have, for each role.
	/// </summary>
	private static IEnumerable<string> ValidateDefaultsCoverKeys(CharacterProperty property, IReadOnlyDictionary<string, CharacterProperty> earlierProperties)
	{
		foreach (var role in s_roles)
		{
			var defaults = property.GetDefaults(role);
			if (defaults?.KeyedBy is { } keyedBy && defaults.ValuesByKey is { } valuesByKey && GetKeyProperty(keyedBy, earlierProperties) is { } keyProperty)
			{
				var path = property.GetRoleSettings(role)?.Defaults is not null ? $"{property.Id}.{GetRoleName(role)}.defaults" : $"{property.Id}.defaults";
				foreach (var option in keyProperty.GetOptions(role).Where(x => x is not null && !valuesByKey.ContainsKey(x.Id)))
					yield return OurResources.StringDefaultsKeyMissing.FormatCurrentCulture(path, option.Id, keyedBy);
			}
		}
	}

	private static CharacterProperty? GetKeyProperty(string keyedBy, IReadOnlyDictionary<string, CharacterProperty> earlierProperties) =>
		earlierProperties.GetValueOrDefault(keyedBy) is { Type: CharacterPropertyType.ValueList } keyProperty ? keyProperty : null;

	private IEnumerable<string> ValidateTemplates(IReadOnlyDictionary<string, CharacterProperty> properties)
	{
		var templateIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var template in Templates)
		{
			// JSON deserialization doesn't stop null list items, even when the type is non-nullable.
			if (template is null)
			{
				yield return OurResources.DataListItemEmpty.FormatCurrentCulture("templates");
			}
			else if (string.IsNullOrWhiteSpace(template.Id))
			{
				yield return OurResources.DataListItemIdMissing.FormatCurrentCulture("templates");
			}
			else
			{
				if (!templateIds.Add(template.Id))
					yield return OurResources.DataListItemIdDuplicate.FormatCurrentCulture(template.Id, "templates");
				if (properties.ContainsKey(template.Id))
					yield return OurResources.TemplateIdIsProperty.FormatCurrentCulture(template.Id);
			}
		}

		foreach (var requiredId in s_requiredTemplateIds.Where(x => !templateIds.Contains(x)))
			yield return OurResources.TemplateRequired.FormatCurrentCulture(requiredId);

		var parsedTemplates = new Dictionary<string, TextTemplate>(StringComparer.Ordinal);
		foreach (var template in Templates.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)))
		{
			if (string.IsNullOrWhiteSpace(template.Template))
				yield return OurResources.TemplateBlank.FormatCurrentCulture(template.Id);
			else if (!TextTemplate.TryParse(template.Template, out var parsed))
				yield return OurResources.TemplateMalformed.FormatCurrentCulture(template.Id);
			else
				parsedTemplates.TryAdd(template.Id, parsed);
		}

		foreach (var (id, template) in parsedTemplates)
		{
			foreach (var error in ValidatePlaceholders(id, template, properties, templateIds).Distinct(StringComparer.Ordinal))
				yield return error;
		}

		foreach (var id in parsedTemplates.Keys.Where(x => RefersToItself(x, parsedTemplates)))
			yield return OurResources.TemplateCycle.FormatCurrentCulture(id);
	}

	/// <summary>
	/// Checks whether a template uses itself, directly or through other templates.
	/// </summary>
	private static bool RefersToItself(string id, IReadOnlyDictionary<string, TextTemplate> templates)
	{
		IEnumerable<string> GetReferences(string templateId) =>
			templates[templateId].Placeholders.Select(x => x.Id).Where(templates.ContainsKey);

		var visited = new HashSet<string>(StringComparer.Ordinal);
		var pending = new Stack<string>(GetReferences(id));
		var found = false;
		while (!found && pending.TryPop(out var next))
		{
			if (next == id)
			{
				found = true;
			}
			else if (visited.Add(next))
			{
				foreach (var reference in GetReferences(next))
					pending.Push(reference);
			}
		}

		return found;
	}

	private static IEnumerable<string> ValidatePlaceholders(string templateId, TextTemplate template, IReadOnlyDictionary<string, CharacterProperty> properties, IReadOnlySet<string> templateIds)
	{
		foreach (var placeholder in template.Placeholders)
		{
			if (placeholder.Format is { } format && !TextTemplate.IsKnownFormat(format))
				yield return OurResources.TemplateFormatUnknown.FormatCurrentCulture(templateId, format);

			var property = properties.GetValueOrDefault(placeholder.Id);
			if (property is null)
			{
				if (!templateIds.Contains(placeholder.Id))
					yield return OurResources.TemplatePropertyUnknown.FormatCurrentCulture(templateId, placeholder.Id);
				else if (placeholder.Field is { } templateField)
					yield return OurResources.TemplateFieldNotAllowed.FormatCurrentCulture(templateId, placeholder.Id, templateField);
			}
			else if (placeholder.Field is { } field)
			{
				if (property.Type != CharacterPropertyType.ValueList)
				{
					yield return OurResources.TemplateFieldNotAllowed.FormatCurrentCulture(templateId, placeholder.Id, field);
				}
				else
				{
					foreach (var option in (property.Values ?? []).Where(x => x is not null && !x.Text.ContainsKey(field)))
						yield return OurResources.TemplateFieldMissing.FormatCurrentCulture(templateId, placeholder.Id, field, option.Id);
				}
			}
		}
	}

	private static string GetRoleName(CharacterRole role) => role switch
	{
		CharacterRole.Player => "player",
		CharacterRole.Npc => "npc",
		_ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
	};

	private static readonly CharacterRole[] s_roles = [CharacterRole.Player, CharacterRole.Npc];
	private static readonly string[] s_requiredTemplateIds = [FullNameTemplateId, DescriptionTemplateId];
}
