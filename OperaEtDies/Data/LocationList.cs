using System.IO;
using System.Text.Json;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// The templates the town is built from when a new game is created, loaded from Locations.json.
/// </summary>
public sealed class LocationList : IValidatedData
{
	public const string FileName = "Locations.json";

	public static DataLoadResult<LocationList> Load(string dataDirectory) => Load(dataDirectory, []);

	/// <summary>
	/// Loads the file from the data folder, with the changes from the mod folders applied in order.
	/// </summary>
	public static DataLoadResult<LocationList> Load(string dataDirectory, IReadOnlyList<string> modDirectories) =>
		JsonDataLoader.Load<LocationList>(Path.Combine(dataDirectory, FileName), modDirectories, []);

	/// <summary>
	/// The kinds of quarter, which decide where a quarter goes on the map, which businesses can be in it, and what its
	/// paths are called.
	/// </summary>
	public required IReadOnlyList<QuarterKind> QuarterKinds { get; init; }

	/// <summary>
	/// How many minutes it takes to go into or out of a house or business from its quarter.
	/// </summary>
	public required int BuildingTravelTime { get; init; }

	/// <summary>
	/// How many landmark quarters (quarters with no houses or businesses) the town has, including unique ones.
	/// </summary>
	public required IntegerRange LandmarkCount { get; init; }

	/// <summary>
	/// The shapes a town can be laid out in, chosen by frequency.
	/// </summary>
	public required IReadOnlyList<TownLayoutChoice> Layouts { get; init; }

	/// <summary>
	/// The map features a town can have, such as a river or a forest, chosen by frequency.
	/// </summary>
	public IReadOnlyList<MapFeature> Features { get; init; } = [];

	/// <summary>
	/// How many map features a town has.
	/// </summary>
	public IntegerRange? FeatureCount { get; init; }

	public required IReadOnlyList<QuarterTemplate> Quarters { get; init; }

	/// <summary>
	/// The houses households live in. A house's title comes from <see cref="HouseTitle"/>.
	/// </summary>
	public required IReadOnlyList<HouseTemplate> Houses { get; init; }

