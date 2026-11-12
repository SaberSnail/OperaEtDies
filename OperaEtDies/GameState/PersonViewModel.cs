using System.Windows.Media;
using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// A person in the game world, including the player.
/// </summary>
public sealed class PersonViewModel : ViewModelBase
{
	/// <summary>
	/// Creates a person, who starts at home unless given another location. Without a workplace, the person works at home.
	/// The random numbers decide whether someone who works from home is working when they're there.
	/// </summary>
	public PersonViewModel(GameData gameData, CharacterTraits traits, LocationViewModel home, bool isPlayer, LocationViewModel? workplace = null, LocationViewModel? location = null, Random? random = null)
	{
		var families = gameData.Families;
		var atWorkProperty = gameData.CharacterProperties.Get(families.Properties.AtWork);
		m_gameData = gameData;
		m_random = random ?? Random.Shared;
		m_atWorkPropertyId = atWorkProperty.Id;
		m_atWork = atWorkProperty.Values!.First(x => x.Id == families.AtWork);
		m_notAtWork = atWorkProperty.Values!.First(x => x.Id == families.NotAtWork);
		m_portraits = [];

		IsPlayer = isPlayer;
		Profession = traits.GetValueId(families.Properties.Profession);
		Home = home;
		Workplace = workplace ?? home;
		m_location = location ?? home;
		m_location.AddPerson(this);

		m_isAtWork = GetIsAtWork(m_location);
		m_traits = traits.With(m_atWorkPropertyId, m_isAtWork ? m_atWork : m_notAtWork);
		FullName = traits.GetFullName(gameData.CharacterTemplates);
		m_description = m_traits.GetDescription(gameData.CharacterTemplates);
	}

	public bool IsPlayer { get; }

	/// <summary>
	/// The values of the character properties defined in the data files. The game keeps the at work property up to date.
	/// </summary>
	public CharacterTraits Traits => VerifyAccess(m_traits);

	public string FullName { get; }

	/// <summary>
	/// Describes the person in a few sentences.
	/// </summary>
	public string Description => VerifyAccess(m_description);

	/// <summary>
	/// The id of the person's profession, or null if they have none.
	/// </summary>
	public string? Profession { get; }

	/// <summary>
	/// Whether the person is working right now. Changing it sets the at work property in <see cref="Traits"/>.
	/// </summary>
	public bool IsAtWork
	{
		get => VerifyAccess(m_isAtWork);
		set
		{
			// The traits change first, so they're up to date when the changes are raised.
			VerifyAccess();
			if (value != m_isAtWork)
			{
				m_traits = m_traits.With(m_atWorkPropertyId, value ? m_atWork : m_notAtWork);
				m_description = m_traits.GetDescription(m_gameData.CharacterTemplates);
				SetPropertyField([nameof(IsAtWork), nameof(Traits), nameof(Description), nameof(PortraitPlace), nameof(Portrait)], value, ref m_isAtWork);
			}
		}
	}

	/// <summary>
	/// A picture of the person where they are, drawn the first time it's needed there, or null if there's no portrait
	/// data.
	/// </summary>
	public ImageSource? Portrait
	{
		get
		{
			VerifyAccess();
			ImageSource? portrait = null;
			if (m_gameData.PortraitRenderer is { } renderer)
			{
				var key = (m_isAtWork, PortraitPlace);
				if (!m_portraits.TryGetValue(key, out portrait))
				{
					portrait = renderer.Render(m_traits, key.PortraitPlace);
					m_portraits.Add(key, portrait);
				}
			}

			return portrait;
		}
	}

	/// <summary>
	/// Where the person is, for drawing their portrait: a business's place is the workplace of its profession, and a
	/// house is a home, unless it's the person's own home and they're working from it. Anywhere else is its kind of
	/// quarter.
	/// </summary>
	public PortraitPlace? PortraitPlace
	{
		get
		{
			var location = Location;
			return location.BusinessProfession is { } businessProfession ? Data.PortraitPlace.InWorkplace(businessProfession) :
				location.IsHouse ? (location == Workplace && m_isAtWork && Profession is { } profession ? Data.PortraitPlace.InWorkplace(profession) : Data.PortraitPlace.Home) :
				location.QuarterKind is { } quarterKind ? Data.PortraitPlace.InQuarter(quarterKind) : null;
		}
	}

	/// <summary>
	/// The house the person lives in.
	/// </summary>
	public LocationViewModel Home { get; }

	/// <summary>
	/// Where the person works: their business's place, or their home if the business is run from home.
	/// </summary>
	public LocationViewModel Workplace { get; }

	/// <summary>
	/// The location the person is in. Changing it moves the person between the locations' lists of people, and
	/// updates whether they're at work.
	/// </summary>
	public LocationViewModel Location
	{
		get => VerifyAccess(m_location);
		set
		{
			ArgumentNullException.ThrowIfNull(value);

			var oldLocation = m_location;
			if (SetPropertyField([nameof(Location), nameof(PortraitPlace), nameof(Portrait)], value, ref m_location))
			{
				oldLocation.RemovePerson(this);
				value.AddPerson(this);
				IsAtWork = GetIsAtWork(value);
			}
		}
	}

	/// <summary>
	/// Works out whether the person is at work when they arrive somewhere: always in their business's place, and half
	/// the time in their home if they have a profession they work at from home.
	/// </summary>
	// TODO: Use schedules to decide when people are working.
	private bool GetIsAtWork(LocationViewModel location) =>
		location == Workplace && (Workplace != Home || (Profession is not null && m_random.Next(2) == 0));

	private readonly GameData m_gameData;
	private readonly Random m_random;
	private readonly string m_atWorkPropertyId;
	private readonly CharacterPropertyOption m_atWork;
	private readonly CharacterPropertyOption m_notAtWork;
	private readonly Dictionary<(bool IsAtWork, PortraitPlace? PortraitPlace), ImageSource> m_portraits;
	private CharacterTraits m_traits;
	private string m_description;
	private bool m_isAtWork;
	private LocationViewModel m_location;
}
