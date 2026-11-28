using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using OperaEtDies.Controls;
using OperaEtDies.Controls.Characters;
using OperaEtDies.Controls.Clothing;
using OperaEtDies.Controls.Conversation;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Controls.Log;
using OperaEtDies.Controls.Map;
using OperaEtDies.Controls.Start;
using OperaEtDies.Controls.Status;
using OperaEtDies.Controls.Villagers;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Settings;

namespace OperaEtDies.Controls.Game;

public sealed class GameScreenViewModel : ViewModelBase, IDisposable
{
	/// <summary>
	/// Shows a game. Time passes at the speed in the settings, or at once without them, as for tests.
	/// </summary>
	public GameScreenViewModel(GameWorld world, bool isDeveloper = false, SaveSettings? saveSettings = null, AppSettings? settings = null)
	{
		m_saveSettings = saveSettings;
		IsDeveloper = isDeveloper;
		World = world;
		TimeRunner = new TimeRunner(world, settings);
		CharacterPanel = new CharacterPanelViewModel(world, StartNeed);
		Concerns = new ConcernsViewModel(world, ChooseDestination, ReplaceDestination, StartNeed);
		m_currentLocation = CreateLocationScreen(world.Player.Location);
		m_sheets = [];
		Sheets = new ReadOnlyObservableCollection<ViewModelBase>(m_sheets);
		EventLog = new EventLogViewModel(world.EventLog, world.GameData.Calendar, OnLogPersonClicked, OnLogPlaceClicked, ToggleLog);
		World.PropertyChanged += OnWorldPropertyChanged;
		World.Player.PropertyChanged += OnPlayerPropertyChanged;
		World.AppointmentMissed += OnAppointmentMissed;
		World.StepFailed += OnStepFailed;
		TimeRunner.PropertyChanged += OnTimeRunnerPropertyChanged;
		UpdateTalkPrompt();
		UpdateAppointmentPrompt();
		UpdateRepeatPrompt();
		UpdateAskPrompt();
		UpdateStartPrompt();
		m_savedState = saveSettings is null ? null : GetSavedState(saveSettings);
	}

	/// <summary>
	/// Whether to show extra information and controls for developing the game.
	/// </summary>
	public bool IsDeveloper { get; }

	/// <summary>
	/// The state of the game world.
	/// </summary>
	public GameWorld World { get; }

	/// <summary>
	/// The left column, showing the state of the player's character.
	/// </summary>
	public CharacterPanelViewModel CharacterPanel { get; }

	/// <summary>
	/// The Concerns panel at the top of the right column, showing what the player should keep in mind.
	/// </summary>
	public ConcernsViewModel Concerns { get; }

	/// <summary>
	/// Runs what the player does that passes time. While it runs, the player can't do anything else but stop it.
	/// </summary>
	public TimeRunner TimeRunner { get; }

	/// <summary>
	/// What the player has seen happen, shown under Concerns in the right column. Clicking a person in it opens their
	/// sheet, clicking a place opens the map, and a button on its heading opens it in a sheet.
	/// </summary>
	public EventLogViewModel EventLog { get; }

	/// <summary>
	/// The screen showing the location the player is in.
	/// </summary>
	public LocationScreenViewModel CurrentLocation
	{
		get => VerifyAccess(m_currentLocation);
		private set => SetPropertyField(value, ref m_currentLocation);
	}

	/// <summary>
	/// What the player can start, while its sheet is open, or null.
	/// </summary>
	public StartScreenViewModel? StartScreen => VerifyAccess(m_sheets.OfType<StartScreenViewModel>().FirstOrDefault());

	/// <summary>
	/// The player's inventory or wardrobe, while its sheet is open, or null. Changing clothes takes time once the player
	/// closes it.
	/// </summary>
	public ClothingScreenViewModel? Clothing => VerifyAccess(m_sheets.OfType<ClothingScreenViewModel>().FirstOrDefault());

	/// <summary>
	/// What's shown on top of <see cref="CurrentLocation"/> in the center: the player being asked whether to keep going or
	/// something else, or null if nothing is. Someone asking the player to chat is only shown here if they aren't listed
	/// in the location, since it's otherwise shown in their line, as a conversation is (see
	/// <see cref="LocationScreenViewModel.Interaction"/>).
	/// </summary>
	public ViewModelBase? Overlay => VerifyAccess((m_isTalkPromptInLine ? null : (ViewModelBase?) m_talkPrompt) ?? (ViewModelBase?) m_askPrompt ?? (ViewModelBase?) m_startPrompt ?? (ViewModelBase?) m_appointmentPrompt ??
		(ViewModelBase?) m_noticePrompt ?? (ViewModelBase?) m_repeatPrompt ?? m_stopPrompt);

	/// <summary>
	/// The sheets drawn over the center, from the bottom up, each one an <see cref="ISheet"/>: detailed information such as a
	/// person, the villagers, the map, the player's status, or the full log, or what the player can start, or their
	/// inventory or wardrobe. Only the top one shows. Time is held still while any are open, and the center can't be used.
	/// </summary>
	public ReadOnlyObservableCollection<ViewModelBase> Sheets { get; }

	/// <summary>
	/// The sheet on top, the one that shows, or null if none are open.
	/// </summary>
	public ViewModelBase? TopSheet => VerifyAccess(m_sheets.Count == 0 ? null : m_sheets[^1]);

	/// <summary>
	/// Whether any sheets are open.
	/// </summary>
	public bool HasSheets => VerifyAccess(m_sheets.Count != 0);

	/// <summary>
	/// Whether the player's own character sheet is open, so its toolbar button looks pressed.
	/// </summary>
	public bool IsPlayerCharacterOpen => VerifyAccess(m_sheets.Any(IsPlayerCharacterSheet));

	/// <summary>
	/// Whether the villagers sheet is open.
	/// </summary>
	public bool IsVillagersOpen => VerifyAccess(m_sheets.Any(x => x is VillagersViewModel));

	/// <summary>
	/// Whether the map sheet is open.
	/// </summary>
	public bool IsMapOpen => VerifyAccess(m_sheets.Any(x => x is TownMapViewModel));

	/// <summary>
	/// Whether the player's status sheet is open.
	/// </summary>
	public bool IsStatusOpen => VerifyAccess(m_sheets.Any(IsPlayerStatusSheet));

	/// <summary>
	/// Whether the wardrobe sheet is open.
	/// </summary>
	public bool IsWardrobeOpen => VerifyAccess(Clothing is { IsWardrobe: true });

	/// <summary>
	/// Whether the inventory sheet is open.
	/// </summary>
	public bool IsInventoryOpen => VerifyAccess(Clothing is { IsWardrobe: false });

	/// <summary>
	/// How soon a second Esc after one that closed a sheet counts as pressing it twice, closing every sheet. It's the
	/// system's double-click time.
	/// </summary>
	public TimeSpan EscapeRepeatTime { get; set; } = TimeSpan.FromMilliseconds(NativeMethods.GetDoubleClickTime());

	/// <summary>
	/// A question someone asked the player as one of their steps, while the player is answering it, or null.
	/// </summary>
	public AskPromptViewModel? AskPrompt
	{
		get => VerifyAccess(m_askPrompt);
		private set => SetPropertyField([nameof(AskPrompt), nameof(Overlay)], value, ref m_askPrompt);
	}

	/// <summary>
	/// The question whether to start an appointment, while the other is asking the player, or null.
	/// </summary>
	public StartPromptViewModel? StartPrompt
	{
		get => VerifyAccess(m_startPrompt);
		private set => SetPropertyField([nameof(StartPrompt), nameof(Overlay)], value, ref m_startPrompt);
	}

	/// <summary>
	/// The reminder that it's time to set off for an appointment, while the player is being reminded, or null.
	/// </summary>
	public AppointmentPromptViewModel? AppointmentPrompt
	{
		get => VerifyAccess(m_appointmentPrompt);
		private set => SetPropertyField([nameof(AppointmentPrompt), nameof(Overlay)], value, ref m_appointmentPrompt);
	}

