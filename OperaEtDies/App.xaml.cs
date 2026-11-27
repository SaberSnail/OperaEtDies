using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.VisualStudio.Threading;
using OperaEtDies.Data;
using OperaEtDies.Controls;
using OperaEtDies.Reports;
using OperaEtDies.Settings;
using OperaEtDies.Themes;
using OperaEtDies.Windows.Main;

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
			Shutdown(VillageReport.Run(e.Args, options.AreModsEnabled, options.ModDirectories, output, error));
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
		SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;

		try
		{
			await appModel.StartupAsync(state, options.AreModsEnabled, options.ModDirectories, options.IsDeveloper, options.ResetSettings).ConfigureAwait(false);
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
		if (e.PropertyName == nameof(AppModel.MainWindow))
			UpdateMainWindowView();
		else if (e.PropertyName == nameof(AppModel.Theme))
			ApplyTheme();
		else if (e.PropertyName == nameof(AppModel.Settings))
			UpdateSettings();
	}

	/// <summary>
	/// Follows the app model's settings, applying them now and whenever they change.
	/// </summary>
	private void UpdateSettings()
	{
		if (m_settings is not null)
			m_settings.PropertyChanged -= OnSettingsPropertyChanged;
		m_settings = AppModel.Instance.Settings;
		if (m_settings is not null)
			m_settings.PropertyChanged += OnSettingsPropertyChanged;

		ApplyFontSizes();
		ApplyMotion();
	}

	private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AppSettings.TextSize))
			ApplyFontSizes();
		else if (e.PropertyName == nameof(AppSettings.IsMotionReduced))
			ApplyMotion();
	}

	// Following Windows' setting for animations means following it when it changes too.
	private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
			ApplyMotion();
	}

	private void ApplyMotion()
	{
		if (m_settings is not null)
			Motion.IsReduced = m_settings.IsMotionReduced;
	}

	/// <summary>
	/// Replaces the text sizes with the theme's (or the built-in theme's) scaled by the text size setting.
	/// </summary>
	private void ApplyFontSizes()
	{
		if (m_fontSizeResources is not null)
			Resources.MergedDictionaries.Remove(m_fontSizeResources);

		var scale = (m_settings?.TextSize ?? AppSettings.DefaultTextSize) / 100.0;
		m_fontSizeResources = ThemeResources.CreateScaledFontSizes(TryFindResource, scale);
		Resources.MergedDictionaries.Add(m_fontSizeResources);
	}

	/// <summary>
	/// Replaces the theme resources with the app model's theme, or goes back to the built-in theme if it has none.
	/// </summary>
	private void ApplyTheme()
	{
		if (m_themeResources is not null)
			Resources.MergedDictionaries.Remove(m_themeResources);

		m_themeResources = AppModel.Instance.Theme is { } theme ? ThemeResources.Create(theme, ThemeSettings.GetFontFolder(JsonDataLoader.DefaultDataDirectory)) : null;
		if (m_themeResources is not null)
			Resources.MergedDictionaries.Add(m_themeResources);

		// The text sizes come from the theme, so they're worked out again, after it.
		ApplyFontSizes();
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
			m_mainWindowView = new MainWindowView(viewModel, AppModel.Instance.WindowPlacementKeyPath);
			m_mainWindowView.Closed += OnMainWindowViewClosed;
			m_mainWindowView.Show();
		}
	}

	private void OnMainWindowViewClosed(object? sender, EventArgs e)
	{
		((Window) sender!).Closed -= OnMainWindowViewClosed;

		// The player closed the window, which exits the game.
		if (sender == m_mainWindowView)
		{
			m_mainWindowView = null;
			AppModel.Instance.ExitGame();
		}
	}

	private void OnExitRequested(object? sender, EventArgs e)
	{
		if (m_isShuttingDown)
			return;

		m_isShuttingDown = true;

		// Close the window first, so it keeps where it was.
		var view = m_mainWindowView;
		m_mainWindowView = null;
		view?.Close();
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
		SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
		if (m_settings is not null)
			m_settings.PropertyChanged -= OnSettingsPropertyChanged;

		await appModel.ShutdownAsync();

		base.OnExit(e);
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(App));

	private MainWindowView? m_mainWindowView;
	private ResourceDictionary? m_themeResources;
	private ResourceDictionary? m_fontSizeResources;
	private AppSettings? m_settings;
	private bool m_isShuttingDown;
	private bool m_isRunningWithoutGame;
}
