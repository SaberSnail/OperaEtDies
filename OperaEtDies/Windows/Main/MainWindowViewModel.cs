using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using OperaEtDies.Characters;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Data;

namespace OperaEtDies.Windows.Main;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
	public MainWindowViewModel(GameInfo gameInfo, GameData gameData, NewGameSettings newGameSettings, Random random)
	{
		m_taskGroup = new TaskGroup();
		Title = gameInfo.Title;
		GameData = gameData;
		NewGameSettings = newGameSettings;
		Npcs = NpcGenerator.Generate(gameData, random, NpcCount, [newGameSettings.Player.FullName]);

		// The locations file is validated on load, so the starting location always exists.
		m_playerLocation = gameData.Locations.GetLocation(gameData.Locations.StartingLocation)!;
		m_currentLocation = CreateLocationViewModel(m_playerLocation);
	}

	public string Title { get; }

	public GameData GameData { get; }

	public NewGameSettings NewGameSettings { get; }

	/// <summary>
	/// The characters in the game world, other than the player.
	/// </summary>
	public IReadOnlyList<Character> Npcs { get; }

	/// <summary>
	/// The location the player is in.
	/// </summary>
	public Location PlayerLocation => VerifyAccess(m_playerLocation);

	/// <summary>
	/// The view of the location the player is in.
	/// </summary>
	public LocationViewModel CurrentLocation
	{
		get => VerifyAccess(m_currentLocation);
		private set => SetPropertyField(value, ref m_currentLocation);
	}

	/// <summary>
	/// Moves the player to the given location.
	/// </summary>
	public void TravelTo(Location location)
	{
		VerifyAccess();
		Log.Info($"Traveling from {m_playerLocation.Id} to {location.Id}");

		m_currentLocation.TravelRequested -= OnTravelRequested;
		SetPropertyField(nameof(PlayerLocation), location, ref m_playerLocation);
		CurrentLocation = CreateLocationViewModel(location);
	}

	public void Dispose()
	{
		m_currentLocation.TravelRequested -= OnTravelRequested;
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	private LocationViewModel CreateLocationViewModel(Location location)
	{
		var viewModel = new LocationViewModel(location, GameData.Locations.GetConnections(location), Npcs.Where(x => x.Location == location).ToList());
		viewModel.TravelRequested += OnTravelRequested;
		return viewModel;
	}

	private void OnTravelRequested(object? sender, GenericEventArgs<Location> e) => TravelTo(e.Value);

	// Until villagers are defined in data, a few random ones are generated.
	private const int NpcCount = 3;

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(MainWindowViewModel));

	private TaskGroup m_taskGroup;
	private Location m_playerLocation;
	private LocationViewModel m_currentLocation;
}