	/// <summary>
	/// The title of every house: a template filled in with the Characters.json properties of the household's head.
	/// </summary>
	public required string HouseTitle { get; init; }

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
		if (BuildingTravelTime < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("buildingTravelTime", BuildingTravelTime, 0));
		foreach (var kind in quarterKinds.Where(x => x.TravelTime < 0))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"quarterKinds[{kind.Id}].travelTime", kind.TravelTime, 0));

		errors.AddRange(CharacterData.ValidateRange("landmarkCount", LandmarkCount));
		if (LandmarkCount.Minimum < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("landmarkCount.minimum", LandmarkCount.Minimum, 0));

		if (Layouts.Count == 0)
			errors.Add(OurResources.DataListEmpty.FormatCurrentCulture("layouts"));
		foreach (var layout in Layouts.Where(x => x is not null && !quarterKinds.Any(kind => kind.Id == x.Center && kind.Unique && kind.Area == MapArea.Village)))
			errors.Add(OurResources.LayoutCenterInvalid.FormatCurrentCulture(JsonNamingPolicy.CamelCase.ConvertName(layout.Shape.ToString()), layout.Center));

		errors.AddRange(DataValidation.ValidateIdentifiedItems("quarters", Quarters, x => x.Id, x => x.Title));
		errors.AddRange(DataValidation.ValidateIdentifiedItems("houses", Houses, x => x.Id, null));
		if (string.IsNullOrWhiteSpace(HouseTitle))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture("houseTitle"));
		else if (!TextTemplate.TryParse(HouseTitle, out _))
			errors.Add(OurResources.TemplateMalformed.FormatCurrentCulture("houseTitle"));
		errors.AddRange(DataValidation.ValidateIdentifiedItems("businesses", Businesses, x => x.Id, x => x.Title));

		// Items that are empty or missing an id are already reported above.
		var quarters = Quarters.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();
		var houses = Houses.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();
		var businesses = Businesses.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();

		foreach (var quarter in quarters)
			errors.AddRange(ValidateQuarter(quarter, kinds));

		errors.AddRange(ValidateFeatures(quarterKinds, quarters));

		// Every town has a quarter of each unique kind, and landmarks if it needs them.
		foreach (var kind in quarterKinds.Where(x => x.Unique && !quarters.Any(quarter => quarter.Kind == x.Id)))
			errors.Add(OurResources.QuarterKindUniqueMissing.FormatCurrentCulture(kind.Id));
		if (LandmarkCount.Maximum > 0 && !quarters.Any(x => x.IsLandmark))
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
			if (FeatureCount is null)
				errors.Add(OurResources.DataValueMissing.FormatCurrentCulture("featureCount"));
		}

		if (FeatureCount is { } featureCount)
		{
			errors.AddRange(CharacterData.ValidateRange("featureCount", featureCount));
			if (featureCount.Minimum < 0)
				errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("featureCount.minimum", featureCount.Minimum, 0));
		}

		var features = Features.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();
		foreach (var feature in features)
		{
			var path = $"features[{feature.Id}]";
			var typeName = JsonNamingPolicy.CamelCase.ConvertName(feature.Type.ToString());
			if (feature.Type == MapFeatureType.Area)
			{
				if (feature.Size is not { } size)
					errors.Add(OurResources.FeatureSettingMissing.FormatCurrentCulture(feature.Id, "size", typeName));
				else if (size.Length < 1 || size.Width < 1)
					errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.size", Math.Min(size.Length, size.Width), 1));
				if (feature.Crossings is not null)
					errors.Add(OurResources.FeatureSettingNotAllowed.FormatCurrentCulture(feature.Id, "crossings", typeName));
			}
			else
			{
				if (feature.Crossings is null)
					errors.Add(OurResources.FeatureSettingMissing.FormatCurrentCulture(feature.Id, "crossings", typeName));
				else
					errors.AddRange(DataValidation.ValidateStrings($"{path}.crossings", feature.Crossings));
				if (feature.Size is not null)
					errors.Add(OurResources.FeatureSettingNotAllowed.FormatCurrentCulture(feature.Id, "size", typeName));
			}

			if (feature.Text.Count == 0)
				errors.Add(OurResources.DataListEmpty.FormatCurrentCulture($"{path}.text"));
			foreach (var text in feature.Text.Where(x => !IsFeatureText(x)))
				errors.Add(OurResources.FeatureTextInvalid.FormatCurrentCulture($"{path}.text", text));
		}

		var featureIds = features.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		foreach (var kind in quarterKinds.Where(x => x.Borders is not null))
		{
			if (!featureIds.Contains(kind.Borders!))
				errors.Add(OurResources.FeatureUnknown.FormatCurrentCulture($"quarterKinds[{kind.Id}].borders", kind.Borders));
			if (kind.Unique)
				errors.Add(OurResources.QuarterKindUniqueBorders.FormatCurrentCulture(kind.Id));
		}

		foreach (var quarter in quarters)
		{
			var path = $"quarters[{quarter.Id}]";
			if (quarter.Borders is { } borders && !featureIds.Contains(borders))
				errors.Add(OurResources.FeatureUnknown.FormatCurrentCulture($"{path}.borders", borders));
			if (quarter.Borders is not null && quarterKinds.Any(x => x.Id == quarter.Kind && x.Unique))
				errors.Add(OurResources.QuarterKindUniqueBorders.FormatCurrentCulture(quarter.Kind));
			foreach (var (featureId, text) in quarter.FeatureText ?? new Dictionary<string, string>())
			{
				if (!featureIds.Contains(featureId))
					errors.Add(OurResources.FeatureUnknown.FormatCurrentCulture($"{path}.featureText", featureId));
				else if (!IsFeatureText(text))
					errors.Add(OurResources.FeatureTextInvalid.FormatCurrentCulture($"{path}.featureText.{featureId}", text));
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
			if (quarter.Wealth is null)
				yield return OurResources.QuarterHousesWithoutWealth.FormatCurrentCulture(quarter.Id);
		}
		else if (quarter.Wealth is not null)
		{
			yield return OurResources.QuarterWealthWithoutHouses.FormatCurrentCulture(quarter.Id);
		}

		if (quarter.Wealth == WealthLevel.VeryLow)
			yield return OurResources.WealthLevelNotAllowed.FormatCurrentCulture($"{path}.wealth", quarter.Wealth.Value.GetDataName());

		if (quarter.Businesses < 0)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.businesses", quarter.Businesses, 0);

		if (quarter.Size.Length < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.size.length", quarter.Size.Length, 1);
		if (quarter.Size.Width < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.size.width", quarter.Size.Width, 1);
	}
}
