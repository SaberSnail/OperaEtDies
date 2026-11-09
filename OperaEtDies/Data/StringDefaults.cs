namespace OperaEtDies.Data;

/// <summary>
/// The values a string character property picks from at random. Either there's a single list of values, or the
/// values depend on the current value of a value list property, with a list for each of that property's values.
/// </summary>
public sealed class StringDefaults
{
	/// <summary>
	/// The id of the value list property that picks which list of values to use.
	/// </summary>
	public string? KeyedBy { get; init; }

	/// <summary>
	/// The values, when they don't depend on another property.
	/// </summary>
	public IReadOnlyList<WeightedString>? Values { get; init; }

	/// <summary>
	/// The values for each value id of the <see cref="KeyedBy"/> property.
	/// </summary>
	public IReadOnlyDictionary<string, IReadOnlyList<WeightedString>>? ValuesByKey { get; init; }

	/// <summary>
	/// Gets the values to pick from, given the value id of the <see cref="KeyedBy"/> property (ignored if the values
	/// aren't keyed). Returns null if there are no values for the key.
	/// </summary>
	public IReadOnlyList<WeightedString>? GetValues(string? keyValueId)
	{
		if (KeyedBy is null)
			return Values;

		return keyValueId is not null && ValuesByKey is not null ? ValuesByKey.GetValueOrDefault(keyValueId) : null;
	}
}
