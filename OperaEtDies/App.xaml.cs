using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.VisualStudio.Threading;
using OperaEtDies.Reports;
using OperaEtDies.Windows.Main;
using OperaEtDies.Windows.StartUp;

namespace OperaEtDies;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		var options = CommandLineOptions.Parse(e.Args);
		if (options.ShowHelp)
		{
			// The game is a windowed app, so the list only appears on the console when its output is redirected.
			m_isRunningWithoutGame = true;
			base.OnStartup(e);
			var help = CommandLineOptions.GetHelpText();
			using (var output = new StreamWriter(Console.OpenStandardOutput()))
				output.Write(help);
			if (!Console.IsOutputRedirected)
				MessageBox.Show(help, OurResources.CommandLineHelpTitle);
			Shutdown(0);
		}
		else if (options.IsVillageReport)
		{
			// The report runs instead of the game, and writes to standard output and error without showing any UI.
			m_isRunningWithoutGame = true;
			base.OnStartup(e);
			using var output = new StreamWriter(Console.OpenStandardOutput());
			using var error = new StreamWriter(Console.OpenStandardError());
			Shutdown(VillageReport.Run(e.Args, options.AreModsEnabled, output, error));
		}
		else
		{
			TaskWatcher.Execute(c => OnStartupAsync(e, options, c), AppModel.Instance.TaskGroup);
		}
	}

	private async Task OnStartupAsync(StartupEventArgs e, CommandLineOptions options, TaskStateController state)
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
			await appModel.StartupAsync(state, options.AreModsEnabled, options.IsDeveloper).ConfigureAwait(false);
			Log.Info($"Finished starting up in {stopwatch.Elapsed}");
		}
#pragma warning disable CA1031 // Do not catch general exception types (any start up failure must shut the app down)
		catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
		{
			// The app only shuts down explicitly, so make sure a failed start up doesn't leave it running with no windows.
			await state.ToSyncContext();
			Log.Error($"Failed to start up: {ex}");
			Shutdown(1);
		}
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

		// The user closed the start up window, which exits the game.
		if (sender == m_startUpWindowView)
		{
			m_startUpWindowView = null;
			AppModel.Instance.ExitGame();
		}
	}

	private void OnMainWindowViewClosed(object? sender, EventArgs e)
	{
		((Window) sender!).Closed -= OnMainWindowViewClosed;

		// The user closed the main window, which returns to the start up window.
		if (sender == m_mainWindowView)
		{
			m_mainWindowView = null;
			AppModel.Instance.EndGame();
		}
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
		// The report and the list of options never create the app model, so there's nothing to shut down.
		if (m_isRunningWithoutGame)
		{
			base.OnExit(e);
			return;
		}

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
	private bool m_isRunningWithoutGame;
}
