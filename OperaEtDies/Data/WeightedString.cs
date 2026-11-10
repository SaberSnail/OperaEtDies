using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// A string that can be chosen at random, where more frequent strings are more likely to be chosen.
/// </summary>
public sealed class WeightedString : IHasFrequency
{
	public required string Value { get; init; }

	/// <summary>
	/// How common the string is when chosen at random.
	/// </summary>
	public FrequencyLevel Frequency { get; init; } = new(Utility.Random.Frequency.Common);
}
