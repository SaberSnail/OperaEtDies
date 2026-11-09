using OperaEtDies.Data;

namespace OperaEtDies.Characters;

/// <summary>
/// A character in the game world, other than the player.
/// </summary>
public sealed class Character
{
	public Character(CharacterTraits traits, Location location)
	{
		Traits = traits;
		Location = location;
	}

	public CharacterTraits Traits { get; }

	/// <summary>
	/// The location the character is in.
	/// </summary>
	public Location Location { get; set; }
}
