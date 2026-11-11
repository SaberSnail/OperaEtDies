using System.Collections.ObjectModel;
using System.Globalization;
using GoldenAnvil.Utility.Logging;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Town;

/// <summary>
/// Lays out the town around its households and businesses. First the quarters are chosen: one of each unique kind,
/// quarters for the houses, quarters for the businesses, and landmarks, leaving out quarters that border map features
/// the town doesn't have. Then a shape arranges them on the map around the river, if there is one, and connects them.
/// Finally, areas such as a forest are placed next to the quarters that border them, and each quarter learns which
/// features it borders.
/// </summary>
internal sealed class TownLayout
{
	public static (IReadOnlyList<TownLocation> Locations, LayoutShape Shape, IReadOnlyList<TownFeature> Features) Create(GameData gameData, Random random,
		IReadOnlyList<Household> households, IReadOnlyList<Business> businesses, IReadOnlyList<MapFeature> features)
	{
		var layout = new TownLayout(gameData, random, features);
		layout.AddUniqueQuarters();
		foreach (var wealth in WealthLevelUtility.HouseholdLevels)
			layout.PlaceHouses(households.Where(x => x.WealthLevel == wealth).ToList(), wealth);
		foreach (var business in businesses.Where(x => !x.Profession.WorksFromHome))
			layout.PlaceBusiness(business);
		layout.AddLandmarks();
		var (shape, townFeatures) = layout.Arrange();
		return (layout.m_locations, shape, townFeatures);
	}

	private TownLayout(GameData gameData, Random random, IReadOnlyList<MapFeature> features)
	{
		m_gameData = gameData;
		m_random = random;
		m_features = features;
		m_featureIds = features.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		m_quarters = [];
		m_locations = [];
		m_templateUses = new Dictionary<string, int>(StringComparer.Ordinal);
	}

	/// <summary>
	/// Adds a quarter of each unique kind, such as the village square.
	/// </summary>
	private void AddUniqueQuarters()
	{
		foreach (var kind in m_gameData.QuarterKinds.Where(x => x.Unique))
			AddQuarter(ChooseTemplate(m_gameData.QuarterTemplates.Where(x => x.Kind == kind.Id && IsAvailable(x)).ToList(), $"the {kind.Id} kind"));
	}

	/// <summary>
	/// Gives each household a house, in quarters of the same wealth. Quarters are added, each planning a number of houses
	/// from its range (leaning toward fewer), until they can hold every household. Each quarter then gets at least one
	/// house, then its fewest, and the rest are spread among the quarters below their planned number.
	/// </summary>
	private void PlaceHouses(IReadOnlyList<Household> households, WealthLevel wealth)
	{
		var templates = m_gameData.QuarterTemplates.Where(x => x.Wealth == wealth && x.MaxHouses > 0 && !IsUniqueKind(x) && IsAvailable(x)).ToList();
		var planned = new Dictionary<MapQuarter, int>();
		while (planned.Values.Sum() < households.Count)
		{
			var quarter = AddQuarter(ChooseTemplate(templates, $"{wealth.GetDataName()} wealth quarters"));
			var fewest = Math.Max(quarter.Template.MinHouses, 1);
			planned[quarter] = Math.Min(m_random.Next(fewest, quarter.Template.MaxHouses + 1), m_random.Next(fewest, quarter.Template.MaxHouses + 1));
		}

		var houses = m_gameData.HouseTemplates.Where(x => x.Wealth == wealth).ToList();
		var remaining = households.ToList();
		m_random.Shuffle(remaining);

		var quarters = planned.Keys.ToList();
		foreach (var minimum in new Func<MapQuarter, int>[] { _ => 1, x => x.Template.MinHouses })
		{
			foreach (var quarter in quarters)
			{
				while (remaining.Count != 0 && quarter.HouseCount < minimum(quarter))
					AddHouse(quarter, remaining, houses);
			}
		}

		while (remaining.Count != 0)
			AddHouse(m_random.NextItem(quarters.Where(x => x.HouseCount < planned[x]).ToList()), remaining, houses);
	}

	private void AddHouse(MapQuarter quarter, IList<Household> remaining, IReadOnlyList<HouseTemplate> houses)
	{
		var household = remaining[^1];
		remaining.RemoveAt(remaining.Count - 1);

		quarter.HouseCount++;
		household.House = AddLocation(ChooseTemplate(houses, $"{quarter.Template.Wealth?.GetDataName()} wealth houses"), quarter);
	}

