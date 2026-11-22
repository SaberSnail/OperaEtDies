using System.Text.Json;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// The templates the village is built from when a new game is created, and the village's settings.
/// </summary>
public sealed class LocationList : IValidatedData
{
	/// <summary>
	/// The kinds of quarter, which decide where a quarter goes on the map, which businesses can be in it, and what its
	/// paths are called.
	/// </summary>
	public required IReadOnlyList<QuarterKind> QuarterKinds { get; init; }

	/// <summary>
	/// The settings for the village as a whole.
	/// </summary>
	public required VillageSettings Village { get; init; }

	/// <summary>
	/// The shapes a town can be laid out in, chosen by frequency.
	/// </summary>
	public required IReadOnlyList<TownLayoutChoice> Layouts { get; init; }

	/// <summary>
	/// The map features a town can have, such as a river or a forest, chosen by frequency.
	/// </summary>
	public IReadOnlyList<MapFeature> Features { get; init; } = [];

	public required IReadOnlyList<QuarterTemplate> Quarters { get; init; }

	/// <summary>
	/// The houses households live in. A house's title comes from <see cref="VillageSettings.HouseTitle"/>.
	/// </summary>
	public required IReadOnlyList<HouseTemplate> Houses { get; init; }

	/// <summary>
	/// The places businesses are run from, each for one profession.
	/// </summary>
	public required IReadOnlyList<BusinessTemplate> Businesses { get; init; }

