using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// One of the values a value list character property can have.
/// </summary>
public sealed class CharacterPropertyOption : IIdentified, IHasFrequency
{
	public required string Id { get; init; }

	public required string Title { get; init; }

	/// <summary>
	/// How common the value is when chosen at random.
	/// </summary>
	public FrequencyLevel Frequency { get; init; } = new(Utility.Random.Frequency.Common);

	/// <summary>
	/// Frequencies that replace <see cref="Frequency"/> when their conditions are met. The first one that's met is used.
	/// </summary>
	public IReadOnlyList<FrequencyOverride>? FrequencyWhen { get; init; }

	/// <summary>
	/// Named pieces of text that templates can use, such as a pronoun or a description.
	/// </summary>
	public IReadOnlyDictionary<string, string> Text { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

	/// <summary>
	/// A condition on other properties that must be met for a character to have this value.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// How hard the value is to hide, from <see cref="CharacterObscurer.MinStrength"/> (easily hidden) to
	/// <see cref="CharacterObscurer.MaxStrength"/> (only hidden by the strongest obscurers, the default).
	/// </summary>
	public int? Prominence { get; init; }

	public int GetProminence() => Prominence ?? CharacterObscurer.MaxStrength;

	/// <summary>
	/// Gets the value's frequency, given a function that gets the condition value id of other properties: the first
	/// override whose condition is met, or else the usual frequency.
	/// </summary>
	public FrequencyLevel GetFrequency(Func<string, string?> getValueId) =>
		FrequencyWhen?.FirstOrDefault(x => CharacterCondition.IsMet(x.When, getValueId))?.Frequency ?? Frequency;
}
