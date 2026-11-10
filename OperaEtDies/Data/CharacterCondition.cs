namespace OperaEtDies.Data;

/// <summary>
/// A condition on a character's properties. It's met when every property in <see cref="Values"/> has one of its
/// listed values, every property in <see cref="NotValues"/> has a value that isn't listed, and every property in
/// <see cref="Hidden"/> has no visible value. A property with no value meets neither <see cref="Values"/> nor
/// <see cref="NotValues"/>.
/// </summary>
public sealed class CharacterCondition
{
	/// <summary>
	/// The value ids allowed for each listed value list property.
	/// </summary>
	public IReadOnlyDictionary<string, IReadOnlyList<string>>? Values { get; init; }

	/// <summary>
	/// The value ids not allowed for each listed value list property.
	/// </summary>
	public IReadOnlyDictionary<string, IReadOnlyList<string>>? NotValues { get; init; }

	/// <summary>
	/// The ids of properties that must have no visible value, because they're obscured or have no value. Only
	/// template conditions can use this.
	/// </summary>
	public IReadOnlyList<string>? Hidden { get; init; }

	/// <summary>
	/// Gets the ids of the value list properties whose values the condition checks.
	/// </summary>
	public IEnumerable<string> ValuePropertyIds => (Values?.Keys ?? []).Concat(NotValues?.Keys ?? []).Distinct(StringComparer.Ordinal);

	/// <summary>
	/// Checks a condition on a character's own values, given a function that gets the value id of a value list
	/// property (or null if it has no value). A missing condition is always met.
	/// </summary>
	public static bool IsMet(CharacterCondition? condition, Func<string, string?> getValueId) =>
		IsMet(condition, getValueId, x => getValueId(x) is not null);

	/// <summary>
	/// Checks a condition on what can be seen of a character, given functions that get the visible value id of a
	/// value list property (or null if it has none) and whether any property has a visible value. A missing
	/// condition is always met.
	/// </summary>
	public static bool IsMet(CharacterCondition? condition, Func<string, string?> getValueId, Func<string, bool> hasVisibleValue) =>
		condition is null ||
		((condition.Values ?? s_empty).All(x => getValueId(x.Key) is { } valueId && x.Value.Contains(valueId, StringComparer.Ordinal)) &&
			(condition.NotValues ?? s_empty).All(x => getValueId(x.Key) is { } valueId && !x.Value.Contains(valueId, StringComparer.Ordinal)) &&
			(condition.Hidden ?? []).All(x => !hasVisibleValue(x)));

	private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> s_empty = new Dictionary<string, IReadOnlyList<string>>();
}
