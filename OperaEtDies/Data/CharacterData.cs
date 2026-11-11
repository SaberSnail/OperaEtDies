using System.IO;
using GoldenAnvil.Utility;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// The properties that describe a character, and the templates used to name and describe characters, loaded from Characters.json.
/// </summary>
public sealed class CharacterData : IValidatedData
{
	public const string FileName = "Characters.json";

	public static DataLoadResult<CharacterData> Load(string dataDirectory) => Load(dataDirectory, []);

	/// <summary>
	/// Loads the file from the data folder, with the changes from the mod folders applied in order.
	/// </summary>
	public static DataLoadResult<CharacterData> Load(string dataDirectory, IReadOnlyList<string> modDirectories) => Load(dataDirectory, modDirectories, null);

	/// <summary>
	/// Loads the file from the data folder, with the changes from the mod folders applied in order. A property whose
	/// values come from the professions gets their values, if they loaded.
	/// </summary>
	public static DataLoadResult<CharacterData> Load(string dataDirectory, IReadOnlyList<string> modDirectories, IReadOnlyList<Profession>? professions) =>
		JsonDataLoader.Load<CharacterData>(Path.Combine(dataDirectory, FileName), modDirectories, [], x => x.WithProfessions(professions));

	/// <summary>
	/// The id of the template that creates a character's full name.
	/// </summary>
	public const string FullNameTemplateId = "fullName";

	/// <summary>
	/// The id of the template that creates a description of a character.
	/// </summary>
	public const string DescriptionTemplateId = "description";

	/// <summary>
	/// The most combinations of condition values that validation checks for each role.
	/// </summary>
	public const int MaxConditionCombinations = 10000;

	/// <summary>
	/// The most properties that obscurers can hide that one template can use, since validation checks each of them
	/// both visible and hidden.
	/// </summary>
	public const int MaxHideablePropertiesPerTemplate = 10;

	/// <summary>
	/// Templates that create text about a character from their properties and from other templates.
	/// </summary>
	public required IReadOnlyList<CharacterTemplate> Templates { get; init; }

	public required IReadOnlyList<CharacterProperty> Properties { get; init; }

	/// <summary>
	/// Things that can keep some of a character's properties from being seen or heard.
	/// </summary>
	public IReadOnlyList<CharacterObscurer> Obscurers { get; init; } = [];

	/// <summary>
	/// Fills in the values of the properties whose values come from the professions, and the defaults of string
	/// properties keyed by them. Without professions, those properties are left without values.
	/// </summary>
	private CharacterData WithProfessions(IReadOnlyList<Profession>? professions)
	{
		var data = this;
		if (professions is not null && Properties.Any(TakesProfessions))
		{
			var items = professions.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();
			var values = items.Select(x => x.ToOption()).ToList();
			var professionPropertyIds = Properties.Where(TakesProfessions).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
			data = new CharacterData
			{
				Templates = Templates,
				Properties = Properties.Select(x => x is null ? x! : TakesProfessions(x) ? x.WithValues(values) : x.WithProfessionDefaults(professionPropertyIds, items)).ToList(),
				Obscurers = Obscurers,
				LowChanceWarning = LowChanceWarning,
			};
		}

		return data;
	}

	/// <summary>
	/// Whether a property should get the professions as its values: a value list property with no values of its own
	/// that takes its values from the professions. Anything else is reported when the data is checked.
	/// </summary>
	private static bool TakesProfessions(CharacterProperty? property) =>
		property is { Type: CharacterPropertyType.ValueList, Values: null, ValuesFrom: CharacterProperty.ProfessionsSource };

	/// <summary>
	/// A percentage chance. Any value that has this chance or less of being chosen is reported as a warning when the
	/// data is loaded. Optional.
	/// </summary>
	public double? LowChanceWarning { get; init; }

	public IEnumerable<string> Validate()
	{
		var errors = new List<string>();
		errors.AddRange(DataValidation.ValidateIdentifiedItems("properties", Properties, x => x.Id, x => x.Title));

		// Items that are empty or missing an id are already reported above.
		var properties = Properties.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();

		var allProperties = new Dictionary<string, CharacterProperty>(StringComparer.Ordinal);
		foreach (var property in properties)
			allProperties.TryAdd(property.Id, property);

		foreach (var property in properties)
			errors.AddRange(ValidateProperty(property, allProperties).Distinct(StringComparer.Ordinal));

		// Only one property can come right after another.
		foreach (var group in properties.Where(x => x.Follows is not null).GroupBy(x => x.Follows!, StringComparer.Ordinal).Where(x => x.Count() > 1))
			errors.Add(OurResources.PropertyFollowedTwice.FormatCurrentCulture(group.Key, string.Join(", ", group.Select(x => $"\"{x.Id}\""))));

		// Properties that depend on each other can't be chosen in any order.
		var cycle = CharacterDependencies.FindCycle(properties);
		if (cycle.Count != 0)
			errors.Add(OurResources.PropertyDependencyCycle.FormatCurrentCulture(string.Join(", ", cycle.Select(x => $"\"{x.Id}\""))));

		errors.AddRange(ValidateObscurers(allProperties));
		if (LowChanceWarning is { } lowChanceWarning && !(lowChanceWarning >= 0 && lowChanceWarning < 100))
			errors.Add(OurResources.LowChanceWarningInvalid.FormatCurrentCulture(lowChanceWarning));
		errors.AddRange(ValidateTemplates(allProperties));

		// Checking every combination of condition values only works once the conditions themselves are valid.
		if (errors.Count == 0)
			errors.AddRange(ValidateConditionCombinations(CharacterDependencies.GetOrder(properties)).Distinct(StringComparer.Ordinal));

		return errors;
	}

