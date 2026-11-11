using System.Globalization;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// Finds character property values that are too unlikely to be chosen, so the data can be adjusted.
/// </summary>
public static class CharacterChances
{
	/// <summary>
	/// Gets a warning for each value that has the given percentage chance or less of being chosen, for each role. A
	/// value list value is checked in every combination of the values its conditions use, and is reported with its
	/// lowest chance. An integer property's values are reported together. The range of an integer property for a role comes
	/// from <paramref name="getRange"/> if given.
	/// </summary>
	public static IReadOnlyList<string> GetLowChanceWarnings(IReadOnlyList<CharacterProperty> properties, FrequencyChances frequencyChances, double threshold, Func<CharacterProperty, CharacterRole, IntegerRange>? getRange = null)
	{
		var warnings = new List<string>();
		var propertiesById = properties.ToDictionary(x => x.Id, StringComparer.Ordinal);
		foreach (var role in CharacterData.Roles)
		{
			var roleName = CharacterData.GetRoleName(role);

			// The lowest chance of each value list value, and the condition values that give it. Validation makes sure
			// there aren't too many combinations to check.
			var lowest = new Dictionary<(string PropertyId, string ValueId), (double Chance, string Condition)>();
			foreach (var property in properties.Where(x => x.Type == CharacterPropertyType.ValueList))
			{
				var conditionProperties = CharacterData.GetConditionClosure(properties, property.GetConditionPropertyIds());
				foreach (var combination in CharacterData.GetConditionCombinations(conditionProperties, role) ?? [])
				{
					string? GetValueId(string id) => combination.GetValueOrDefault(id);
					if (property.HasValue(role, GetValueId))
					{
						var options = property.GetOptions(role, GetValueId);
						var chances = property.GetChances(options, frequencyChances, GetValueId);
						for (var index = 0; index < options.Count; index++)
						{
							var key = (property.Id, options[index].Id);
							if (!lowest.TryGetValue(key, out var current) || chances[index] < current.Chance)
								lowest[key] = (chances[index], DescribeCondition(conditionProperties, combination));
						}
					}
				}
			}

			foreach (var ((propertyId, valueId), (chance, condition)) in lowest.Where(x => x.Value.Chance <= threshold))
				warnings.Add(OurResources.LowChanceValue.FormatCurrentCulture(valueId, propertyId, FormatChance(chance), roleName, condition, FormatChance(threshold)));

			foreach (var property in properties.Where(x => x.Type == CharacterPropertyType.Integer))
			{
				var range = getRange?.Invoke(property, role) ?? property.GetRange(role);
				var chances = range.Shape.GetChances(range.Maximum - range.Minimum + 1);
				var lowIndexes = Enumerable.Range(0, chances.Count).Where(x => chances[x] <= threshold).ToList();
				if (lowIndexes.Count != 0)
				{
					var lowestIndex = lowIndexes.MinBy(x => chances[x]);
					warnings.Add(OurResources.LowChanceIntegers.FormatCurrentCulture(roleName, lowIndexes.Count, property.Id, FormatChance(threshold), range.Minimum + lowestIndex, FormatChance(chances[lowestIndex])));
				}
			}

			foreach (var property in properties.Where(x => x.Type == CharacterPropertyType.String))
			{
				foreach (var (values, condition) in GetDefaultLists(property, role, propertiesById))
				{
					var chances = frequencyChances.GetChances(values);
					for (var index = 0; index < values.Count; index++)
					{
						if (chances[index] <= threshold)
							warnings.Add(OurResources.LowChanceDefault.FormatCurrentCulture(values[index].Value, property.Id, FormatChance(chances[index]), roleName, condition, FormatChance(threshold)));
					}
				}
			}
		}

		return warnings;
	}

	/// <summary>
	/// Gets each list of defaults a string property can pick from for the role, with the condition that picks it.
	/// </summary>
	private static IEnumerable<(IReadOnlyList<WeightedString> Values, string Condition)> GetDefaultLists(CharacterProperty property, CharacterRole role, IReadOnlyDictionary<string, CharacterProperty> propertiesById)
	{
		var defaults = property.GetDefaults(role);
		if (defaults?.Values is { } values)
		{
			yield return (values, "");
		}
		else if (defaults?.KeyedBy is { } keyedBy && defaults.ValuesByKey is { } valuesByKey)
		{
			foreach (var option in propertiesById[keyedBy].GetOptions(role).Where(x => valuesByKey.ContainsKey(x.Id)))
				yield return (valuesByKey[option.Id], OurResources.LowChanceCondition.FormatCurrentCulture(OurResources.ConditionCombinationValue.FormatCurrentCulture(keyedBy, option.Id)));
		}
	}

	private static string DescribeCondition(IReadOnlyList<CharacterProperty> properties, IReadOnlyDictionary<string, string> combination) =>
		properties.Count == 0 ? "" : OurResources.LowChanceCondition.FormatCurrentCulture(CharacterData.DescribeCombination(properties, combination));

	private static string FormatChance(double chance) => chance.ToString("G3", CultureInfo.CurrentCulture);
}
