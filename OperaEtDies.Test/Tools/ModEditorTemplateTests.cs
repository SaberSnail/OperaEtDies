using System.Windows;

namespace OperaEtDies.Test.Tools;

/// <summary>
/// Loads the mod editor's templates, as WPF does when it first shows each kind of item, so mistakes in them fail here
/// instead of when the editor shows them.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class ModEditorTemplateTests
{
	/// <summary>
	/// Makes the editor's windows, which reads their XAML, so mistakes in it fail here instead of when the editor starts.
	/// </summary>
	[Test]
	public void WindowsLoad()
	{
		LoadResources();

		Assert.That(() => new ModEditor.Views.MainWindowView().Close(), Throws.Nothing);
		Assert.That(() => new ModEditor.Views.Help.HelpWindow().CloseForGood(), Throws.Nothing);
	}

	[TestCase("Views/EditorResources.xaml")]
	[TestCase("Views/FormResources.xaml")]
	public void TemplatesLoad(string path)
	{
		var application = LoadResources();
		var dictionary = application.Resources.MergedDictionaries.Single(x => x.Source?.OriginalString.EndsWith(path, StringComparison.Ordinal) == true);
		var templates = dictionary.Values.OfType<FrameworkTemplate>().ToList();

		Assert.That(templates, Is.Not.Empty);
		foreach (var template in templates)
			Assert.That(template.LoadContent, Throws.Nothing, template.ToString());
	}

	private static Application LoadResources()
	{
		// The pack scheme needs an application, and the templates need the editor's resources, merged as App.xaml does.
		var application = Application.Current ?? new Application();
		if (application.Resources.MergedDictionaries.Count == 0)
		{
			foreach (var source in new[]
			{
				"pack://application:,,,/GoldenAnvil.Utility.Windows;component/Themes/Generic.xaml",
				"pack://application:,,,/OperaEtDies;component/Themes/Default/Default.xaml",
				"pack://application:,,,/OperaEtDies;component/Themes/Tools/Tools.xaml",
				"pack://application:,,,/OperaEtDies;component/Themes/Shared/Shared.xaml",
				"pack://application:,,,/ModEditor;component/Views/EditorResources.xaml",
				"pack://application:,,,/ModEditor;component/Views/FormResources.xaml",
			})
			{
				application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source) });
			}
		}

		return application;
	}
}
