using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// Clothing that's worn together, from the outfits section of the data, which people are given when they're created.
/// </summary>
public sealed class Outfit : IIdentified, IHasFrequency
{
	public required string Id { get; init; }

	public required OutfitKind Kind { get; init; }

	/// <summary>
	/// A condition on character properties that must be met for a character to be given the outfit.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// How common the outfit is when chosen at random among the outfits of its kind.
	/// </summary>
	public FrequencyLevel Frequency { get; init; } = new(Utility.Random.Frequency.Common);

	/// <summary>
	/// The ids of the clothing in the outfit, no two of which take the same slot.
	/// </summary>
	public required IReadOnlyList<string> Clothing { get; init; }

	/// <summary>
	/// For work clothes, whether they're a complete set of clothes worn on their own, such as a uniform, rather than going
	/// over what someone is already wearing.
	/// </summary>
	[AllowedFor(nameof(Kind), OutfitKind.Work)]
	public bool Complete { get; init; }
}

/// <summary>
/// When an outfit is worn.
/// </summary>
public enum OutfitKind
{
	/// <summary>
	/// Worn most of the time.
	/// </summary>
	Everyday,

	/// <summary>
	/// Worn while working. It only fills its own slots, so it can be worn over everyday clothing.
	/// </summary>
	Work,

	/// <summary>
	/// Worn while sleeping at home.
	/// </summary>
	Night,

	/// <summary>
	/// What someone has on when they'd otherwise wear nothing, such as for swimming or bathing: nothing at all, or only
	/// a little, such as a loincloth. It's always complete, and everyone has one.
	/// </summary>
	Nothing,
}
