using System.Collections.Specialized;
using System.ComponentModel;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// Shows the location the player is in, with options to examine it, examine the people in it, wait there, and travel to
/// connected locations, and spend time on anything the player can do there, such as sleeping at home. The people in it are
/// kept up to date as people come and go, and how long each action would take as time passes. Developers also see
/// where each person is going and why.
/// </summary>
public sealed class LocationScreenViewModel : ViewModelBase
{
	public LocationScreenViewModel(Location location, GameWorld world, Person? following = null, bool isDeveloper = false)
	{
		Location = location;
		m_world = world;
		m_isDeveloper = isDeveloper;
		m_following = following is not null && location.People.Contains(following) ? following : null;
		var calendar = world.GameData.Calendar;
		var connections = location.Exits
			.Select(x => new LocationConnectionViewModel(x, calendar.FormatOffsetFrom(world.Time, world.GetTravelTime(location, x.Location), TimeFormat.Long), OnTravelRequested, GetMarker(world.Player, x.Location)))
			.ToList();
		Paths = connections.Where(x => x.Direction is not null).OrderBy(x => x.Direction).ToList();
		Places = connections.Where(x => x.Direction is null).ToList();
		Connections = [.. Paths, .. Places];
		m_people = CreatePeople([]);
		m_actionChoices = world.GetPlayerActions();
		m_actions = CreateActions();

		// The location and world outlive the view model, so they're listened to weakly.
		CollectionChangedEventManager.AddHandler(location.People, OnPeopleChanged);
		PropertyChangedEventManager.AddHandler(world, OnWorldTimeChanged, nameof(GameWorld.Time));
	}

	public event EventHandler<GenericEventArgs<Location>>? TravelRequested;

	/// <summary>
	/// Raised when the player followed someone out of the location while waiting, to where they went.
	/// </summary>
	public event EventHandler<GenericEventArgs<Location>>? Followed;

	/// <summary>
	/// Raised when the player examines someone here, to open a view focused on them.
	/// </summary>
	public event EventHandler<GenericEventArgs<Person>>? ExamineRequested;

	/// <summary>
	/// Raised when the player starts talking with someone here.
	/// </summary>
	public event EventHandler<GenericEventArgs<Person>>? TalkRequested;

	/// <summary>
	/// The person the player is following, or null if they aren't following anyone.
	/// </summary>
	public Person? Following => VerifyAccess(m_following);

	/// <summary>
	/// The location being shown.
	/// </summary>
	public Location Location { get; }

	public string Title => Location.Title;

	public string Description => Location.MediumDescription;

	/// <summary>
	/// The detailed description, which is only shown once the location has been examined.
	/// </summary>
	public IReadOnlyList<string> LongDescription => Location.LongDescription;

	public bool IsExamined
	{
		get => VerifyAccess(m_isExamined);
		private set => SetPropertyField(value, ref m_isExamined);
	}

	/// <summary>
	/// Everywhere the player can go from here: the paths, then the places.
	/// </summary>
	public IReadOnlyList<LocationConnectionViewModel> Connections { get; }

	/// <summary>
	/// The paths to other quarters, in compass order starting from north.
	/// </summary>
	public IReadOnlyList<LocationConnectionViewModel> Paths { get; }

	public bool HasPaths => Paths.Count != 0;

	/// <summary>
	/// The places that aren't reached by a path: the houses and businesses in a quarter, or the quarter a house or
	/// business is in.
	/// </summary>
	public IReadOnlyList<LocationConnectionViewModel> Places { get; }

	public bool HasPlaces => Places.Count != 0;

	/// <summary>
	/// The heading for the places: the places in this quarter, or where to go from a house or business.
	/// </summary>
	public string PlacesHeader => HasPaths ? OurResources.PlacesHeader : OurResources.ConnectionsHeader;

	/// <summary>
	/// The people in the location, other than the player.
	/// </summary>
	public IReadOnlyList<LocationPersonViewModel> People
	{
		get => VerifyAccess(m_people);
		private set => SetPropertyField([nameof(People), nameof(HasPeople)], value, ref m_people);
	}

	public bool HasPeople => People.Count != 0;

	/// <summary>
	/// What the player can spend time on here, such as sleeping at home or working at their workplace.
	/// </summary>
	public IReadOnlyList<PlayerActionViewModel> Actions
	{
		get => VerifyAccess(m_actions);
		private set => SetPropertyField([nameof(Actions), nameof(HasActions)], value, ref m_actions);
	}

	public bool HasActions => Actions.Count != 0;

	/// <summary>
	/// How the last wait or action here went, or null if the player hasn't waited or done anything here.
	/// </summary>
	public string? WaitResult
	{
		get => VerifyAccess(m_waitResult);
		private set => SetPropertyField(value, ref m_waitResult);
	}

	public bool CanExamine() => !m_isExamined;

	public void Examine()
	{
		VerifyAccess();
		IsExamined = true;
	}

