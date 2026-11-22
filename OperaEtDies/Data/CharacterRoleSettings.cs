namespace OperaEtDies.Data;

/// <summary>
/// Settings that replace a character property's settings for the player or for non-player characters.
/// Which property types allow each setting is marked on the setting.
/// </summary>
public sealed class CharacterRoleSettings
{
	/// <summary>
	/// For value list properties, the ids of the values allowed for this role.
	/// </summary>
	[AllowedFor(nameof(CharacterProperty.Type), CharacterPropertyType.ValueList, OnParent = true)]
	public IReadOnlyList<string>? Values { get; init; }

	/// <summary>
	/// For integer properties, the range used for this role.
	/// </summary>
	[AllowedFor(nameof(CharacterProperty.Type), CharacterPropertyType.Integer, OnParent = true)]
	public IntegerRange? Range { get; init; }

	/// <summary>
	/// For string properties, the default values used for this role.
	/// </summary>
	[AllowedFor(nameof(CharacterProperty.Type), CharacterPropertyType.String, OnParent = true)]
	public StringDefaults? Defaults { get; init; }

	/// <summary>
	/// Creates a copy with the given defaults.
	/// </summary>
	internal CharacterRoleSettings WithDefaults(StringDefaults? defaults) => new() { Values = Values, Range = Range, Defaults = defaults };

	/// <summary>
	/// Creates a copy that allows every value.
	/// </summary>
	internal CharacterRoleSettings WithoutValues() => new() { Range = Range, Defaults = Defaults };
}
