using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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
public sealed class LocationScreenViewModel : ViewModelBase, INumberedChoices
{
	public LocationScreenViewModel(Location location, GameWorld world, TimeRunner timeRunner, Person? following = null, bool isDeveloper = false)
	{
		Location = location;
		m_world = world;
		m_timeRunner = timeRunner;
		m_isDeveloper = isDeveloper;
		m_following = following is not null && location.People.Contains(following) ? following : null;
		var calendar = world.GameData.Calendar;
		var exits = location.Exits.Where(x => x.Direction is not null).OrderBy(x => x.Direction).Concat(location.Exits.Where(x => x.Direction is null)).ToList();
		var connections = exits
			.Select((x, index) => new LocationConnectionViewModel(x, calendar.FormatOffsetFrom(world.Time, world.GetTravelTime(location, x.Location), TimeFormat.Long), OnTravelRequested,
				GetMarker(world, x.Location), Hotkeys.GetChoiceKeyText(index + 1)))
			.ToList();
		Paths = connections.Where(x => x.Direction is not null).ToList();
		Places = connections.Where(x => x.Direction is null).ToList();
		Connections = [.. Paths, .. Places];
		m_people = CreatePeople([]);
		m_steps = CreateSteps();
		m_waitAction = CreateWaitAction();
		UpdateDestination();

		// The location and world outlive the view model, so they're listened to weakly.
		CollectionChangedEventManager.AddHandler(location.People, OnPeopleChanged);
		PropertyChangedEventManager.AddHandler(world, OnWorldTimeChanged, nameof(GameWorld.Time));
		PropertyChangedEventManager.AddHandler(world.Player, OnPlayerDestinationChanged, nameof(Person.Destination));
	}

	/// <summary>
	/// Raised when the player asks to walk all the way to their destination.
	/// </summary>
	public event EventHandler? WalkToDestinationRequested;


	/// <summary>
	/// Waiting: until someone arrives or leaves, or the next part of the day, with the other times to wait until in its menu.
	/// </summary>
	public SplitActionViewModel WaitAction
	{
		get => VerifyAccess(m_waitAction);
		private set => SetPropertyField(value, ref m_waitAction);
	}

	/// <summary>
	/// The player can walk to their destination if they have one they can get to.
	/// </summary>
	public bool CanWalkToDestination() => m_world.GetPlayerWayToDestination() is not null;

	/// <summary>
	/// Walks the player all the way to their destination.
	/// </summary>
	public void WalkToDestination()
	{
		VerifyAccess();
		if (CanWalkToDestination())
			WalkToDestinationRequested.Raise(this);
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
	/// Raised when the player stops steps that have something left to take care of, so they're asked how to stop them.
	/// </summary>
	public event EventHandler<GenericEventArgs<StepSequence>>? StopRequested;

	/// <summary>
	/// Raised when the player asks to see what they can start, such as work or a chore.
	/// </summary>
	public event EventHandler? ChooseStartRequested;

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
		private set
		{
			SetPropertyField(value, ref m_steps);

			// When the step the player is on opens is one of the times to wait until.
			WaitAction = CreateWaitAction();
		}
	}

	/// <summary>
	/// How the last wait or action here went, or null if the player hasn't waited or done anything here.
	/// </summary>
	public string? WaitResult
	{
		get => VerifyAccess(m_waitResult);
		private set => SetPropertyField(value, ref m_waitResult);
	}

	/// <summary>
	/// Presses a numbered choice. The paths and places come first, in the order they're listed, so each location keeps its
	/// numbers, then the buttons of the player's steps take the numbers left.
	/// </summary>
	public bool ChooseNumber(int number)
	{
		VerifyAccess();
		var isChosen = false;
		if (number >= 1 && number <= Connections.Count)
		{
			Connections[number - 1].Travel();
			isChosen = true;
		}
		else if (Steps is { } steps)
		{
			isChosen = steps.ChooseNumber(number);
		}
		return isChosen;
	}

