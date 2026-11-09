using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// A person in the game world, including the player.
/// </summary>
public sealed class PersonViewModel : ViewModelBase
{
	public PersonViewModel(CharacterTraits traits, LocationViewModel location, bool isPlayer)
	{
		IsPlayer = isPlayer;
		Gender = traits.Gender;
		FirstName = traits.FirstName;
		LastName = traits.LastName;
		Profession = traits.Profession;
		Height = traits.Height;
		Weight = traits.Weight;
		Age = traits.Age;

		m_location = location;
		location.AddPerson(this);
	}

	public bool IsPlayer { get; }

	public Gender Gender { get; }

	public string FirstName { get; }

	public string LastName { get; }

	public string FullName => $"{FirstName} {LastName}";

	/// <summary>
	/// The profession's definition from the data files.
	/// </summary>
	public Profession Profession { get; }

	/// <summary>
	/// The height's definition from the data files.
	/// </summary>
	public CharacterOption Height { get; }

	/// <summary>
	/// The weight's definition from the data files.
	/// </summary>
	public CharacterOption Weight { get; }

	public int Age { get; }

	/// <summary>
	/// The location the person is in. Changing it moves the person between the locations' lists of people.
	/// </summary>
	public LocationViewModel Location
	{
		get => VerifyAccess(m_location);
		set
		{
			ArgumentNullException.ThrowIfNull(value);

			var oldLocation = m_location;
			if (SetPropertyField(value, ref m_location))
			{
				oldLocation.RemovePerson(this);
				value.AddPerson(this);
			}
		}
	}

	/// <summary>
	/// Describes the person in a few sentences.
	/// </summary>
	public string Description
	{
		get
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

	private LocationViewModel m_location;
}
