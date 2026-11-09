using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using OperaEtDies.Controls.Locations;
using OperaEtDies.GameState;

namespace OperaEtDies.Windows.Main;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
	public MainWindowViewModel(string title, GameStateViewModel gameState)
	{
		m_taskGroup = new TaskGroup();
		Title = title;
		GameState = gameState;
		m_currentLocation = CreateLocationScreen(gameState.Player.Location);
	}

	public string Title { get; }

	/// <summary>
	/// The state of the game world.
	/// </summary>
	public GameStateViewModel GameState { get; }

	/// <summary>
	/// The screen showing the location the player is in.
	/// </summary>
	public LocationScreenViewModel CurrentLocation
	{
		get => VerifyAccess(m_currentLocation);
		private set => SetPropertyField(value, ref m_currentLocation);
	}

	/// <summary>
	/// Moves the player to the given location.
	/// </summary>
	public void TravelTo(LocationViewModel location)
	{
		VerifyAccess();
		var player = GameState.Player;
		Log.Info($"Traveling from {player.Location.Id} to {location.Id}");

		player.Location = location;
		m_currentLocation.TravelRequested -= OnTravelRequested;
		CurrentLocation = CreateLocationScreen(location);
	}

	public void Dispose()
	{
		m_currentLocation.TravelRequested -= OnTravelRequested;
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	private LocationScreenViewModel CreateLocationScreen(LocationViewModel location)
	{
		var viewModel = new LocationScreenViewModel(location);
		viewModel.TravelRequested += OnTravelRequested;
		return viewModel;
	}

	private void OnTravelRequested(object? sender, GenericEventArgs<LocationViewModel> e) => TravelTo(e.Value);

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(MainWindowViewModel));

	private TaskGroup m_taskGroup;
	private LocationScreenViewModel m_currentLocation;
}