	/// <summary>
	/// Waits here until someone arrives or leaves, or until the next part of the day starts. While following someone, waits
	/// until they start spending time on a different goal or leave, and goes with them if they leave.
	/// </summary>
	public void Wait()
	{
		VerifyAccess();
		var start = m_world.Time;
		var following = m_following;
		var isChanged = m_world.WaitForPeople(Location, following);
		var calendar = m_world.GameData.Calendar;
		var waited = calendar.FormatOffsetFrom(start, m_world.Time - start, TimeFormat.Long);
		if (following is not null && following.Location != Location)
			Followed.Raise(this, new GenericEventArgs<Location>(following.Location));
		else if (isChanged || m_world.Player.TalkRequest is not null)
			WaitResult = OurResources.WaitResultChanged.FormatCurrentCulture(waited);
		else
			WaitResult = OurResources.WaitResultDayPart.FormatCurrentCulture(waited, GetDayPartAnnouncement(calendar.GetDayPart(m_world.Time)));
	}

	/// <summary>
	/// Describes the start of a part of the day, with its announcement if it has one.
	/// </summary>
	private static string GetDayPartAnnouncement(CalendarDayPart dayPart) =>
		dayPart.Announcement?.Text ?? OurResources.DayPartStarted.FormatCurrentCulture(dayPart.Title);

	/// <summary>
	/// Shows how a wait went, such as after following someone here.
	/// </summary>
	internal void ShowWaitResult(string result) => WaitResult = result;

	/// <summary>
	/// Starts following someone here, or stops following them.
	/// </summary>
	private void ToggleFollow(Person person)
	{
		m_following = m_following == person ? null : person;
		foreach (var viewModel in m_people)
			viewModel.IsFollowed = viewModel.Person == m_following;
		RaisePropertyChanged(nameof(Following));
	}

	/// <summary>
	/// Lists the people in the location other than the player, keeping the view models of anyone already listed so
	/// they stay examined.
	/// </summary>
	private IReadOnlyList<LocationPersonViewModel> CreatePeople(IReadOnlyList<LocationPersonViewModel> existing) =>
		Location.People.Where(x => !x.IsPlayer)
			.Select(x => existing.FirstOrDefault(person => person.Person == x) ?? CreatePerson(x))
			.ToList();

	private LocationPersonViewModel CreatePerson(Person person) =>
		new(person, m_world.GameData.PortraitRenderer, m_world.GameData.Schedules, m_world.GameData.Relationships, toggleFollow: ToggleFollow,
			developerCalendar: m_isDeveloper ? m_world.GameData.Calendar : null, examine: x => ExamineRequested.Raise(this, new GenericEventArgs<Person>(x)),
			talk: x => TalkRequested.Raise(this, new GenericEventArgs<Person>(x))) { IsFollowed = person == m_following };

	/// <summary>
	/// Spends time on a need here, through its player action, and shows how long it took. After the part of a trip at
	/// its place, the player is told to finish it at home.
	/// </summary>
	private void DoAction(PlayerActionChoice choice, int minutes)
	{
		var need = choice.Need;
		var action = need.PlayerAction!;
		var start = m_world.Time;
		if (need.IsTrips)
			m_world.DoPlayerTrip(need, choice.IsHome);
		else
			m_world.DoPlayerAction(need, minutes);

		var spent = m_world.GameData.Calendar.FormatOffsetFrom(start, m_world.Time - start, TimeFormat.Long);
		var result = choice.IsHome ? action.Home!.Result : action.Result;
		var text = minutes == 0 ? OurResources.PlayerActionResultInstant.FormatCurrentCulture(result) : OurResources.PlayerActionResult.FormatCurrentCulture(result, spent);
		WaitResult = !choice.IsHome && action.Return is { } returnText ? OurResources.PlayerActionResultReturn.FormatCurrentCulture(text, returnText) : text;

		// Finishing a trip at home can take no time, so the actions are brought up to date here too.
		Actions = CreateActions();
	}

	/// <summary>
	/// Creates the actions the player could do here when they arrived, other than chores that are done: a quota need with
	/// no time left to spend on it, or a trip that's been finished at home. The rest stay listed (though they may not be
	/// available right now).
	/// </summary>
	private IReadOnlyList<PlayerActionViewModel> CreateActions() => m_actionChoices
		.Where(x => x.IsHome ? m_world.Player.PendingTrip == x.Need : x.Need.Kind != NeedKind.Quota || m_world.GetPlayerRemainingMinutes(x.Need) > 0).Select(x => new PlayerActionViewModel(x, m_world, DoAction)).ToList();

	private void OnWorldTimeChanged(object? sender, PropertyChangedEventArgs e) => Actions = CreateActions();

	/// <summary>
	/// Gets the word marking a place as the player's home or the place they work (if it isn't their home), or null.
	/// </summary>
	private static string? GetMarker(Person player, Location location) =>
		location == player.Home ? OurResources.MapHomeMarker :
		player.Business is not null && location == player.Workplace ? OurResources.MapWorkMarker : null;

	private void OnPeopleChanged(object? sender, NotifyCollectionChangedEventArgs e) => People = CreatePeople(m_people);

	private void OnTravelRequested(Location location) => TravelRequested.Raise(this, new GenericEventArgs<Location>(location));

	private readonly GameWorld m_world;
	private readonly bool m_isDeveloper;
	private Person? m_following;
	private IReadOnlyList<LocationPersonViewModel> m_people;
	private readonly IReadOnlyList<PlayerActionChoice> m_actionChoices;
	private IReadOnlyList<PlayerActionViewModel> m_actions;
	private string? m_waitResult;
	private bool m_isExamined;
}
