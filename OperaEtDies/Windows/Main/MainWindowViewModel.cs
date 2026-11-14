using System.ComponentModel;
using System.IO;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
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
	/// Today's date, such as "Thursday, Mid Winter, 227 CR".
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
	/// The view shown in the main part of the window: the list of villagers, the map, or the player's status while one
	/// is open, or else the player's location.
	/// </summary>
	public ViewModelBase CurrentView => VerifyAccess((ViewModelBase?) m_villagers ?? (ViewModelBase?) m_map ?? (ViewModelBase?) m_status ?? m_currentLocation);

	/// <summary>
	/// Opens the player's status page from the sidebar, or closes it if it's already open.
	/// </summary>
	public void ToggleStatus()
	{
		if (m_status is null)
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
		CloseVillagers();
		CloseMap();
		if (m_status is null)
		{
			var status = new StatusViewModel(World);
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
	/// Opens the list of villagers from the sidebar, or closes it if it's already open.
	/// </summary>
	public void ToggleVillagers()
	{
		if (m_villagers is null)
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
		CloseMap();
		CloseStatus();
		if (m_villagers is null)
		{
			var villagers = new VillagersViewModel(World.Families, IsDeveloper, World.GameData.PortraitRenderer, World.GameData.Schedules, World.GameData.Families, World.GameData.Calendar);
			villagers.CloseRequested += OnVillagersCloseRequested;
			villagers.TravelRequested += OnVillagersTravelRequested;
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
			Villagers = null;
		}
	}

	/// <summary>
	/// Opens the map from the sidebar, or closes it if it's already open.
	/// </summary>
	public void ToggleMap()
	{
		if (m_map is null)
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
		if (m_map is null)
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
	/// Moves the player to a location next to theirs, which takes the travel time between them.
	/// </summary>
	public void TravelTo(Location location)
	{
		VerifyAccess();
		World.AdvanceTime(World.GetTravelTime(World.Player.Location, location));
		MoveTo(location);
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

		player.Location = location;
		SaveStatus = null;
		m_currentLocation.TravelRequested -= OnTravelRequested;
		m_currentLocation.Followed -= OnFollowed;
		CurrentLocation = CreateLocationScreen(location, following);
		MiniMap = CreateMiniMap(location);
	}

	public void Dispose()
	{
		World.PropertyChanged -= OnWorldPropertyChanged;
		m_currentLocation.TravelRequested -= OnTravelRequested;
		m_currentLocation.Followed -= OnFollowed;
		if (m_villagers is not null)
		{
			m_villagers.CloseRequested -= OnVillagersCloseRequested;
			m_villagers.TravelRequested -= OnVillagersTravelRequested;
		}
		if (m_map is not null)
		{
			m_map.CloseRequested -= OnMapCloseRequested;
			m_map.TravelRequested -= OnMapTravelRequested;
		}
		if (m_status is not null)
			m_status.CloseRequested -= OnStatusCloseRequested;
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	private LocationScreenViewModel CreateLocationScreen(Location location, Person? following = null)
	{
		var viewModel = new LocationScreenViewModel(location, World, following, IsDeveloper);
		viewModel.TravelRequested += OnTravelRequested;
		viewModel.Followed += OnFollowed;
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
	private TownMapViewModel m_miniMap;
	private string? m_saveStatus;
}
