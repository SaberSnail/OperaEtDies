namespace OperaEtDies.Data;

/// <summary>
/// Settings for the village as a whole, from the village section of the data.
/// </summary>
public sealed class VillageSettings
{
	/// <summary>
	/// The name of the data section the settings are read from.
	/// </summary>
	public const string SectionName = "village";

	/// <summary>
	/// How many households the village has, other than the player's.
	/// </summary>
	public required IntegerRange FamilyCount { get; init; }

	/// <summary>
	/// How many landmark quarters (quarters with no houses or businesses) the village has, including unique ones.
	/// </summary>
	public required IntegerRange LandmarkCount { get; init; }

	/// <summary>
	/// How many map features the village has. Required if there are map features.
	/// </summary>
	public IntegerRange? FeatureCount { get; init; }

	/// <summary>
	/// How many minutes it takes to go into or out of a house or business from its quarter.
	/// </summary>
	public required int BuildingTravelTime { get; init; }

	/// <summary>
	/// The title of every house: a template filled in with the character properties of the household's head.
	/// </summary>
	public required string HouseTitle { get; init; }
}
