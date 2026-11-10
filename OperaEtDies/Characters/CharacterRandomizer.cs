using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Characters;

/// <summary>
/// Picks random values for every character property, using the settings for the given role. Any value can be
/// predefined by setting it, and locked so that randomizing leaves it alone. A string property whose defaults are
/// keyed by another property picks a new value when that property changes, unless it's locked. A property whose
/// condition isn't met has a null value, and a value list property picks a new value when its current value's
/// condition stops being met.
/// </summary>
public sealed class CharacterRandomizer
{
	public CharacterRandomizer(GameData gameData, Random random, CharacterRole role)
	{
		Role = role;
		Properties = gameData.CharacterProperties;
		m_random = random;
		m_frequencyChances = gameData.FrequencyChances;
		m_propertiesById = gameData.CharacterProperties.ToDictionary(x => x.Id, StringComparer.Ordinal);
		m_values = new Dictionary<string, RandomizedValue<object?>>(StringComparer.Ordinal);
		m_propertiesInDependencyOrder = CharacterDependencies.GetOrder(Properties);

		// Properties are created in dependency order, so the values a property is keyed by or conditioned on already
		// exist when it picks its starting value. Validation makes sure there are no cycles.
		foreach (var property in m_propertiesInDependencyOrder)
		{
			var value = new RandomizedValue<object?>(() => PickRandom(property));
			m_values.Add(property.Id, value);

			if (property.Type == CharacterPropertyType.String && property.GetDefaults(role)?.KeyedBy is { } keyedBy)
				m_values[keyedBy].ValueChanged += (_, _) => value.Randomize();

			foreach (var conditionId in property.GetConditionPropertyIds())
				m_values[conditionId].ValueChanged += (_, _) => UpdateForConditions(property);
		}
	}

	public CharacterRole Role { get; }

	public IReadOnlyList<CharacterProperty> Properties { get; }

	/// <summary>
	/// Gets the value of the property with the given id. The value is null if the property's condition isn't met.
	/// </summary>
	public RandomizedValue<object?> this[string propertyId] => m_values[propertyId];

	/// <summary>
	/// Whether the property has a value, given the current values of the properties its conditions use.
	/// </summary>
	public bool HasValue(CharacterProperty property) => property.HasValue(Role, GetValueId);

	/// <summary>
	/// Gets the values a value list property can have for this role, given the current values of the properties
	/// its values' conditions use.
	/// </summary>
	public IReadOnlyList<CharacterPropertyOption> GetOptions(CharacterProperty property) => property.GetOptions(Role, GetValueId);

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
		var keyValueId = defaults?.KeyedBy is { } keyedBy ? GetValueId(keyedBy) : null;
		return defaults?.GetValues(keyValueId) is { Count: > 0 } values ? values : null;
	}

	/// <summary>
	/// Picks new random values for every property that isn't locked.
	/// </summary>
	public void RandomizeAll()
	{
		foreach (var property in m_propertiesInDependencyOrder)
			m_values[property.Id].Randomize();
	}

	/// <summary>
	/// Gets the current values of every property that has one.
	/// </summary>
	public CharacterTraits GetTraits() => new(m_values
		.Where(x => x.Value.Value is not null)
		.ToDictionary(x => x.Key, x => x.Value.Value!, StringComparer.Ordinal));

	private string? GetValueId(string propertyId) => m_propertiesById[propertyId].GetConditionValueId(m_values[propertyId].Value);

	/// <summary>
	/// Makes the value fit the current values of the properties the conditions use: no value if the property no
	/// longer applies, a new value if it starts applying, and a new value if its current value isn't allowed anymore.
	/// A value that still fits is kept.
	/// </summary>
	private void UpdateForConditions(CharacterProperty property)
	{
		var value = m_values[property.Id];
		if (!HasValue(property))
		{
			value.IsLocked = false;
			value.Value = null;
		}
		else if (value.Value is null || (value.Value is CharacterPropertyOption option && !GetOptions(property).Contains(option)))
		{
			value.Value = PickRandom(property);
		}
	}

	private object? PickRandom(CharacterProperty property)
	{
		object? value = null;
		if (HasValue(property))
		{
			value = property.Type switch
			{
				CharacterPropertyType.String => GetDefaultValues(property) is { } values ? m_frequencyChances.Choose(m_random, values).Value : "",
				CharacterPropertyType.Integer => PickInteger(GetRange(property)),
				CharacterPropertyType.ValueList => PickOption(property),
				_ => throw new InvalidOperationException($"Unknown property type {property.Type}."),
			};
		}

		return value;
	}

	private CharacterPropertyOption PickOption(CharacterProperty property)
	{
		var options = GetOptions(property);
		var chances = property.GetChances(options, m_frequencyChances, GetValueId);
		return options[m_random.NextWeighted(Enumerable.Range(0, options.Count).ToList(), x => chances[x])];
	}

	private int PickInteger(IntegerRange range)
	{
		var chances = range.Shape.GetChances(range.Maximum - range.Minimum + 1);
		return range.Minimum + m_random.NextWeighted(Enumerable.Range(0, chances.Count).ToList(), x => chances[x]);
	}

	private readonly Random m_random;
	private readonly FrequencyChances m_frequencyChances;
	private readonly Dictionary<string, CharacterProperty> m_propertiesById;
	private readonly IReadOnlyList<CharacterProperty> m_propertiesInDependencyOrder;
	private readonly Dictionary<string, RandomizedValue<object?>> m_values;
}
