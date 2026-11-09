using System.Text.Json.Serialization;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// One of the values a value list character property can have.
/// </summary>
public sealed class CharacterPropertyOption : IIdentified, IWeighted
{
	public required string Id { get; init; }

	public required string Title { get; init; }

	public required int Frequency { get; init; }

	/// <summary>
	/// Named pieces of text that templates can use, such as a pronoun or a description.
	/// </summary>
	public IReadOnlyDictionary<string, string> Text { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

	[JsonIgnore]
	public int Weight => FrequencyUtility.GetWeight(Frequency);
}
