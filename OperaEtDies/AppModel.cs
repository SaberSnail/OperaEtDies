using System.Windows.Media;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Mods;
using OperaEtDies.Settings;
using OperaEtDies.Windows.Main;

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
		Random = new CryptoRandom();
		m_gameInfoResult = DataLoadResult<GameInfo>.Failure([]);
		m_gameDataResult = DataLoadResult<GameData>.Failure([]);
	}

	/// <summary>
	/// Raised when the application should shut down.
	/// </summary>
	public event EventHandler? ExitRequested;

	/// <summary>
	/// The game's one window, from start up until it shuts down.
	/// </summary>
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

	/// <summary>
	/// The theme the window is drawn in, or null for the built-in theme. It comes from the game data, or from the data
	/// without mods until the game data loads, or when it fails to load.
	/// </summary>
	public ThemeSettings? Theme
	{
		get => VerifyAccess(m_theme);
		private set => SetPropertyField(value, ref m_theme);
	}

	/// <summary>
	/// The player's settings, such as the text size, or null until the game starts up.
	/// </summary>
	public AppSettings? Settings
	{
		get => VerifyAccess(m_settings);
		private set => SetPropertyField(value, ref m_settings);
	}

	/// <summary>
	/// Opens the window, showing the main menu, and loads the game data. Mods aren't loaded if they're turned off, the
	/// given mod folders are loaded instead of the mod list if there are any, and developer UI is only shown if it's
	/// turned on. The kept settings are forgotten first if they're to be reset.
	/// </summary>
	public async Task StartupAsync(TaskStateController state, bool areModsEnabled, IReadOnlyList<string>? modDirectories, bool isDeveloper, bool resetSettings = false)
	{
		m_areModsEnabled = areModsEnabled;
		m_modDirectoriesOverride = modDirectories;
		m_isDeveloper = isDeveloper;
		if (isDeveloper)
			Log.Info("Developer UI is turned on");
		if (!areModsEnabled)
			Log.Info("Mods are turned off");
		else if (modDirectories is not null)
			Log.Info("Mods were chosen on the command line, instead of the mod list");

		// The window opens where it was last, which is kept under the game's title, so the game info is read first, along
		// with the theme the window is drawn in. The rest of the data loads while the window shows that it's loading.
		await state.ToThreadPool();
		var (baseGameInfoResult, baseTheme) = LoadBase(JsonDataLoader.DefaultDataDirectory);
		await state.ToSyncContext();
		Theme = baseTheme;
		var baseGameInfo = baseGameInfoResult.Value;
		WindowPlacementKeyPath = baseGameInfo is null ? null : GameRegistry.GetKeyPath(baseGameInfo.Title);

		// The settings are kept under the game's title too. Without it, they still work, but aren't kept.
		var settingsStore = baseGameInfo is null ? null : new RegistryAppSettingsStore(baseGameInfo.Title);
		if (resetSettings)
		{
			Log.Info("Resetting the settings");
			settingsStore?.Reset();
		}
		Settings = new AppSettings(settingsStore);
		OpenMainWindow();

		await LoadDataAsync(state);
		ShowData();
	}

	/// <summary>
	/// The registry key under HKEY_CURRENT_USER where the window keeps where it was, or null if there's nowhere to keep it.
	/// </summary>
	public string? WindowPlacementKeyPath { get; private set; }

	public void ExitGame()
	{
		VerifyAccess();
		ExitRequested.Raise(this);
	}

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
	public async Task ShutdownAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
	{
		CloseMainWindow();
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	/// <summary>
	/// Loads the game content from the default data folder, with the files from each mod folder in the mod list applied
	/// in order if mods are turned on. The given mod folders are loaded instead of the mod list if there are any. The mod
	/// list is kept under the game's title, so it's found using the game info without mods (mods can't change the title).
	/// The mod list is null if the game info without mods didn't load. The theme without mods is loaded too.
	/// </summary>
	internal static (GameContent Content, DataLoadResult<GameInfo> BaseGameInfoResult, ThemeSettings? BaseTheme, IModListStore? ModListStore) LoadContent(bool areModsEnabled, IReadOnlyList<string>? modDirectoriesOverride = null)
	{
		var dataDirectory = JsonDataLoader.DefaultDataDirectory;
		var (baseGameInfoResult, baseTheme) = LoadBase(dataDirectory);
		var modListStore = baseGameInfoResult.Value is { } baseGameInfo ? new RegistryModListStore(baseGameInfo.Title) : null;
		IReadOnlyList<string> modDirectories = !areModsEnabled ? [] : modDirectoriesOverride ?? modListStore?.Load() ?? [];
		if (modDirectories.Count != 0)
			Log.Info($"Loading mods from {string.Join(", ", modDirectories.Select(x => $"\"{x}\""))}");

		return (GameContent.Load(dataDirectory, modDirectories, baseGameInfoResult), baseGameInfoResult, baseTheme, modListStore);
	}

	/// <summary>
	/// Loads the game info and the theme from the data folder without mods. The theme is null if there isn't one or it
	/// didn't load; its problems are reported when the game data loads.
	/// </summary>
	private static (DataLoadResult<GameInfo> GameInfoResult, ThemeSettings? Theme) LoadBase(string dataDirectory)
	{
		var document = DataDocument.Read(dataDirectory, []);
		return document.Value is { } value
			? (GameInfo.Read(value), ThemeSettings.Read(value))
			: (DataLoadResult<GameInfo>.Failure(document.Errors), null);
	}

	/// <summary>
	/// Loads the game data and its theme, and the icon from the game info without mods.
	/// </summary>
	private async Task LoadDataAsync(TaskStateController state)
	{
		await state.ToThreadPool();

		var (content, baseGameInfoResult, baseTheme, modListStore) = LoadContent(m_areModsEnabled, m_modDirectoriesOverride);
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

		// When the game data doesn't load, the theme without mods is used, so the errors are drawn in the usual theme.
		Theme = gameDataResult.Value is { } gameData ? gameData.Theme : baseTheme;

		m_modListStore = modListStore;
		m_saveSettings = gameInfoResult.Value is { } gameInfo ? SaveSettings.Create(gameInfo.Title, content.DataHash, content.ModsHash, content.ModDirectories) : null;
	}

	private IReadOnlyList<DataLoadError> GetLoadErrors() => [.. m_gameInfoResult.Errors, .. m_iconErrors, .. m_gameDataResult.Errors];

	private async Task ReloadDataAsync(TaskStateController state)
	{
		await LoadDataAsync(state);
		ShowData();
	}

	private void ShowData() => m_mainWindow?.SetData(m_gameInfoResult.Value, m_gameDataResult.Value, GetLoadErrors(), m_modListStore, m_saveSettings);

	private void OpenMainWindow()
	{
		var mainWindow = new MainWindowViewModel(Random, new FolderPicker(), m_areModsEnabled, m_isDeveloper, m_modDirectoriesOverride is not null, m_settings);
		mainWindow.ExitRequested += OnExitGameRequested;
		mainWindow.ReloadDataRequested += OnReloadDataRequested;
		MainWindow = mainWindow;
	}

	private void CloseMainWindow()
	{
		var mainWindow = m_mainWindow;
		if (mainWindow is null)
			return;

		MainWindow = null;
		mainWindow.ExitRequested -= OnExitGameRequested;
		mainWindow.ReloadDataRequested -= OnReloadDataRequested;
		mainWindow.Dispose();
	}

	private void OnExitGameRequested(object? sender, EventArgs e) => ExitGame();

	private void OnReloadDataRequested(object? sender, EventArgs e) => TaskWatcher.Execute(ReloadDataAsync, m_taskGroup);

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(AppModel));
	private static readonly Lazy<AppModel> s_appModel = new(() => new AppModel());

	private MainWindowViewModel? m_mainWindow;
	private DataLoadResult<GameInfo> m_gameInfoResult;
	private DataLoadResult<GameData> m_gameDataResult;
	private ImageSource? m_icon;
	private IReadOnlyList<DataLoadError> m_iconErrors = [];
	private IModListStore? m_modListStore;
	private SaveSettings? m_saveSettings;
	private bool m_areModsEnabled = true;
	private IReadOnlyList<string>? m_modDirectoriesOverride;
	private bool m_isDeveloper;
	private ThemeSettings? m_theme;
	private AppSettings? m_settings;
	private TaskGroup m_taskGroup;
}
