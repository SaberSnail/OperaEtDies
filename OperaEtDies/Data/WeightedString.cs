using System.Text.Json.Serialization;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// A string that can be randomly selected, where more frequent strings are more likely to be chosen.
/// </summary>
public sealed class WeightedString : IWeighted
{
	public required string Value { get; init; }

	public required int Frequency { get; init; }

	[JsonIgnore]
	public int Weight => FrequencyUtility.GetWeight(Frequency);
}
