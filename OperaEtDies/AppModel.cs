using System.Windows.Media;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Mods;
using OperaEtDies.Windows.Main;
using OperaEtDies.Windows.StartUp;

namespace OperaEtDies;

// The task group is disposed in ShutdownAsync rather than through IDisposable, since this is an app-lifetime singleton.
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
public sealed class AppModel : ViewModelBase
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
	public static AppModel Instance => s_appModel.Value;

	private AppModel()
	{
		m_taskGroup = new TaskGroup();
		LogManager.Initialize(new DebugLogDestination());
		m_currentTheme = new Uri(@"/Themes/Default/Default.xaml", UriKind.Relative);
		Random = new CryptoRandom();
		m_gameInfoResult = DataLoadResult<GameInfo>.Failure([]);
		m_gameDataResult = DataLoadResult<GameData>.Failure([]);
	}

	/// <summary>
	/// Raised when the application should shut down.
	/// </summary>
	public event EventHandler? ExitRequested;

	public StartUpWindowViewModel? StartUpWindow
	{
		get => VerifyAccess(m_startUpWindow);
		private set => SetPropertyField(value, ref m_startUpWindow);
	}

	public MainWindowViewModel? MainWindow
	{
		get => VerifyAccess(m_mainWindow);
		private set => SetPropertyField(value, ref m_mainWindow);
	}

	public GameInfo? GameInfo => VerifyAccess(m_gameInfoResult.Value);

	public GameData? GameData => VerifyAccess(m_gameDataResult.Value);

	/// <summary>
	/// The application icon from the game data, or null if there isn't one.
	/// </summary>
	public ImageSource? Icon => VerifyAccess(m_icon);

	public TaskGroup TaskGroup => m_taskGroup;

	public Random Random { get; }

	public Uri CurrentTheme
	{
		get
		{
			VerifyAccess();
			return m_currentTheme;
		}
		set
		{
			if (SetPropertyField(value, ref m_currentTheme))
				Log.Info($"Changing theme to \"{m_currentTheme.OriginalString}\"");
		}
	}

	/// <summary>
	/// Loads the game data and opens the start up window. Mods aren't loaded if they're turned off, and developer UI is
	/// only shown if it's turned on.
	/// </summary>
	public async Task StartupAsync(TaskStateController state, bool areModsEnabled, bool isDeveloper)
	{
		m_areModsEnabled = areModsEnabled;
		m_isDeveloper = isDeveloper;
		if (isDeveloper)
			Log.Info("Developer UI is turned on");
		if (!areModsEnabled)
			Log.Info("Mods are turned off");

		await LoadDataAsync(state);
		OpenStartUpWindow();
	}

	/// <summary>
	/// Replaces the start up window with the main window.
	/// </summary>
	public void StartNewGame(NewGameSettings settings)
	{
		VerifyAccess();

		var gameInfo = m_gameInfoResult.Value;
		var gameData = m_gameDataResult.Value;
		if (gameInfo is null || gameData is null)
		{
			Log.Error("Can't start a new game because the game data failed to load");
			return;
		}

		Log.Info($"Starting new game as {settings.Player.GetFullName(gameData.CharacterTemplates)}");

		var gameState = GameStateViewModel.CreateNewGame(gameData, settings.Player, Random);

		// Open the main window before closing the start up window, so there's always a window showing.
		MainWindow = new MainWindowViewModel(gameInfo.Title, gameState, m_isDeveloper);
		CloseStartUpWindow();
	}

	/// <summary>
	/// Replaces the main window with the start up window.
	/// </summary>
	public void EndGame()
	{
		VerifyAccess();

		OpenStartUpWindow();
		CloseMainWindow();
	}

	public void ExitGame()
	{
		VerifyAccess();
		ExitRequested.Raise(this);
	}

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
	public async Task ShutdownAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
	{
		CloseStartUpWindow();
		CloseMainWindow();
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	/// <summary>
	/// Loads the game content from the default data folder, with the files from each mod folder in the mod list applied
	/// in order if mods are turned on. The mod list is kept under the game's title, so it's found using the game info
	/// without mods (mods can't change the title). The mod list is null if the game info without mods didn't load.
	/// </summary>
	internal static (GameContent Content, DataLoadResult<GameInfo> BaseGameInfoResult, IModListStore? ModListStore) LoadContent(bool areModsEnabled)
	{
		var dataDirectory = JsonDataLoader.DefaultDataDirectory;
		var baseGameInfoResult = GameInfo.Load(dataDirectory);
		var modListStore = baseGameInfoResult.Value is { } baseGameInfo ? new RegistryModListStore(baseGameInfo.Title) : null;
		var modDirectories = areModsEnabled && modListStore is not null ? modListStore.Load() : [];
		if (modDirectories.Count != 0)
			Log.Info($"Loading mods from {string.Join(", ", modDirectories.Select(x => $"\"{x}\""))}");

		return (GameContent.Load(dataDirectory, modDirectories, baseGameInfoResult), baseGameInfoResult, modListStore);
	}

	/// <summary>
	/// Loads the game data, and the icon from the game info without mods.
	/// </summary>
	private async Task LoadDataAsync(TaskStateController state)
	{
		await state.ToThreadPool();

		var (content, baseGameInfoResult, modListStore) = LoadContent(m_areModsEnabled);
		var gameInfoResult = content.GameInfoResult;
		var gameDataResult = content.GameDataResult;

		var iconPath = baseGameInfoResult.Value?.GetIconPath(JsonDataLoader.DefaultDataDirectory);
		var iconReadResult = iconPath is null ? null : ImageDataLoader.Read(iconPath);

		await state.ToSyncContext();

		// Images belong to the thread that decodes them, so the icon is decoded here rather than on the thread pool.
		var iconResult = iconReadResult?.Value is { } iconContents ? ImageDataLoader.Decode(iconPath!, iconContents) : null;

		using (ScopedPropertyChange(nameof(GameInfo), nameof(GameData), nameof(Icon)))
		{
			m_gameInfoResult = gameInfoResult;
			m_gameDataResult = gameDataResult;
			m_icon = iconResult?.Value;
			m_iconErrors = iconResult?.Errors ?? iconReadResult?.Errors ?? [];
		}

		m_modListStore = modListStore;
	}

	private IReadOnlyList<DataLoadError> GetLoadErrors() => [.. m_gameInfoResult.Errors, .. m_iconErrors, .. m_gameDataResult.Errors];

	private async Task ReloadDataAsync(TaskStateController state)
	{
		await LoadDataAsync(state);
		m_startUpWindow?.SetData(m_gameInfoResult.Value, m_gameDataResult.Value, GetLoadErrors(), m_modListStore);
	}

	private void OpenStartUpWindow()
	{
		if (m_startUpWindow is not null)
			return;

		var startUpWindow = new StartUpWindowViewModel(m_gameInfoResult.Value, m_gameDataResult.Value, Random, GetLoadErrors(), m_modListStore, new FolderPicker(), m_areModsEnabled, m_isDeveloper);
		startUpWindow.StartNewGameRequested += OnStartNewGameRequested;
		startUpWindow.ExitGameRequested += OnExitGameRequested;
		startUpWindow.ReloadDataRequested += OnReloadDataRequested;
		StartUpWindow = startUpWindow;
	}

	private void CloseStartUpWindow()
	{
		var startUpWindow = m_startUpWindow;
		if (startUpWindow is null)
			return;

		StartUpWindow = null;
		startUpWindow.StartNewGameRequested -= OnStartNewGameRequested;
		startUpWindow.ExitGameRequested -= OnExitGameRequested;
		startUpWindow.ReloadDataRequested -= OnReloadDataRequested;
		startUpWindow.Dispose();
	}

	private void CloseMainWindow()
	{
		var mainWindow = m_mainWindow;
		if (mainWindow is null)
			return;

		MainWindow = null;
		mainWindow.Dispose();
	}

	private void OnStartNewGameRequested(object? sender, GenericEventArgs<NewGameSettings> e) => StartNewGame(e.Value);

	private void OnExitGameRequested(object? sender, EventArgs e) => ExitGame();

	private void OnReloadDataRequested(object? sender, EventArgs e) => TaskWatcher.Execute(ReloadDataAsync, m_taskGroup);

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(AppModel));
	private static readonly Lazy<AppModel> s_appModel = new(() => new AppModel());

	private StartUpWindowViewModel? m_startUpWindow;
	private MainWindowViewModel? m_mainWindow;
	private DataLoadResult<GameInfo> m_gameInfoResult;
	private DataLoadResult<GameData> m_gameDataResult;
	private ImageSource? m_icon;
	private IReadOnlyList<DataLoadError> m_iconErrors = [];
	private IModListStore? m_modListStore;
	private bool m_areModsEnabled = true;
	private bool m_isDeveloper;
	private Uri m_currentTheme;
	private TaskGroup m_taskGroup;
}
