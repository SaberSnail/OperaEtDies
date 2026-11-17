namespace OperaEtDies.Data;

/// <summary>
/// Settings for clothing as a whole, from the clothing settings section of the data.
/// </summary>
public sealed class ClothingSettings
{
	/// <summary>
	/// The name of the data section the settings are read from.
	/// </summary>
	public const string SectionName = "clothingSettings";

	/// <summary>
	/// Settings for data without clothing settings, where only complete coverage hides clothing.
	/// </summary>
	public static ClothingSettings None { get; } = new();

	/// <summary>
	/// How much coverage of each of its slots, from clothing in the layers outside it, leaves a piece out of the description
	/// of what someone is wearing.
	/// </summary>
	public int HidesClothingAt { get; init; } = ClothingItem.MaxCoverage;
}
