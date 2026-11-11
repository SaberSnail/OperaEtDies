namespace OperaEtDies.Data;

/// <summary>
/// Settings that replace a character property's settings for the player or for non-player characters.
/// Each setting is only used by the property type it applies to.
/// </summary>
public sealed class CharacterRoleSettings
{
	/// <summary>
	/// For value list properties, the ids of the values allowed for this role.
	/// </summary>
	public IReadOnlyList<string>? Values { get; init; }

	/// <summary>
	/// For integer properties, the range used for this role.
	/// </summary>
	public IntegerRange? Range { get; init; }

	/// <summary>
	/// For string properties, the default values used for this role.
	/// </summary>
	public StringDefaults? Defaults { get; init; }

	/// <summary>
	/// Creates a copy with the given defaults.
	/// </summary>
	internal CharacterRoleSettings WithDefaults(StringDefaults? defaults) => new() { Values = Values, Range = Range, Defaults = defaults };
}