	/// <summary>
	/// Opens the menu of a numbered split button: an exit's (the way to the destination has one), or the player's steps'.
	/// </summary>
	public bool OpenChoiceMenu(int number)
	{
		VerifyAccess();
		return number >= 1 && number <= Connections.Count ? Connections[number - 1].Action.OpenMenu() : Steps is { } steps && steps.OpenChoiceMenu(number);
	}

	public bool CanExamine() => !m_isExamined;

	public void Examine()
	{
		VerifyAccess();
		IsExamined = true;
	}

	/// <summary>
	/// Asks to see what the player can start, such as work or a chore.
	/// </summary>
	public void ChooseStart()
	{
		VerifyAccess();
		ChooseStartRequested.Raise(this);
	}

	/// <summary>
	/// Waits here until someone arrives or leaves, or until the next part of the day starts. While following someone, waits
	/// until they start spending time on a different goal or leave, and goes with them if they leave.
	/// </summary>
	public void Wait()
	{
		VerifyAccess();
		m_timeRunner.Run(() => WaitAsync(null));
	}

	/// <summary>
	/// Waits here until a time, unless something stops it first, such as an appointment starting, someone asking to talk,
	/// or someone the player is following leaving, who they go with.
	/// </summary>
	public void WaitUntil(TimePoint until)
	{
		VerifyAccess();
		m_timeRunner.Run(() => WaitAsync(until));
	}

	private async Task WaitAsync(TimePoint? until)
	{
		var start = m_world.Time;
		var following = Following;
		var isChanged = await m_world.WaitForPeopleAsync(Location, following, until).ConfigureAwait(true);
		var calendar = m_world.GameData.Calendar;
		var waited = calendar.FormatOffsetFrom(start, m_world.Time - start, TimeFormat.Long);
		if (following is not null && following.Location != Location && m_world.Player.Location != Location)
			Followed.Raise(this, new GenericEventArgs<Location>(following.Location));
		else if (following is not null && following.Location != Location)
			WaitResult = ModestyWarning.Format(m_world.GetPlayerUncoveredSlotsAt(following.Location));
		else if (isChanged || m_world.Player.TalkRequest is not null || until is not null)
			WaitResult = OurResources.WaitResultChanged.FormatCurrentCulture(waited);
		else
			WaitResult = OurResources.WaitResultDayPart.FormatCurrentCulture(waited, GetDayPartAnnouncement(calendar.GetDayPart(m_world.Time)));

		// The player's steps can move on at the end of the wait without time passing, such as once an appointment starts.
		Steps = CreateSteps();
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
	private void DoAction(StepSequence sequence) => m_timeRunner.Run(() => DoActionAsync(sequence));

	private async Task DoActionAsync(StepSequence sequence)
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
			await m_world.DoPlayerStepAsync(sequence).ConfigureAwait(true);
			var spent = m_world.GameData.Calendar.FormatOffsetFrom(start, m_world.Time - start, TimeFormat.Long);
			var result = step.Player!.Result!;
			var text = m_world.Time == start ? OurResources.PlayerActionResultInstant.FormatCurrentCulture(result) :
				(step.Kind == StepKind.Do ? OurResources.PlayerActionResult : OurResources.PlayerActionResultTook).FormatCurrentCulture(result, spent);
			WaitResult = m_world.Player.Sequences.Contains(sequence) && sequence.NextText is { } next ? OurResources.PlayerActionResultReturn.FormatCurrentCulture(text, next) : text;
		}

