namespace OperaEtDies.Data;

/// <summary>
/// One of the properties that describe a character, such as a name or an age.
/// </summary>
public sealed class CharacterProperty : IIdentified
{
	public required string Id { get; init; }

	public required string Title { get; init; }

	public required CharacterPropertyType Type { get; init; }

	/// <summary>
	/// A condition on other properties that must be met for a character to have this property.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// For value list properties, the values the property can have.
	/// </summary>
	public IReadOnlyList<CharacterPropertyOption>? Values { get; init; }

	/// <summary>
	/// For value list properties, weights the values by their order instead of by their frequencies.
	/// </summary>
	public WeightShape? Shape { get; init; }

	/// <summary>
	/// For integer properties, the range used unless a role replaces it.
	/// </summary>
	public IntegerRange? Range { get; init; }

	/// <summary>
	/// For integer properties, named parts of the numbers that conditions can use like values.
	/// </summary>
	public IReadOnlyList<IntegerGroup>? Groups { get; init; }

	/// <summary>
	/// For string properties, the default values used unless a role replaces them.
	/// </summary>
	public StringDefaults? Defaults { get; init; }

	/// <summary>
	/// Settings that replace the ones above for the player.
	/// </summary>
	public CharacterRoleSettings? Player { get; init; }

	/// <summary>
	/// Settings that replace the ones above for non-player characters.
	/// </summary>
	public CharacterRoleSettings? Npc { get; init; }

	public CharacterRoleSettings? GetRoleSettings(CharacterRole role) => role switch
	{
		CharacterRole.Player => Player,
		CharacterRole.Npc => Npc,
		_ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
	};

	/// <summary>
	/// Gets the values a value list property can have for the role, in the order they were defined.
	/// </summary>
	public IReadOnlyList<CharacterPropertyOption> GetOptions(CharacterRole role)
	{
		var options = Values ?? [];
		if (GetRoleSettings(role)?.Values is not { } allowedIds)
			return options;

		var allowed = new HashSet<string>(allowedIds, StringComparer.Ordinal);
		return options.Where(x => allowed.Contains(x.Id)).ToList();
	}

	/// <summary>
	/// Gets the values a value list property can have for the role, given a function that gets the value id of other
	/// value list properties. Only values whose conditions are met are included.
	/// </summary>
	public IReadOnlyList<CharacterPropertyOption> GetOptions(CharacterRole role, Func<string, string?> getValueId) =>
		GetOptions(role).Where(x => CharacterCondition.IsMet(x.When, getValueId)).ToList();

	/// <summary>
	/// Gets the percentage chance of choosing each of the given values of a value list property: by the property's
	/// shape and the values' order if it has one, or else by the values' frequencies, which can depend on the values of
	/// other properties.
	/// </summary>
	public IReadOnlyList<double> GetChances(IReadOnlyList<CharacterPropertyOption> options, FrequencyChances frequencyChances, Func<string, string?> getValueId) =>
		Shape?.GetChances(options.Count) ?? frequencyChances.GetChances(options, x => x.Frequency, x => x.GetFrequency(getValueId));

	/// <summary>
	/// Gets the id that conditions see for one of this property's values: the value's id for a value list property, or the
	/// id of the group a number is in for an integer property. Returns null if there is none.
	/// </summary>
	public string? GetConditionValueId(object? value) => value switch
	{
		CharacterPropertyOption option => option.Id,
		int number => Groups?.FirstOrDefault(x => x.Contains(number))?.Id,
		_ => null,
	};

	/// <summary>
	/// Gets the ids conditions can use for this property's values: its value ids, or its group ids.
	/// </summary>
	public IReadOnlyList<string> GetConditionValueIds() =>
		Type == CharacterPropertyType.ValueList ? (Values ?? []).Where(x => x is not null).Select(x => x.Id).ToList() : (Groups ?? []).Where(x => x is not null).Select(x => x.Id).ToList();

	/// <summary>
	/// Checks whether a character has a value for this property, given a function that gets the value id of other
	/// value list properties. The property's condition must be met, and a value list property must have a value
	/// whose condition is met.
	/// </summary>
	public bool HasValue(CharacterRole role, Func<string, string?> getValueId) =>
		CharacterCondition.IsMet(When, getValueId) && (Type != CharacterPropertyType.ValueList || GetOptions(role, getValueId).Count != 0);

	/// <summary>
	/// Gets the ids of the properties used by the conditions of this property and its values, including the conditions
	/// that change its values' frequencies.
	/// </summary>
	public IReadOnlyList<string> GetConditionPropertyIds() =>
		(When?.ValuePropertyIds ?? [])
			.Concat((Values ?? []).Where(x => x?.When is not null).SelectMany(x => x.When!.ValuePropertyIds))
			.Concat((Values ?? []).Where(x => x?.FrequencyWhen is not null).SelectMany(x => x.FrequencyWhen!).Where(x => x?.When is not null).SelectMany(x => x.When.ValuePropertyIds))
			.Distinct(StringComparer.Ordinal)
			.ToList();

	/// <summary>
	/// Gets the range of an integer property for the role. Validation makes sure every integer property has one.
	/// </summary>
	public IntegerRange GetRange(CharacterRole role) =>
		GetRoleSettings(role)?.Range ?? Range ?? throw new InvalidOperationException($"The property \"{Id}\" has no range.");

	/// <summary>
	/// Gets the default values of a string property for the role, or null if it has none.
	/// </summary>
	public StringDefaults? GetDefaults(CharacterRole role) => GetRoleSettings(role)?.Defaults ?? Defaults;
}
