using System.IO;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// Checks that the data files agree with each other, once each file has been loaded and checked on its own.
/// </summary>
internal static class GameDataValidation
{
	public static IReadOnlyList<DataLoadError> Validate(string dataDirectory, CharacterData characters, LocationList locations, ProfessionList professions, FamilySettings families, PortraitData? portraits = null)
	{
		var properties = new Dictionary<string, CharacterProperty>(StringComparer.Ordinal);
		foreach (var property in characters.Properties)
			properties.TryAdd(property.Id, property);

		var errors = new List<DataLoadError>();
		var familyErrors = ValidateFamilies(families, properties).ToList();
		errors.AddRange(familyErrors.Select(x => new DataLoadError(Path.Combine(dataDirectory, FamilySettings.FileName), x)));

		// Professions can only be matched to the profession property once Families.json says which property it is.
		var professionProperty = familyErrors.Count == 0 ? properties[families.Properties.Profession] : null;
		errors.AddRange(ValidateProfessions(professions, professionProperty, families, properties, locations)
			.Select(x => new DataLoadError(Path.Combine(dataDirectory, ProfessionList.FileName), x)));
		errors.AddRange(ValidateBusinesses(locations, professions, families)
			.Select(x => new DataLoadError(Path.Combine(dataDirectory, LocationList.FileName), x)));

		// House titles are filled in from the head's character properties.
		if (TextTemplate.TryParse(locations.HouseTitle ?? "", out var houseTitle))
		{
			errors.AddRange(CharacterData.ValidatePlaceholders("houseTitle", houseTitle, null, properties, new HashSet<string>(StringComparer.Ordinal))
				.Select(x => new DataLoadError(Path.Combine(dataDirectory, LocationList.FileName), x)));
		}

		// Portrait conditions use the character properties, like other conditions outside of templates.
		if (portraits is not null)
		{
			errors.AddRange(portraits.GetConditions()
				.SelectMany(x => CharacterData.ValidateCondition(x.Path, x.Condition, properties, isTemplate: false))
				.Select(x => new DataLoadError(Path.Combine(dataDirectory, PortraitData.FileName), x)));

			// Layers are drawn in kinds of quarter, and in the workplaces of professions.
			var quarterKinds = locations.QuarterKinds.Where(x => x?.Id is not null).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
			errors.AddRange(portraits.GetQuarterKinds()
				.Where(x => !quarterKinds.Contains(x.QuarterKind))
				.Select(x => new DataLoadError(Path.Combine(dataDirectory, PortraitData.FileName), OurResources.PortraitQuarterKindUnknown.FormatCurrentCulture(x.Path, x.QuarterKind))));
			if (professionProperty is not null)
			{
				var professionIds = professionProperty.GetConditionValueIds().ToHashSet(StringComparer.Ordinal);
				errors.AddRange(portraits.GetWorkplaces()
					.Where(x => !professionIds.Contains(x.Profession))
					.Select(x => new DataLoadError(Path.Combine(dataDirectory, PortraitData.FileName), OurResources.PortraitWorkplaceUnknown.FormatCurrentCulture(x.Path, x.Profession, professionProperty.Id))));
			}
		}

		return errors;
	}

