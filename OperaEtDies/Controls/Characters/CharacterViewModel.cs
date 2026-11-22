using GoldenAnvil.Utility;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Time;

namespace OperaEtDies.Controls.Characters;

/// <summary>
/// A view focused on one person: their portrait, their description, and everything else shown about them, such as what
/// they're doing and how they're related to the player. The player sees their own relationships, and developers see
/// everyone's, with their scores, where the person is going and why, and who they've seen lately. It's shown on top of the view it was opened from,
/// which is shown again when it closes.
/// </summary>
public sealed class CharacterViewModel : ViewModelBase
{
	public CharacterViewModel(Person person, GameWorld world, bool isDeveloper)
	{
		var gameData = world.GameData;
		CanViewStatus = isDeveloper && !person.IsPlayer;
		Person = new LocationPersonViewModel(person, gameData.PortraitRenderer, gameData.Schedules, gameData.Relationships, developerCalendar: isDeveloper ? gameData.Calendar : null);

		// Relationships are listed from the closest down.
		var settings = gameData.Relationships;
		if (person.IsPlayer || isDeveloper)
		{
			Relationships = person.Relationships
				.OrderByDescending(x => x.Value.Score)
				.ThenBy(x => x.Key.FullName, StringComparer.CurrentCulture)
				.Select(x => new CharacterRelationshipViewModel(x.Key, x.Value, settings, isDeveloper))
				.ToList();
			IsRelationshipsDeveloperOnly = !person.IsPlayer;
		}

		// Developers see the last time the person saw each other person, from the most recent.
		if (isDeveloper)
		{
			Sightings = person.Sightings
				.OrderByDescending(x => x.Value.Time)
				.ThenBy(x => x.Key.FullName, StringComparer.CurrentCulture)
				.Select(x => FormatSighting(x.Key, x.Value, gameData.Schedules, gameData.Calendar))
				.ToList();
		}
	}

	public event EventHandler? CloseRequested;

	/// <summary>
	/// Raised when a developer asks to see the person's needs.
	/// </summary>
	public event EventHandler<GenericEventArgs<Person>>? StatusRequested;

	/// <summary>
	/// The person, with their name, portrait, what they're doing, and the rest of what's shown about them.
	/// </summary>
	public LocationPersonViewModel Person { get; }

	/// <summary>
	/// The person's relationships with everyone else, from the closest down, or null if they aren't shown: the player's are
	/// always shown, and everyone else's only to developers.
	/// </summary>
	public IReadOnlyList<CharacterRelationshipViewModel>? Relationships { get; }

	/// <summary>
	/// Whether the relationships are only shown because the player is a developer.
	/// </summary>
	public bool IsRelationshipsDeveloperOnly { get; }

	/// <summary>
	/// Whether the relationships are shown to everyone.
	/// </summary>
	public bool IsRelationshipsShown => Relationships is not null && !IsRelationshipsDeveloperOnly;

	/// <summary>
	/// For developers, the last time the person saw each other person, from the most recent, such as "Anna Smith: Washing
	/// at The Ford, 7:15", or null if they aren't shown.
	/// </summary>
	public IReadOnlyList<string>? Sightings { get; }

	/// <summary>
	/// Whether sightings are shown, but there are none.
	/// </summary>
	public bool IsSightingsEmpty => Sightings is { Count: 0 };

	/// <summary>
	/// Whether developers can see the person's needs, for anyone but the player, whose needs are always on the status page.
	/// </summary>
	public bool CanViewStatus { get; }

	/// <summary>
	/// Shows the person's needs, for developers.
	/// </summary>
	public void ViewStatus()
	{
		VerifyAccess();
		if (CanViewStatus)
			StatusRequested.Raise(this, new GenericEventArgs<Person>(Person.Person));
	}

	public void Close()
	{
		VerifyAccess();
		CloseRequested.Raise(this);
	}

	/// <summary>
	/// Describes a sighting: who, what they were doing and where, and when, and who told the person about it if they didn't
	/// see it themselves.
	/// </summary>
	internal static string FormatSighting(Person other, Sighting sighting, ScheduleSettings schedules, GameCalendar calendar)
	{
		var (name, place, time) = (other.FullName, sighting.Location.Title, calendar.FormatClock(sighting.Time));
		var text = sighting.Activity switch
		{
			SightingActivity.Busy when sighting.Need is { } need => OurResources.SightingDoing.FormatCurrentCulture(name,
				sighting.Doing ?? need.GetActivityAt(x => Scheduler.IsPlaceFor(other, null, x, sighting.Location), other.Business?.Profession.Id), place, time),
			SightingActivity.Idle => OurResources.SightingDoing.FormatCurrentCulture(name, schedules.Idle.Activity ?? OurResources.SightingIdle, place, time),
			SightingActivity.Talking => OurResources.SightingDoing.FormatCurrentCulture(name, OurResources.SightingTalking, place, time),
			SightingActivity.Walking => OurResources.SightingWalking.FormatCurrentCulture(name, place, sighting.Destination?.Title, time),
			SightingActivity.Left => OurResources.SightingLeft.FormatCurrentCulture(name, place, sighting.Destination?.Title, time),
			_ => OurResources.SightingThere.FormatCurrentCulture(name, place, time),
		};
		return sighting.Source is { } source ? OurResources.SightingToldBy.FormatCurrentCulture(text, source.FullName) : text;
	}
}

/// <summary>
/// The person's relationship with someone else: who, and what it's called. Developers also see its score, and can change it.
/// </summary>
public sealed class CharacterRelationshipViewModel : ViewModelBase
{
	public CharacterRelationshipViewModel(Person other, Relationship relationship, RelationshipSettings settings, bool isDeveloper)
	{
		Name = other.FullName;
		IsDeveloper = isDeveloper;
		m_relationship = relationship;
		m_settings = settings;
	}

	/// <summary>
	/// The other person's name.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Whether the score is shown, and can be changed.
	/// </summary>
	public bool IsDeveloper { get; }

	/// <summary>
	/// What the relationship is called, such as "Friend".
	/// </summary>
	public string Level => m_settings.GetLevel(m_relationship.Score).Title;

	/// <summary>
	/// Whether the two are friends.
	/// </summary>
	public bool IsFriend => m_settings.IsFriend(m_relationship.Score);

	/// <summary>
	/// The relationship's score, from 0 to <see cref="RelationshipSettings.MaximumScore"/>, which developers can change.
	/// </summary>
	public int Score
	{
		get => VerifyAccess(m_relationship.Score);
		set
		{
			VerifyAccess();
			var score = Math.Clamp(value, 0, RelationshipSettings.MaximumScore);
			if (IsDeveloper && score != m_relationship.Score)
			{
				m_relationship.Score = score;
				RaisePropertyChanged(nameof(Score), nameof(Level), nameof(IsFriend));
			}
		}
	}

	private readonly Relationship m_relationship;
	private readonly RelationshipSettings m_settings;
}
