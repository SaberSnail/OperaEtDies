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
	/// For keyed values, other values to pick from when the player wants something other than the usual value, such as
	/// general last names for a property whose usual value comes from the profession. They're also used for a key that
	/// has no values of its own.
	/// </summary>
	public IReadOnlyList<WeightedString>? OtherValues { get; init; }

	/// <summary>
	/// Whether <see cref="ValuesByKey"/> was filled in from the professions rather than read from the file.
	/// </summary>
	internal bool HasProfessionValuesByKey { get; private init; }

	/// <summary>
	/// Gets the values to pick from, given the value id of the <see cref="KeyedBy"/> property (ignored if the values
	/// aren't keyed). A key with no values of its own uses the other values. Returns null if there are no values for the
	/// key.
	/// </summary>
	public IReadOnlyList<WeightedString>? GetValues(string? keyValueId)
	{
		if (KeyedBy is null)
			return Values;

		return (keyValueId is not null && ValuesByKey is not null ? ValuesByKey.GetValueOrDefault(keyValueId) : null) ?? OtherValues;
	}

	/// <summary>
	/// Creates a copy with the given values for each key, taken from the professions.
	/// </summary>
	internal StringDefaults WithValuesByKey(IReadOnlyDictionary<string, IReadOnlyList<WeightedString>> valuesByKey) => new()
	{
		KeyedBy = KeyedBy,
		Values = Values,
		ValuesByKey = valuesByKey,
		OtherValues = OtherValues,
		HasProfessionValuesByKey = true,
	};
}
