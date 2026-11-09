using OperaEtDies.Data;

namespace OperaEtDies.Characters;

/// <summary>
/// The traits that describe who a character is, used to create a person in the game world.
/// </summary>
public sealed record CharacterTraits(
	Gender Gender,
	string FirstName,
	string LastName,
	Profession Profession,
	CharacterOption Height,
	CharacterOption Weight,
	int Age)
{
	public string FullName => $"{FirstName} {LastName}";
}