	/// <summary>
	/// Something that happened while time passed that the player is being told, such as that they missed an appointment, or
	/// null.
	/// </summary>
	public NoticePromptViewModel? NoticePrompt
	{
		get => VerifyAccess(m_noticePrompt);
		private set => SetPropertyField([nameof(NoticePrompt), nameof(Overlay)], value, ref m_noticePrompt);
	}

	/// <summary>
	/// Someone asking the player to chat, shown in their line in the location (or on top of it, if they aren't listed there)
	/// until the player answers, or null if no one is.
	/// </summary>
	public TalkPromptViewModel? TalkPrompt
	{
		get => VerifyAccess(m_talkPrompt);
		private set => SetPropertyField([nameof(TalkPrompt), nameof(Overlay)], value, ref m_talkPrompt);
	}

	/// <summary>
	/// The player being asked how to stop steps that have something left to take care of, shown on top of everything until
	/// they answer, or null if they aren't being asked.
	/// </summary>
	public StopPromptViewModel? StopPrompt
	{
		get => VerifyAccess(m_stopPrompt);
		private set => SetPropertyField([nameof(StopPrompt), nameof(Overlay)], value, ref m_stopPrompt);
	}

	/// <summary>
	/// The player being asked whether to keep going with what they're doing, shown on top of everything until they answer,
	/// or null if they aren't being asked.
	/// </summary>
	public RepeatPromptViewModel? RepeatPrompt
	{
		get => VerifyAccess(m_repeatPrompt);
		private set => SetPropertyField([nameof(RepeatPrompt), nameof(Overlay)], value, ref m_repeatPrompt);
	}

	/// <summary>
	/// The conversation the player is having with someone in their location, shown in their line, or null if there isn't one.
	/// </summary>
	public ConversationViewModel? Conversation
	{
		get => VerifyAccess(m_conversation);
		private set
		{
			SetPropertyField(value, ref m_conversation);
			UpdateInteraction();
		}
	}

	/// <summary>
	/// Starts a conversation with someone in the player's location, replacing any other.
	/// </summary>
	public void ShowConversation(Person person)
	{
		VerifyAccess();
		CloseConversation();
		var conversation = new ConversationViewModel(person, World, TimeRunner);
		conversation.CloseRequested += OnConversationCloseRequested;
		Conversation = conversation;
	}

	/// <summary>
	/// Ends the conversation, showing the player's location again.
	/// </summary>
	public void CloseConversation()
	{
		VerifyAccess();
		if (m_conversation is { } conversation)
		{
			conversation.CloseRequested -= OnConversationCloseRequested;
			Conversation = null;
		}
	}

	/// <summary>
	/// Opens the player's own character sheet from the toolbar, replacing any sheets, or closes it if it's on top.
	/// </summary>
	public void TogglePlayerCharacter() => ToggleSheet(IsPlayerCharacterSheet, () => CreateCharacter(World.Player));

	/// <summary>
	/// Opens the villagers sheet from the toolbar, replacing any sheets, or closes it if it's on top.
	/// </summary>
	public void ToggleVillagers() => ToggleSheet(x => x is VillagersViewModel, CreateVillagers);

	/// <summary>
	/// Opens the map sheet from the toolbar, replacing any sheets, or closes it if it's on top.
	/// </summary>
	public void ToggleMap() => ToggleSheet(x => x is TownMapViewModel, CreateMap);

	/// <summary>
	/// Opens the player's status sheet from the toolbar, replacing any sheets, or closes it if it's on top.
	/// </summary>
	public void ToggleStatus() => ToggleSheet(IsPlayerStatusSheet, () => CreateStatus(World.Player));

	/// <summary>
	/// Opens the log in a sheet, replacing any sheets, or closes it if it's on top.
	/// </summary>
	public void ToggleLog() => ToggleSheet(x => x is LogSheetViewModel, () => new LogSheetViewModel(EventLog));

	/// <summary>
	/// Opens the villagers sheet, replacing any sheets.
	/// </summary>
	public void ShowVillagers() => WhenStopped(() => ReplaceSheets(CreateVillagers()));

	/// <summary>
	/// Opens the map sheet, replacing any sheets.
	/// </summary>
	public void ShowMap() => WhenStopped(() => ReplaceSheets(CreateMap()));

	/// <summary>
	/// Opens the player's status sheet, replacing any sheets.
	/// </summary>
	public void ShowStatus() => WhenStopped(() => ReplaceSheets(CreateStatus(World.Player)));

	/// <summary>
	/// Opens a sheet focused on a person, on top of any other sheets.
	/// </summary>
	public void ShowCharacter(Person person)
	{
		VerifyAccess();
		WhenStopped(() => PushSheet(CreateCharacter(person)));
	}

	/// <summary>
	/// For developers, opens someone's needs in a sheet, on top of any other sheets. If any of their needs are changed, they
	/// choose a new goal once it closes.
	/// </summary>
	public void ShowCharacterStatus(Person person)
	{
		VerifyAccess();
		WhenStopped(() => PushSheet(CreateStatus(person)));
	}

	/// <summary>
	/// Closes the sheet on top, showing the one under it, if there is one, or else the center. The player's inventory or
	/// wardrobe stays open while they aren't dressed enough to leave it, still warning them.
	/// </summary>
	public void CloseTopSheet()
	{
		VerifyAccess();
		if (m_sheets.Count != 0 && CanCloseSheet(m_sheets[^1]))
		{
			RemoveTopSheet();
			OnSheetsChanged();
		}
	}

	/// <summary>
	/// Closes every sheet, returning to the center, down to the player's inventory or wardrobe if they aren't dressed enough
	/// to leave it.
	/// </summary>
	public void CloseSheets()
	{
		VerifyAccess();
		if (RemoveSheets())
			OnSheetsChanged();
	}

	/// <summary>
	/// The toolbar's button for returning to the center can only be used while a sheet is open.
	/// </summary>
	public bool CanCloseSheets() => HasSheets;

	/// <summary>
	/// Opens what the player can start in a sheet, replacing any sheets, unless the player isn't dressed enough to leave
	/// their inventory or wardrobe. If it's already open, the sheets over it close.
	/// </summary>
	public void ShowStart()
	{
		VerifyAccess();
		CloseConversation();
		if (StartScreen is { } start)
			CloseSheetsAbove(start);
		else
			TryReplaceSheets(() => new StartScreenViewModel(World));
	}

	/// <summary>
	/// Closes what the player can start, and any sheets over it.
	/// </summary>
	public void CloseStart()
	{
		VerifyAccess();
		if (StartScreen is { } start)
			CloseSheetsFrom(start);
	}

	/// <summary>
	/// Opens the player's inventory from the toolbar, or closes it if it's on top.
	/// </summary>
	public void ToggleInventory() => ToggleClothing(isWardrobe: false);

	/// <summary>
	/// Opens the player's wardrobe from the toolbar, or closes it if it's on top.
	/// </summary>
	public void ToggleWardrobe() => ToggleClothing(isWardrobe: true);

	/// <summary>
	/// The wardrobe can only be opened at home.
	/// </summary>
	public bool CanToggleWardrobe() => World.Player.Location == World.Player.Home;

	/// <summary>
	/// Opens the player's inventory or wardrobe in a sheet, replacing any sheets, including the other one, unless the
	/// player isn't dressed enough to leave it. If it's already open, the sheets over it close. Time stops first if it's
	/// passing. Changing clothes takes its time once the player closes it.
	/// </summary>
	public void ShowClothing(bool isWardrobe)
	{
		VerifyAccess();
		WhenStopped(() =>
		{
			CloseConversation();
			if (Clothing is { } clothing && clothing.IsWardrobe == isWardrobe)
				CloseSheetsAbove(clothing);

			// Stopping time can leave the player somewhere the wardrobe isn't.
			else if (!isWardrobe || CanToggleWardrobe())
				TryReplaceSheets(() => new ClothingScreenViewModel(World, isWardrobe, IsDeveloper));
		});
	}

