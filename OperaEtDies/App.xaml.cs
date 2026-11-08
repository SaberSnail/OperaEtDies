using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Microsoft.VisualStudio.Threading;
using OperaEtDies.Windows.Main;
using OperaEtDies.Windows.StartUp;

namespace OperaEtDies;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e) => TaskWatcher.Execute(c => OnStartupAsync(e, c), AppModel.Instance.TaskGroup);

	private async Task OnStartupAsync(StartupEventArgs e, TaskStateController state)
	{
		var stopwatch = Stopwatch.StartNew();

		base.OnStartup(e);

		FrameworkElement.StyleProperty.OverrideMetadata(typeof(Window), new FrameworkPropertyMetadata
		{
			DefaultValue = FindResource(typeof(Window))
		});

		var appModel = AppModel.Instance;
		appModel.PropertyChanged += OnAppModelPropertyChanged;
		appModel.ExitRequested += OnExitRequested;

		try
		{
			await appModel.StartupAsync(state).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// The app only shuts down explicitly, so make sure a failed start up doesn't leave it running with no windows.
			await state.ToSyncContext();
			Log.Error($"Failed to start up: {ex}");
			Shutdown(1);
			return;
		}

		Log.Info($"Finished starting up in {stopwatch.Elapsed}");
	}

	private void OnAppModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AppModel.StartUpWindow))
			UpdateStartUpWindowView();
		else if (e.PropertyName == nameof(AppModel.MainWindow))
			UpdateMainWindowView();
	}

	private void UpdateStartUpWindowView()
	{
		var viewModel = AppModel.Instance.StartUpWindow;
		if (m_startUpWindowView?.ViewModel == viewModel)
			return;

		// Clear the field before closing, so OnStartUpWindowViewClosed can tell this close didn't come from the user.
		var oldView = m_startUpWindowView;
		m_startUpWindowView = null;
		oldView?.Close();

		if (viewModel is not null)
		{
			m_startUpWindowView = new StartUpWindowView(viewModel);
			m_startUpWindowView.Closed += OnStartUpWindowViewClosed;
			m_startUpWindowView.Show();
		}
	}

	private void UpdateMainWindowView()
	{
		var viewModel = AppModel.Instance.MainWindow;
		if (m_mainWindowView?.ViewModel == viewModel)
			return;

		var oldView = m_mainWindowView;
		m_mainWindowView = null;
		oldView?.Close();

		if (viewModel is not null)
		{
			m_mainWindowView = new MainWindowView(viewModel);
			m_mainWindowView.Closed += OnMainWindowViewClosed;
			m_mainWindowView.Show();
		}
	}

	private void OnStartUpWindowViewClosed(object? sender, EventArgs e)
	{
		((Window) sender!).Closed -= OnStartUpWindowViewClosed;
		if (sender != m_startUpWindowView)
			return;

		// The user closed the start up window, which exits the game.
		m_startUpWindowView = null;
		AppModel.Instance.ExitGame();
	}

	private void OnMainWindowViewClosed(object? sender, EventArgs e)
	{
		((Window) sender!).Closed -= OnMainWindowViewClosed;
		if (sender != m_mainWindowView)
			return;

		// The user closed the main window, which returns to the start up window.
		m_mainWindowView = null;
		AppModel.Instance.EndGame();
	}

	private void OnExitRequested(object? sender, EventArgs e)
	{
		if (m_isShuttingDown)
			return;

		m_isShuttingDown = true;
		Shutdown();
	}

#pragma warning disable VSTHRD100 // Avoid async void methods
	protected override async void OnExit(ExitEventArgs e) => await OnShutdownAsync(e);
#pragma warning restore VSTHRD100 // Avoid async void methods

	private async Task OnShutdownAsync(ExitEventArgs e)
	{
		using var logScope = Log.TimedInfo("Shutting down");

		m_isShuttingDown = true;
		var appModel = AppModel.Instance;
		appModel.PropertyChanged -= OnAppModelPropertyChanged;
		appModel.ExitRequested -= OnExitRequested;

		await appModel.ShutdownAsync();

		base.OnExit(e);
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(App));

	private StartUpWindowView? m_startUpWindowView;
	private MainWindowView? m_mainWindowView;
	private bool m_isShuttingDown;
}
