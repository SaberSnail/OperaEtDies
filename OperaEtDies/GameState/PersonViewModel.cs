using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// A person in the game world, including the player.
/// </summary>
public sealed class PersonViewModel : ViewModelBase
{
	public PersonViewModel(GameData gameData, CharacterTraits traits, LocationViewModel location, bool isPlayer)
	{
		IsPlayer = isPlayer;
		Traits = traits;
		FullName = traits.GetFullName(gameData.CharacterTemplates);
		Description = traits.GetDescription(gameData.CharacterTemplates);

		m_location = location;
		location.AddPerson(this);
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