	private void ToggleClothing(bool isWardrobe)
	{
		if (TopSheet is ClothingScreenViewModel clothing && clothing.IsWardrobe == isWardrobe)
			CloseTopSheet();
		else
			ShowClothing(isWardrobe);
	}

	/// <summary>
	/// Whether a sheet can be closed: any can but the player's inventory or wardrobe, while they aren't dressed enough to
	/// leave it.
	/// </summary>
	private static bool CanCloseSheet(ViewModelBase sheet) => sheet is not ClothingScreenViewModel clothing || clothing.CanClose();

	private void RemoveTopSheet()
	{
		var sheet = m_sheets[^1];
		m_sheets.RemoveAt(m_sheets.Count - 1);
		DetachSheet(sheet);
	}

	/// <summary>
	/// Removes the sheets from the top down, stopping at any that can't be closed, without telling anyone. Returns whether
	/// any were removed.
	/// </summary>
	private bool RemoveSheets()
	{
		var isRemoved = false;
		while (m_sheets.Count != 0 && CanCloseSheet(m_sheets[^1]))
		{
			RemoveTopSheet();
			isRemoved = true;
		}
		return isRemoved;
	}

	/// <summary>
	/// Closes the sheets over a sheet, as far as they can be closed.
	/// </summary>
	private void CloseSheetsAbove(ViewModelBase sheet)
	{
		var isRemoved = false;
		while (m_sheets.Count != 0 && m_sheets[^1] != sheet && CanCloseSheet(m_sheets[^1]))
		{
			RemoveTopSheet();
			isRemoved = true;
		}
		if (isRemoved)
			OnSheetsChanged();
	}

	/// <summary>
	/// Closes a sheet and the sheets over it, as far as they can be closed.
	/// </summary>
	private void CloseSheetsFrom(ViewModelBase sheet)
	{
		var isRemoved = false;
		while (m_sheets.Contains(sheet) && CanCloseSheet(m_sheets[^1]))
		{
			RemoveTopSheet();
			isRemoved = true;
		}
		if (isRemoved)
			OnSheetsChanged();
	}

	/// <summary>
	/// Does something once time has stopped: at once if it isn't passing, or else once it stops, after asking it to. Only
	/// the last thing asked for is done.
	/// </summary>
	public void WhenStopped(Action action)
	{
		VerifyAccess();
		if (TimeRunner.IsRunning)
		{
			m_whenStopped = action;
			TimeRunner.Stop();
		}
		else
		{
			action();
		}
	}

	/// <summary>
	/// Opens a sheet from the toolbar, replacing any sheets, or closes it if it's the one on top.
	/// </summary>
	private void ToggleSheet(Func<ViewModelBase, bool> isSheet, Func<ViewModelBase> create)
	{
		VerifyAccess();
		if (m_sheets.Count != 0 && isSheet(m_sheets[^1]))
			CloseTopSheet();
		else
			WhenStopped(() => ReplaceSheets(create()));
	}

	/// <summary>
	/// Replaces the sheets with a new one. If the player isn't dressed enough to leave their inventory or wardrobe, it stays
	/// open under the new sheet.
	/// </summary>
	private void ReplaceSheets(ViewModelBase sheet)
	{
		RemoveSheets();
		PushSheet(sheet);
	}

	/// <summary>
	/// Replaces the sheets with a new one, unless the player isn't dressed enough to leave their inventory or wardrobe, in
	/// which case only the sheets over it close.
	/// </summary>
	private void TryReplaceSheets(Func<ViewModelBase> create)
	{
		var isRemoved = RemoveSheets();
		if (m_sheets.Count == 0)
			PushSheet(create());
		else if (isRemoved)
			OnSheetsChanged();
	}

	private void PushSheet(ViewModelBase sheet)
	{
		AttachSheet(sheet);
		m_sheets.Add(sheet);
		OnSheetsChanged();
	}

	/// <summary>
	/// Holds time still while any sheets are open, tells the window what changed, and once they're all closed, finishes
	/// changing clothes and carries on with steps given up while they were open.
	/// </summary>
	private void OnSheetsChanged()
	{
		TimeRunner.IsHeld = m_sheets.Count != 0;
		RaisePropertyChanged(nameof(TopSheet), nameof(HasSheets), nameof(IsPlayerCharacterOpen), nameof(IsVillagersOpen), nameof(IsMapOpen), nameof(IsStatusOpen),
			nameof(StartScreen), nameof(Clothing), nameof(IsWardrobeOpen), nameof(IsInventoryOpen));
		UpdatePendingPlayerSteps();
	}

	private void AttachSheet(ViewModelBase sheet)
	{
		switch (sheet)
		{
		case StartScreenViewModel start:
			start.StartRequested += OnStartRequested;
			break;
		case VillagersViewModel villagers:
			villagers.TravelRequested += OnSheetTravelRequested;
			villagers.ExamineRequested += OnExamineRequested;
			break;
		case TownMapViewModel map:
			map.TravelRequested += OnSheetTravelRequested;
			break;
		case CharacterViewModel character:
			character.StatusRequested += OnCharacterStatusRequested;
			break;
		}
	}

	/// <summary>
	/// Stops listening to a sheet that closed. If a developer changed someone else's needs in it, they choose a new goal.
	/// Closing the player's clothes finishes their changing once no sheets are open.
	/// </summary>
	private void DetachSheet(ViewModelBase sheet)
	{
		switch (sheet)
		{
		case StartScreenViewModel start:
			start.StartRequested -= OnStartRequested;
			break;
		case ClothingScreenViewModel clothing:
			m_closedClothing = clothing;
			break;
		case VillagersViewModel villagers:
			villagers.TravelRequested -= OnSheetTravelRequested;
			villagers.ExamineRequested -= OnExamineRequested;
			break;
		case TownMapViewModel map:
			map.TravelRequested -= OnSheetTravelRequested;
			break;
		case CharacterViewModel character:
			character.StatusRequested -= OnCharacterStatusRequested;
			break;
		case StatusViewModel { IsChanged: true, Person.IsPlayer: false } status:
			World.CancelGoal(status.Person);
			break;
		}
	}

	private CharacterViewModel CreateCharacter(Person person) => new(person, World, IsDeveloper);

	private VillagersViewModel CreateVillagers() =>
		new(World.Families, IsDeveloper, World.GameData.PortraitRenderer, World.GameData.Schedules, World.GameData.Relationships, World.GameData.Calendar, ChooseDestination);

	/// <summary>
	/// Creates the map. Choosing a quarter on it makes it the player's destination, and developers can go straight there.
	/// </summary>
	private TownMapViewModel CreateMap() =>
		new(World.Locations, World.Player.Location, World.Features, canTravel: IsDeveloper, World.Player.Home, World.Player.Business is null ? null : World.Player.Workplace,
			CharacterPanel.GetAppointmentPlace(), World.Player.Destination, ChooseDestination);

	private StatusViewModel CreateStatus(Person person) => person.IsPlayer ? new StatusViewModel(World, IsDeveloper) : new StatusViewModel(World, person, IsDeveloper);

	private bool IsPlayerCharacterSheet(ViewModelBase sheet) => sheet is CharacterViewModel character && character.Person.Person == World.Player;

	private static bool IsPlayerStatusSheet(ViewModelBase sheet) => sheet is StatusViewModel { Person.IsPlayer: true };

	/// <summary>
	/// Makes a place the player's destination, chosen in a sheet or a side column. If that would give up what they're
	/// doing, since their steps chose the destination they have now, Concerns asks them first. Nothing else happens until
	/// they set off.
	/// </summary>
	public void ChooseDestination(Location place)
	{
		VerifyAccess();
		ArgumentNullException.ThrowIfNull(place);
		if (World.WouldDestinationStopPlayerSteps(place) && World.Player.Current is { } sequence)
		{
			var activity = PlayerStepsViewModel.LowerFirst(ActivityText.Fill(sequence.Need.GetActivity(sequence.IsDone ? null : sequence.Step, World.Player.Business?.Profession.Id), sequence));
			Concerns.AskToReplaceDestination(place, activity);
		}
		else
		{
			ReplaceDestination(place);
		}
	}

