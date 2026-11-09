namespace OperaEtDies.Data;

/// <summary>
/// The kind of value a character property holds.
/// </summary>
public enum CharacterPropertyType
{
	/// <summary>
	/// Text, which can have default values to pick from.
	/// </summary>
	String,

	/// <summary>
	/// A whole number within a range.
	/// </summary>
	Integer,

	/// <summary>
	/// One of a list of values defined in the data.
	/// </summary>
	ValueList,
}
