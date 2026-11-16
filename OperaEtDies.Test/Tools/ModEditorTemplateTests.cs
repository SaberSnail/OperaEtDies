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
	[TestCase("Views/EditorResources.xaml")]
	[TestCase("Views/FormResources.xaml")]
	public void TemplatesLoad(string path)
	{
		// The pack scheme needs an application, and the templates need the editor's resources, merged as App.xaml does.
		var application = Application.Current ?? new Application();
		if (application.Resources.MergedDictionaries.Count == 0)
		{
			foreach (var source in new[]
			{
				"pack://application:,,,/GoldenAnvil.Utility.Windows;component/Themes/Generic.xaml",
				"pack://application:,,,/OperaEtDies;component/Themes/Default/Default.xaml",
				"pack://application:,,,/OperaEtDies;component/Themes/Shared/Shared.xaml",
				"pack://application:,,,/ModEditor;component/Views/EditorResources.xaml",
				"pack://application:,,,/ModEditor;component/Views/FormResources.xaml",
			})
			{
				application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source) });
			}
		}

		var dictionary = application.Resources.MergedDictionaries.Single(x => x.Source?.OriginalString.EndsWith(path, StringComparison.Ordinal) == true);
		var templates = dictionary.Values.OfType<FrameworkTemplate>().ToList();

		Assert.That(templates, Is.Not.Empty);
		foreach (var template in templates)
			Assert.That(template.LoadContent, Throws.Nothing, template.ToString());
	}
}