	/// <summary>
	/// Starts a need of the player's chosen in the left column, or their work or a chore chosen in Concerns, or carries on
	/// with it, as choosing it from what they can start would. Starting it passes no time, so it can be chosen with sheets open; the player carries on with its steps once
	/// no sheets are open, as they do after choosing a destination. What the player can start closes, if it's open, since
	/// it's out of date. If time is passing, it stops first.
	/// </summary>
	public void StartNeed(Need need)
	{
		VerifyAccess();
		ArgumentNullException.ThrowIfNull(need);
		WhenStopped(() =>
		{
			World.StartPlayerSteps(need);
			CharacterPanel.OnStepsChanged();
			Concerns.Update();
			m_isPlayerStepsUpdatePending = true;
			CloseStart();
			UpdatePendingPlayerSteps();
		});
	}

	/// <summary>
	/// Walks the player all the way to their destination, a path at a time, as ordinary walking, so whatever would stop a
	/// walk stops it, as does anything they'd need to answer, or asking time to stop. It stops before a path they aren't
	/// dressed enough to take.
	/// </summary>
	public void WalkToDestination()
	{
		VerifyAccess();
		TimeRunner.Run(async () =>
		{
			while (World.GetPlayerWayToDestination() is { } next && !World.IsPlayerAsked && !TimeRunner.IsStopping)
			{
				if (GetTravelWarning(next) is { } warning)
				{
					m_currentLocation.ShowWaitResult(warning);
					break;
				}

				Log.Info($"Walking toward {World.Player.Destination?.Title} by {next.Title} ({next.Id})");
				if (!await World.WalkPlayerToAsync(next).ConfigureAwait(true))
					break;
				ShowLocation();
				m_currentLocation.Refresh();
			}
		});
	}

	/// <summary>
	/// Makes a place the player's destination, giving up what they're doing if their steps chose the one they have now.
	/// The steps carry on once no sheets are open.
	/// </summary>
	private void ReplaceDestination(Location place)
	{
		if (World.SetPlayerDestination(place))
		{
			m_isPlayerStepsUpdatePending = true;
			UpdatePendingPlayerSteps();
		}
	}

	/// <summary>
	/// Once nothing is open over the center and time isn't passing, finishes the player's changing of clothes, if they
	/// closed their inventory or wardrobe, and carries them on with their steps, if they did that or gave some up while
	/// choosing a destination.
	/// </summary>
	private void UpdatePendingPlayerSteps()
	{
		if ((m_isPlayerStepsUpdatePending || m_closedClothing is not null) && m_sheets.Count == 0 && !TimeRunner.IsRunning)
		{
			var clothing = TakeClosedClothing();
			TimeRunner.Run(async () =>
			{
				await FinishChangingAsync(clothing).ConfigureAwait(true);
				await World.UpdatePlayerStepsAsync().ConfigureAwait(true);
				ShowPlayerLocation();
			});
		}
	}

	/// <summary>
	/// Takes the player's closed inventory or wardrobe, if they closed one that hasn't finished their changing yet, and
	/// clears any pending update of their steps, which whatever runs next does.
	/// </summary>
	private ClothingScreenViewModel? TakeClosedClothing()
	{
		var clothing = m_closedClothing;
		m_closedClothing = null;
		m_isPlayerStepsUpdatePending = false;
		return clothing;
	}

	/// <summary>
	/// Finishes the player's changing of clothes once they've closed their inventory or wardrobe, which takes its time if
	/// they changed what they're wearing, and carries them on through their steps, such as taking their work clothes along.
	/// </summary>
	private async Task FinishChangingAsync(ClothingScreenViewModel? clothing)
	{
		if (clothing is not null && await clothing.FinishAsync().ConfigureAwait(true) is { } result)
			m_currentLocation.ShowWaitResult(result);
	}

	/// <summary>
	/// Shows the player's destination wherever it's shown, once it changes.
	/// </summary>
	private void OnDestinationChanged()
	{
		Concerns.Update();
		CharacterPanel.OnDestinationChanged();
		foreach (var map in m_sheets.OfType<TownMapViewModel>())
			map.SetDestination(World.Player.Destination);
	}

	/// <summary>
	/// Says why the player can't go to a location next to theirs, since they wouldn't be dressed enough there with the
	/// people there now, or null if they can. Going home while they aren't dressed enough where they are is always allowed.
	/// </summary>
	private string? GetTravelWarning(Location location) =>
		World.IsPlayerRetreatingTo(location) ? null : ModestyWarning.Format(World.GetPlayerUncoveredSlotsAt(location));

	/// <summary>
	/// Whether the last save worked, or null if the game hasn't been saved since the window opened.
	/// </summary>
	public string? SaveStatus
	{
		get => VerifyAccess(m_saveStatus);
		private set => SetPropertyField(value, ref m_saveStatus);
	}

	// Games can't be saved without knowing where saves are kept, or while time passes.
	public bool CanSaveGame() => m_saveSettings is not null && !TimeRunner.IsRunning;

	/// <summary>
	/// Saves the game to a new file in the save folder.
	/// </summary>
	public void SaveGame() => TrySaveGame();

	/// <summary>
	/// Saves the game to a new file in the save folder. Returns whether it was saved.
	/// </summary>
	public bool TrySaveGame()
	{
		VerifyAccess();
		return TrySaveGame(null, OurResources.GameSaved, OurResources.GameSaveFailed);
	}

	/// <summary>
	/// Saves the game to the given file, or to a new file in the save folder, saying how it went with the given messages
	/// (the second takes the reason it failed). Returns whether it was saved.
	/// </summary>
	private bool TrySaveGame(string? filePath, string savedStatus, string failedStatus)
	{
		var isSaved = false;
		if (CanSaveGame())
		{
			var settings = m_saveSettings!;
			var save = SaveFile.Create(World, settings, DateTimeOffset.Now);
			filePath ??= Path.Combine(settings.Directory, save.GetFileName());
			try
			{
				save.Write(filePath);
				Log.Info($"Saved the game to {filePath}");
				SaveStatus = savedStatus;
				m_savedState = GetSavedState(settings);
				isSaved = true;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				Log.Error($"Couldn't save the game to {filePath}: {e.Message}");
				SaveStatus = failedStatus.FormatCurrentCulture(e.Message);
			}
		}
		return isSaved;
	}

	/// <summary>
	/// Saves the game to the quick save, replacing any there, once time has stopped, and says how it went in the center.
	/// </summary>
	public void QuickSave()
	{
		VerifyAccess();
		WhenStopped(() =>
		{
			if (m_saveSettings is { } settings)
				TrySaveGame(SaveFile.GetQuickSavePath(settings.Directory), OurResources.QuickSaved, OurResources.QuickSaveFailed);
		});
	}

	/// <summary>
	/// Shows a message about saving or loading at the top of the center, until the player does something.
	/// </summary>
	public void ShowSaveStatus(string status)
	{
		VerifyAccess();
		SaveStatus = status;
	}

	/// <summary>
	/// Handles a hotkey during play, with nothing over the game: the sheets' keys open or close their sheets (stopping time
	/// first), W and I open or close the wardrobe and the inventory, S opens or closes what the player can start, J moves
	/// keyboard focus to Concerns, F5 quick saves, and the number keys press the center's numbered choices. Returns whether
	/// the key is one the game uses.
	/// </summary>
	public bool HandleHotkey(Hotkey hotkey)
	{
		VerifyAccess();
		var isHandled = true;
		switch (hotkey.Command)
		{
		case HotkeyCommand.Character:
			TogglePlayerCharacter();
			break;
		case HotkeyCommand.Villagers:
			ToggleVillagers();
			break;
		case HotkeyCommand.Map:
			ToggleMap();
			break;
		case HotkeyCommand.Status:
			ToggleStatus();
			break;
		case HotkeyCommand.Wardrobe:
			if (CanToggleWardrobe())
				ToggleWardrobe();
			break;
		case HotkeyCommand.Inventory:
			ToggleInventory();
			break;
		case HotkeyCommand.Start:
			ToggleStart();
			break;
		case HotkeyCommand.Concerns:
			FocusConcerns();
			break;
		case HotkeyCommand.QuickSave:
			QuickSave();
			break;
		case HotkeyCommand.Choice:
			ChooseNumber(hotkey.Number);
			break;
		case HotkeyCommand.ChoiceMenu:
			OpenChoiceMenu(hotkey.Number);
			break;
		default:
			isHandled = false;
			break;
		}
		return isHandled;
	}

