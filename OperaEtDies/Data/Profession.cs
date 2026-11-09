using System.Text.Json.Serialization;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// A profession the player can choose when starting a new game.
/// </summary>
public sealed class Profession : IWeighted
{
	public required string Id { get; init; }

	public required string Title { get; init; }

	public required string DefaultLastName { get; init; }

	public required int Frequency { get; init; }

	[JsonIgnore]
	public int Weight => FrequencyUtility.GetWeight(Frequency);
}