	private static IEnumerable<string> ValidateFamilies(FamilySettings families, IReadOnlyDictionary<string, CharacterProperty> properties)
	{
		var errors = new List<string>();
		var gender = ValidateProperty("properties.gender", families.Properties.Gender, CharacterPropertyType.ValueList, "valueList", properties, errors);
		var age = ValidateProperty("properties.age", families.Properties.Age, CharacterPropertyType.Integer, "integer", properties, errors);
		var profession = ValidateProperty("properties.profession", families.Properties.Profession, CharacterPropertyType.ValueList, "valueList", properties, errors);
		var lastName = ValidateProperty("properties.lastName", families.Properties.LastName, CharacterPropertyType.String, "string", properties, errors);
		var workRole = ValidateProperty("properties.workRole", families.Properties.WorkRole, CharacterPropertyType.ValueList, "valueList", properties, errors);
		var householdRole = ValidateProperty("properties.householdRole", families.Properties.HouseholdRole, CharacterPropertyType.ValueList, "valueList", properties, errors);
		var headRelation = ValidateProperty("properties.headRelation", families.Properties.HeadRelation, CharacterPropertyType.ValueList, "valueList", properties, errors);
		var headName = ValidateProperty("properties.headName", families.Properties.HeadName, CharacterPropertyType.String, "string", properties, errors);
		var atWork = ValidateProperty("properties.atWork", families.Properties.AtWork, CharacterPropertyType.ValueList, "valueList", properties, errors);
		var married = ValidateProperty("properties.married", families.Properties.Married, CharacterPropertyType.ValueList, "valueList", properties, errors);

		// Households whose usual last name is taken, and in-laws, pick from the other last names.
		if (lastName is not null && lastName.GetDefaults(CharacterRole.Npc)?.OtherValues is not { Count: > 0 })
			errors.Add(OurResources.FamilyLastNameOtherValuesMissing.FormatCurrentCulture(lastName.Id));

		if (workRole is not null)
			ValidateGameSetProperty(workRole, [("independent", families.Independent), ("helper", families.Helper)], errors);
		if (householdRole is not null)
			ValidateGameSetProperty(householdRole, [("head", families.Head), ("member", families.Member)], errors);
		if (headRelation is not null)
			ValidateGameSetProperty(headRelation, families.HeadRelations.GetAll(), errors);
		if (headName is not null)
			ValidateGameSetProperty(headName, [], errors);
		if (atWork is not null)
			ValidateGameSetProperty(atWork, [("atWork", families.AtWork), ("notAtWork", families.NotAtWork)], errors);
		if (married is not null)
			ValidateGameSetProperty(married, [("married", families.Married), ("unmarried", families.Unmarried)], errors);

		if (gender is not null)
		{
			var genderIds = gender.GetConditionValueIds();
			if (!genderIds.Contains(families.Male, StringComparer.Ordinal))
				errors.Add(OurResources.FamilyValueUnknown.FormatCurrentCulture("male", families.Male, gender.Id));
			if (!genderIds.Contains(families.Female, StringComparer.Ordinal))
				errors.Add(OurResources.FamilyValueUnknown.FormatCurrentCulture("female", families.Female, gender.Id));
		}

		if (profession is not null)
		{
			var professionIds = profession.GetConditionValueIds();
			if (!professionIds.Contains(families.Homemaker, StringComparer.Ordinal))
				errors.Add(OurResources.FamilyValueUnknown.FormatCurrentCulture("homemaker", families.Homemaker, profession.Id));
			if (!professionIds.Contains(families.Infirm, StringComparer.Ordinal))
				errors.Add(OurResources.FamilyValueUnknown.FormatCurrentCulture("infirm", families.Infirm, profession.Id));
		}

		if (age is not null)
		{
			var groupIds = age.GetConditionValueIds();
			foreach (var group in families.InfirmChance.Keys.Where(x => !groupIds.Contains(x, StringComparer.Ordinal)))
				errors.Add(OurResources.FamilyAgeGroupUnknown.FormatCurrentCulture("infirmChance", group, age.Id));
			foreach (var group in families.HeadAgeWealth.Keys.Where(x => !groupIds.Contains(x, StringComparer.Ordinal)))
				errors.Add(OurResources.FamilyAgeGroupUnknown.FormatCurrentCulture("headAgeWealth", group, age.Id));

			var range = age.GetRange(CharacterRole.Npc);
			if (families.HeadAge.Minimum < range.Minimum || families.HeadAge.Maximum > range.Maximum)
				errors.Add(OurResources.FamilyHeadAgeOutOfRange.FormatCurrentCulture(families.HeadAge.Minimum, families.HeadAge.Maximum, age.Id, range.Minimum, range.Maximum));

			// The player's age comes from the head ages, from the youngest age at which they can have an adult child.
			if (age.Player?.Range is not null)
				errors.Add(OurResources.FamilyAgePlayerRangeNotAllowed.FormatCurrentCulture(age.Id));
			var minPlayerAge = GameData.GetMinPlayerAge(age, families);
			if (families.HeadAge.Maximum < minPlayerAge)
				errors.Add(OurResources.FamilyHeadAgeTooYoung.FormatCurrentCulture(minPlayerAge));
		}

		// The properties the generator sets for itself can't also be inherited.
		var familyPropertyIds = new[] { families.Properties.Gender, families.Properties.Age, families.Properties.Profession, families.Properties.LastName, families.Properties.WorkRole, families.Properties.HouseholdRole, families.Properties.HeadRelation, families.Properties.HeadName, families.Properties.AtWork, families.Properties.Married };
		for (var index = 0; index < families.InheritedProperties.Count; index++)
		{
			var inherited = families.InheritedProperties[index];
			var path = $"inheritedProperties[{index}]";
			if (familyPropertyIds.Contains(inherited.Property, StringComparer.Ordinal))
			{
				errors.Add(OurResources.FamilyInheritedPropertyNotAllowed.FormatCurrentCulture($"{path}.property", inherited.Property));
			}
			else if (ValidateProperty($"{path}.property", inherited.Property, CharacterPropertyType.ValueList, "valueList", properties, errors) is { } property)
			{
				var valueIds = property.Values?.Select(x => x.Id).ToList() ?? [];
				foreach (var valueId in inherited.Unblended.Where(x => !valueIds.Contains(x, StringComparer.Ordinal)))
					errors.Add(OurResources.FamilyValueUnknown.FormatCurrentCulture($"{path}.unblended", valueId, property.Id));
			}
		}

		return errors;
	}

