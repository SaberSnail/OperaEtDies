using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// A color that clothing can be, from the clothing colors section of the data. Any clothing can be any color.
/// </summary>
public sealed class ClothingColor : IIdentified, IHasFrequency
{
	public required string Id { get; init; }

	/// <summary>
	/// The word for the color in descriptions, such as "russet".
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// The color, written as #RRGGBB.
	/// </summary>
	public required string Color { get; init; }

	/// <summary>
	/// The article that goes before the color's name, such as "a" or "an".
	/// </summary>
	public string Article { get; init; } = "a";

	/// <summary>
	/// How common the color is when chosen at random.
	/// </summary>
	public FrequencyLevel Frequency { get; init; } = new(Utility.Random.Frequency.Common);
}
