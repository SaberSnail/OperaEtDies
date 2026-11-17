using System.ComponentModel;
using System.IO;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using OperaEtDies.Controls.Characters;
using OperaEtDies.Controls.Clothing;
using OperaEtDies.Controls.Conversation;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Controls.Map;
using OperaEtDies.Controls.Status;
using OperaEtDies.Controls.Villagers;
using OperaEtDies.GameState;

namespace OperaEtDies.Windows.Main;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
	public MainWindowViewModel(string title, GameWorld world, bool isDeveloper = false, SaveSettings? saveSettings = null)
	{
		m_saveSettings = saveSettings;
		m_taskGroup = new TaskGroup();
		Title = title;
		IsDeveloper = isDeveloper;
		World = world;
		Player = new LocationPersonViewModel(world.Player, world.GameData.PortraitRenderer);
		m_currentLocation = CreateLocationScreen(world.Player.Location);
		m_miniMap = CreateMiniMap(world.Player.Location);
		World.PropertyChanged += OnWorldPropertyChanged;
		World.Player.PropertyChanged += OnPlayerPropertyChanged;
		UpdateTalkPrompt();
	}

	public string Title { get; }

	/// <summary>
	/// Whether to show extra information and controls for developing the game.
	/// </summary>
	public bool IsDeveloper { get; }

	/// <summary>
	/// The state of the game world.
	/// </summary>
	public GameWorld World { get; }

	/// <summary>
	/// The player's character, shown at the top of the sidebar.
	/// </summary>
	public LocationPersonViewModel Player { get; }

	/// <summary>
	/// Today's date, such as "Aftday, Mid Winter, 227 CR".
	/// </summary>
	public string Date => VerifyAccess(World.GameData.Calendar.FormatDate(World.Time));

	/// <summary>
	/// The part of the day and the time on the clock, such as "Morning, 6:00".
	/// </summary>
	public string TimeOfDay
	{
		get
		{
			VerifyAccess();
			var calendar = World.GameData.Calendar;
			return OurResources.TimeOfDay.FormatCurrentCulture(calendar.GetDayPart(World.Time).Title, calendar.FormatClock(World.Time));
		}
	}

	/// <summary>
	/// The special days that fall today, such as a festival, or null if today is an ordinary day.
	/// </summary>
	public string? SpecialDay
	{
		get
		{
			VerifyAccess();
			var specialDays = World.GameData.Calendar.GetSpecialDays(World.Time);
			return specialDays.Count == 0 ? null : string.Join(OurResources.SpecialDaySeparator, specialDays.Select(x => x.Title));
		}
	}

	/// <summary>
	/// How the player is doing with each need shown to them, such as "Fatigue: Tired".
	/// </summary>
	public IReadOnlyList<string> PlayerNeeds
	{
		get
		{
			VerifyAccess();
			return World.GameData.Schedules.Needs
				.Select(x => x.GetLevelTitle(World.Player.GetLevel(x)) is { } level ? OurResources.PlayerNeed.FormatCurrentCulture(x.Title, level) : null)
				.OfType<string>()
				.ToList();
		}
	}

	/// <summary>
	/// The screen showing the location the player is in.
	/// </summary>
	public LocationScreenViewModel CurrentLocation
	{
		get => VerifyAccess(m_currentLocation);
		private set => SetPropertyField([nameof(CurrentLocation), nameof(CurrentView)], value, ref m_currentLocation);
	}

	/// <summary>
	/// The list of villagers, while it's open.
	/// </summary>
	public VillagersViewModel? Villagers
	{
		get => VerifyAccess(m_villagers);
		private set => SetPropertyField([nameof(Villagers), nameof(CurrentView)], value, ref m_villagers);
	}

	/// <summary>
	/// The player's status page, while it's open.
	/// </summary>
	public StatusViewModel? Status
	{
		get => VerifyAccess(m_status);
		private set => SetPropertyField([nameof(Status), nameof(CurrentView)], value, ref m_status);
	}

	/// <summary>
	/// The player's inventory or wardrobe, while it's open.
	/// </summary>
	public ClothingScreenViewModel? Clothing
	{
		get => VerifyAccess(m_clothing);
		private set => SetPropertyField([nameof(Clothing), nameof(CurrentView)], value, ref m_clothing);
	}

	/// <summary>
	/// The mini-map on the main screen, showing the player's quarter and the quarters connected to it.
	/// </summary>
	public TownMapViewModel MiniMap
	{
		get => VerifyAccess(m_miniMap);
		private set => SetPropertyField(value, ref m_miniMap);
	}

	/// <summary>
	/// The map of the town, while it's open.
	/// </summary>
	public TownMapViewModel? Map
	{
		get => VerifyAccess(m_map);
		private set => SetPropertyField([nameof(Map), nameof(CurrentView)], value, ref m_map);
	}

	/// <summary>
	/// The view shown in the main part of the window: the list of villagers, the map, the player's status, inventory, or
	/// wardrobe while one is open, or else the player's location.
	/// </summary>
	public ViewModelBase CurrentView => VerifyAccess((ViewModelBase?) m_villagers ?? (ViewModelBase?) m_map ?? (ViewModelBase?) m_status ?? (ViewModelBase?) m_clothing ?? m_currentLocation);

	/// <summary>
	/// The view focused on one person, shown on top of <see cref="CurrentView"/>, or null if it isn't open. Closing it shows
	/// the view it was opened from again.
	/// </summary>
	public CharacterViewModel? Character
	{
		get => VerifyAccess(m_character);
		private set => SetPropertyField([nameof(Character), nameof(Overlay)], value, ref m_character);
	}

	/// <summary>
	/// For developers, someone's needs, shown on top of their character view, or null if they aren't shown. Closing it
	/// shows the character view again, and if any of the person's needs were changed, they choose a new goal.
	/// </summary>
	public StatusViewModel? CharacterStatus
	{
		get => VerifyAccess(m_characterStatus);
		private set => SetPropertyField([nameof(CharacterStatus), nameof(Overlay)], value, ref m_characterStatus);
	}

	/// <summary>
	/// What's shown on top of <see cref="CurrentView"/>: someone asking the player to chat, someone's needs, their character view,
	/// or a conversation, or null if nothing is.
	/// </summary>
	public ViewModelBase? Overlay => VerifyAccess((ViewModelBase?) m_talkPrompt ?? (ViewModelBase?) m_characterStatus ?? (ViewModelBase?) m_character ?? m_conversation);

	/// <summary>
	/// Someone asking the player to chat, shown on top of everything until the player answers, or null if no one is.
	/// </summary>
	public TalkPromptViewModel? TalkPrompt
	{
		get => VerifyAccess(m_talkPrompt);
		private set => SetPropertyField([nameof(TalkPrompt), nameof(Overlay)], value, ref m_talkPrompt);
	}

	/// <summary>
	/// The conversation the player is having with someone in their location, shown on top of it, or null if there isn't one.
	/// </summary>
	public ConversationViewModel? Conversation
	{
		get => VerifyAccess(m_conversation);
		private set => SetPropertyField([nameof(Conversation), nameof(Overlay)], value, ref m_conversation);
	}

	/// <summary>
	/// Starts a conversation with someone in the player's location, replacing any other.
	/// </summary>
	public void ShowConversation(Person person)
	{
		VerifyAccess();
		CloseConversation();
		var conversation = new ConversationViewModel(person, World);
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
	/// Opens the player's own character view from the sidebar, or closes it if it's already open.
	/// </summary>
	public void TogglePlayerCharacter()
	{
		if (m_character?.Person.Person == World.Player)
			CloseCharacter();
		else
			ShowCharacter(World.Player);
	}

	/// <summary>
	/// Opens a view focused on a person on top of the current view, replacing any other person's.
	/// </summary>
	public void ShowCharacter(Person person)
	{
		VerifyAccess();
		CloseCharacter();
		var character = new CharacterViewModel(person, World, IsDeveloper);
		character.CloseRequested += OnCharacterCloseRequested;
		character.StatusRequested += OnCharacterStatusRequested;
		Character = character;
	}

	/// <summary>
	/// For developers, shows someone's needs on top of their character view.
	/// </summary>
	public void ShowCharacterStatus(Person person)
	{
		VerifyAccess();
		CloseCharacterStatus();
		var status = new StatusViewModel(World, person, IsDeveloper);
		status.CloseRequested += OnCharacterStatusCloseRequested;
		CharacterStatus = status;
	}

	/// <summary>
	/// Closes someone's needs, showing their character view again. If any of their needs were changed, they choose a new
	/// goal.
	/// </summary>
	public void CloseCharacterStatus()
	{
		VerifyAccess();
		if (m_characterStatus is { } status)
		{
			status.CloseRequested -= OnCharacterStatusCloseRequested;
			CharacterStatus = null;
			if (status.IsChanged)
				World.CancelGoal(status.Person);
		}
	}

	/// <summary>
	/// Closes the view focused on a person, showing the view it was opened from again.
	/// </summary>
	public void CloseCharacter()
	{
		VerifyAccess();
		CloseCharacterStatus();
		if (m_character is { } character)
		{
			character.CloseRequested -= OnCharacterCloseRequested;
			character.StatusRequested -= OnCharacterStatusRequested;
			Character = null;
		}
	}

	/// <summary>
	/// Opens the player's status page from the sidebar, or closes it if it's already open and nothing is on top of it.
	/// </summary>
	public void ToggleStatus()
	{
		if (m_status is null || Overlay is not null)
			ShowStatus();
		else
			CloseStatus();
	}

	/// <summary>
	/// Opens the player's status page, showing their needs, in place of the player's location.
	/// </summary>
	public void ShowStatus()
	{
		VerifyAccess();
		CloseCharacter();
		CloseConversation();
		CloseVillagers();
		CloseMap();
		if (TryCloseClothing() && m_status is null)
		{
			var status = new StatusViewModel(World, IsDeveloper);
			status.CloseRequested += OnStatusCloseRequested;
			Status = status;
		}
	}

	/// <summary>
	/// Closes the player's status page, returning to the player's location.
	/// </summary>
	public void CloseStatus()
	{
		VerifyAccess();
		if (m_status is { } status)
		{
			status.CloseRequested -= OnStatusCloseRequested;
			Status = null;
		}
	}

	/// <summary>
	/// Opens the list of villagers from the sidebar, or closes it if it's already open and nothing is on top of it.
	/// </summary>
	public void ToggleVillagers()
	{
		if (m_villagers is null || Overlay is not null)
			ShowVillagers();
		else
			CloseVillagers();
	}

	/// <summary>
	/// Opens the list of villagers, grouped by family, in place of the player's location.
	/// </summary>
	public void ShowVillagers()
	{
		VerifyAccess();
		CloseCharacter();
		CloseConversation();
		CloseMap();
		CloseStatus();
		if (TryCloseClothing() && m_villagers is null)
		{
			var villagers = new VillagersViewModel(World.Families, IsDeveloper, World.GameData.PortraitRenderer, World.GameData.Schedules, World.GameData.Relationships, World.GameData.Calendar);
			villagers.CloseRequested += OnVillagersCloseRequested;
			villagers.TravelRequested += OnVillagersTravelRequested;
			villagers.ExamineRequested += OnExamineRequested;
			Villagers = villagers;
		}
	}

	/// <summary>
	/// Closes the list of villagers, returning to the player's location.
	/// </summary>
	public void CloseVillagers()
	{
		VerifyAccess();
		if (m_villagers is { } villagers)
		{
			villagers.CloseRequested -= OnVillagersCloseRequested;
			villagers.TravelRequested -= OnVillagersTravelRequested;
			villagers.ExamineRequested -= OnExamineRequested;
			Villagers = null;
		}
	}

	/// <summary>
	/// Opens the map from the sidebar, or closes it if it's already open and nothing is on top of it.
	/// </summary>
	public void ToggleMap()
	{
		if (m_map is null || Overlay is not null)
			ShowMap();
		else
			CloseMap();
	}

	/// <summary>
	/// Opens the map of the town in place of the player's location. Developers can travel by choosing a quarter on it.
	/// </summary>
	public void ShowMap()
	{
		VerifyAccess();
		CloseCharacter();
		CloseConversation();
		if (m_map is null && TryCloseClothing())
		{
			CloseVillagers();
			CloseStatus();
			var map = new TownMapViewModel(World.Locations, World.Player.Location, World.Features, canTravel: IsDeveloper, World.Player.Home, World.Player.Business is null ? null : World.Player.Workplace);
			map.CloseRequested += OnMapCloseRequested;
			map.TravelRequested += OnMapTravelRequested;
			Map = map;
		}
	}

	/// <summary>
	/// Closes the map, returning to the player's location.
	/// </summary>
	public void CloseMap()
	{
		VerifyAccess();
		if (m_map is { } map)
		{
			map.CloseRequested -= OnMapCloseRequested;
			map.TravelRequested -= OnMapTravelRequested;
			Map = null;
		}
	}

	/// <summary>
	/// Opens the player's inventory from the sidebar, or closes it if it's already open and nothing is on top of it.
	/// </summary>
	public void ToggleInventory() => ToggleClothing(isWardrobe: false);

	/// <summary>
	/// Opens the player's wardrobe from the sidebar, or closes it if it's already open and nothing is on top of it.
	/// </summary>
	public void ToggleWardrobe() => ToggleClothing(isWardrobe: true);

	/// <summary>
	/// The wardrobe can only be opened at home.
	/// </summary>
	public bool CanToggleWardrobe() => World.Player.Location == World.Player.Home;

	/// <summary>
	/// Opens the player's inventory or wardrobe in place of the player's location, replacing the other one.
	/// </summary>
	public void ShowClothing(bool isWardrobe)
	{
		VerifyAccess();
		CloseCharacter();
		CloseConversation();
		CloseVillagers();
		CloseMap();
		CloseStatus();
		if (m_clothing?.IsWardrobe != isWardrobe && TryCloseClothing())
		{
			var clothing = new ClothingScreenViewModel(World, isWardrobe, IsDeveloper);
			clothing.CloseRequested += OnClothingCloseRequested;
			Clothing = clothing;
		}
	}

	/// <summary>
	/// Closes the player's inventory or wardrobe, returning to the player's location, which takes the time to change clothes if they changed
	/// what they're wearing.
	/// </summary>
	public void CloseClothing()
	{
		VerifyAccess();
		if (m_clothing is { } clothing)
		{
			clothing.CloseRequested -= OnClothingCloseRequested;
			Clothing = null;

			// Changing clothes takes its time once the player is done.
			if (clothing.Finish() is { } result)
				m_currentLocation.ShowWaitResult(result);
		}
	}

	/// <summary>
	/// Closes the player's inventory or wardrobe, if it's open, unless they aren't dressed enough to leave it, in which case
	/// it stays open, still warning them. Returns whether it's closed.
	/// </summary>
	private bool TryCloseClothing()
	{
		var canClose = m_clothing?.CanClose() ?? true;
		if (canClose)
			CloseClothing();
		return canClose;
	}

	private void ToggleClothing(bool isWardrobe)
	{
		if (m_clothing?.IsWardrobe != isWardrobe || Overlay is not null)
			ShowClothing(isWardrobe);
		else
			TryCloseClothing();
	}

	/// <summary>
	/// Whether the last save worked, or null if the game hasn't been saved since the window opened.
	/// </summary>
	public string? SaveStatus
	{
		get => VerifyAccess(m_saveStatus);
		private set => SetPropertyField(value, ref m_saveStatus);
	}

	// Games can't be saved without knowing where saves are kept.
	public bool CanSaveGame() => m_saveSettings is not null;

	/// <summary>
	/// Saves the game to a new file in the save folder.
	/// </summary>
	public void SaveGame()
	{
		VerifyAccess();
		if (m_saveSettings is { } settings)
		{
			var save = SaveFile.Create(World, settings, DateTimeOffset.Now);
			var filePath = Path.Combine(settings.Directory, save.GetFileName());
			try
			{
				save.Write(filePath);
				Log.Info($"Saved the game to {filePath}");
				SaveStatus = OurResources.GameSaved;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				Log.Error($"Couldn't save the game to {filePath}: {e.Message}");
				SaveStatus = OurResources.GameSaveFailed.FormatCurrentCulture(e.Message);
			}
		}
	}

	/// <summary>
	/// Moves the player to a location next to theirs, which takes the travel time between them. They can't leave home unless
	/// they're dressed enough to go out.
	/// </summary>
	public void TravelTo(Location location)
	{
		VerifyAccess();
		var player = World.Player;
		var warning = player.Location == player.Home && location != player.Home ? ModestyWarning.Format(World.GetPlayerUncoveredSlotsOutside()) : null;
		if (warning is not null)
		{
			m_currentLocation.ShowWaitResult(warning);
		}
		else
		{
			World.AdvanceTime(World.GetTravelTime(player.Location, location));

			// Someone asking the player to chat on the way stops them where they are.
			if (player.TalkRequest is null)
				MoveTo(location);
		}
	}

	/// <summary>
	/// Moves the player straight to any location without any time passing, for developers.
	/// </summary>
	private void JumpTo(Location location) => MoveTo(location);

	/// <summary>
	/// Moves the player to a location and shows it, still following the given person, if any.
	/// </summary>
	private void MoveTo(Location location, Person? following = null)
	{
		var player = World.Player;
		Log.Info($"Traveling from {player.Location.Title} ({player.Location.Id}) to {location.Title} ({location.Id})");

		World.MovePlayer(location);
		SaveStatus = null;
		CloseCharacter();
		CloseConversation();
		m_currentLocation.TravelRequested -= OnTravelRequested;
		m_currentLocation.Followed -= OnFollowed;
		m_currentLocation.ExamineRequested -= OnExamineRequested;
		m_currentLocation.TalkRequested -= OnTalkRequested;
		CurrentLocation = CreateLocationScreen(location, following);
		MiniMap = CreateMiniMap(location);
	}

	public void Dispose()
	{
		World.PropertyChanged -= OnWorldPropertyChanged;
		World.Player.PropertyChanged -= OnPlayerPropertyChanged;
		if (m_talkPrompt is not null)
		{
			m_talkPrompt.Accepted -= OnTalkPromptAccepted;
			m_talkPrompt.Declined -= OnTalkPromptDeclined;
		}
		m_currentLocation.TravelRequested -= OnTravelRequested;
		m_currentLocation.Followed -= OnFollowed;
		m_currentLocation.ExamineRequested -= OnExamineRequested;
		m_currentLocation.TalkRequested -= OnTalkRequested;
		if (m_villagers is not null)
		{
			m_villagers.CloseRequested -= OnVillagersCloseRequested;
			m_villagers.TravelRequested -= OnVillagersTravelRequested;
			m_villagers.ExamineRequested -= OnExamineRequested;
		}
		if (m_map is not null)
		{
			m_map.CloseRequested -= OnMapCloseRequested;
			m_map.TravelRequested -= OnMapTravelRequested;
		}
		if (m_status is not null)
			m_status.CloseRequested -= OnStatusCloseRequested;
		if (m_clothing is not null)
			m_clothing.CloseRequested -= OnClothingCloseRequested;
		if (m_character is not null)
		{
			m_character.CloseRequested -= OnCharacterCloseRequested;
			m_character.StatusRequested -= OnCharacterStatusRequested;
		}
		if (m_characterStatus is not null)
			m_characterStatus.CloseRequested -= OnCharacterStatusCloseRequested;
		if (m_conversation is not null)
			m_conversation.CloseRequested -= OnConversationCloseRequested;
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	private LocationScreenViewModel CreateLocationScreen(Location location, Person? following = null)
	{
		var viewModel = new LocationScreenViewModel(location, World, following, IsDeveloper);
		viewModel.TravelRequested += OnTravelRequested;
		viewModel.Followed += OnFollowed;
		viewModel.ExamineRequested += OnExamineRequested;
		viewModel.TalkRequested += OnTalkRequested;
		return viewModel;
	}

	private TownMapViewModel CreateMiniMap(Location location) => TownMapViewModel.CreateMiniMap(location, World.Features, MiniMapSize, World.Player.Home, World.Player.Business is null ? null : World.Player.Workplace);

	private void OnTravelRequested(object? sender, GenericEventArgs<Location> e) => TravelTo(e.Value);

	/// <summary>
	/// Shows where the player followed someone to, still following them.
	/// </summary>
	private void OnFollowed(object? sender, GenericEventArgs<Location> e)
	{
		var following = m_currentLocation.Following;
		MoveTo(e.Value, following);
		if (following is not null)
			m_currentLocation.ShowWaitResult(OurResources.WaitResultFollowed.FormatCurrentCulture(following.FullName, e.Value.Title));
	}

	private void OnWorldPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(GameWorld.Time))
			RaisePropertyChanged(nameof(Date), nameof(TimeOfDay), nameof(SpecialDay), nameof(PlayerNeeds));
	}

	private void OnVillagersCloseRequested(object? sender, EventArgs e) => CloseVillagers();

	private void OnVillagersTravelRequested(object? sender, GenericEventArgs<Location> e)
	{
		CloseVillagers();
		JumpTo(e.Value);
	}

	private void OnMapCloseRequested(object? sender, EventArgs e) => CloseMap();

	private void OnStatusCloseRequested(object? sender, EventArgs e) => CloseStatus();

	private void OnClothingCloseRequested(object? sender, EventArgs e) => CloseClothing();

	private void OnCharacterCloseRequested(object? sender, EventArgs e) => CloseCharacter();

	private void OnCharacterStatusRequested(object? sender, GenericEventArgs<Person> e) => ShowCharacterStatus(e.Value);

	private void OnCharacterStatusCloseRequested(object? sender, EventArgs e) => CloseCharacterStatus();

	private void OnExamineRequested(object? sender, GenericEventArgs<Person> e) => ShowCharacter(e.Value);

	private void OnTalkRequested(object? sender, GenericEventArgs<Person> e) => ShowConversation(e.Value);

	private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(Person.TalkRequest) or null or "")
			UpdateTalkPrompt();
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
	}

	private void OnTalkPromptAccepted(object? sender, EventArgs e)
	{
		if (World.Player.TalkRequest is { } asker)
		{
			var start = World.Time;
			var passed = World.AcceptTalk();
			m_currentLocation.ShowWaitResult(passed is { } time
				? OurResources.TalkPromptChatted.FormatCurrentCulture(asker.FullName, World.GameData.Calendar.FormatOffsetFrom(start, time, TimeFormat.Long))
				: OurResources.TalkPromptGone.FormatCurrentCulture(asker.FullName));
		}
	}

	private void OnTalkPromptDeclined(object? sender, EventArgs e) => World.DeclineTalk();

	private void OnConversationCloseRequested(object? sender, EventArgs e) => CloseConversation();

	private void OnMapTravelRequested(object? sender, GenericEventArgs<Location> e)
	{
		CloseMap();
		JumpTo(e.Value);
	}

	/// <summary>
	/// The most room the mini-map takes, in each direction, in device-independent pixels.
	/// </summary>
	private const double MiniMapSize = 196;

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(MainWindowViewModel));

	private readonly SaveSettings? m_saveSettings;
	private TaskGroup m_taskGroup;
	private LocationScreenViewModel m_currentLocation;
	private VillagersViewModel? m_villagers;
	private TownMapViewModel? m_map;
	private StatusViewModel? m_status;
	private ClothingScreenViewModel? m_clothing;
	private CharacterViewModel? m_character;
	private StatusViewModel? m_characterStatus;
	private ConversationViewModel? m_conversation;
	private TalkPromptViewModel? m_talkPrompt;
	private TownMapViewModel m_miniMap;
	private string? m_saveStatus;
}