	/// <summary>
	/// Puts the business in a quarter of a kind its place allows, adding a quarter if none has room. Only places that can
	/// go in a kind of quarter the town can have are used, if there are any. If no quarter can be added, such as when the
	/// only kind allowed is unique, it goes in an allowed quarter even though it's full.
	/// </summary>
	private void PlaceBusiness(Business business)
	{
		var places = m_gameData.GetBusinessTemplates(business.Profession.Id);
		var availablePlaces = places.Where(x => x.QuarterKinds.Any(kind => TownGenerator.IsKindAvailable(m_gameData, kind, m_featureIds))).ToList();
		var template = ChooseTemplate(availablePlaces.Count != 0 ? availablePlaces : places, $"the {business.Profession.Id} profession");
		var kinds = template.QuarterKinds.Where(x => TownGenerator.IsKindAvailable(m_gameData, x, m_featureIds)).ToHashSet(StringComparer.Ordinal);
		var allowed = m_quarters.Where(x => kinds.Contains(x.Template.Kind)).ToList();
		var withRoom = allowed.Where(x => x.BusinessCount < x.Template.Businesses).ToList();

		// Every household already has a house, so a quarter that's meant to have people living in it is only added if
		// there's no other choice.
		var candidates = m_gameData.QuarterTemplates.Where(x => kinds.Contains(x.Kind) && x.Businesses > 0 && !IsUniqueKind(x) && IsAvailable(x)).ToList();
		var empty = candidates.Where(x => x.MinHouses == 0).ToList();

		MapQuarter quarter;
		if (withRoom.Count != 0)
		{
			quarter = m_random.NextItem(withRoom);
		}
		else if (candidates.Count != 0)
		{
			quarter = AddQuarter(ChooseTemplate(empty.Count != 0 ? empty : candidates, $"quarters of the kinds {string.Join(", ", kinds)}"));
		}
		else
		{
			Log.Warn($"No quarter has room for {template.Id}, and none can be added, so it's going in a full quarter.");
			quarter = m_random.NextItem(allowed.Count != 0 ? allowed : m_quarters.ToList());
		}

		quarter.BusinessCount++;
		business.Location = AddLocation(template, quarter);
	}

	/// <summary>
	/// Adds landmarks until the town has as many as it should, counting unique landmarks. Village and outskirts
	/// landmarks take turns, starting with either, as long as both have templates.
	/// </summary>
	private void AddLandmarks()
	{
		var count = m_random.NextInRange(m_gameData.LandmarkCount);
		var templates = m_gameData.QuarterTemplates.Where(x => x.IsLandmark && !IsUniqueKind(x) && IsAvailable(x)).ToList();
		var byArea = templates.GroupBy(x => m_gameData.QuarterKinds.Get(x.Kind).Area).ToDictionary(x => x.Key, x => x.ToList());
		var areas = byArea.Keys.OrderBy(x => x).ToList();
		var turn = areas.Count == 0 ? 0 : m_random.Next(areas.Count);

		while (areas.Count != 0 && m_quarters.Count(x => x.Template.IsLandmark) < count)
		{
			var area = areas[turn++ % areas.Count];
			AddQuarter(ChooseTemplate(byArea[area], $"{area.ToString().ToLowerInvariant()} landmarks"));
		}
	}

	/// <summary>
	/// Arranges the quarters on the map with a layout chosen by frequency, connects them, and places the town's map
	/// features. Returns the layout's shape and where the features are.
	/// </summary>
	private (LayoutShape Shape, IReadOnlyList<TownFeature> Features) Arrange()
	{
		var layout = m_gameData.FrequencyChances.Choose(m_random, m_gameData.Layouts);
		var center = m_quarters.First(x => x.Template.Kind == layout.Center);
		var map = new TownMap(m_random);
		var river = m_features.FirstOrDefault(x => x.Type == MapFeatureType.River);
		TownShape.Create(layout.Shape, map, m_random).Build(center, m_quarters.Where(x => x != center).ToList(), river?.Id);
		map.Finish(GetPath);

		var features = new List<TownFeature>();
		if (river is not null && map.River is { } mapRiver)
			features.Add(new TownFeature(river, GetRiverBounds(map, mapRiver)));
		foreach (var area in m_features.Where(x => x.Type == MapFeatureType.Area))
		{
			if (PlaceArea(map, area) is { } bounds)
				features.Add(new TownFeature(area, bounds));
		}

		foreach (var quarter in m_quarters)
			quarter.Location.Surroundings = GetSurroundings(map, quarter, features);
		foreach (var quarter in m_quarters.Where(x => x.Borders.Any(id => !x.Location.Surroundings.Any(surrounding => surrounding.Feature.Id == id))))
			Log.Warn($"The quarter {quarter.Location.Id} should border {string.Join(", ", quarter.Borders)}, but there wasn't room for it to.");

		return (layout.Shape, features);
	}

	/// <summary>
	/// Places an area next to a quarter that borders it, or else next to a quarter in the outskirts, or else anywhere
	/// there's room, starting from the quarters farthest from the center. Returns where it went, or null if there was no
	/// room anywhere.
	/// </summary>
	private MapRect? PlaceArea(TownMap map, MapFeature area)
	{
		var borderers = map.Quarters.Where(x => x.Borders.Contains(area.Id)).ToList();
		m_random.Shuffle(borderers);
		var others = map.Quarters.Where(x => !borderers.Contains(x))
			.OrderBy(x => x.Kind.Area == MapArea.Outskirts ? 0 : 1)
			.ThenByDescending(x => map.GetDistanceFromCenter(x.Rect) + m_random.NextDouble() * 2)
			.ToList();

		MapRect? placed = null;
		foreach (var quarter in borderers.Concat(others).Where(_ => placed is null))
			placed = map.TryPlaceArea(area.Size!, quarter);

		if (placed is null)
			Log.Warn($"There was no room for the {area.Id} on the map.");
		return placed;
	}

