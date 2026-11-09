using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Data;

namespace OperaEtDies.Windows.Main;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
	public MainWindowViewModel(GameInfo gameInfo, GameData gameData, NewGameSettings newGameSettings)
	{
		m_taskGroup = new TaskGroup();
		Title = gameInfo.Title;
		GameData = gameData;
		NewGameSettings = newGameSettings;

		// The locations file is validated on load, so the starting location always exists.
		m_playerLocation = gameData.Locations.GetLocation(gameData.Locations.StartingLocation)!;
		m_currentLocation = CreateLocationViewModel(m_playerLocation);
	}

	public string Title { get; }

	public GameData GameData { get; }

	public NewGameSettings NewGameSettings { get; }

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
		m_playerLocation = location;
		RaisePropertyChanged(nameof(PlayerLocation));
		CurrentLocation = CreateLocationViewModel(location);
	}

	public void Dispose()
	{
		m_currentLocation.TravelRequested -= OnTravelRequested;
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	private LocationViewModel CreateLocationViewModel(Location location)
	{
		var viewModel = new LocationViewModel(location, GameData.Locations.GetConnections(location));
		viewModel.TravelRequested += OnTravelRequested;
		return viewModel;
	}

	private void OnTravelRequested(object? sender, GenericEventArgs<Location> e) => TravelTo(e.Value);

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(MainWindowViewModel));

	private TaskGroup m_taskGroup;
	private Location m_playerLocation;
	private LocationViewModel m_currentLocation;
}