	/// <summary>
	/// Opens what the player can start, as the location's button does, replacing any sheets, or closes it if it's on top.
	/// It doesn't open while a prompt is waiting for the player. Time stops first if it's passing.
	/// </summary>
	public void ToggleStart()
	{
		VerifyAccess();
		if (TopSheet is StartScreenViewModel)
		{
			CloseTopSheet();
		}
		else
		{
			WhenStopped(() =>
			{
				if (Overlay is null && !m_currentLocation.HasInteraction)
					ShowStart();
			});
		}
	}

	/// <summary>
	/// Presses the numbered choice in what shows: the sheet on top, or else what's in the center, if time isn't passing.
	/// Returns whether it was pressed.
	/// </summary>
	public bool ChooseNumber(int number)
	{
		VerifyAccess();
		return !TimeRunner.IsRunning && NumberedChoices is { } choices && choices.ChooseNumber(number);
	}

	/// <summary>
	/// Opens the menu of other ways of the numbered choice in what shows, when it's a split button, if time isn't passing.
	/// Returns whether it opened.
	/// </summary>
	public bool OpenChoiceMenu(int number)
	{
		VerifyAccess();
		return !TimeRunner.IsRunning && NumberedChoices is { } choices && choices.OpenChoiceMenu(number);
	}

	/// <summary>
	/// What shows that the number keys could press, or null if it has no numbered choices: the sheet on top, or else
	/// what's on top of the center.
	/// </summary>
	private INumberedChoices? NumberedChoices => (TopSheet ?? Overlay ?? m_currentLocation) as INumberedChoices;

	/// <summary>
	/// Changes to a new object each time keyboard focus should move to Concerns, which the view does. Null until then.
	/// </summary>
	public object? ConcernsFocusRequest
	{
		get => VerifyAccess(m_concernsFocusRequest);
		private set => SetPropertyField(value, ref m_concernsFocusRequest);
	}

	/// <summary>
	/// Moves keyboard focus to the first control in Concerns, so its buttons can be used from the keyboard.
	/// </summary>
	public void FocusConcerns()
	{
		VerifyAccess();
		ConcernsFocusRequest = new object();
	}

	/// <summary>
	/// Whether the game has changed since it was last saved, or since it was started or loaded if it hasn't been saved since.
	/// It's always changing while time passes. A game that can't be saved has nothing to offer saving.
	/// </summary>
	public bool HasUnsavedChanges()
	{
		VerifyAccess();
		if (m_saveSettings is not { } settings)
			return false;

		return TimeRunner.IsRunning || GetSavedState(settings) != m_savedState;
	}

	/// <summary>
	/// Handles Esc: stops time if it's passing (giving up any sheet waiting for it to stop), or else closes the sheet on top.
	/// Pressed again soon after it closed a sheet, it closes every other sheet, and never does anything else. Returns false if there was nothing for it to do, so it can open the main
	/// menu instead.
	/// </summary>
	public bool HandleEscape()
	{
		VerifyAccess();
		var now = s_clock.Elapsed;
		var isRepeat = m_lastSheetEscape is { } last && now - last <= EscapeRepeatTime;
		m_lastSheetEscape = null;
		var isHandled = true;
		if (TimeRunner.IsRunning)
		{
			m_whenStopped = null;
			TimeRunner.Stop();
		}
		else if (m_sheets.Count != 0)
		{
			if (isRepeat)
				CloseSheets();
			else
				CloseTopSheet();
			m_lastSheetEscape = now;
		}
		else if (isRepeat)
		{
			// A quick second Esc only closes sheets.
		}
		else
		{
			isHandled = false;
		}
		return isHandled;
	}

	/// <summary>
	/// The game as it would be saved, without the time it was saved, for telling whether it changed.
	/// </summary>
	private string GetSavedState(SaveSettings settings) => SaveFile.Create(World, settings, DateTimeOffset.MinValue).Serialize();

	/// <summary>
	/// Moves the player to a location next to theirs, which takes the travel time between them, and can't be stopped
	/// partway. They can't go anywhere they wouldn't be dressed enough for, with the people there now, unless they aren't
	/// dressed enough where they are and it's on the way home.
	/// </summary>
	public void TravelTo(Location location)
	{
		VerifyAccess();
		var player = World.Player;
		if (GetTravelWarning(location) is { } warning)
		{
			m_currentLocation.ShowWaitResult(warning);
		}
		else
		{
			TimeRunner.Run(async () =>
			{
				// Someone asking the player to chat on the way stops them where they are.
				Log.Info($"Traveling from {player.Location.Title} ({player.Location.Id}) to {location.Title} ({location.Id})");
				if (await World.WalkPlayerToAsync(location).ConfigureAwait(true))
				{
					ShowLocation();
					m_currentLocation.Refresh();
				}
			});
		}
	}

	/// <summary>
	/// Moves the player to a location and shows it.
	/// </summary>
	private async Task MoveToAsync(Location location)
	{
		var player = World.Player;
		Log.Info($"Traveling from {player.Location.Title} ({player.Location.Id}) to {location.Title} ({location.Id})");

		await World.MovePlayerAsync(location).ConfigureAwait(true);
		ShowLocation();
	}

	/// <summary>
	/// Shows the location the player is in, still following the given person, if any. The screen it replaces stops being
	/// listened to, unless it's kept, such as while it's running something the player did that will say how it went.
	/// </summary>
	private void ShowLocation(Person? following = null, bool isOldScreenKept = false)
	{
		var location = World.Player.Location;
		SaveStatus = null;
		CloseConversation();
		if (!isOldScreenKept)
		{
			m_currentLocation.TravelRequested -= OnTravelRequested;
			m_currentLocation.Followed -= OnFollowed;
			m_currentLocation.ExamineRequested -= OnExamineRequested;
			m_currentLocation.TalkRequested -= OnTalkRequested;
			m_currentLocation.StepsCarriedOut -= OnStepsCarriedOut;
			m_currentLocation.StopRequested -= OnStopRequested;
			m_currentLocation.ChooseStartRequested -= OnChooseStartRequested;
			m_currentLocation.WalkToDestinationRequested -= OnWalkToDestinationRequested;
		}
		CurrentLocation = CreateLocationScreen(location, following);
		UpdateInteraction();
		CharacterPanel.OnLocationChanged();
	}

	/// <summary>
	/// Shows where the player is now, if their steps took them somewhere else, or brings what they can do here up to date.
	/// </summary>
	private void ShowPlayerLocation()
	{
		if (World.Player.Location != m_currentLocation.Location)
			ShowLocation();
		else
			m_currentLocation.Refresh();
	}

