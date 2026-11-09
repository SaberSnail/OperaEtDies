using System.Text.Json.Serialization;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// A profession the player can choose when starting a new game.
/// </summary>
public sealed class Profession : IIdentified, IWeighted
{
	public required string Id { get; init; }

	public required string Title { get; init; }

	/// <summary>
	/// Finishes the sentence "He works as ..." when describing a character.
	/// </summary>
	public required string Description { get; init; }

	public required string DefaultLastName { get; init; }

	public required int Frequency { get; init; }

	[JsonIgnore]
	public int Weight => FrequencyUtility.GetWeight(Frequency);
}
