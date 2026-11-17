using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// Checks that the parts of the data agree with each other, once each part has been checked on its own.
/// </summary>
internal static class GameDataValidation
{
	public static IReadOnlyList<string> Validate(CharacterData characters, LocationList locations, ProfessionList professions, FamilySettings families, RelationshipSettings relationships, CalendarSettings calendar, ScheduleSettings schedules, PortraitData? portraits = null, ClothingData? clothing = null)
	{
		var properties = new Dictionary<string, CharacterProperty>(StringComparer.Ordinal);
		foreach (var property in characters.Properties.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)))
			properties.TryAdd(property.Id, property);

		var errors = new List<string>();
		errors.AddRange(ValidateFamilies(families, characters, properties));
		errors.AddRange(relationships.PlayerRelations.OfType<PlayerRelation>().SelectMany((x, index) => CharacterData.ValidateCondition($"{RelationshipSettings.SectionName}.playerRelations[{index}].when", x.When, properties, isTemplate: false)));

		var professionProperty = characters.FindRole(CharacterData.ProfessionRole);
		errors.AddRange(ValidateProfessions(professions, professionProperty, properties, locations));
		errors.AddRange(ValidateBusinesses(locations, professions));

		// House titles are filled in from the head's character properties.
		if (TextTemplate.TryParse(locations.Village.HouseTitle ?? "", out var houseTitle))
			errors.AddRange(CharacterData.ValidatePlaceholders("village.houseTitle", houseTitle, null, properties, new HashSet<string>(StringComparer.Ordinal), allowsClothing: false));

		// Schedules use the character properties, the kinds of quarter, and the days of the week.
		errors.AddRange(ValidateSchedules(schedules, properties, locations, calendar));

		// Portrait conditions use the character properties, like other conditions outside of templates.
		if (portraits is not null)
		{
			var quarterKinds = locations.QuarterKinds.Where(x => x?.Id is not null).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
			errors.AddRange(ValidatePortraits(portraits, properties, quarterKinds, professionProperty, clothing ?? ClothingData.None));
		}

		// Outfits and modesty rules are given to characters by their properties.
		if (clothing is not null)
			errors.AddRange(clothing.GetConditions().SelectMany(x => CharacterData.ValidateCondition(x.Path, x.Condition, properties, isTemplate: false)));

		// Properties show on regions of the body, and professions change how well covered people must be in them while
		// working. Without clothing, there are no regions, and clothing never hides anything.
		var slotIds = (clothing ?? ClothingData.None).Slots.Where(x => !string.IsNullOrWhiteSpace(x?.Id)).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		foreach (var property in properties.Values.Where(x => x.Regions is { Count: > 0 } && slotIds.Count != 0))
			errors.AddRange(ValidateRegions($"properties[{property.Id}].regions", property.Regions, slotIds));
		var ruleIds = (clothing ?? ClothingData.None).ModestyRules.Where(x => !string.IsNullOrWhiteSpace(x?.Id)).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		foreach (var profession in professions.Professions.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)))
		{
			var path = $"professions[{profession.Id}].modesty";
			foreach (var (ruleId, adjustments) in profession.Modesty ?? new Dictionary<string, IReadOnlyDictionary<string, int>>())
			{
				if (!ruleIds.Contains(ruleId))
					errors.Add(OurResources.DataIdUnknown.FormatCurrentCulture(path, ruleId, "modestyRules"));
				errors.AddRange(ClothingData.ValidateSlotValues($"{path}.{ruleId}", adjustments ?? new Dictionary<string, int>(), slotIds, -ClothingItem.MaxCoverage));
			}
		}

		return errors;
	}

	/// <summary>
	/// Checks a list of regions of the body: unique clothing slot ids.
	/// </summary>
	private static IEnumerable<string> ValidateRegions(string path, IReadOnlyList<string> regions, IReadOnlySet<string> slotIds) =>
		DataValidation.ValidateStrings(path, regions)
			.Concat(regions.Where(x => !string.IsNullOrWhiteSpace(x) && !slotIds.Contains(x)).Select(x => OurResources.DataIdUnknown.FormatCurrentCulture(path, x, "clothingSlots")));

	/// <summary>
	/// Checks the portrait data against the character properties, the kinds of quarter, and the profession property (if
	/// it's known), and against the clothing: the regions whose coverage hides layers, the clothing layers draw, and the
	/// colors of that clothing they use.
	/// </summary>
	public static IEnumerable<string> ValidatePortraits(PortraitData portraits, IReadOnlyDictionary<string, CharacterProperty> properties, IReadOnlySet<string> quarterKinds, CharacterProperty? professionProperty, ClothingData clothing)
	{
		var clothingById = new Dictionary<string, ClothingItem>(StringComparer.Ordinal);
		foreach (var item in clothing.Items.Where(x => !string.IsNullOrWhiteSpace(x?.Id)))
			clothingById.TryAdd(item.Id, item);

		// Layers are hidden by how much clothing covers regions of the body.
		var slotIds = clothing.Slots.Where(x => !string.IsNullOrWhiteSpace(x?.Id)).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		var clothingErrors = new List<string>();
		foreach (var layer in portraits.Layers.Where(x => x?.HiddenBy is not null && slotIds.Count != 0))
			clothingErrors.AddRange(ValidateRegions($"portraitLayers[{layer.Id}].hiddenBy.regions", layer.HiddenBy!.Regions ?? [], slotIds));

		foreach (var layer in portraits.Layers.Where(x => x?.Clothing is not null))
		{
			foreach (var id in layer.Clothing!.Where(x => !string.IsNullOrWhiteSpace(x) && !clothingById.ContainsKey(x)))
				clothingErrors.Add(OurResources.DataIdUnknown.FormatCurrentCulture($"portraitLayers[{layer.Id}].clothing", id, "clothing"));
		}

		// A clothing layer's shapes can use the colors of each article of clothing it draws.
		foreach (var (path, layerClothing, color) in portraits.GetClothingColors())
		{
			foreach (var item in layerClothing.Select(clothingById.GetValueOrDefault).OfType<ClothingItem>().Where(x => !(x.Colors ?? []).Contains(color, StringComparer.Ordinal)))
				clothingErrors.Add(OurResources.PortraitClothingColorUnknown.FormatCurrentCulture(path, color, item.Id));
		}

		var errors = portraits.GetConditions()
			.SelectMany(x => CharacterData.ValidateCondition(x.Path, x.Condition, properties, isTemplate: false))
			.ToList();

		// Layers are drawn in kinds of quarter, and in the workplaces of professions.
		errors.AddRange(portraits.GetQuarterKinds()
			.Where(x => !quarterKinds.Contains(x.QuarterKind))
			.Select(x => OurResources.PortraitQuarterKindUnknown.FormatCurrentCulture(x.Path, x.QuarterKind)));
		if (professionProperty is not null)
		{
			var professionIds = professionProperty.GetConditionValueIds().ToHashSet(StringComparer.Ordinal);
			errors.AddRange(portraits.GetWorkplaces()
				.Where(x => !professionIds.Contains(x.Profession))
				.Select(x => OurResources.PortraitWorkplaceUnknown.FormatCurrentCulture(x.Path, x.Profession, professionProperty.Id)));
		}

		errors.AddRange(clothingErrors);
		return errors;
	}

	private static IEnumerable<string> ValidateFamilies(FamilySettings families, CharacterData characters, IReadOnlyDictionary<string, CharacterProperty> properties)
	{
		const string path = FamilySettings.SectionName;
		var errors = new List<string>();

		// Households whose usual last name is taken, and in-laws, pick from the other last names.
		if (characters.FindRole("lastName") is { Type: CharacterPropertyType.String } lastName && lastName.GetDefaults(CharacterRole.Npc)?.OtherValues is not { Count: > 0 })
			errors.Add(OurResources.FamilyLastNameOtherValuesMissing.FormatCurrentCulture(lastName.Id));

		if (characters.FindRole("age") is { Type: CharacterPropertyType.Integer } age)
		{
			var groupIds = age.GetConditionValueIds();
			foreach (var group in families.Elder.InfirmChance.Keys.Where(x => !groupIds.Contains(x, StringComparer.Ordinal)))
				errors.Add(OurResources.FamilyAgeGroupUnknown.FormatCurrentCulture($"{path}.elder.infirmChance", group, age.Id));
			foreach (var group in families.Wealth.HeadAge.Keys.Where(x => !groupIds.Contains(x, StringComparer.Ordinal)))
				errors.Add(OurResources.FamilyAgeGroupUnknown.FormatCurrentCulture($"{path}.wealth.headAge", group, age.Id));

			if (age.Range is not null || age.Npc?.Range is not null)
			{
				var range = age.GetRange(CharacterRole.Npc);
				if (families.Head.Age.Minimum < range.Minimum || families.Head.Age.Maximum > range.Maximum)
					errors.Add(OurResources.FamilyHeadAgeOutOfRange.FormatCurrentCulture(families.Head.Age.Minimum, families.Head.Age.Maximum, age.Id, range.Minimum, range.Maximum));

				// The player's age comes from the head ages, from the youngest age at which they can have an adult child.
				var minPlayerAge = GameData.GetMinPlayerAge(age, families);
				if (families.Head.Age.Maximum < minPlayerAge)
					errors.Add(OurResources.FamilyHeadAgeTooYoung.FormatCurrentCulture(minPlayerAge));
			}

			if (age.Player?.Range is not null)
				errors.Add(OurResources.FamilyAgePlayerRangeNotAllowed.FormatCurrentCulture(age.Id));
		}

		// The properties the generator sets for itself can't also be inherited.
		foreach (var (inherited, index) in families.InheritedProperties.Select((x, i) => (x, i)).Where(x => x.x is not null))
		{
			var inheritedPath = $"{path}.inheritedProperties[{index}]";
			var property = properties.GetValueOrDefault(inherited.Property);
			if (property is { Role: not null } && CharacterData.PropertyRoles.Any(x => x.Role == property.Role))
			{
				errors.Add(OurResources.FamilyInheritedPropertyNotAllowed.FormatCurrentCulture($"{inheritedPath}.property", inherited.Property));
			}
			else if (property is not { Type: CharacterPropertyType.ValueList })
			{
				errors.Add(OurResources.FamilyPropertyInvalid.FormatCurrentCulture($"{inheritedPath}.property", inherited.Property, "valueList"));
			}
			else
			{
				var valueIds = property.Values?.Select(x => x.Id).ToList() ?? [];
				foreach (var valueId in inherited.Unblended.Where(x => !valueIds.Contains(x, StringComparer.Ordinal)))
					errors.Add(OurResources.FamilyValueUnknown.FormatCurrentCulture($"{inheritedPath}.unblended", valueId, property.Id));
			}
		}

		return errors;
	}

	private static IEnumerable<string> ValidateProfessions(ProfessionList professions, CharacterProperty? professionProperty, IReadOnlyDictionary<string, CharacterProperty> properties, LocationList locations)
	{
		var items = professions.Professions.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();
		var professionsWithBusinesses = locations.Businesses.Where(x => x is not null).Select(x => x.Profession).ToHashSet(StringComparer.Ordinal);
		foreach (var profession in items)
		{
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
				foreach (var error in CharacterData.ValidateCondition($"professions[{profession.Id}].business.helperWhen", helperWhen, properties, isTemplate: false))
					yield return error;
			}

			if (profession.IsBusiness && !profession.WorksFromHome && !professionsWithBusinesses.Contains(profession.Id))
				yield return OurResources.ProfessionLocationsMissing.FormatCurrentCulture(profession.Id);
		}
	}

	private static IEnumerable<string> ValidateSchedules(ScheduleSettings schedules, IReadOnlyDictionary<string, CharacterProperty> properties, LocationList locations, CalendarSettings calendar)
	{
		var quarterKinds = locations.QuarterKinds.Where(x => x?.Id is not null).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		var days = calendar.Days.Where(x => x?.Id is not null).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		return schedules.GetConditions().SelectMany(x => CharacterData.ValidateCondition(x.Path, x.Condition, properties, isTemplate: false))
			.Concat(schedules.GetQuarterKinds().Where(x => !quarterKinds.Contains(x.QuarterKind)).Select(x => OurResources.DataIdUnknown.FormatCurrentCulture(x.Path, x.QuarterKind, "quarterKinds")))
			.Concat(schedules.GetDays().Where(x => !days.Contains(x.Day)).Select(x => OurResources.DataIdUnknown.FormatCurrentCulture(x.Path, x.Day, "days")));
	}

	private static IEnumerable<string> ValidateBusinesses(LocationList locations, ProfessionList professions)
	{
		var professionsById = new Dictionary<string, Profession>(StringComparer.Ordinal);
		foreach (var profession in professions.Professions.Where(x => x is not null && x.IsBusiness))
			professionsById.TryAdd(profession.Id, profession);

		foreach (var business in locations.Businesses.Where(x => x is not null))
		{
			if (professionsById.GetValueOrDefault(business.Profession) is not { } profession)
				yield return OurResources.BusinessProfessionUnknown.FormatCurrentCulture(business.Id, business.Profession);
			else if (profession.WorksFromHome)
				yield return OurResources.BusinessProfessionWorksFromHome.FormatCurrentCulture(business.Id, business.Profession);
		}
	}
}