	private static IEnumerable<string> ValidateProperty(CharacterProperty property, IReadOnlyDictionary<string, CharacterProperty> allProperties)
	{
		var id = property.Id;
		var type = property.Type;

		if (property.When is not null)
		{
			foreach (var error in ValidateCondition($"{id}.when", property.When, allProperties, isTemplate: false))
				yield return error;
		}

		if (property.Follows is { } follows)
		{
			if (follows == id)
				yield return OurResources.PropertyFollowsItself.FormatCurrentCulture(id);
			else if (!allProperties.ContainsKey(follows))
				yield return OurResources.PropertyFollowsUnknown.FormatCurrentCulture(id, follows);
		}

		if (property.ValuesFrom is { } valuesFrom)
		{
			if (type != CharacterPropertyType.ValueList)
				yield return OurResources.CharacterSettingNotAllowed.FormatCurrentCulture(id, "valuesFrom");
			else if (valuesFrom != CharacterProperty.ProfessionsSource)
				yield return OurResources.ValuesFromUnknown.FormatCurrentCulture(id, valuesFrom, CharacterProperty.ProfessionsSource);
			else if (property.Values is not null && !property.HasSourceValues)
				yield return OurResources.ValuesAndValuesFrom.FormatCurrentCulture(id);
			else if (property.Values is null)
				yield return OurResources.ValuesFromNotLoaded.FormatCurrentCulture(id, valuesFrom);
		}

		if (type == CharacterPropertyType.ValueList)
		{
			if (property.Values is null)
			{
				if (property.ValuesFrom is null)
					yield return OurResources.CharacterSettingMissing.FormatCurrentCulture(id, "values");
			}
			else
			{
				foreach (var error in ValidateOptions(id, property.Values, allProperties))
					yield return error;
			}

			if (property.Shape is { } shape)
			{
				foreach (var error in shape.Validate($"{id}.shape"))
					yield return error;
				foreach (var option in (property.Values ?? []).Where(x => x is not null && (x.Frequency != Frequency.Common || x.FrequencyWhen is not null)))
					yield return OurResources.ShapeFrequencyNotAllowed.FormatCurrentCulture(option.Id, id);
			}
		}
		else if (property.Shape is not null)
		{
			yield return OurResources.CharacterSettingNotAllowed.FormatCurrentCulture(id, "shape");
		}

		if (type != CharacterPropertyType.ValueList && property.Values is not null)
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

			if (property.Groups is not null)
			{
				foreach (var error in ValidateGroups(id, property.Groups))
					yield return error;
			}
		}
		else
		{
			if (property.Range is not null)
				yield return OurResources.CharacterSettingNotAllowed.FormatCurrentCulture(id, "range");
			if (property.Groups is not null)
				yield return OurResources.CharacterSettingNotAllowed.FormatCurrentCulture(id, "groups");
		}

		if (type == CharacterPropertyType.String)
		{
			if (property.Defaults is not null)
			{
				foreach (var error in ValidateDefaults($"{id}.defaults", property.Defaults, allProperties))
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
				foreach (var error in ValidateRoleSettings(property, role, settings, allProperties))
					yield return error;
			}
		}