	public IEnumerable<string> Validate()
	{
		var errors = new List<string>();
		errors.AddRange(DataValidation.ValidateIdentifiedItems("quarterKinds", QuarterKinds, x => x.Id, null));
		var quarterKinds = QuarterKinds.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();
		var kinds = quarterKinds.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		foreach (var kind in quarterKinds.Where(x => string.IsNullOrWhiteSpace(x.Path)))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture($"quarterKinds[{kind.Id}].path"));
		if (Village.BuildingTravelTime < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("village.buildingTravelTime", Village.BuildingTravelTime, 0));
		foreach (var kind in quarterKinds.Where(x => x.TravelTime < 0))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"quarterKinds[{kind.Id}].travelTime", kind.TravelTime, 0));
		foreach (var kind in quarterKinds.Where(x => x.Count is not null))
		{
			var path = $"quarterKinds[{kind.Id}].count";
			if (kind.Count!.Min is < 0)
				errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.min", kind.Count.Min, 0));
			if (kind.Count.Max is { } max && max < Math.Max(kind.MinCount, 1))
				errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.max", max, Math.Max(kind.MinCount, 1)));
		}

		errors.AddRange(CharacterData.ValidateRange("village.familyCount", Village.FamilyCount));
		if (Village.FamilyCount.Minimum < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("village.familyCount.minimum", Village.FamilyCount.Minimum, 0));
		errors.AddRange(CharacterData.ValidateRange("village.landmarkCount", Village.LandmarkCount));
		if (Village.LandmarkCount.Minimum < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("village.landmarkCount.minimum", Village.LandmarkCount.Minimum, 0));

		errors.AddRange(DataValidation.ValidateIdentifiedItems("layouts", Layouts, x => x.Id, null));
		foreach (var layout in Layouts.Where(x => x is not null && !quarterKinds.Any(kind => kind.Id == x.Center && kind is { MinCount: 1, MaxCount: 1, OptionalWith: null, Area: MapArea.Village })))
			errors.Add(OurResources.LayoutCenterInvalid.FormatCurrentCulture(JsonNamingPolicy.CamelCase.ConvertName(layout.Shape.ToString()), layout.Center));

		errors.AddRange(DataValidation.ValidateIdentifiedItems("quarters", Quarters, x => x.Id, x => x.Title));
		errors.AddRange(DataValidation.ValidateIdentifiedItems("houses", Houses, x => x.Id, null));
		if (string.IsNullOrWhiteSpace(Village.HouseTitle))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture("village.houseTitle"));
		else if (!TextTemplate.TryParse(Village.HouseTitle, out _))
			errors.Add(OurResources.TemplateMalformed.FormatCurrentCulture("village.houseTitle"));
		errors.AddRange(DataValidation.ValidateIdentifiedItems("businesses", Businesses, x => x.Id, x => x.Title));

		// Items that are empty or missing an id are already reported above.
		var quarters = Quarters.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();
		var houses = Houses.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();
		var businesses = Businesses.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();

		foreach (var quarter in quarters)
			errors.AddRange(ValidateQuarter(quarter, kinds));

		errors.AddRange(ValidateFeatures(quarterKinds, quarters));

		// Every town has the fewest quarters of each kind it must have, which can't depend on map features, and landmarks if
		// it needs them.
		foreach (var kind in quarterKinds.Where(x => x.MinCount > 0 && !quarters.Any(quarter => quarter.Kind == x.Id && quarter.Borders is null)))
			errors.Add(OurResources.QuarterKindMinimumMissing.FormatCurrentCulture(kind.Id));
		if (Village.LandmarkCount.Maximum > 0 && !quarters.Any(x => x.IsLandmark))
			errors.Add(OurResources.LandmarkQuartersMissing);

		foreach (var house in houses)
		{
			errors.AddRange(house.ValidateDescriptions());
			if (house.Title is not null)
				errors.Add(OurResources.HouseTitleNotAllowed.FormatCurrentCulture(house.Id));
			if (house.Wealth == WealthLevel.VeryLow)
				errors.Add(OurResources.WealthLevelNotAllowed.FormatCurrentCulture($"houses[{house.Id}].wealth", house.Wealth.GetDataName()));
		}

		var kindsWithBusinesses = quarters.Where(x => x.Businesses > 0).Select(x => x.Kind).ToHashSet(StringComparer.Ordinal);
		foreach (var business in businesses)
			errors.AddRange(ValidateBusiness(business, kinds, kindsWithBusinesses));

		// Every household needs somewhere to live, whatever its wealth.
		foreach (var wealth in WealthLevelUtility.HouseholdLevels)
		{
			if (!houses.Any(x => x.Wealth == wealth))
				errors.Add(OurResources.HouseWealthMissing.FormatCurrentCulture(wealth.GetDataName()));
			if (!quarters.Any(x => x.MaxHouses > 0 && x.Wealth == wealth))
				errors.Add(OurResources.QuarterWealthMissing.FormatCurrentCulture(wealth.GetDataName()));
		}

		// Ids that are duplicated within one list are already reported above.
		errors.AddRange(new[] { quarters.Select(x => x.Id), houses.Select(x => x.Id), businesses.Select(x => x.Id) }
			.SelectMany(x => x.Distinct(StringComparer.Ordinal))
			.GroupBy(x => x, StringComparer.Ordinal)
			.Where(x => x.Count() > 1)
			.Select(x => OurResources.LocationIdDuplicate.FormatCurrentCulture(x.Key)));

		return errors;
	}

	private static IEnumerable<string> ValidateBusiness(BusinessTemplate business, IReadOnlySet<string> kinds, IReadOnlySet<string> kindsWithBusinesses)
	{
		var path = $"businesses[{business.Id}]";
		foreach (var error in business.ValidateDescriptions())
			yield return error;
		if (string.IsNullOrWhiteSpace(business.Profession))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.profession");

		foreach (var error in DataValidation.ValidateStrings($"{path}.quarterKinds", business.QuarterKinds))
			yield return error;
		var businessKinds = business.QuarterKinds.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
		foreach (var kind in businessKinds.Where(x => !kinds.Contains(x)))
			yield return OurResources.QuarterKindUnknown.FormatCurrentCulture(business.Id, kind);
		if (businessKinds.Count != 0 && businessKinds.All(kinds.Contains) && !businessKinds.Any(kindsWithBusinesses.Contains))
			yield return OurResources.BusinessQuarterMissing.FormatCurrentCulture(business.Id);
	}

	/// <summary>
	/// Checks the map features, and the quarter kinds and quarters that refer to them.
	/// </summary>
	private IEnumerable<string> ValidateFeatures(IReadOnlyList<QuarterKind> quarterKinds, IReadOnlyList<QuarterTemplate> quarters)
	{
		var errors = new List<string>();
		if (Features.Count != 0)
		{
			errors.AddRange(DataValidation.ValidateIdentifiedItems("features", Features, x => x.Id, x => x.Title));
			if (Village.FeatureCount is null)
				errors.Add(OurResources.DataValueMissing.FormatCurrentCulture("village.featureCount"));
		}

		if (Village.FeatureCount is { } featureCount)
		{
			errors.AddRange(CharacterData.ValidateRange("village.featureCount", featureCount));
			if (featureCount.Minimum < 0)
				errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("village.featureCount.minimum", featureCount.Minimum, 0));
		}

		var features = Features.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();
		foreach (var feature in features)
		{
			var path = $"features[{feature.Id}]";
			var typeName = JsonNamingPolicy.CamelCase.ConvertName(feature.Type.ToString());

			// Which types of feature allow or require each setting is marked on the setting.
			foreach (var (property, isSet) in new[] { (nameof(MapFeature.Size), feature.Size is not null), (nameof(MapFeature.Crossings), feature.Crossings is not null) })
			{
				var name = DataRules.GetDataName(typeof(MapFeature), property);
				var violation = DataRules.Check(feature, property, isSet);
				if (violation == DataRuleViolation.NotAllowed)
					errors.Add(OurResources.FeatureSettingNotAllowed.FormatCurrentCulture(feature.Id, name, typeName));
				else if (violation == DataRuleViolation.Missing)
					errors.Add(OurResources.FeatureSettingMissing.FormatCurrentCulture(feature.Id, name, typeName));
			}

			if (feature.Size is { } size && (size.Length < 1 || size.Width < 1))
				errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.size", Math.Min(size.Length, size.Width), 1));
			if (feature.Crossings is { } crossings)
				errors.AddRange(DataValidation.ValidateStrings($"{path}.crossings", crossings));

			if (feature.Text.Count == 0)
				errors.Add(OurResources.DataListEmpty.FormatCurrentCulture($"{path}.text"));
			foreach (var text in feature.Text.Where(x => x is null || !IsFeatureText(x.Text)))
				errors.Add(OurResources.FeatureTextInvalid.FormatCurrentCulture($"{path}.text", text?.Text));
		}

		var featureIds = features.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		foreach (var kind in quarterKinds.Where(x => x.Borders is not null))
		{
			if (!featureIds.Contains(kind.Borders!))
				errors.Add(OurResources.FeatureUnknown.FormatCurrentCulture($"quarterKinds[{kind.Id}].borders", kind.Borders));
			if (kind.MinCount > 0)
				errors.Add(OurResources.QuarterKindMinimumBorders.FormatCurrentCulture(kind.Id));
		}

		foreach (var kind in quarterKinds.Where(x => x.OptionalWith is not null))
		{
			if (!featureIds.Contains(kind.OptionalWith!))
				errors.Add(OurResources.FeatureUnknown.FormatCurrentCulture($"quarterKinds[{kind.Id}].optional.with", kind.OptionalWith));
			if (kind.MinCount == 0)
				errors.Add(OurResources.QuarterKindOptionalNoMinimum.FormatCurrentCulture(kind.Id));
			errors.AddRange(DataValidation.ValidateChance($"quarterKinds[{kind.Id}].optional.chance", kind.OptionalChance));
		}

		foreach (var quarter in quarters)
		{
			var path = $"quarters[{quarter.Id}]";
			if (quarter.Borders is { } borders && !featureIds.Contains(borders))
				errors.Add(OurResources.FeatureUnknown.FormatCurrentCulture($"{path}.borders", borders));
			foreach (var (featureId, text) in quarter.FeatureText ?? new Dictionary<string, LongText>())
			{
				if (!featureIds.Contains(featureId))
					errors.Add(OurResources.FeatureUnknown.FormatCurrentCulture($"{path}.featureText", featureId));
				else if (text is null || !IsFeatureText(text.Text))
					errors.Add(OurResources.FeatureTextInvalid.FormatCurrentCulture($"{path}.featureText.{featureId}", text?.Text));
			}
		}

		return errors;
	}

	/// <summary>
	/// Whether text is a valid template for a map feature: filled in, with no placeholder but the direction.
	/// </summary>
	private static bool IsFeatureText(string text) =>
		!string.IsNullOrWhiteSpace(text) && TextTemplate.TryParse(text, out var template)
		&& template.Placeholders.All(x => x.Id == MapFeature.DirectionPlaceholder && x.Field is null && (x.Format is null || TextTemplate.IsKnownFormat(x.Format)));

	private static IEnumerable<string> ValidateQuarter(QuarterTemplate quarter, IReadOnlySet<string> kinds)
	{
		var path = $"quarters[{quarter.Id}]";
		foreach (var error in quarter.ValidateDescriptions())
			yield return error;

		if (string.IsNullOrWhiteSpace(quarter.Kind))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.kind");
		else if (!kinds.Contains(quarter.Kind))
			yield return OurResources.QuarterKindUnknown.FormatCurrentCulture(quarter.Id, quarter.Kind);

		if (quarter.Houses is { } houses)
		{
			if (houses.Minimum < 0)
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.houses.minimum", houses.Minimum, 0);
			if (houses.Maximum < Math.Max(houses.Minimum, 1))
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.houses.maximum", houses.Maximum, Math.Max(houses.Minimum, 1));
			foreach (var error in houses.Shape?.Validate($"{path}.houses.shape") ?? [])
				yield return error;
			if (houses.Wealth == WealthLevel.VeryLow)
				yield return OurResources.WealthLevelNotAllowed.FormatCurrentCulture($"{path}.houses.wealth", houses.Wealth.GetDataName());
		}

		if (quarter.Businesses < 0)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.businesses", quarter.Businesses, 0);

		if (quarter.Size.Length < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.size.length", quarter.Size.Length, 1);
		if (quarter.Size.Width < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.size.width", quarter.Size.Width, 1);
	}
}
