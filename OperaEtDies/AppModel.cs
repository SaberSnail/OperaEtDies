using System.Windows.Media;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Data;
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

	public async Task StartupAsync(TaskStateController state)
	{
		await state.ToThreadPool();

		var gameInfoResult = GameInfo.Load(JsonDataLoader.DefaultDataDirectory);
		var gameDataResult = GameData.Load(JsonDataLoader.DefaultDataDirectory);
		var iconPath = gameInfoResult.Value?.GetIconPath(JsonDataLoader.DefaultDataDirectory);
		var iconReadResult = iconPath is null ? null : ImageDataLoader.Read(iconPath);

		await state.ToSyncContext();

		// Images belong to the thread that decodes them, so the icon is decoded here rather than on the thread pool.
		var iconResult = iconReadResult?.Value is { } iconContents ? ImageDataLoader.Decode(iconPath!, iconContents) : null;

		m_gameInfoResult = gameInfoResult;
		m_gameDataResult = gameDataResult;
		m_icon = iconResult?.Value;
		m_iconErrors = iconResult?.Errors ?? iconReadResult?.Errors ?? [];
		RaisePropertyChanged(nameof(GameInfo), nameof(GameData), nameof(Icon));

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

		Log.Info($"Starting new game as {settings.FirstName} {settings.LastName}, {settings.Profession.Id}, age {settings.Age}");

		// Open the main window before closing the start up window, so there's always a window showing.
		MainWindow = new MainWindowViewModel(gameInfo, gameData, settings);
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

	private void OpenStartUpWindow()
	{
		if (m_startUpWindow is not null)
			return;

		var startUpWindow = new StartUpWindowViewModel(m_gameInfoResult.Value, m_gameDataResult.Value, Random, [.. m_gameInfoResult.Errors, .. m_iconErrors, .. m_gameDataResult.Errors]);
		startUpWindow.StartNewGameRequested += OnStartNewGameRequested;
		startUpWindow.ExitGameRequested += OnExitGameRequested;
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

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(AppModel));
	private static readonly Lazy<AppModel> s_appModel = new(() => new AppModel());

	private StartUpWindowViewModel? m_startUpWindow;
	private MainWindowViewModel? m_mainWindow;
	private DataLoadResult<GameInfo> m_gameInfoResult;
	private DataLoadResult<GameData> m_gameDataResult;
	private ImageSource? m_icon;
	private IReadOnlyList<DataLoadError> m_iconErrors = [];
	private Uri m_currentTheme;
	private TaskGroup m_taskGroup;
}
