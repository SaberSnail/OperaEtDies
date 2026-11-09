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
	/// For value list properties, the values the property can have.
	/// </summary>
	public IReadOnlyList<CharacterPropertyOption>? Values { get; init; }

	/// <summary>
	/// For integer properties, the range used unless a role replaces it.
	/// </summary>
	public IntegerRange? Range { get; init; }

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
	/// Gets the range of an integer property for the role. Validation makes sure every integer property has one.
	/// </summary>
	public IntegerRange GetRange(CharacterRole role) =>
		GetRoleSettings(role)?.Range ?? Range ?? throw new InvalidOperationException($"The property \"{Id}\" has no range.");

	/// <summary>
	/// Gets the default values of a string property for the role, or null if it has none.
	/// </summary>
	public StringDefaults? GetDefaults(CharacterRole role) => GetRoleSettings(role)?.Defaults ?? Defaults;
}