		Steps = CreateSteps();
	}

	/// <summary>
	/// Gives up the steps the player is in the middle of, and says so, or if they have something left to take care of, asks
	/// the player how to stop them.
	/// </summary>
	private void StopSteps(StepSequence sequence)
	{
		if (m_world.GetPlayerStopOptions(sequence).HasChoice)
		{
			StopRequested.Raise(this, new GenericEventArgs<StepSequence>(sequence));
			return;
		}

		var activity = ActivityText.Fill(sequence.Need.GetActivity(sequence.IsDone ? null : sequence.Step, m_world.Player.Business?.Profession.Id), sequence);
		m_world.StopPlayerSteps(sequence);
		WaitResult = OurResources.PlayerStepsGaveUp.FormatCurrentCulture(PlayerStepsViewModel.LowerFirst(activity));
		Steps = CreateSteps();
	}

	/// <summary>
	/// Has the step the player is on carried out for them, or all of the steps, and tells them how long it took, or once
	/// that finishes what they started, that they finished it.
	/// </summary>
	private void DoSteps(StepSequence sequence, bool isAll) => m_timeRunner.Run(() => DoStepsAsync(sequence, isAll));

	private async Task DoStepsAsync(StepSequence sequence, bool isAll)
	{
		var activity = PlayerStepsViewModel.LowerFirst(ActivityText.Fill(sequence.Need.GetActivity(sequence.IsDone ? null : sequence.Step, m_world.Player.Business?.Profession.Id), sequence));
		var start = m_world.Time;
		if (isAll)
			await m_world.DoPlayerStepsAutomaticallyAsync(sequence).ConfigureAwait(true);
		else
			await m_world.DoPlayerStepAsync(sequence).ConfigureAwait(true);
		var spent = m_world.GameData.Calendar.FormatOffsetFrom(start, m_world.Time - start, TimeFormat.Long);
		var result = m_world.Player.Sequences.Contains(sequence) ? OurResources.PlayerStepDone.FormatCurrentCulture(spent) : OurResources.PlayerStepsDone.FormatCurrentCulture(activity, spent);
		Steps = CreateSteps();
		StepsCarriedOut.Raise(this, new GenericEventArgs<string>(result));
	}

	/// <summary>
	/// Has the player choose an option of the step they're on.
	/// </summary>
	private void Choose(StepSequence sequence, StepOption option) => m_timeRunner.Run(() => ChooseAsync(sequence, option));

	private async Task ChooseAsync(StepSequence sequence, StepOption option)
	{
		var start = m_world.Time;
		await m_world.ChoosePlayerOptionAsync(sequence, option).ConfigureAwait(true);
		Steps = CreateSteps();
		if (m_world.Time != start)
			StepsCarriedOut.Raise(this, new GenericEventArgs<string>(OurResources.PlayerStepDone.FormatCurrentCulture(m_world.GameData.Calendar.FormatOffsetFrom(start, m_world.Time - start, TimeFormat.Long))));
	}

	/// <summary>
	/// Switches the player to something else they're in the middle of.
	/// </summary>
	private void Switch(StepSequence sequence) =>
		m_timeRunner.Run(async () =>
		{
			m_world.FocusPlayerSteps(sequence);
			await m_world.UpdatePlayerStepsAsync().ConfigureAwait(true);
			Steps = CreateSteps();
		});

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
				: new PlayerStepActionViewModel(GetCarryWarning(other) is { } warning ? $"{other.Need.Title} ({warning})" : other.Need.Title, isEnabled: true, note: null, () => Switch(other))).ToList();
		var viewModel = new PlayerStepsViewModel(sequence, steps, player, m_isStepsExpanded, CreateStepAction(sequence), choices, others,
			() => DoSteps(sequence, isAll: false), () => DoSteps(sequence, isAll: true), () => StopSteps(sequence), m_world.CanDoPlayerStep(sequence), GetCarryWarning(sequence),
			firstNumber: Connections.Count + 1);
		viewModel.ExpandedChanged += (_, _) => m_isStepsExpanded = viewModel.IsExpanded;
		return viewModel;
	}

	/// <summary>
	/// Reminds the player of the bundles they've kept for steps to finish later, once they've carried them for a while, or
	/// gets null.
	/// </summary>
	private string? GetCarryWarning(StepSequence sequence) =>
		m_world.IsPlayerCarryingTooLong(sequence) && sequence.Bundles.Count != 0
			? OurResources.PlayerStepsCarryWarning.FormatCurrentCulture(TextList.Join(sequence.Bundles.Values.Select(x => PlayerStepsViewModel.LowerFirst(x.Title)).ToList()),
				m_world.GameData.Calendar.FormatClock(sequence.KeptSince!))
			: null;

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
		return new PlayerStepActionViewModel(minutes is > 0 ? PlayerActionText.FormatUntil(m_world, title, minutes.Value) : title, isEnabled: true, note: null, () => DoAction(sequence));
	}

	/// <summary>
	/// Creates waiting, with the other times to wait until in its menu: for so many minutes, until the next part of the day,
	/// until it's time to set off for the next appointment, and until the step the player is on opens.
	/// </summary>
	private SplitActionViewModel CreateWaitAction()
	{
		var calendar = m_world.GameData.Calendar;
		var alternatives = m_world.GetPlayerWaitChoices().Select(choice =>
		{
			var clock = calendar.FormatClock(choice.Until);
			var duration = calendar.FormatOffsetFrom(m_world.Time, choice.Until - m_world.Time, TimeFormat.Long);
			var text = choice.Reason switch
			{
				PlayerWaitReason.DayPart => OurResources.WaitUntilDayPart.FormatCurrentCulture(PlayerStepsViewModel.LowerFirst(choice.DayPart!.Title), duration),
				PlayerWaitReason.SetOff => OurResources.WaitUntilSetOff.FormatCurrentCulture(choice.Appointment!.GetOther(m_world.Player).FullName, duration),
				PlayerWaitReason.Opens => OurResources.WaitUntilOpens.FormatCurrentCulture(clock, PlayerStepsViewModel.LowerFirst(choice.Title!), duration),
				_ => OurResources.WaitUntilTime.FormatCurrentCulture(clock, duration),
			};
			return new MenuActionViewModel(text, () => WaitUntil(choice.Until));
		}).ToList();
		return new SplitActionViewModel(OurResources.Wait, Wait, alternatives: alternatives, toolTip: OurResources.WaitToolTip);
	}

	private void OnWorldTimeChanged(object? sender, PropertyChangedEventArgs e) => Steps = CreateSteps();

	/// <summary>
	/// Gets what marks a place as the player's home, the place they work (if it isn't their home), or the place of their
	/// next appointment, or null.
	/// </summary>
	private static ExitMarker? GetMarker(GameWorld world, Location location) =>
		location == world.Player.Home ? ExitMarker.Home :
		world.Player.Business is not null && location == world.Player.Workplace ? ExitMarker.Work :
		world.GetPlayerAppointments() is [var next, ..] && next.Place == location ? ExitMarker.Appointment :
		null;

	private void OnPeopleChanged(object? sender, NotifyCollectionChangedEventArgs e) => People = CreatePeople(m_people);

	private void OnPlayerDestinationChanged(object? sender, PropertyChangedEventArgs e) => UpdateDestination();

	/// <summary>
	/// Marks the way to the player's destination, with walking all the way there, and how long that takes, on the exit
	/// that's the quickest way there.
	/// </summary>
	private void UpdateDestination()
	{
		var destination = m_world.Player.Destination;
		var way = m_world.GetPlayerWayToDestination();
		var time = destination is null ? null : m_world.GetPlayerTravelTimeTo(destination);
		var walkTime = time is { } offset ? m_world.GameData.Calendar.FormatOffsetFrom(m_world.Time, offset, TimeFormat.Long) : null;
		foreach (var connection in Connections)
		{
			var marker = destination is null ? (ExitMarker?) null :
				connection.Location == destination ? ExitMarker.Destination :
				connection.Location == way ? ExitMarker.TowardDestination :
				null;
			connection.SetDestination(marker, destination?.Title, walkTime, WalkToDestination);
		}
	}

	private void OnTravelRequested(Location location) => TravelRequested.Raise(this, new GenericEventArgs<Location>(location));

	private readonly GameWorld m_world;
	private readonly TimeRunner m_timeRunner;
	private readonly bool m_isDeveloper;
	private Person? m_following;
	private IReadOnlyList<LocationPersonViewModel> m_people;
	private PlayerStepsViewModel? m_steps;
	private bool m_isStepsExpanded;
	private string? m_waitResult;
	private SplitActionViewModel m_waitAction;
	private bool m_isExamined;
}
