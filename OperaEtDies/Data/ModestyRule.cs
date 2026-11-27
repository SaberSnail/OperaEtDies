namespace OperaEtDies.Data;

/// <summary>
/// How well covered someone must be, from the modesty rules section of the data: for each slot, the least modesty the
/// clothing worn in it must have at each level of comfort. Each person follows the first rule whose condition they meet.
/// </summary>
public sealed class ModestyRule : IIdentified
{
	public required string Id { get; init; }

	/// <summary>
	/// A condition on character properties that must be met for a character to follow the rule.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// The least modesty in each slot, keyed by slot id, as a list of minimums in order of comfort: each applies from its
	/// comfort up to the next one's.
	/// </summary>
	public IReadOnlyDictionary<string, IReadOnlyList<ModestyMinimum>> Slots { get; init; } = new Dictionary<string, IReadOnlyList<ModestyMinimum>>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the least modesty a slot needs at a level of comfort: the minimum of the last entry whose comfort it reaches, or
	/// below the first entry, the first entry's minimum. A slot the rule doesn't list needs none.
	/// </summary>
	public int GetMinimum(string slotId, double comfort)
	{
		var minimums = Slots.GetValueOrDefault(slotId) ?? [];
		return minimums.Count == 0 ? 0 : (minimums.LastOrDefault(x => comfort >= x.Comfort) ?? minimums[0]).Minimum;
	}
}

/// <summary>
/// The least modesty a slot needs from a level of comfort up.
/// </summary>
public sealed class ModestyMinimum
{
	/// <summary>
	/// The comfort, from 0 to 100, from which the minimum applies.
	/// </summary>
	public required int Comfort { get; init; }

	/// <summary>
	/// The least modesty the slot needs, from 0 to <see cref="ClothingItem.MaxCoverage"/>.
	/// </summary>
	public required int Minimum { get; init; }
}
