using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// A person in the game world, including the player.
/// </summary>
public sealed class PersonViewModel : ViewModelBase
{
	/// <summary>
	/// Creates a person who starts at home. Without a workplace, the person works at home.
	/// </summary>
	public PersonViewModel(GameData gameData, CharacterTraits traits, LocationViewModel home, bool isPlayer, LocationViewModel? workplace = null)
	{
		IsPlayer = isPlayer;
		Traits = traits;
		FullName = traits.GetFullName(gameData.CharacterTemplates);
		Description = traits.GetDescription(gameData.CharacterTemplates);
		Home = home;
		Workplace = workplace ?? home;

		m_location = home;
		home.AddPerson(this);
	}

	public bool IsPlayer { get; }

	/// <summary>
	/// The values of the character properties defined in the data files.
	/// </summary>
	public CharacterTraits Traits { get; }

	public string FullName { get; }

	/// <summary>
	/// Describes the person in a few sentences.
	/// </summary>
	public string Description { get; }

	/// <summary>
	/// The house the person lives in.
	/// </summary>
	public LocationViewModel Home { get; }

	/// <summary>
	/// Where the person works: their business's place, or their home if the business is run from home.
	/// </summary>
	public LocationViewModel Workplace { get; }

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

	private LocationViewModel m_location;
}
