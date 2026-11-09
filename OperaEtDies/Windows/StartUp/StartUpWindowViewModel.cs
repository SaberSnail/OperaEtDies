using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Data;

namespace OperaEtDies.Windows.StartUp;

public sealed class StartUpWindowViewModel : ViewModelBase, IDisposable
{
	public StartUpWindowViewModel(GameInfo? gameInfo, GameData? gameData, Random random, IReadOnlyList<DataLoadError> loadErrors)
	{
		m_taskGroup = new TaskGroup();
		Title = gameInfo?.Title ?? OurResources.EngineName;
		Description = gameInfo?.Description ?? "";
		m_hasGameInfo = gameInfo is not null;
		m_gameData = gameData;
		m_random = random;
		LoadErrors = loadErrors.Select(x => x.ToString()).ToList();
	}

	public event EventHandler<GenericEventArgs<NewGameSettings>>? StartNewGameRequested;

	public event EventHandler? ExitGameRequested;

	public string Title { get; }

	public string Description { get; }

	public IReadOnlyList<string> LoadErrors { get; }

	public bool HasLoadErrors => LoadErrors.Count != 0;

	/// <summary>
	/// The new game configuration being shown in place of the start up menu, or null if the menu is showing.
	/// </summary>
	public NewGameViewModel? NewGame
	{
		get => VerifyAccess(m_newGame);
		private set => SetPropertyField(value, ref m_newGame);
	}

	// A new game can't be started without the game data.
	public bool CanShowNewGame() => m_hasGameInfo && m_gameData is not null;

	public void ShowNewGame()
	{
		VerifyAccess();
		if (!CanShowNewGame() || m_newGame is not null)
			return;

		var newGame = new NewGameViewModel(m_gameData!, m_random);
		newGame.StartGameRequested += OnStartGameRequested;
		newGame.BackRequested += OnBackRequested;
		NewGame = newGame;
	}

	// Saving and loading aren't implemented yet.
	// MethodToCommandConverter only finds instance methods, so this can't be static.
#pragma warning disable CA1822 // Mark members as static
	public bool CanLoadGame() => false;
#pragma warning restore CA1822 // Mark members as static

	public void LoadGame()
	{
		VerifyAccess();
	}

	public void ExitGame()
	{
		VerifyAccess();
		ExitGameRequested.Raise(this);
	}

	public void Dispose()
	{
		CloseNewGame();
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	private void CloseNewGame()
	{
		var newGame = m_newGame;
		if (newGame is null)
			return;

		NewGame = null;
		newGame.StartGameRequested -= OnStartGameRequested;
		newGame.BackRequested -= OnBackRequested;
	}

	private void OnStartGameRequested(object? sender, GenericEventArgs<NewGameSettings> e)
	{
		Log.Info("Starting new game");
		StartNewGameRequested.Raise(this, e);
	}

	private void OnBackRequested(object? sender, EventArgs e) => CloseNewGame();

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(StartUpWindowViewModel));

	private readonly bool m_hasGameInfo;
	private readonly GameData? m_gameData;
	private readonly Random m_random;
	private NewGameViewModel? m_newGame;
	private TaskGroup m_taskGroup;
}
