using System.Text.Json.Serialization;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// A name that can be randomly selected, where more frequent names are more likely to be chosen.
/// </summary>
public sealed class WeightedName : IWeighted
{
	public required string Name { get; init; }

	public required int Frequency { get; init; }

	[JsonIgnore]
	public int Weight => FrequencyUtility.GetWeight(Frequency);
}