	public void Dispose()
	{
		World.PropertyChanged -= OnWorldPropertyChanged;
		World.Player.PropertyChanged -= OnPlayerPropertyChanged;
		World.AppointmentMissed -= OnAppointmentMissed;
		World.StepFailed -= OnStepFailed;
		TimeRunner.PropertyChanged -= OnTimeRunnerPropertyChanged;
		if (m_askPrompt is not null)
			m_askPrompt.Answered -= OnAskPromptAnswered;
		if (m_startPrompt is not null)
			m_startPrompt.Answered -= OnStartPromptAnswered;
		if (m_appointmentPrompt is not null)
		{
			m_appointmentPrompt.SetOff -= OnAppointmentPromptSetOff;
			m_appointmentPrompt.CalledOff -= OnAppointmentPromptCalledOff;
		}
		if (m_noticePrompt is not null)
			m_noticePrompt.Dismissed -= OnNoticePromptDismissed;
		if (m_talkPrompt is not null)
		{
			m_talkPrompt.Accepted -= OnTalkPromptAccepted;
			m_talkPrompt.Declined -= OnTalkPromptDeclined;
		}
		if (m_repeatPrompt is not null)
		{
			m_repeatPrompt.Continued -= OnRepeatPromptContinued;
			m_repeatPrompt.Stopped -= OnRepeatPromptStopped;
		}
		m_currentLocation.TravelRequested -= OnTravelRequested;
		m_currentLocation.Followed -= OnFollowed;
		m_currentLocation.ExamineRequested -= OnExamineRequested;
		m_currentLocation.TalkRequested -= OnTalkRequested;
		m_currentLocation.StepsCarriedOut -= OnStepsCarriedOut;
		m_currentLocation.StopRequested -= OnStopRequested;
		m_currentLocation.ChooseStartRequested -= OnChooseStartRequested;
		m_currentLocation.WalkToDestinationRequested -= OnWalkToDestinationRequested;
		foreach (var sheet in m_sheets)
			DetachSheet(sheet);
		if (m_conversation is not null)
			m_conversation.CloseRequested -= OnConversationCloseRequested;
		TimeRunner.Dispose();
	}

	private LocationScreenViewModel CreateLocationScreen(Location location, Person? following = null)
	{
		var viewModel = new LocationScreenViewModel(location, World, TimeRunner, following, IsDeveloper);
		viewModel.TravelRequested += OnTravelRequested;
		viewModel.Followed += OnFollowed;
		viewModel.ExamineRequested += OnExamineRequested;
		viewModel.TalkRequested += OnTalkRequested;
		viewModel.StepsCarriedOut += OnStepsCarriedOut;
		viewModel.StopRequested += OnStopRequested;
		viewModel.ChooseStartRequested += OnChooseStartRequested;
		viewModel.WalkToDestinationRequested += OnWalkToDestinationRequested;
		return viewModel;
	}

	/// <summary>
	/// Shows the player's next appointment and today's appointments, and marks the next one's place on the mini-map, once
	/// their appointments may have changed.
	/// </summary>
	private void OnAppointmentsChanged()
	{
		CharacterPanel.OnAppointmentsChanged();
		Concerns.Update();
	}

	private void OnTravelRequested(object? sender, GenericEventArgs<Location> e) => TravelTo(e.Value);

	/// <summary>
	/// Shows where the player's steps took them, and how it went.
	/// </summary>
	private void OnStepsCarriedOut(object? sender, GenericEventArgs<string> e)
	{
		ShowPlayerLocation();
		m_currentLocation.ShowWaitResult(e.Value);
	}

	/// <summary>
	/// Shows where the player followed someone to, still following them.
	/// </summary>
	private void OnFollowed(object? sender, GenericEventArgs<Location> e)
	{
		var following = m_currentLocation.Following;
		Log.Info($"Followed {following?.FullName} to {e.Value.Title} ({e.Value.Id})");
		ShowLocation(following);
		if (following is not null)
			m_currentLocation.ShowWaitResult(OurResources.WaitResultFollowed.FormatCurrentCulture(following.FullName, e.Value.Title));
	}