	/// <summary>
	/// Gets the part of the river beside the quarters: as long as the map is wide, and a little more.
	/// </summary>
	private static MapRect GetRiverBounds(TownMap map, MapRiver river)
	{
		var rects = map.Quarters.Select(x => x.Rect).ToList();
		return river.IsHorizontal
			? new MapRect(rects.Min(x => x.X) - 1, river.Line, rects.Max(x => x.Right) - rects.Min(x => x.X) + 2, 1)
			: new MapRect(river.Line, rects.Min(x => x.Y) - 1, 1, rects.Max(x => x.Bottom) - rects.Min(x => x.Y) + 2);
	}

	/// <summary>
	/// Gets the features a quarter borders, each with the direction it's in and a sentence describing it: the quarter's
	/// own sentence for the feature if it has one, or else one of the feature's, at random.
	/// </summary>
	private IReadOnlyList<TownSurrounding> GetSurroundings(TownMap map, MapQuarter quarter, IReadOnlyList<TownFeature> features)
	{
		var surroundings = new List<TownSurrounding>();
		foreach (var feature in features)
		{
			CompassDirection? direction = feature.Feature.Type == MapFeatureType.River
				? map.GetRiverSide(quarter.Rect)
				: quarter.Rect.GetSharedEdge(feature.Bounds) is { } edge ? TownMap.GetDirectionsToward(quarter.Rect, edge)[0] : null;
			if (direction is { } facing)
			{
				var text = quarter.Template.FeatureText?.GetValueOrDefault(feature.Feature.Id) ?? m_random.NextItem(feature.Feature.Text);
				var directionName = facing.GetTitle().ToLower(CultureInfo.CurrentCulture);
				surroundings.Add(new TownSurrounding(feature.Feature, facing, TextTemplate.Parse(text).Format(_ => directionName)));
			}
		}

		return surroundings;
	}

	/// <summary>
	/// Gets what the path between two quarters is called. A path across the river is one of the river's crossings.
	/// Otherwise, it's the path of the outskirts quarter's kind if one is in the outskirts, or else of either quarter's
	/// kind, preferring kinds that aren't unique.
	/// </summary>
	private string GetPath(MapQuarter first, MapQuarter second, bool isCrossing)
	{
		var path = "";
		if (isCrossing && m_features.FirstOrDefault(x => x.Type == MapFeatureType.River)?.Crossings is { Count: > 0 } crossings)
		{
			path = m_random.NextItem(crossings);
		}
		else
		{
			var kinds = new[] { first.Kind, second.Kind };
			var outskirts = kinds.Where(x => x.Area == MapArea.Outskirts).ToList();
			var choices = outskirts.Count != 0 ? outskirts : kinds.Where(x => !x.Unique).ToList();
			path = m_random.NextItem(choices.Count != 0 ? choices : kinds.ToList()).Path;
		}

		return path;
	}

	private bool IsUniqueKind(QuarterTemplate template) => m_gameData.QuarterKinds.Get(template.Kind).Unique;

	/// <summary>
	/// Whether the town has every map feature the quarter must border.
	/// </summary>
	private bool IsAvailable(QuarterTemplate template) => m_gameData.GetBorderedFeatures(template).All(m_featureIds.Contains);

	private MapQuarter AddQuarter(QuarterTemplate template)
	{
		var quarter = new MapQuarter(template, m_gameData.QuarterKinds.Get(template.Kind), CreateLocation(template), m_gameData.GetBorderedFeatures(template).ToList());
		m_quarters.Add(quarter);
		return quarter;
	}

	private TownLocation AddLocation(LocationTemplate template, MapQuarter quarter)
	{
		var location = CreateLocation(template);
		location.Connect(quarter.Location);
		return location;
	}

	private TownLocation CreateLocation(LocationTemplate template)
	{
		var uses = m_templateUses.GetValueOrDefault(template.Id) + 1;
		m_templateUses[template.Id] = uses;

		var location = new TownLocation(uses == 1 ? template.Id : $"{template.Id}_{uses}", template);
		m_locations.Add(location);
		return location;
	}

	/// <summary>
	/// Chooses a template that hasn't been used yet, by frequency. If every template has been used, one is used again.
	/// </summary>
	private T ChooseTemplate<T>(IReadOnlyList<T> templates, string description)
		where T : LocationTemplate
	{
		var unused = templates.Where(x => !m_templateUses.ContainsKey(x.Id)).ToList();
		if (unused.Count == 0)
			Log.Warn($"Every location template for {description} has been used, so one is being used again.");

		return m_gameData.FrequencyChances.Choose(m_random, unused.Count != 0 ? unused : templates, templates);
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(TownLayout));

	private readonly GameData m_gameData;
	private readonly Random m_random;
	private readonly IReadOnlyList<MapFeature> m_features;
	private readonly HashSet<string> m_featureIds;
	private readonly Collection<MapQuarter> m_quarters;
	private readonly Collection<TownLocation> m_locations;
	private readonly Dictionary<string, int> m_templateUses;
}
