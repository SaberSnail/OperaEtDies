using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using OperaEtDies.Windows.Main;

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
	}

	public MainWindowViewModel? MainWindow
	{
		get => VerifyAccess(m_mainWindow);
		set => SetPropertyField(value, ref m_mainWindow);
	}

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
		await state.ToSyncContext();

		m_mainWindow = new MainWindowViewModel();
	}

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
	public async Task ShutdownAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
	{
		DisposableUtility.Dispose(ref m_mainWindow);
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(AppModel));
	private static readonly Lazy<AppModel> s_appModel = new(() => new AppModel());

	private MainWindowViewModel? m_mainWindow;
	private Uri m_currentTheme;
	private TaskGroup m_taskGroup;
}
