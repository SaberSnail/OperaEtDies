namespace OperaEtDies.Data;

/// <summary>
/// Something that keeps some of a character's properties from being seen or heard, such as a mask, darkness, or
/// the character not having spoken yet.
/// </summary>
public sealed class CharacterObscurer : IIdentified
{
	/// <summary>
	/// The key in <see cref="Hides"/> that applies to every property.
	/// </summary>
	public const string AllProperties = "*";

	/// <summary>
	/// The lowest and highest prominence of a value, and strength of an obscurer.
	/// </summary>
	public const int MinStrength = 1;
	public const int MaxStrength = 3;

	public required string Id { get; init; }

	/// <summary>
	/// How strongly each property is hidden, keyed by property id (or <see cref="AllProperties"/>). A value is hidden
	/// when the strength is at least the value's prominence.
	/// </summary>
	public required IReadOnlyDictionary<string, int> Hides { get; init; }

	public static bool IsValidStrength(int strength) => strength is >= MinStrength and <= MaxStrength;

	/// <summary>
	/// Gets how strongly this hides a property, or 0 if it doesn't.
	/// </summary>
	public int GetStrength(string propertyId) =>
		Math.Max(Hides.GetValueOrDefault(propertyId), Hides.GetValueOrDefault(AllProperties));
}
