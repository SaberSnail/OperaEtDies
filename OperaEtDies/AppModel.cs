using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using OperaEtDies.Data;
using OperaEtDies.Windows.Main;
using OperaEtDies.Windows.StartUp;

namespace OperaEtDies;

public sealed class AppModel : ViewModelBase
{
	public static AppModel Instance => s_appModel.Value;

	private AppModel()
	{
		m_taskGroup = new TaskGroup();
		LogManager.Initialize(new DebugLogDestination());
		m_currentTheme = new Uri(@"/Themes/Default/Default.xaml", UriKind.Relative);
		Random = new CryptoRandom();
		m_gameInfoResult = DataLoadResult<GameInfo>.Failure([]);
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

		await state.ToSyncContext();

		m_gameInfoResult = gameInfoResult;
		RaisePropertyChanged(nameof(GameInfo));

		OpenStartUpWindow();
	}

	/// <summary>
	/// Replaces the start up window with the main window.
	/// </summary>
	public void StartNewGame()
	{
		VerifyAccess();

		var gameInfo = m_gameInfoResult.Value;
		if (gameInfo is null)
		{
			Log.Error("Can't start a new game because the game info failed to load");
			return;
		}

		// Open the main window before closing the start up window, so there's always a window showing.
		MainWindow = new MainWindowViewModel(gameInfo);
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

		var startUpWindow = new StartUpWindowViewModel(m_gameInfoResult.Value, m_gameInfoResult.Errors);
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

	private void OnStartNewGameRequested(object? sender, EventArgs e) => StartNewGame();

	private void OnExitGameRequested(object? sender, EventArgs e) => ExitGame();

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(AppModel));
	private static readonly Lazy<AppModel> s_appModel = new(() => new AppModel());

	private StartUpWindowViewModel? m_startUpWindow;
	private MainWindowViewModel? m_mainWindow;
	private DataLoadResult<GameInfo> m_gameInfoResult;
	private Uri m_currentTheme;
	private TaskGroup m_taskGroup;
}
