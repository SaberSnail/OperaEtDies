using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Town;

/// <summary>
/// Creates the town for a new game.
/// </summary>
public static class TownGenerator
{
	/// <summary>
	/// Chooses the town's map features, creates the player's household, then the other households one at a time, and
	/// then lays out the town around them. Households only take up trades the town has a place for, given its features.
	/// </summary>
	public static GeneratedTown Generate(GameData gameData, CharacterTraits player, Random random)
	{
		var features = ChooseFeatures(gameData, random, player);
		var featureIds = features.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);

		var families = new FamilyGenerator(gameData, random, x => IsProfessionAvailable(gameData, x, featureIds));
		families.AddPlayerHousehold(player);

		var count = random.NextInRange(gameData.Families.FamilyCount);
		for (var index = 0; index < count; index++)
			families.AddHousehold();

		var (locations, shape, townFeatures) = TownLayout.Create(gameData, random, families.Households, families.Businesses, features);
		return new GeneratedTown(families.Households, families.Businesses, locations, shape, townFeatures);
	}

	/// <summary>
	/// Whether a profession can be practiced in a town with the given features: it works from home, or one of its places
	/// can go in a kind of quarter the town can have.
	/// </summary>
	public static bool IsProfessionAvailable(GameData gameData, Profession profession, IReadOnlySet<string> featureIds) =>
		profession.WorksFromHome || gameData.GetBusinessTemplates(profession.Id).Any(x => x.QuarterKinds.Any(kind => IsKindAvailable(gameData, kind, featureIds)));

	/// <summary>
	/// Whether a town with the given features can have quarters of a kind.
	/// </summary>
	public static bool IsKindAvailable(GameData gameData, string kindId, IReadOnlySet<string> featureIds) =>
		gameData.QuarterKinds.Find(kindId) is { } kind && (kind.Borders is null || featureIds.Contains(kind.Borders));

	/// <summary>
	/// Chooses the town's map features by frequency, with at most one river. If the player's profession needs a feature
	/// to have a place in the town, that feature is added.
	/// </summary>
	private static IReadOnlyList<MapFeature> ChooseFeatures(GameData gameData, Random random, CharacterTraits player)
	{
		var chosen = new List<MapFeature>();
		List<MapFeature> GetOptions() => gameData.MapFeatures.Where(x => !chosen.Contains(x) && (x.Type != MapFeatureType.River || !chosen.Any(other => other.Type == MapFeatureType.River))).ToList();

		var count = random.NextInRange(gameData.FeatureCount);
		var options = GetOptions();
		while (chosen.Count < count && options.Count != 0)
		{
			chosen.Add(gameData.FrequencyChances.Choose(random, options, gameData.MapFeatures));
			options = GetOptions();
		}

		if (player.Values.GetValueOrDefault(gameData.Families.Properties.Profession) is CharacterPropertyOption option && gameData.Professions.Find(option.Id) is { } profession)
		{
			var ids = chosen.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
			var needed = gameData.MapFeatures.FirstOrDefault(x => !ids.Contains(x.Id) && !IsProfessionAvailable(gameData, profession, ids) && IsProfessionAvailable(gameData, profession, new HashSet<string>(ids, StringComparer.Ordinal) { x.Id }));
			if (needed is not null && (needed.Type != MapFeatureType.River || !chosen.Any(x => x.Type == MapFeatureType.River)))
				chosen.Add(needed);
		}

		return chosen;
	}
}
