using System.Text.Json.Serialization;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// One of the values a character attribute (such as height) can have.
/// </summary>
public sealed class CharacterOption : IWeighted
{
	public required string Id { get; init; }

	public required string Title { get; init; }

	public required int Frequency { get; init; }

	[JsonIgnore]
	public int Weight => FrequencyUtility.GetWeight(Frequency);
}
