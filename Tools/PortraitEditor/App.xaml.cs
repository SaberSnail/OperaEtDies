using System.IO;
using System.Windows;
using GoldenAnvil.Utility.Logging;
using PortraitEditor.Model;
using PortraitEditor.ViewModels;
using PortraitEditor.Views;

namespace PortraitEditor;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		LogManager.Initialize(new DebugLogDestination());

		var options = EditorOptions.Parse(e.Args);
		if (options.SaveIconPath is { } iconPath)
		{
			// Saving the icon runs instead of the editor, without showing any UI. Problems go to standard error.
			var errors = IconRenderer.Save(options.DataDirectory, iconPath, options.IconPreviewDirectory);
			using (var error = new StreamWriter(Console.OpenStandardError()))
			{
				foreach (var message in errors)
					error.WriteLine(message);
			}

			Shutdown(errors.Count == 0 ? 0 : 1);
		}
		else
		{
			var viewModel = new MainWindowViewModel(options);
			var window = new MainWindowView(viewModel);
			window.Closed += (_, _) => viewModel.Dispose();
			MainWindow = window;
			window.Show();
		}
	}
}
