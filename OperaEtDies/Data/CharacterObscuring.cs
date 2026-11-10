namespace OperaEtDies.Data;

/// <summary>
/// The obscurers in effect when describing a character, which decide which property values can't be seen.
/// </summary>
public sealed class CharacterObscuring
{
	public CharacterObscuring(IEnumerable<CharacterObscurer> obscurers)
	{
		m_obscurers = obscurers.ToList();
	}

	/// <summary>
	/// Nothing is obscured.
	/// </summary>
	public static CharacterObscuring None { get; } = new([]);

	/// <summary>
	/// Gets how strongly a property is hidden: the strongest of the obscurers, or 0 if none hide it.
	/// </summary>
	public int GetStrength(string propertyId) => m_obscurers.Count == 0 ? 0 : m_obscurers.Max(x => x.GetStrength(propertyId));

	/// <summary>
	/// Whether a property's value is hidden. Value list values use their prominence; other values have the highest prominence.
	/// </summary>
	public bool Hides(string propertyId, object value) =>
		GetStrength(propertyId) >= ((value as CharacterPropertyOption)?.GetProminence() ?? CharacterObscurer.MaxStrength);

	private readonly IReadOnlyList<CharacterObscurer> m_obscurers;
}