	private void OnWorldPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(GameWorld.Time))
		{
			CharacterPanel.OnTimeChanged();
			Concerns.Update();
			UpdateAppointmentPrompt();
			UpdateRepeatPrompt();
		}
	}

	/// <summary>
	/// For developers, goes straight to a place chosen in a sheet, which takes no time. It uses the engine, so it closes the
	/// sheets. Developers still have to be dressed enough, so it does nothing if the player isn't dressed enough for the
	/// place or can't leave their inventory or wardrobe. If they closed their clothes to get here, they finish changing
	/// first.
	/// </summary>
	private void OnSheetTravelRequested(object? sender, GenericEventArgs<Location> e)
	{
		var location = e.Value;
		if (GetTravelWarning(location) is { } warning)
		{
			Log.Info($"Not jumping to {location.Title} ({location.Id}): {warning}");
		}
		else if (m_sheets.All(CanCloseSheet))
		{
			RemoveSheets();
			var clothing = TakeClosedClothing();
			OnSheetsChanged();
			TimeRunner.Run(async () =>
			{
				await FinishChangingAsync(clothing).ConfigureAwait(true);
				await MoveToAsync(location).ConfigureAwait(true);
			});
		}
	}

	private void OnChooseStartRequested(object? sender, EventArgs e) => ShowStart();

	private void OnWalkToDestinationRequested(object? sender, EventArgs e) => WalkToDestination();

	/// <summary>
	/// Starts something the player chose, or carries on with it, showing where they are while it goes on, and how it went
	/// if it was done for them. What they can start closes, along with any sheets over it, and if they closed their
	/// clothes to open it, they finish changing first.
	/// </summary>
	private void OnStartRequested(object? sender, GenericEventArgs<StartRequest> e)
	{
		RemoveSheets();
		if (m_sheets.Count == 0)
		{
			var clothing = TakeClosedClothing();
			OnSheetsChanged();
			ShowPlayerLocation();
			var (need, isAutomatic, minutes) = e.Value;
			TimeRunner.Run(async () =>
			{
				await FinishChangingAsync(clothing).ConfigureAwait(true);
				var activity = PlayerStepsViewModel.LowerFirst(ActivityText.Fill(need.GetActivity(0, World.Player.Business?.Profession.Id), World.Player.GetSequence(need)));
				var start = World.Time;
				await World.StartPlayerStepsAsync(need, isAutomatic, minutes).ConfigureAwait(true);
				var spent = World.GameData.Calendar.FormatOffsetFrom(start, World.Time - start, TimeFormat.Long);
				ShowPlayerLocation();
				if (isAutomatic)
					m_currentLocation.ShowWaitResult((World.Player.GetSequence(need) is not null ? OurResources.PlayerStepsStopped : OurResources.PlayerStepsDone).FormatCurrentCulture(activity, spent));
			});
		}
		else
		{
			OnSheetsChanged();
		}
	}

	private void OnCharacterStatusRequested(object? sender, GenericEventArgs<Person> e) => ShowCharacterStatus(e.Value);

	private void OnExamineRequested(object? sender, GenericEventArgs<Person> e) => ShowCharacter(e.Value);

	private void OnTalkRequested(object? sender, GenericEventArgs<Person> e) => ShowConversation(e.Value);

	private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// While time passes, the player's location is shown as they go, such as on a walk done for them. The screen they
		// left is still running what they did, and says how it went once it's done.
		if (e.PropertyName is nameof(Person.Location) && TimeRunner.IsRunning && World.Player.Location != m_currentLocation.Location)
			ShowLocation(m_currentLocation.Following, isOldScreenKept: true);
		// Walking passes the time before the player arrives, so how far away their destination is (and the places to go to)
		// changes again once they have.
		if (e.PropertyName is nameof(Person.Location) or null or "")
			Concerns.Update();
		if (e.PropertyName is nameof(Person.TalkRequest) or null or "")
			UpdateTalkPrompt();
		if (e.PropertyName is nameof(Person.Sequences) or null or "")
		{
			UpdateAppointmentPrompt();
			OnAppointmentsChanged();
		}
		if (e.PropertyName is nameof(Person.Current) or null or "")
			UpdateRepeatPrompt();
		if (e.PropertyName is nameof(Person.AskRequest) or null or "")
			UpdateAskPrompt();
		if (e.PropertyName is nameof(Person.StartRequest) or null or "")
			UpdateStartPrompt();
		if (e.PropertyName is nameof(Person.Destination) or null or "")
			OnDestinationChanged();
	}

	/// <summary>
	/// Shows the question while the other of an appointment is asking the player whether to start it, and closes it once
	/// they've answered.
	/// </summary>
	private void UpdateStartPrompt()
	{
		if (m_startPrompt is { } prompt)
		{
			prompt.Answered -= OnStartPromptAnswered;
			StartPrompt = null;
		}

		if (World.Player.StartRequest is { } appointment)
		{
			var newPrompt = new StartPromptViewModel(appointment, World);
			newPrompt.Answered += OnStartPromptAnswered;
			StartPrompt = newPrompt;
		}
	}

	/// <summary>
	/// Answers whether to start the appointment, and carries the player on with their steps.
	/// </summary>
	private void OnStartPromptAnswered(object? sender, GenericEventArgs<bool> e) =>
		TimeRunner.Run(async () =>
		{
			await World.AnswerStartAsync(e.Value).ConfigureAwait(true);
			ShowPlayerLocation();
			OnAppointmentsChanged();
		});

	/// <summary>
	/// Shows the question while someone is asking the player something, and closes it once they've answered.
	/// </summary>
	private void UpdateAskPrompt()
	{
		if (m_askPrompt is { } prompt)
		{
			prompt.Answered -= OnAskPromptAnswered;
			AskPrompt = null;
		}

		if (World.Player.AskRequest is { } asker)
		{
			var newPrompt = new AskPromptViewModel(asker, World);
			newPrompt.Answered += OnAskPromptAnswered;
			AskPrompt = newPrompt;
		}
	}

	/// <summary>
	/// Answers the question, and tells the player of any appointment it made.
	/// </summary>
	private void OnAskPromptAnswered(object? sender, GenericEventArgs<bool> e) =>
		TimeRunner.Run(async () =>
		{
			var asker = World.Player.AskRequest;
			var appointment = await World.AnswerAskAsync(e.Value).ConfigureAwait(true);
			ShowPlayerLocation();
			if (appointment is not null && asker is not null)
				m_currentLocation.ShowWaitResult(OurResources.ConversationAgreedToMeet.FormatCurrentCulture(asker.FullName, AppointmentText.FormatPlace(World, appointment), AppointmentText.FormatWhen(World, appointment.Time)));
			OnAppointmentsChanged();
		});

	/// <summary>
	/// Shows the prompts that wait for what the player did to finish, once it has, and anything waiting for time to stop.
	/// </summary>
	private void OnTimeRunnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(Controls.TimeRunner.IsRunning) && TimeRunner.IsRunning)
		{
			// What the last save said is only shown until the player does something.
			SaveStatus = null;
		}
		else if (e.PropertyName is nameof(Controls.TimeRunner.IsRunning) && !TimeRunner.IsRunning)
		{
			UpdateAppointmentPrompt();
			UpdateRepeatPrompt();

			// A sheet asked for while time passed opens once it has stopped.
			if (m_whenStopped is { } whenStopped)
			{
				m_whenStopped = null;
				whenStopped();
			}
			UpdatePendingPlayerSteps();
		}
	}

	/// <summary>
	/// Shows the reminder while it's time for the player to set off for an appointment, and closes it once they've answered.
	/// It waits for what the player did to finish, such as asking someone the question that made the appointment, so they
	/// see how it went first. Time stays stopped meanwhile.
	/// </summary>
	private void UpdateAppointmentPrompt()
	{
		var sequence = World.GetPlayerSetOffSequence();
		if (m_appointmentPrompt is { } prompt && prompt.Sequence != sequence)
		{
			prompt.SetOff -= OnAppointmentPromptSetOff;
			prompt.CalledOff -= OnAppointmentPromptCalledOff;
			AppointmentPrompt = null;
		}

		if (m_appointmentPrompt is null && sequence is not null && !TimeRunner.IsRunning)
		{
			var newPrompt = new AppointmentPromptViewModel(World, sequence);
			newPrompt.SetOff += OnAppointmentPromptSetOff;
			newPrompt.CalledOff += OnAppointmentPromptCalledOff;
			AppointmentPrompt = newPrompt;
		}
	}

	/// <summary>
	/// Sets off for the appointment, showing its steps in place of what the player was doing.
	/// </summary>
	private void OnAppointmentPromptSetOff(object? sender, EventArgs e) =>
		TimeRunner.Run(async () =>
		{
			await World.AnswerSetOffAsync(isSettingOff: true).ConfigureAwait(true);
			UpdateAppointmentPrompt();
			ShowPlayerLocation();
		});

	private void OnAppointmentPromptCalledOff(object? sender, EventArgs e) =>
		TimeRunner.Run(async () =>
		{
			await World.AnswerSetOffAsync(isSettingOff: false).ConfigureAwait(true);
			UpdateAppointmentPrompt();
			OnAppointmentsChanged();
		});

	/// <summary>
	/// Tells the player they missed an appointment, on top of whatever they were doing.
	/// </summary>
	private void OnAppointmentMissed(object? sender, AppointmentMissedEventArgs e)
	{
		var name = e.Appointment.GetOther(World.Player).FullName;
		ShowNotice((e.IsOtherWaiting ? OurResources.AppointmentGaveUp : OurResources.AppointmentNeverCame).FormatCurrentCulture(name));
		OnAppointmentsChanged();
	}

	/// <summary>
	/// Tells the player a step of theirs failed, and why, on top of whatever they were doing.
	/// </summary>
	private void OnStepFailed(object? sender, StepFailedEventArgs e)
	{
		// Missing an appointment is told as such (see OnAppointmentMissed), and a failure the steps carry on from, or one from
		// setting off for an appointment, which the player chose, is only logged. One the steps carry on from is remembered,
		// so if it ends a pass of steps that repeat, the player is told why when they're asked whether to keep going.
		var player = World.Player;
		if (e.Person != player || e.Cause is Data.FailCause.NoShow or Data.FailCause.Interrupted)
			return;
		if (!e.IsEnding)
		{
			m_lastCarriedOnFailure = (e.Sequence, e.Time, ClothingPieceViewModel.FirstUpper(LogEntryText.GetCauseText(e.Cause, isPlayer: true, e.Who)));
			UpdateRepeatPrompt();
			return;
		}

		var step = PlayerStepsViewModel.Describe(e.Sequence.Need, e.Step, place: null, player, e.Sequence.Partner);
		ShowNotice(OurResources.PlayerStepFailed.FormatCurrentCulture(step, LogEntryText.GetCauseText(e.Cause, isPlayer: true, e.Who)));
	}

	/// <summary>
	/// Asks the player how to stop steps that have something left to take care of.
	/// </summary>
	private void OnStopRequested(object? sender, GenericEventArgs<StepSequence> e)
	{
		if (m_stopPrompt is { } old)
			old.Chosen -= OnStopPromptChosen;
		var prompt = new StopPromptViewModel(World, e.Value);
		prompt.Chosen += OnStopPromptChosen;
		StopPrompt = prompt;
	}

	/// <summary>
	/// Stops the steps the way the player chose: finishing up first, keeping the bundle they carry to finish later, or at
	/// once, or else keeps going, and says how it went.
	/// </summary>
	private void OnStopPromptChosen(object? sender, GenericEventArgs<StopChoice> e)
	{
		if (m_stopPrompt is not { } prompt)
			return;

		prompt.Chosen -= OnStopPromptChosen;
		StopPrompt = null;
		var sequence = prompt.Sequence;
		var activity = PlayerStepsViewModel.LowerFirst(ActivityText.Fill(sequence.Need.GetActivity(sequence.IsDone ? null : sequence.Step, World.Player.Business?.Profession.Id), sequence));
		TimeRunner.Run(async () =>
		{
			string? result = null;
			if (e.Value == StopChoice.FinishUp)
			{
				World.FinishUpPlayerSteps(sequence);
				result = OurResources.PlayerStepsFinishingUp.FormatCurrentCulture(activity);
			}
			else if (e.Value == StopChoice.Keep)
			{
				World.KeepPlayerSteps(sequence);
				result = OurResources.PlayerStepsKept.FormatCurrentCulture(activity);
			}
			else if (e.Value == StopChoice.JustStop)
			{
				World.StopPlayerSteps(sequence);
				result = OurResources.PlayerStepsGaveUp.FormatCurrentCulture(activity);
			}

			await World.UpdatePlayerStepsAsync().ConfigureAwait(true);
			ShowPlayerLocation();
			if (result is not null)
				m_currentLocation.ShowWaitResult(result);
		});
	}

	/// <summary>
	/// Shows a notice, along with any the player hasn't closed yet.
	/// </summary>
	private void ShowNotice(string text)
	{
		if (m_noticePrompt is { } prompt)
		{
			prompt.Dismissed -= OnNoticePromptDismissed;
			text = prompt.Text + " " + text;
		}

		var newPrompt = new NoticePromptViewModel(text);
		newPrompt.Dismissed += OnNoticePromptDismissed;
		NoticePrompt = newPrompt;
	}

	private void OnNoticePromptDismissed(object? sender, EventArgs e)
	{
		if (m_noticePrompt is { } prompt)
		{
			prompt.Dismissed -= OnNoticePromptDismissed;
			NoticePrompt = null;
		}
		ShowPlayerLocation();
	}

	/// <summary>
	/// Shows the prompt while someone is asking the player to chat, and closes it once they've answered.
	/// </summary>
	private void UpdateTalkPrompt()
	{
		if (m_talkPrompt is { } prompt)
		{
			prompt.Accepted -= OnTalkPromptAccepted;
			prompt.Declined -= OnTalkPromptDeclined;
			TalkPrompt = null;
		}

		if (World.Player.TalkRequest is { } asker)
		{
			var newPrompt = new TalkPromptViewModel(asker, World);
			newPrompt.Accepted += OnTalkPromptAccepted;
			newPrompt.Declined += OnTalkPromptDeclined;
			TalkPrompt = newPrompt;
		}
		UpdateInteraction();
	}

	/// <summary>
	/// Shows someone asking the player to chat in their line in the location, if they're listed there, or else the
	/// conversation, in the line of the one it's with. Someone asking to chat who isn't listed is shown on top instead.
	/// </summary>
	private void UpdateInteraction()
	{
		var isTalkPromptInLine = m_talkPrompt is { } prompt && m_currentLocation.Location.People.Contains(prompt.Asker.Person);
		if (isTalkPromptInLine != m_isTalkPromptInLine)
		{
			m_isTalkPromptInLine = isTalkPromptInLine;
			RaisePropertyChanged(nameof(Overlay));
		}

		if (isTalkPromptInLine)
			m_currentLocation.SetInteraction(m_talkPrompt!.Asker.Person, m_talkPrompt);
		else if (m_conversation is { } conversation)
			m_currentLocation.SetInteraction(conversation.Person.Person, conversation);
		else
			m_currentLocation.SetInteraction(null, null);
	}

	private void OnTalkPromptAccepted(object? sender, EventArgs e)
	{
		if (World.Player.TalkRequest is { } asker)
		{
			TimeRunner.Run(async () =>
			{
				var start = World.Time;
				var passed = await World.AcceptTalkAsync().ConfigureAwait(true);
				m_currentLocation.ShowWaitResult(passed is { } time
					? OurResources.TalkPromptChatted.FormatCurrentCulture(asker.FullName, World.GameData.Calendar.FormatOffsetFrom(start, time, TimeFormat.Long))
					: OurResources.TalkPromptGone.FormatCurrentCulture(asker.FullName));
			});
		}
	}

	private void OnTalkPromptDeclined(object? sender, EventArgs e) => World.DeclineTalk();

	/// <summary>
	/// Shows the prompt while the player is asked whether to keep going with what they're doing, and closes it once they've
	/// answered. It waits for what the player did to finish, which is also when it's checked again, since the steps can
	/// come to it without the time or the player's current steps changing.
	/// </summary>
	private void UpdateRepeatPrompt()
	{
		if (m_repeatPrompt is { } prompt)
		{
			prompt.Continued -= OnRepeatPromptContinued;
			prompt.Stopped -= OnRepeatPromptStopped;
			RepeatPrompt = null;
		}

		if (World.Player.Current is { } sequence && World.IsAskingPlayerToKeepGoing(sequence) && !TimeRunner.IsRunning)
		{
			// A failure the steps just carried on from to get here is why the pass ended.
			var reason = m_lastCarriedOnFailure is { } failure && failure.Sequence == sequence && failure.Time == World.Time ? failure.Reason : null;
			var newPrompt = new RepeatPromptViewModel(World, sequence, reason);
			newPrompt.Continued += OnRepeatPromptContinued;
			newPrompt.Stopped += OnRepeatPromptStopped;
			RepeatPrompt = newPrompt;
		}
	}

	/// <summary>
	/// Keeps going, which for steps done for the player carries them on, and goes back to their steps.
	/// </summary>
	private void OnRepeatPromptContinued(object? sender, EventArgs e) =>
		TimeRunner.Run(async () =>
		{
			if (World.Player.Current is { } sequence && World.IsAskingPlayerToKeepGoing(sequence))
				await World.AnswerKeepGoingAsync(sequence, keepGoing: true).ConfigureAwait(true);
			UpdateRepeatPrompt();
			ShowPlayerLocation();
		});

	/// <summary>
	/// Stops, carrying on with the steps after the ones repeated, such as changing out of work clothes.
	/// </summary>
	private void OnRepeatPromptStopped(object? sender, EventArgs e)
	{
		var player = World.Player;
		if (player.Current is { } sequence && World.IsAskingPlayerToKeepGoing(sequence))
		{
			TimeRunner.Run(async () =>
			{
				var activity = ActivityText.Fill(sequence.Need.GetActivity(null, player.Business?.Profession.Id), sequence);
				await World.AnswerKeepGoingAsync(sequence, keepGoing: false).ConfigureAwait(true);
				UpdateRepeatPrompt();
				ShowPlayerLocation();
				m_currentLocation.ShowWaitResult(OurResources.RepeatPromptStopped.FormatCurrentCulture(char.ToLower(activity[0], CultureInfo.CurrentCulture) + activity[1..]));
			});
		}
	}

	private void OnConversationCloseRequested(object? sender, EventArgs e) => CloseConversation();

	/// <summary>
	/// Opens the sheet of someone clicked in the event log, on top of any sheets, stopping time first if it's passing.
	/// </summary>
	private void OnLogPersonClicked(Person person) => ShowCharacter(person);

	/// <summary>
	/// Opens the map for a place clicked in the event log, on top of any sheets, stopping time first if it's passing.
	/// </summary>
	private void OnLogPlaceClicked(Location place) => WhenStopped(() => PushSheet(CreateMap()));

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(GameScreenViewModel));

	private readonly SaveSettings? m_saveSettings;
	private string? m_savedState;
	private LocationScreenViewModel m_currentLocation;
	private readonly ObservableCollection<ViewModelBase> m_sheets;
	private ClothingScreenViewModel? m_closedClothing;
	private ConversationViewModel? m_conversation;
	private bool m_isTalkPromptInLine;
	private TalkPromptViewModel? m_talkPrompt;
	private AppointmentPromptViewModel? m_appointmentPrompt;
	private AskPromptViewModel? m_askPrompt;
	private StartPromptViewModel? m_startPrompt;
	private NoticePromptViewModel? m_noticePrompt;
	private RepeatPromptViewModel? m_repeatPrompt;
	private StopPromptViewModel? m_stopPrompt;
	private (StepSequence Sequence, GoldenAnvil.Utility.Calendar.TimePoint Time, string Reason)? m_lastCarriedOnFailure;
	private string? m_saveStatus;
	private object? m_concernsFocusRequest;
	private Action? m_whenStopped;
	private bool m_isPlayerStepsUpdatePending;
	private TimeSpan? m_lastSheetEscape;

	private static readonly Stopwatch s_clock = Stopwatch.StartNew();

	private static class NativeMethods
	{
		[DllImport("user32.dll")]
		public static extern uint GetDoubleClickTime();
	}
}
