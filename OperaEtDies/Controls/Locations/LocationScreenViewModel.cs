using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Characters;
using OperaEtDies.Controls.Clothing;
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
			.Select(x => new LocationConnectionViewModel(x, calendar.FormatOffsetFrom(world.Time, world.GetTravelTime(location, x.Location), TimeFormat.Long), OnTravelRequested, GetMarker(world, x.Location)))
			.ToList();
		Paths = connections.Where(x => x.Direction is not null).OrderBy(x => x.Direction).ToList();
		Places = connections.Where(x => x.Direction is null).ToList();
		Connections = [.. Paths, .. Places];
		m_people = CreatePeople([]);
		m_steps = CreateSteps();

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
	/// Raised when the player's steps were carried out for them, with how it went, since it can take them somewhere else.
	/// </summary>
	public event EventHandler<GenericEventArgs<string>>? StepsCarriedOut;

	/// <summary>
	/// The person the player is following: someone they chose to follow, or someone whose steps they're taking part in,
	/// or null if they aren't following anyone.
	/// </summary>
	public Person? Following => VerifyAccess(m_following ?? m_world.GetPlayerLeader());

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
	/// The steps the player has left for what they're in the middle of, or null if they aren't in the middle of anything.
	/// </summary>
	public PlayerStepsViewModel? Steps
	{
		get => VerifyAccess(m_steps);
		private set => SetPropertyField(value, ref m_steps);
	}

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
		var following = Following;
		var isChanged = m_world.WaitForPeople(Location, following);
		var calendar = m_world.GameData.Calendar;
		var waited = calendar.FormatOffsetFrom(start, m_world.Time - start, TimeFormat.Long);
		if (following is not null && following.Location != Location && m_world.Player.Location != Location)
			Followed.Raise(this, new GenericEventArgs<Location>(following.Location));
		else if (following is not null && following.Location != Location)
			WaitResult = ModestyWarning.Format(m_world.GetPlayerUncoveredSlotsOutside());
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
	/// Brings what the player can do here up to date, such as after they answer whether to keep going.
	/// </summary>
	internal void Refresh() => Steps = CreateSteps();

	/// <summary>
	/// Starts following someone here, or stops following them.
	/// </summary>
	private void ToggleFollow(Person person)
	{
		m_following = m_following == person ? null : person;
		foreach (var viewModel in m_people)
			viewModel.IsFollowed = viewModel.Person == Following;
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
			talk: x => TalkRequested.Raise(this, new GenericEventArgs<Person>(x))) { IsFollowed = person == (m_following ?? m_world.GetPlayerLeader()) };

	/// <summary>
	/// Has the player press the button of the step they're on, which carries it out, and shows how long it took. When the
	/// player has more steps to do somewhere else, they're told what to do next.
	/// </summary>
	private void DoAction(StepSequence sequence)
	{
		var step = sequence.CurrentStep!;
		var start = m_world.Time;

		// The player changes clothes themselves, so doing something, such as working, mustn't leave them too uncovered.
		var warning = ModestyWarning.Format(m_world.GetPlayerStepUncoveredSlots(step));
		if (warning is not null)
		{
			WaitResult = warning;
		}
		else
		{
			m_world.DoPlayerStep(sequence);
			var spent = m_world.GameData.Calendar.FormatOffsetFrom(start, m_world.Time - start, TimeFormat.Long);
			var result = step.Player!.Result!;
			var text = m_world.Time == start ? OurResources.PlayerActionResultInstant.FormatCurrentCulture(result) : OurResources.PlayerActionResult.FormatCurrentCulture(result, spent);
			WaitResult = m_world.Player.Sequences.Contains(sequence) && sequence.NextText is { } next ? OurResources.PlayerActionResultReturn.FormatCurrentCulture(text, next) : text;
		}

		Steps = CreateSteps();
	}

	/// <summary>
	/// Gives up the steps the player is in the middle of, and says so.
	/// </summary>
	private void StopSteps(StepSequence sequence)
	{
		var activity = sequence.Need.GetActivity(sequence.IsDone ? null : sequence.Step, m_world.Player.Business?.Profession.Id);
		m_world.StopPlayerSteps(sequence);
		WaitResult = OurResources.PlayerStepsGaveUp.FormatCurrentCulture(PlayerStepsViewModel.LowerFirst(activity));
		Steps = CreateSteps();
	}

	/// <summary>
	/// Has the step the player is on carried out for them, or all of the steps, and tells them how long it took, or once
	/// that finishes what they started, that they finished it.
	/// </summary>
	private void DoSteps(StepSequence sequence, bool isAll)
	{
		var activity = PlayerStepsViewModel.LowerFirst(sequence.Need.GetActivity(sequence.IsDone ? null : sequence.Step, m_world.Player.Business?.Profession.Id));
		var start = m_world.Time;
		if (isAll)
			m_world.DoPlayerStepsAutomatically(sequence);
		else
			m_world.DoPlayerStep(sequence);
		var spent = m_world.GameData.Calendar.FormatOffsetFrom(start, m_world.Time - start, TimeFormat.Long);
		var result = m_world.Player.Sequences.Contains(sequence) ? OurResources.PlayerStepDone.FormatCurrentCulture(spent) : OurResources.PlayerStepsDone.FormatCurrentCulture(activity, spent);
		Steps = CreateSteps();
		StepsCarriedOut.Raise(this, new GenericEventArgs<string>(result));
	}

	/// <summary>
	/// Has the player choose an option of the step they're on.
	/// </summary>
	private void Choose(StepSequence sequence, StepOption option)
	{
		var start = m_world.Time;
		m_world.ChoosePlayerOption(sequence, option);
		Steps = CreateSteps();
		if (m_world.Time != start)
			StepsCarriedOut.Raise(this, new GenericEventArgs<string>(OurResources.PlayerStepDone.FormatCurrentCulture(m_world.GameData.Calendar.FormatOffsetFrom(start, m_world.Time - start, TimeFormat.Long))));
	}

	/// <summary>
	/// Switches the player to something else they're in the middle of.
	/// </summary>
	private void Switch(StepSequence sequence)
	{
		m_world.FocusPlayerSteps(sequence);
		m_world.UpdatePlayerSteps();
		Steps = CreateSteps();
	}

	/// <summary>
	/// Creates the steps of what the player is in the middle of, keeping whether they're expanded, or null if they aren't
	/// in the middle of anything, or are being asked whether to keep going.
	/// </summary>
	private PlayerStepsViewModel? CreateSteps()
	{
		var player = m_world.Player;
		if (player.Current is not { } sequence || m_world.IsAskingPlayerToKeepGoing(sequence))
			return null;

		var steps = m_world.GetPlayerSteps(sequence);
		var choices = m_world.GetPlayerOptions(sequence).Select(option => new PlayerStepActionViewModel(option.Player?.Title ?? "", isEnabled: true, note: null, () => Choose(sequence, option))).ToList();
		// Appointments they haven't set off for yet are shown, but can't be switched to until it's time.
		var others = player.Sequences.Where(x => x != sequence).Reverse()
			.Select(other => other is { HasSetOff: false, Appointment: { } later }
				? new PlayerStepActionViewModel(OurResources.PlayerStepsLater.FormatCurrentCulture(later.GetOther(player).FullName, AppointmentText.FormatWhen(m_world, later.Time)), isEnabled: false, note: null, () => { })
				: new PlayerStepActionViewModel(other.Need.Title, isEnabled: true, note: null, () => Switch(other))).ToList();
		var viewModel = new PlayerStepsViewModel(sequence, steps, player, m_isStepsExpanded, CreateStepAction(sequence), choices, others,
			() => DoSteps(sequence, isAll: false), () => DoSteps(sequence, isAll: true), () => StopSteps(sequence), m_world.CanDoPlayerStep(sequence));
		viewModel.ExpandedChanged += (_, _) => m_isStepsExpanded = viewModel.IsExpanded;
		return viewModel;
	}

	/// <summary>
	/// Creates the button of the step the player is on, if it has one: with how long it takes, for a step that takes time,
	/// or else, if it's outside its hours, when they are.
	/// </summary>
	private PlayerStepActionViewModel? CreateStepAction(StepSequence sequence)
	{
		if (sequence is not { CurrentStep: { IsPlayerAction: true } step })
			return null;

		var title = step.Player!.Title!;
		if (step.Hours is { } hours && !hours.Contains(m_world.GameData.Calendar.GetParts(m_world.Time).Hour))
		{
			var open = OurResources.PlayerStepOpenHours.FormatCurrentCulture(OurResources.GameClock.FormatCurrentCulture(hours.Start, 0), OurResources.GameClock.FormatCurrentCulture(hours.End % 24, 0));
			return new PlayerStepActionViewModel(title, isEnabled: false, open, () => { });
		}

		var minutes = step.Kind == StepKind.Do ? step.Minutes is { } amount ? (int) amount.GetAmount(sequence.Need.SharedByFamily ? m_world.Player.Family.Members.Count : 0) : sequence.MinutesLeft : null;
		return new PlayerStepActionViewModel(minutes is > 0 ? OurResources.PlayerActionOptionText.FormatCurrentCulture(title, PlayerActionText.FormatMinutes(m_world, minutes.Value)) : title,
			isEnabled: true, note: null, () => DoAction(sequence));
	}

	private void OnWorldTimeChanged(object? sender, PropertyChangedEventArgs e) => Steps = CreateSteps();

	/// <summary>
	/// Gets the word marking a place as the player's home, the place they work (if it isn't their home), or the place of
	/// their next appointment, or null.
	/// </summary>
	private static string? GetMarker(GameWorld world, Location location) =>
		location == world.Player.Home ? OurResources.MapHomeMarker :
		world.Player.Business is not null && location == world.Player.Workplace ? OurResources.MapWorkMarker :
		world.GetPlayerAppointments() is [var next, ..] && next.Place == location ? OurResources.MapAppointmentMarker :
		null;

	private void OnPeopleChanged(object? sender, NotifyCollectionChangedEventArgs e) => People = CreatePeople(m_people);

	private void OnTravelRequested(Location location) => TravelRequested.Raise(this, new GenericEventArgs<Location>(location));

	private readonly GameWorld m_world;
	private readonly bool m_isDeveloper;
	private Person? m_following;
	private IReadOnlyList<LocationPersonViewModel> m_people;
	private PlayerStepsViewModel? m_steps;
	private bool m_isStepsExpanded;
	private string? m_waitResult;
	private bool m_isExamined;
}