		if (type == CharacterPropertyType.String)
		{
			foreach (var error in ValidateDefaultsCoverKeys(property, allProperties))
				yield return error;
		}
	}

	private static IEnumerable<string> ValidateOptions(string propertyId, IReadOnlyList<CharacterPropertyOption?> options, IReadOnlyDictionary<string, CharacterProperty> allProperties)
	{
		var listName = $"{propertyId}.values";
		foreach (var error in DataValidation.ValidateIdentifiedItems(listName, options, x => x.Id, x => x.Title))
			yield return error;

		foreach (var option in options.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)))
		{
			foreach (var (name, text) in option!.Text)
			{
				if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(text))
					yield return OurResources.CharacterOptionTextBlank.FormatCurrentCulture(option.Id, propertyId, name);
			}

			if (option.When is not null)
			{
				foreach (var error in ValidateCondition($"{listName}.{option.Id}.when", option.When, allProperties, isTemplate: false))
					yield return error;
			}

			if (option.Prominence is { } prominence && !CharacterObscurer.IsValidStrength(prominence))
				yield return OurResources.ProminenceInvalid.FormatCurrentCulture(option.Id, listName, prominence, CharacterObscurer.MinStrength, CharacterObscurer.MaxStrength);

			if (option.FrequencyWhen is { } frequencyWhen)
			{
				var overridesName = $"{listName}.{option.Id}.frequencyWhen";
				if (frequencyWhen.Count == 0)
					yield return OurResources.DataListEmpty.FormatCurrentCulture(overridesName);

				for (var index = 0; index < frequencyWhen.Count; index++)
				{
					// JSON deserialization doesn't stop null list items, even when the type is non-nullable.
					if (frequencyWhen[index] is null)
					{
						yield return OurResources.DataListItemEmpty.FormatCurrentCulture(overridesName);
					}
					else
					{
						foreach (var error in ValidateCondition($"{overridesName}[{index}].when", frequencyWhen[index].When, allProperties, isTemplate: false))
							yield return error;
					}
				}
			}
		}
	}

	/// <summary>
	/// Checks that an integer property's groups have unique ids, valid limits, and don't overlap each other.
	/// </summary>
	private static IEnumerable<string> ValidateGroups(string propertyId, IReadOnlyList<IntegerGroup?> groups)
	{
		var listName = $"{propertyId}.groups";
		if (groups.Count == 0)
			yield return OurResources.DataListEmpty.FormatCurrentCulture(listName);

		var ids = new HashSet<string>(StringComparer.Ordinal);
		var validGroups = new List<IntegerGroup>();
		foreach (var group in groups)
		{
			// JSON deserialization doesn't stop null list items, even when the type is non-nullable.
			if (group is null)
			{
				yield return OurResources.DataListItemEmpty.FormatCurrentCulture(listName);
			}
			else if (string.IsNullOrWhiteSpace(group.Id))
			{
				yield return OurResources.DataListItemIdMissing.FormatCurrentCulture(listName);
			}
			else
			{
				if (!ids.Add(group.Id))
					yield return OurResources.DataListItemIdDuplicate.FormatCurrentCulture(group.Id, listName);

				if (group is { Minimum: { } minimum, Maximum: { } maximum } && maximum < minimum)
				{
					yield return OurResources.IntegerRangeInvalid.FormatCurrentCulture($"{listName}.{group.Id}", maximum, minimum);
				}
				else
				{
					foreach (var other in validGroups.Where(x => x.Overlaps(group.Minimum ?? int.MinValue, group.Maximum ?? int.MaxValue)))
						yield return OurResources.IntegerGroupsOverlap.FormatCurrentCulture(other.Id, group.Id, propertyId);
					validGroups.Add(group);
				}
			}
		}
	}

	private IEnumerable<string> ValidateObscurers(IReadOnlyDictionary<string, CharacterProperty> properties)
	{
		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (var obscurer in Obscurers)
		{
			// JSON deserialization doesn't stop null list items, even when the type is non-nullable.
			if (obscurer is null)
			{
				yield return OurResources.DataListItemEmpty.FormatCurrentCulture("obscurers");
			}
			else if (string.IsNullOrWhiteSpace(obscurer.Id))
			{
				yield return OurResources.DataListItemIdMissing.FormatCurrentCulture("obscurers");
			}
			else
			{
				if (!ids.Add(obscurer.Id))
					yield return OurResources.DataListItemIdDuplicate.FormatCurrentCulture(obscurer.Id, "obscurers");

				if (obscurer.Hides is null || obscurer.Hides.Count == 0)
				{
					yield return OurResources.DataListEmpty.FormatCurrentCulture($"{obscurer.Id}.hides");
				}
				else
				{
					foreach (var (propertyId, strength) in obscurer.Hides)
					{
						if (propertyId != CharacterObscurer.AllProperties && !properties.ContainsKey(propertyId))
							yield return OurResources.ObscurerPropertyUnknown.FormatCurrentCulture(obscurer.Id, propertyId);
						if (!CharacterObscurer.IsValidStrength(strength))
							yield return OurResources.ObscurerStrengthInvalid.FormatCurrentCulture(obscurer.Id, propertyId, strength, CharacterObscurer.MinStrength, CharacterObscurer.MaxStrength);
					}
				}
			}
		}
	}

	/// <summary>
	/// Checks that a condition's values use value list properties and their values, and that its hidden properties
	/// exist. Only template conditions can use hidden properties.
	/// </summary>
	internal static IEnumerable<string> ValidateCondition(string path, CharacterCondition condition, IReadOnlyDictionary<string, CharacterProperty> properties, bool isTemplate)
	{
		if (condition.Values is null && condition.NotValues is null && condition.Hidden is null)
			yield return OurResources.ConditionEmpty.FormatCurrentCulture(path);

		if (condition.Values is { } values)
		{
			foreach (var error in ValidateConditionValues($"{path}.values", values, properties, isTemplate))
				yield return error;
		}

		if (condition.NotValues is { } notValues)
		{
			foreach (var error in ValidateConditionValues($"{path}.notValues", notValues, properties, isTemplate))
				yield return error;
		}

		if (condition.Hidden is { } hidden)
		{
			var listName = $"{path}.hidden";
			if (!isTemplate)
			{
				yield return OurResources.ConditionHiddenNotAllowed.FormatCurrentCulture(path);
			}
			else
			{
				foreach (var error in ValidateIds(listName, hidden, properties.Keys.ToHashSet(StringComparer.Ordinal), x => OurResources.TemplateHiddenPropertyUnknown.FormatCurrentCulture(listName, x)))
					yield return error;
			}
		}
	}

	/// <summary>
	/// Checks that a map of property ids to value ids uses value list properties and their values. Conditions other
	/// than template conditions can also use integer properties with groups, and their group ids.
	/// </summary>
	private static IEnumerable<string> ValidateConditionValues(string path, IReadOnlyDictionary<string, IReadOnlyList<string>> values, IReadOnlyDictionary<string, CharacterProperty> properties, bool isTemplate)
	{
		if (values.Count == 0)
			yield return OurResources.DataListEmpty.FormatCurrentCulture(path);

		foreach (var (propertyId, valueIds) in values)
		{
			var property = isTemplate ? GetValueListProperty(propertyId, properties) : GetConditionProperty(propertyId, properties);
			if (property is null)
				yield return (isTemplate ? OurResources.ConditionPropertyInvalid : OurResources.ConditionPropertyWithoutValues).FormatCurrentCulture(path, propertyId);

			var listName = $"{path}.{propertyId}";
			if (valueIds is null)
			{
				yield return OurResources.DataListEmpty.FormatCurrentCulture(listName);
			}
			else
			{
				foreach (var error in ValidateIds(listName, valueIds, property?.GetConditionValueIds().ToHashSet(StringComparer.Ordinal), x => OurResources.ConditionValueUnknown.FormatCurrentCulture(listName, x, propertyId)))
					yield return error;
			}
		}
	}

	internal static IEnumerable<string> ValidateRange(string path, IntegerRange range)
	{
		if (range.Maximum < range.Minimum)
			yield return OurResources.IntegerRangeInvalid.FormatCurrentCulture(path, range.Maximum, range.Minimum);
		foreach (var error in range.Shape.Validate($"{path}.shape"))
			yield return error;
	}

	/// <summary>
	/// Checks whether a property can have a value for the role in any combination of the values its conditions can
	/// see. A property that can't, such as one that needs a gender the role can't have, never applies.
	/// </summary>
	internal static bool CanHaveValue(IReadOnlyList<CharacterProperty> properties, CharacterProperty property, CharacterRole role)
	{
		// If there are too many combinations to check, assume the property can have a value.
		var combinations = GetConditionCombinations(GetConditionClosure(properties, property.GetConditionPropertyIds()), role);
		return combinations is null || combinations.Any(x => property.HasValue(role, id => x.GetValueOrDefault(id)));
	}

	/// <summary>
	/// Gets the properties with the given ids, plus the properties their conditions use, and so on. They're in
	/// dependency order, so each property comes after the properties its conditions use.
	/// </summary>
	internal static IReadOnlyList<CharacterProperty> GetConditionClosure(IReadOnlyList<CharacterProperty> properties, IEnumerable<string> ids)
	{
		var propertiesById = properties.ToDictionary(x => x.Id, StringComparer.Ordinal);
		var conditionIds = new HashSet<string>(StringComparer.Ordinal);
		var pending = new Stack<string>(ids);
		while (pending.TryPop(out var id))
		{
			if (conditionIds.Add(id) && propertiesById.TryGetValue(id, out var property))
			{
				foreach (var conditionId in property.GetConditionPropertyIds())
					pending.Push(conditionId);
			}
		}

		return CharacterDependencies.GetOrder(properties.Where(x => conditionIds.Contains(x.Id)).ToList());
	}

	/// <summary>
	/// Describes the values of the properties in a combination of condition values, for messages.
	/// </summary>
	internal static string DescribeCombination(IReadOnlyList<CharacterProperty> properties, IReadOnlyDictionary<string, string> combination) =>
		DescribeCombination(properties, combination, []);

	private static IEnumerable<string> ValidateRoleSettings(CharacterProperty property, CharacterRole role, CharacterRoleSettings settings, IReadOnlyDictionary<string, CharacterProperty> allProperties)
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
				// An empty list means characters of this role never have the property.
				var listName = $"{path}.values";
				foreach (var error in settings.Values.Count == 0 ? Enumerable.Empty<string>() : ValidateIds(listName, settings.Values, GetValueIds(property), x => OurResources.RoleValueUnknown.FormatCurrentCulture(listName, x)))
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
				foreach (var error in ValidateDefaults($"{path}.defaults", settings.Defaults, allProperties))
					yield return error;
			}
		}
	}

	/// <summary>
	/// Checks that a list of ids isn't empty, and that its ids are filled in and unique. If the known ids are given,
	/// also checks that each id is one of them.
	/// </summary>
	private static IEnumerable<string> ValidateIds(string listName, IReadOnlyList<string?> ids, IReadOnlySet<string>? knownIds, Func<string, string> formatUnknown)
	{
		if (ids.Count == 0)
		{
			yield return OurResources.DataListEmpty.FormatCurrentCulture(listName);
		}
		else
		{
			var seenIds = new HashSet<string>(StringComparer.Ordinal);
			foreach (var id in ids)
			{
				if (string.IsNullOrWhiteSpace(id))
					yield return OurResources.DataListItemIdMissing.FormatCurrentCulture(listName);
				else if (!seenIds.Add(id))
					yield return OurResources.DataListItemIdDuplicate.FormatCurrentCulture(id, listName);
				else if (knownIds is not null && !knownIds.Contains(id))
					yield return formatUnknown(id);
			}
		}
	}

	private static HashSet<string> GetValueIds(CharacterProperty property) =>
		(property.Values ?? []).Where(x => x is not null).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);

	private static IEnumerable<string> ValidateDefaults(string path, StringDefaults defaults, IReadOnlyDictionary<string, CharacterProperty> allProperties)
	{
		var isKeyed = defaults.KeyedBy is not null;
		if (isKeyed ? defaults.ValuesByKey is null || defaults.Values is not null : defaults.Values is null || defaults.ValuesByKey is not null || defaults.OtherValues is not null)
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
			var keyProperty = GetValueListProperty(keyedBy, allProperties);
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

			if (defaults.OtherValues is not null)
			{
				foreach (var error in DataValidation.ValidateWeightedStrings($"{path}.otherValues", defaults.OtherValues))
					yield return error;
			}
		}
	}

	/// <summary>
	/// Checks that keyed defaults have values for every value the key property can have, for each role, unless they have
	/// other values to fall back on.
	/// </summary>
	private static IEnumerable<string> ValidateDefaultsCoverKeys(CharacterProperty property, IReadOnlyDictionary<string, CharacterProperty> allProperties)
	{
		foreach (var role in s_roles)
		{
			var defaults = property.GetDefaults(role);
			if (defaults?.KeyedBy is { } keyedBy && defaults.ValuesByKey is { } valuesByKey && defaults.OtherValues is null && GetValueListProperty(keyedBy, allProperties) is { } keyProperty)
			{
				var path = property.GetRoleSettings(role)?.Defaults is not null ? $"{property.Id}.{GetRoleName(role)}.defaults" : $"{property.Id}.defaults";
				foreach (var option in keyProperty.GetOptions(role).Where(x => x is not null && !valuesByKey.ContainsKey(x.Id)))
					yield return OurResources.StringDefaultsKeyMissing.FormatCurrentCulture(path, option.Id, keyedBy);
			}
		}
	}

	private static CharacterProperty? GetValueListProperty(string id, IReadOnlyDictionary<string, CharacterProperty> properties) =>
		properties.GetValueOrDefault(id) is { Type: CharacterPropertyType.ValueList } property ? property : null;

	/// <summary>
	/// Gets a property that conditions on properties and values can use: a value list property, or an integer property with groups.
	/// </summary>
	private static CharacterProperty? GetConditionProperty(string id, IReadOnlyDictionary<string, CharacterProperty> properties) =>
		properties.GetValueOrDefault(id) is { } property && (property.Type == CharacterPropertyType.ValueList || (property.Type == CharacterPropertyType.Integer && property.Groups is not null)) ? property : null;

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

		// The ids each template uses in its placeholders, across all of its variants.
		var references = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
		foreach (var template in Templates.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)))
		{
			var templateReferences = new HashSet<string>(StringComparer.Ordinal);
			foreach (var error in ValidateTemplate(template, properties, templateIds, templateReferences))
				yield return error;
			references.TryAdd(template.Id, templateReferences);
		}

		foreach (var id in references.Keys.Where(x => RefersToItself(x, references)))
			yield return OurResources.TemplateCycle.FormatCurrentCulture(id);
	}

	/// <summary>
	/// Checks a template's form and each of its variants, adding the ids its placeholders use to the given set.
	/// </summary>
	private static IEnumerable<string> ValidateTemplate(CharacterTemplate template, IReadOnlyDictionary<string, CharacterProperty> properties, IReadOnlySet<string> templateIds, ISet<string> references)
	{
		var id = template.Id;
		if ((template.Template is null) == (template.Variants is null))
		{
			yield return OurResources.TemplateFormInvalid.FormatCurrentCulture(id);
		}
		else if (template.Variants is { Count: 0 })
		{
			yield return OurResources.DataListEmpty.FormatCurrentCulture($"{id}.variants");
		}
		else
		{
			var variants = template.GetVariants();
			for (var index = 0; index < variants.Count; index++)
			{
				var variant = variants[index];
				var name = GetVariantName(template, index);

				// JSON deserialization doesn't stop null list items, even when the type is non-nullable.
				if (variant is null)
				{
					yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{id}.variants");
				}
				else
				{
					if (variant.When is not null)
					{
						foreach (var error in ValidateCondition($"{name}.when", variant.When, properties, isTemplate: true))
							yield return error;
					}

					// Variants can be empty, so a template can leave out text.
					if (template.Variants is null ? string.IsNullOrWhiteSpace(variant.Template) : variant.Template is null)
					{
						yield return OurResources.TemplateBlank.FormatCurrentCulture(name);
					}
					else if (!TextTemplate.TryParse(variant.Template, out var parsed))
					{
						yield return OurResources.TemplateMalformed.FormatCurrentCulture(name);
					}
					else
					{
						references.UnionWith(parsed.Placeholders.Select(x => x.Id));
						foreach (var error in ValidatePlaceholders(name, parsed, variant.When, properties, templateIds).Distinct(StringComparer.Ordinal))
							yield return error;
					}
				}
			}
		}
	}

	/// <summary>
	/// Gets the name of a template variant for error messages: the template's id, plus the variant's position if it has variants.
	/// </summary>
	private static string GetVariantName(CharacterTemplate template, int index) =>
		template.Variants is null ? template.Id : $"{template.Id}.variants[{index}]";

	/// <summary>
	/// Checks whether a template uses itself, directly or through other templates.
	/// </summary>
	private static bool RefersToItself(string id, IReadOnlyDictionary<string, HashSet<string>> references)
	{
		IEnumerable<string> GetReferences(string templateId) => references[templateId].Where(references.ContainsKey);

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

	/// <summary>
	/// Checks the placeholders of a template. A placeholder with a field needs that text on every value the template's
	/// condition allows for its property.
	/// </summary>
	internal static IEnumerable<string> ValidatePlaceholders(string templateName, TextTemplate template, CharacterCondition? when, IReadOnlyDictionary<string, CharacterProperty> properties, IReadOnlySet<string> templateIds)
	{
		foreach (var placeholder in template.Placeholders)
		{
			if (placeholder.Format is { } format && !TextTemplate.IsKnownFormat(format))
				yield return OurResources.TemplateFormatUnknown.FormatCurrentCulture(templateName, format);

			var property = properties.GetValueOrDefault(placeholder.Id);
			if (property is null)
			{
				if (!templateIds.Contains(placeholder.Id))
					yield return OurResources.TemplatePropertyUnknown.FormatCurrentCulture(templateName, placeholder.Id);
				else if (placeholder.Field is { } templateField)
					yield return OurResources.TemplateFieldNotAllowed.FormatCurrentCulture(templateName, placeholder.Id, templateField);
			}
			else if (placeholder.Field is { } field)
			{
				if (property.Type != CharacterPropertyType.ValueList)
				{
					yield return OurResources.TemplateFieldNotAllowed.FormatCurrentCulture(templateName, placeholder.Id, field);
				}
				else
				{
					var allowedIds = when?.Values?.GetValueOrDefault(property.Id);
					var notAllowedIds = when?.NotValues?.GetValueOrDefault(property.Id) ?? [];
					foreach (var option in (property.Values ?? []).Where(x => x is not null && (allowedIds?.Contains(x.Id) ?? true) && !notAllowedIds.Contains(x.Id) && !x.Text.ContainsKey(field)))
						yield return OurResources.TemplateFieldMissing.FormatCurrentCulture(templateName, placeholder.Id, field, option.Id);
				}
			}
		}
	}

	/// <summary>
	/// Checks every combination of values that conditions can see, for each role. Each check only combines the
	/// values of the properties it depends on, so properties that don't affect each other aren't multiplied together.
	/// Every value list property that applies must have a value whose condition is met. Every template used must
	/// have a variant whose conditions are met, and every property a used variant shows must have a visible value.
	/// Each property a template uses that the obscurers can hide is checked both visible and hidden.
	/// </summary>
	private IEnumerable<string> ValidateConditionCombinations(IReadOnlyList<CharacterProperty> properties)
	{
		var errors = new List<string>();
		var propertiesById = properties.ToDictionary(x => x.Id, StringComparer.Ordinal);
		var templates = Templates.ToDictionary(x => x.Id, x => (Template: x, Variants: x.ParseVariants().ToList()), StringComparer.Ordinal);
		var obscuring = new CharacterObscuring(Obscurers);

		// The properties each template's variants use, in their conditions or placeholders.
		var templatePropertyIds = templates.ToDictionary(
			x => x.Key,
			x => x.Value.Variants
				.SelectMany(v => (v.When?.ValuePropertyIds ?? []).Concat(v.When?.Hidden ?? []).Concat(v.Template.Placeholders.Select(p => p.Id)))
				.Where(propertiesById.ContainsKey)
				.Distinct(StringComparer.Ordinal)
				.ToList(),
			StringComparer.Ordinal);

		// The properties whose values decide which variant of a template is used, and whether the properties it uses
		// have values.
		var templateConditionProperties = templates.ToDictionary(
			x => x.Key,
			x => GetConditionClosure(properties, x.Value.Variants.SelectMany(v => v.When?.ValuePropertyIds ?? []).Concat(templatePropertyIds[x.Key].SelectMany(id => propertiesById[id].GetConditionPropertyIds()))),
			StringComparer.Ordinal);

		// Templates that aren't used by other templates are used directly, as are the required templates.
		var usedIds = templates.Values.SelectMany(x => x.Variants).SelectMany(x => x.Template.Placeholders).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		var rootTemplateIds = templates.Keys.Where(x => s_requiredTemplateIds.Contains(x) || !usedIds.Contains(x)).ToList();

		foreach (var role in s_roles)
		{
			var roleName = GetRoleName(role);

			foreach (var property in properties.Where(x => x.Type == CharacterPropertyType.ValueList))
			{
				var conditionProperties = GetConditionClosure(properties, property.GetConditionPropertyIds());
				var combinations = GetConditionCombinations(conditionProperties, role);
				if (combinations is null)
				{
					errors.Add(OurResources.ConditionCombinationsTooMany.FormatCurrentCulture(roleName, MaxConditionCombinations));
				}
				else
				{
					foreach (var combination in combinations)
					{
						string? GetValueId(string id) => combination.GetValueOrDefault(id);
						if (CharacterCondition.IsMet(property.When, GetValueId) && property.GetOptions(role, GetValueId).Count == 0 && property.GetRoleSettings(role)?.Values is not { Count: 0 })
							errors.Add(OurResources.ConditionPropertyNoValues.FormatCurrentCulture(property.Id, roleName, DescribeCombination(conditionProperties, combination, [])));
					}
				}
			}

			// Checks a template in each combination of the values it depends on that's consistent with the template
			// using it, then checks the templates its chosen variant uses.
			void CheckTemplate(string templateId, TemplateScenario scenario)
			{
				var (template, variants) = templates[templateId];
				var newProperties = templateConditionProperties[templateId].Where(x => !scenario.AssignedIds.Contains(x.Id)).ToList();
				var combinations = GetConditionCombinations(newProperties, role, scenario.Values);
				if (combinations is null)
				{
					errors.Add(OurResources.ConditionCombinationsTooMany.FormatCurrentCulture(roleName, MaxConditionCombinations));
				}
				else
				{
					var assignedIds = scenario.AssignedIds.Concat(newProperties.Select(x => x.Id)).ToHashSet(StringComparer.Ordinal);
					var assignedProperties = properties.Where(x => assignedIds.Contains(x.Id)).ToList();
					foreach (var combination in combinations)
					{
						string? GetValueId(string id) => combination.GetValueOrDefault(id);
						var hideableIds = templatePropertyIds[templateId].Where(x => !scenario.DecidedIds.Contains(x) && CanBeHidden(propertiesById[x], role, GetValueId, obscuring)).ToList();
						if (hideableIds.Count > MaxHideablePropertiesPerTemplate)
						{
							errors.Add(OurResources.TemplateHideableTooMany.FormatCurrentCulture(templateId, MaxHideablePropertiesPerTemplate));
						}
						else
						{
							var decidedIds = scenario.DecidedIds.Concat(hideableIds).ToHashSet(StringComparer.Ordinal);

							// Each bit of the mask says whether one of the hideable properties is hidden.
							for (var mask = 0; mask < 1 << hideableIds.Count; mask++)
							{
								var hiddenIds = scenario.HiddenIds.Concat(hideableIds.Where((_, index) => (mask & (1 << index)) != 0)).ToHashSet(StringComparer.Ordinal);
								bool HasVisibleValue(string id) => !hiddenIds.Contains(id) && propertiesById[id].HasValue(role, GetValueId);
								string? GetVisibleValueId(string id) => hiddenIds.Contains(id) ? null : GetValueId(id);

								var index = variants.FindIndex(x => CharacterCondition.IsMet(x.When, GetVisibleValueId, HasVisibleValue));
								if (index < 0)
								{
									errors.Add(OurResources.TemplateVariantMissing.FormatCurrentCulture(templateId, roleName, DescribeCombination(assignedProperties, combination, hiddenIds)));
								}
								else
								{
									foreach (var placeholder in variants[index].Template.Placeholders)
									{
										if (templates.ContainsKey(placeholder.Id))
											CheckTemplate(placeholder.Id, new TemplateScenario(combination, assignedIds, decidedIds, hiddenIds));
										else if (!HasVisibleValue(placeholder.Id))
											errors.Add(OurResources.TemplatePropertyMissing.FormatCurrentCulture(GetVariantName(template, index), placeholder.Id, roleName, DescribeCombination(assignedProperties, combination, hiddenIds)));
									}
								}
							}
						}
					}
				}
			}

			var empty = new HashSet<string>(StringComparer.Ordinal);
			foreach (var rootTemplateId in rootTemplateIds)
				CheckTemplate(rootTemplateId, new TemplateScenario(new Dictionary<string, string>(StringComparer.Ordinal), empty, empty, empty));
		}

		return errors;
	}

	/// <summary>
	/// The values assumed while checking a template, carried down to the templates it uses: the values of the
	/// properties assigned so far (a property with no value is assigned but left out of the values), the hideable
	/// properties whose visibility has been decided, and which of them are hidden.
	/// </summary>
	private sealed record TemplateScenario(IReadOnlyDictionary<string, string> Values, IReadOnlySet<string> AssignedIds, IReadOnlySet<string> DecidedIds, IReadOnlySet<string> HiddenIds);

	/// <summary>
	/// Gets every combination of values the given value list properties can have for the role, as maps from property
	/// id to value id, added to the given starting values. A property with no value is left out of the map. The
	/// properties must be in dependency order, and the properties they depend on must be among them or in the starting
	/// values. Returns null if there are too many combinations.
	/// </summary>
	internal static IReadOnlyList<IReadOnlyDictionary<string, string>>? GetConditionCombinations(IReadOnlyList<CharacterProperty> properties, CharacterRole role, IReadOnlyDictionary<string, string>? start = null)
	{
		var combinations = new List<Dictionary<string, string>> { start is null ? new(StringComparer.Ordinal) : new(start, StringComparer.Ordinal) };
		for (var index = 0; index < properties.Count && combinations.Count <= MaxConditionCombinations; index++)
		{
			var property = properties[index];
			var next = new List<Dictionary<string, string>>();
			foreach (var combination in combinations)
			{
				string? GetValueId(string id) => combination.GetValueOrDefault(id);
				var valueIds = property.HasValue(role, GetValueId) ? GetPossibleConditionValueIds(property, role, GetValueId) : [null];
				next.AddRange(valueIds.Select(x => x is null ? combination : new Dictionary<string, string>(combination, StringComparer.Ordinal) { [property.Id] = x }));
			}

			combinations = next;
		}

		return combinations.Count <= MaxConditionCombinations ? combinations : null;
	}

	/// <summary>
	/// Gets the ids conditions can see for a property that has a value: the ids of the values whose conditions are met
	/// for a value list property, or the ids of the groups an integer property's range overlaps for the role. A null id
	/// stands for numbers in no group.
	/// </summary>
	private static IReadOnlyList<string?> GetPossibleConditionValueIds(CharacterProperty property, CharacterRole role, Func<string, string?> getValueId)
	{
		IReadOnlyList<string?> valueIds = [null];
		if (property.Type == CharacterPropertyType.ValueList)
		{
			valueIds = property.GetOptions(role, getValueId).Select(x => (string?) x.Id).ToList();
		}
		else if (property.Type == CharacterPropertyType.Integer && property.Groups is { } groups)
		{
			var range = property.GetRange(role);
			var groupIds = groups.Where(x => x.Overlaps(range.Minimum, range.Maximum)).Select(x => (string?) x.Id);
			var isCovered = Enumerable.Range(range.Minimum, range.Maximum - range.Minimum + 1).All(x => groups.Any(g => g.Contains(x)));
			valueIds = (isCovered ? groupIds : groupIds.Append(null)).ToList();
		}

		return valueIds;
	}

	/// <summary>
	/// Whether the obscurers can hide a property: it has a value, and the strongest obscurer of it is at least as
	/// strong as the least prominent value it can have.
	/// </summary>
	private static bool CanBeHidden(CharacterProperty property, CharacterRole role, Func<string, string?> getValueId, CharacterObscuring obscuring)
	{
		var canBeHidden = false;
		if (property.HasValue(role, getValueId))
		{
			var minProminence = property.Type == CharacterPropertyType.ValueList ? property.GetOptions(role, getValueId).Min(x => x.GetProminence()) : CharacterObscurer.MaxStrength;
			canBeHidden = obscuring.GetStrength(property.Id) >= minProminence;
		}

		return canBeHidden;
	}

	private static string DescribeCombination(IReadOnlyList<CharacterProperty> properties, IReadOnlyDictionary<string, string> combination, IEnumerable<string> hiddenIds) =>
		string.Join(", ", properties
			.Select(x => combination.TryGetValue(x.Id, out var valueId)
				? OurResources.ConditionCombinationValue.FormatCurrentCulture(x.Id, valueId)
				: OurResources.ConditionCombinationNoValue.FormatCurrentCulture(x.Id))
			.Concat(hiddenIds.Order(StringComparer.Ordinal).Select(x => OurResources.ConditionCombinationHidden.FormatCurrentCulture(x))));

	internal static string GetRoleName(CharacterRole role) => role switch
	{
		CharacterRole.Player => "player",
		CharacterRole.Npc => "npc",
		_ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
	};

	internal static IReadOnlyList<CharacterRole> Roles => s_roles;

	private static readonly CharacterRole[] s_roles = [CharacterRole.Player, CharacterRole.Npc];
	private static readonly string[] s_requiredTemplateIds = [FullNameTemplateId, DescriptionTemplateId];
}
