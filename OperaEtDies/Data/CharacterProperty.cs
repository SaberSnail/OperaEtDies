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
	/// What the engine uses the property for, such as the gender, or null for none. The property with the profession role
	/// takes its values from the professions.
	/// </summary>
	public string? Role { get; init; }

	/// <summary>
	/// A condition on other properties that must be met for a character to have this property.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// The id of the property this one comes right after when properties are listed, such as a last name right after
	/// a first name. The other property is held back until this one can be placed too, so they stay together.
	/// </summary>
	public string? Follows { get; init; }

	/// <summary>
	/// Whether the game sets the property's value itself, such as the family generator setting who heads a household.
	/// It isn't shown on the new game screen.
	/// </summary>
	public bool SetByGame { get; init; }

	/// <summary>
	/// For value list properties, the values the property can have.
	/// </summary>
	[AllowedFor(nameof(Type), CharacterPropertyType.ValueList)]
	public IReadOnlyList<CharacterPropertyOption>? Values { get; init; }

	/// <summary>
	/// Whether <see cref="Values"/> were filled in from the professions rather than read from the data.
	/// </summary>
	internal bool HasSourceValues { get; private init; }

	/// <summary>
	/// For value list properties, weights the values by their order instead of by their frequencies.
	/// </summary>
	[AllowedFor(nameof(Type), CharacterPropertyType.ValueList)]
	public WeightShape? Shape { get; init; }

	/// <summary>
	/// For value list properties, whether the values are listed in alphabetical order of their titles when choosing
	/// one, instead of the order they're defined in. It doesn't change how values are chosen at random.
	/// </summary>
	[AllowedFor(nameof(Type), CharacterPropertyType.ValueList)]
	public bool SortByTitle { get; init; }

	/// <summary>
	/// For integer properties, the range used unless a role replaces it.
	/// </summary>
	[AllowedFor(nameof(Type), CharacterPropertyType.Integer)]
	public IntegerRange? Range { get; init; }

	/// <summary>
	/// For integer properties, named parts of the numbers that conditions can use like values.
	/// </summary>
	[AllowedFor(nameof(Type), CharacterPropertyType.Integer)]
	public IReadOnlyList<IntegerGroup>? Groups { get; init; }

	/// <summary>
	/// For string properties, the default values used unless a role replaces them.
	/// </summary>
	[AllowedFor(nameof(Type), CharacterPropertyType.String)]
	public StringDefaults? Defaults { get; init; }

	/// <summary>
	/// Settings that replace the ones above for the player.
	/// </summary>
	public CharacterRoleSettings? Player { get; init; }

	/// <summary>
	/// Settings that replace the ones above for non-player characters.
	/// </summary>
	public CharacterRoleSettings? Npc { get; init; }

	/// <summary>
	/// The ids of the clothing slots (regions of the body) the property shows on. Clothing hides a value when it covers
	/// every one of them at least as much as the value's prominence. Without any, clothing never hides it.
	/// </summary>
	public IReadOnlyList<string> Regions { get; init; } = [];

	/// <summary>
	/// Whether the property is about the form of the body, such as a paunch, which only clothing whose bulk hides form can
	/// hide.
	/// </summary>
	public bool Form { get; init; }

	/// <summary>
	/// Creates a copy of the property with the given values.
	/// </summary>
	internal CharacterProperty WithValues(IReadOnlyList<CharacterPropertyOption> values) => Copy(values, true, Defaults, Player, Npc);

	/// <summary>
	/// Creates a copy of the property that allows every value for the role.
	/// </summary>
	internal CharacterProperty WithAllValuesFor(CharacterRole role) => role == CharacterRole.Player
		? Copy(Values, HasSourceValues, Defaults, Player?.WithoutValues(), Npc)
		: Copy(Values, HasSourceValues, Defaults, Player, Npc?.WithoutValues());

	/// <summary>
	/// Creates a copy of a string property whose defaults are keyed by one of the given properties, with the values for
	/// each key taken from the professions' defaults for this property. Only defaults without their own values for each
	/// key are filled in. Returns the property itself if nothing changes.
	/// </summary>
	internal CharacterProperty WithProfessionDefaults(IReadOnlySet<string> professionPropertyIds, IReadOnlyList<Profession> professions)
	{
		StringDefaults? Fill(StringDefaults? defaults) =>
			defaults is { KeyedBy: { } keyedBy, ValuesByKey: null } && professionPropertyIds.Contains(keyedBy)
				? defaults.WithValuesByKey(professions
					.Where(x => x.Defaults?.ContainsKey(Id) ?? false)
					.GroupBy(x => x.Id, StringComparer.Ordinal)
					.ToDictionary(x => x.Key, x => x.First().Defaults![Id], StringComparer.Ordinal))
				: defaults;

		var defaults = Fill(Defaults);
		var playerDefaults = Fill(Player?.Defaults);
		var npcDefaults = Fill(Npc?.Defaults);
		return Type != CharacterPropertyType.String || (defaults == Defaults && playerDefaults == Player?.Defaults && npcDefaults == Npc?.Defaults)
			? this
			: Copy(Values, HasSourceValues, defaults, Player?.WithDefaults(playerDefaults), Npc?.WithDefaults(npcDefaults));
	}

	private CharacterProperty Copy(IReadOnlyList<CharacterPropertyOption>? values, bool hasSourceValues, StringDefaults? defaults, CharacterRoleSettings? player, CharacterRoleSettings? npc) => new()
	{
		Id = Id,
		Title = Title,
		Type = Type,
		When = When,
		Follows = Follows,
		SetByGame = SetByGame,
		Values = values,
		Role = Role,
		HasSourceValues = hasSourceValues,
		Shape = Shape,
		SortByTitle = SortByTitle,
		Range = Range,
		Groups = Groups,
		Defaults = defaults,
		Player = player,
		Npc = npc,
		Regions = Regions,
		Form = Form,
	};

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
		IReadOnlyList<CharacterPropertyOption> options = Values ?? [];
		if (role == CharacterRole.Player && options.Any(x => x is { IsPlayerAllowed: false }))
			options = options.Where(x => x is null || x.IsPlayerAllowed).ToList();
		if (GetRoleSettings(role)?.Values is { } allowedIds)
		{
			var allowed = new HashSet<string>(allowedIds, StringComparer.Ordinal);
			options = options.Where(x => allowed.Contains(x.Id)).ToList();
		}

		return options;
	}

	/// <summary>
	/// Gets the values a value list property can have for the role, given a function that gets the value id of other
	/// value list properties. Only values whose conditions are met are included.
	/// </summary>
	public IReadOnlyList<CharacterPropertyOption> GetOptions(CharacterRole role, Func<string, string?> getValueId) =>
		GetOptions(role).Where(x => CharacterCondition.IsMet(x.When, getValueId)).ToList();

	/// <summary>
	/// Gets the values a value list property can have for any role, given a function that gets the value id of other
	/// value list properties. Only values whose conditions are met are included.
	/// </summary>
	public IReadOnlyList<CharacterPropertyOption> GetAllOptions(Func<string, string?> getValueId) =>
		(Values ?? []).Where(x => CharacterCondition.IsMet(x.When, getValueId)).ToList();

	/// <summary>
	/// Gets the percentage chance of choosing each of the given values of a value list property: by the property's
	/// shape and the values' order if it has one, or else by the values' frequencies, which can depend on the values of
	/// other properties. Frequencies are shared as they would be among all of the property's values, so leaving some
	/// values out doesn't make the others of the same frequency more likely compared to the rest.
	/// </summary>
	public IReadOnlyList<double> GetChances(IReadOnlyList<CharacterPropertyOption> options, FrequencyChances frequencyChances, Func<string, string?> getValueId) =>
		Shape?.GetChances(options.Count) ?? frequencyChances.GetChances(options, x => x.Frequency, x => x.GetFrequency(getValueId), Values);

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
	/// Gets the ids of the values in a group of a value list property, or null if no value is in the group.
	/// </summary>
	public IReadOnlyList<string>? GetGroupValueIds(string group)
	{
		var ids = Type == CharacterPropertyType.ValueList ? (Values ?? []).Where(x => x?.Groups?.Contains(group, StringComparer.Ordinal) ?? false).Select(x => x.Id).ToList() : [];
		return ids.Count == 0 ? null : ids;
	}

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
	/// Gets the ids of the properties used by the conditions that decide whether this property has a value and which
	/// values it can have: its own condition and its values' conditions, but not the conditions that only change its
	/// values' frequencies.
	/// </summary>
	public IReadOnlyList<string> GetAvailabilityPropertyIds() =>
		(When?.ValuePropertyIds ?? [])
			.Concat((Values ?? []).Where(x => x?.When is not null).SelectMany(x => x.When!.ValuePropertyIds))
			.Distinct(StringComparer.Ordinal)
			.ToList();

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