	/// <summary>
	/// Checks a property the family generator sets: the game must set it, and a value list property must have each of
	/// the named values.
	/// </summary>
	private static void ValidateGameSetProperty(CharacterProperty property, IReadOnlyList<(string Name, string ValueId)> values, ICollection<string> errors)
	{
		if (!property.SetByGame)
			errors.Add(OurResources.FamilyPropertyNotSetByGame.FormatCurrentCulture(property.Id));

		var valueIds = property.GetConditionValueIds();
		foreach (var (name, valueId) in values.Where(x => !valueIds.Contains(x.ValueId, StringComparer.Ordinal)))
			errors.Add(OurResources.FamilyValueUnknown.FormatCurrentCulture(name, valueId, property.Id));
	}

	/// <summary>
	/// Gets the property with the given id, or adds an error and returns null if there's no property of the given type with that id.
	/// </summary>
	private static CharacterProperty? ValidateProperty(string path, string id, CharacterPropertyType type, string typeName, IReadOnlyDictionary<string, CharacterProperty> properties, ICollection<string> errors)
	{
		var property = properties.GetValueOrDefault(id);
		if (property is null || property.Type != type)
		{
			errors.Add(OurResources.FamilyPropertyInvalid.FormatCurrentCulture(path, id, typeName));
			property = null;
		}

		return property;
	}

	private static IEnumerable<string> ValidateProfessions(ProfessionList professions, CharacterProperty? professionProperty, FamilySettings families, IReadOnlyDictionary<string, CharacterProperty> properties, LocationList locations)
	{
		var items = professions.Professions.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();

		// The profession property's values are the professions.
		if (professionProperty is not null && professionProperty.ValuesFrom != CharacterProperty.ProfessionsSource)
			yield return OurResources.FamilyProfessionValuesNotFromProfessions.FormatCurrentCulture(professionProperty.Id, CharacterProperty.ProfessionsSource);

		// The homemaker and infirm professions aren't businesses, and every other profession is.
		var notBusinesses = new Dictionary<string, string>(StringComparer.Ordinal) { [families.Homemaker] = "homemaker", [families.Infirm] = "infirm" };
		var professionsWithBusinesses = locations.Businesses.Select(x => x.Profession).ToHashSet(StringComparer.Ordinal);
		foreach (var profession in items)
		{
			var isBusiness = !notBusinesses.TryGetValue(profession.Id, out var setting);
			if (!isBusiness && profession.HasBusinessSettings)
				yield return OurResources.ProfessionNotBusiness.FormatCurrentCulture(profession.Id, setting);
			else if (isBusiness && profession.Wealth is null)
				yield return OurResources.ProfessionWealthMissing.FormatCurrentCulture(profession.Id);

			// Profession defaults are only used by string properties keyed by the profession property, which don't list
			// their own values for each key.
			if (professionProperty is not null && profession.Defaults is not null)
			{
				foreach (var propertyId in profession.Defaults.Keys)
				{
					var property = properties.GetValueOrDefault(propertyId);
					var isUsed = property is { Type: CharacterPropertyType.String }
						&& new[] { property.Defaults, property.Player?.Defaults, property.Npc?.Defaults }.Any(x => x?.KeyedBy == professionProperty.Id && x.HasProfessionValuesByKey);
					if (!isUsed)
						yield return OurResources.ProfessionDefaultsPropertyInvalid.FormatCurrentCulture(profession.Id, propertyId, professionProperty.Id);
				}
			}

			if (profession.HelperWhen is { } helperWhen)
			{
				foreach (var error in CharacterData.ValidateCondition($"professions[{profession.Id}].helperWhen", helperWhen, properties, isTemplate: false))
					yield return error;
			}

			if (isBusiness && !profession.WorksFromHome && !professionsWithBusinesses.Contains(profession.Id))
				yield return OurResources.ProfessionLocationsMissing.FormatCurrentCulture(profession.Id);
		}
	}

	private static IEnumerable<string> ValidateBusinesses(LocationList locations, ProfessionList professions, FamilySettings families)
	{
		// Homemaker and infirm aren't businesses, so they can't have places.
		var professionsById = new Dictionary<string, Profession>(StringComparer.Ordinal);
		foreach (var profession in professions.Professions.Where(x => x is not null && x.Id != families.Homemaker && x.Id != families.Infirm))
			professionsById.TryAdd(profession.Id, profession);

		foreach (var business in locations.Businesses)
		{
			if (professionsById.GetValueOrDefault(business.Profession) is not { } profession)
				yield return OurResources.BusinessProfessionUnknown.FormatCurrentCulture(business.Id, business.Profession);
			else if (profession.WorksFromHome)
				yield return OurResources.BusinessProfessionWorksFromHome.FormatCurrentCulture(business.Id, business.Profession);
		}
	}
}
