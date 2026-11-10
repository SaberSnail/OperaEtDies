using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// A frequency that replaces a value's usual frequency when a condition on the character's other properties is met.
/// </summary>
public sealed class FrequencyOverride
{
	public required CharacterCondition When { get; init; }

	public required FrequencyLevel Frequency { get; init; }
}
