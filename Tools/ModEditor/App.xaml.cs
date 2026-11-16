using System.Windows;
using GoldenAnvil.Utility.Logging;
using ModEditor.Model;
using ModEditor.ViewModels;
using ModEditor.Views;

namespace ModEditor;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		LogManager.Initialize(new DebugLogDestination());

		// A bug in the editor is reported rather than closing it, so unsaved changes aren't lost.
		DispatcherUnhandledException += (_, args) =>
		{
			Log.Error($"Unexpected error: {args.Exception}");
			MessageBox.Show(MainWindow, $"Something went wrong in the editor, but your changes are still there. Save them, and restart the editor if it misbehaves.\n\n{args.Exception.GetBaseException().Message}", "Mod Editor", MessageBoxButton.OK, MessageBoxImage.Error);
			args.Handled = true;
		};

		// No mod is opened until the user chooses one.
		var window = new MainWindowView();
		var viewModel = new MainWindowViewModel(new EditorDialogs(window), EditorSettings.Load());
		window.SetViewModel(viewModel);
		MainWindow = window;
		window.Show();
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(App));
}
