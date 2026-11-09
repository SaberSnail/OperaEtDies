using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Characters;

/// <summary>
/// Picks random values for every character property, using the settings for the given role. Any value can be
/// predefined by setting it, and locked so that randomizing leaves it alone. A string property whose defaults are
/// keyed by another property picks a new value when that property changes, unless it's locked.
/// </summary>
public sealed class CharacterRandomizer
{
	public CharacterRandomizer(GameData gameData, Random random, CharacterRole role)
	{
		Role = role;
		Properties = gameData.CharacterProperties;
		m_random = random;
		m_values = new Dictionary<string, RandomizedValue<object>>(StringComparer.Ordinal);

		// Validation makes sure a property is only keyed by properties defined before it, so those values already
		// exist when it picks its starting value.
		foreach (var property in Properties)
		{
			var value = new RandomizedValue<object>(() => PickRandom(property));
			m_values.Add(property.Id, value);

			if (property.Type == CharacterPropertyType.String && property.GetDefaults(role)?.KeyedBy is { } keyedBy)
				m_values[keyedBy].ValueChanged += (_, _) => value.Randomize();
		}
	}

	public CharacterRole Role { get; }

	public IReadOnlyList<CharacterProperty> Properties { get; }

	/// <summary>
	/// Gets the value of the property with the given id.
	/// </summary>
	public RandomizedValue<object> this[string propertyId] => m_values[propertyId];

	/// <summary>
	/// Gets the values a value list property can have for this role.
	/// </summary>
	public IReadOnlyList<CharacterPropertyOption> GetOptions(CharacterProperty property) => property.GetOptions(Role);

	/// <summary>
	/// Gets the range of an integer property for this role.
	/// </summary>
	public IntegerRange GetRange(CharacterProperty property) => property.GetRange(Role);

	/// <summary>
	/// Gets the default values a string property currently picks from, which may depend on the value of another
	/// property. Returns null if there are none.
	/// </summary>
	public IReadOnlyList<WeightedString>? GetDefaultValues(CharacterProperty property)
	{
		var defaults = property.GetDefaults(Role);
		var keyValue = defaults?.KeyedBy is { } keyedBy ? m_values[keyedBy].Value as CharacterPropertyOption : null;
		return defaults?.GetValues(keyValue?.Id) is { Count: > 0 } values ? values : null;
	}

	/// <summary>
	/// Picks new random values for every property that isn't locked.
	/// </summary>
	public void RandomizeAll()
	{
		foreach (var property in Properties)
			m_values[property.Id].Randomize();
	}

	/// <summary>
	/// Gets the current values of every property.
	/// </summary>
	public CharacterTraits GetTraits() => new(m_values.ToDictionary(x => x.Key, x => x.Value.Value, StringComparer.Ordinal));

	private object PickRandom(CharacterProperty property) => property.Type switch
	{
		CharacterPropertyType.String => GetDefaultValues(property) is { } values ? m_random.NextWeighted(values).Value : "",
		CharacterPropertyType.Integer => PickInteger(GetRange(property)),
		CharacterPropertyType.ValueList => m_random.NextWeighted(GetOptions(property)),
		_ => throw new InvalidOperationException($"Unknown property type {property.Type}."),
	};

	/// <summary>
	/// Combines two rolls so values at the favored part of the range are common and the rest grow increasingly rare.
	/// </summary>
	private int PickInteger(IntegerRange range)
	{
		var count = range.Maximum - range.Minimum + 1;
		var first = m_random.Next(count);
		var second = m_random.Next(count);
		var offset = range.Shape switch
		{
			IntegerShape.Low => Math.Min(first, second),
			IntegerShape.High => Math.Max(first, second),
			IntegerShape.Middle => (first + second + m_random.Next(2)) / 2,
			_ => throw new InvalidOperationException($"Unknown integer shape {range.Shape}."),
		};
		return range.Minimum + offset;
	}

	private readonly Random m_random;
	private readonly Dictionary<string, RandomizedValue<object>> m_values;
}
