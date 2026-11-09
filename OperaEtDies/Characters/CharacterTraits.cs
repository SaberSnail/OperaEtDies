using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.Characters;

/// <summary>
/// The traits that describe who a character is.
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

	/// <summary>
	/// Describes the character in a few sentences.
	/// </summary>
	public string GetDescription()
	{
		var (noun, pronoun) = Gender switch
		{
			Gender.Male => (OurResources.GenderNounMale, OurResources.GenderPronounMale),
			Gender.Female => (OurResources.GenderNounFemale, OurResources.GenderPronounFemale),
			_ => throw new InvalidOperationException($"Unknown gender {Gender}."),
		};

		return OurResources.CharacterDescription.FormatCurrentCulture(FullName, noun, Age, pronoun, Height.Description, Weight.Description, Profession.Description);
	}
}
