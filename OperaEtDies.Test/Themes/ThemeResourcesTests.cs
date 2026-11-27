using System.Windows;
using System.Windows.Media;
using OperaEtDies.Data;
using OperaEtDies.Themes;

namespace OperaEtDies.Test.Themes;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class ThemeResourcesTests
{
	[Test]
	public void CreatesEveryThemeResource()
	{
		var resources = ThemeResources.Create(LoadShippedTheme(), ThemeSettings.GetFontFolder(JsonDataLoader.DefaultDataDirectory));

		Assert.That(resources["AccentColor"], Is.InstanceOf<Color>());
		Assert.That(resources["AccentBrush"], Is.InstanceOf<SolidColorBrush>().With.Property(nameof(Freezable.IsFrozen)).True);
		Assert.That(resources["NarrativeFontFamily"], Is.InstanceOf<FontFamily>());
		Assert.That(resources[ThemeResources.DefaultFontSizeKey], Is.EqualTo(16));
	}

	[Test]
	public void FontSizesAreMultiplesOfTheBase()
	{
		var theme = LoadShippedTheme();

		var resources = ThemeResources.Create(theme, ThemeSettings.GetFontFolder(JsonDataLoader.DefaultDataDirectory));

		Assert.That(resources["TitleFontSize"], Is.EqualTo(theme.FontSize * theme.FontScales.Title));
		Assert.That(resources["SmallFontSize"], Is.EqualTo(theme.FontSize * theme.FontScales.Small));
	}

	[Test]
	public void ScaledFontSizesMultiplyEverySize()
	{
		var theme = ThemeResources.Create(LoadShippedTheme(), ThemeSettings.GetFontFolder(JsonDataLoader.DefaultDataDirectory));

		var scaled = ThemeResources.CreateScaledFontSizes(x => theme.Contains(x) ? theme[x] : null, 1.5);

		Assert.That(scaled[ThemeResources.DefaultFontSizeKey], Is.EqualTo((double) theme[ThemeResources.DefaultFontSizeKey] * 1.5));
		Assert.That(scaled["TitleFontSize"], Is.EqualTo((double) theme["TitleFontSize"] * 1.5));
	}

	[Test]
	public void FontSizeKeysAreEveryThemeFontSize()
	{
		var theme = ThemeResources.Create(LoadShippedTheme(), ThemeSettings.GetFontFolder(JsonDataLoader.DefaultDataDirectory));

		Assert.That(ThemeResources.FontSizeKeys, Is.EquivalentTo(theme.Keys.OfType<string>().Where(x => x.EndsWith("FontSize", StringComparison.Ordinal))));
	}

	[Test]
	public void FontFamiliesLookInTheFontFolderFirst()
	{
		Assert.That(ThemeResources.GetFamilySource("EB Garamond, Georgia"), Is.EqualTo("./#EB Garamond, EB Garamond, ./#Georgia, Georgia"));
	}

	/// <summary>
	/// Default.xaml is the built-in theme, and Tools.xaml the tools' palette, so each must define every resource the data's
	/// theme replaces, or a theme could leave a resource the built-in one doesn't have.
	/// </summary>
	[TestCase("Themes/Default/Default.xaml")]
	[TestCase("Themes/Tools/Tools.xaml")]
	public void BuiltInThemesDefineEveryThemeResource(string path)
	{
		// The pack scheme needs an application.
		_ = Application.Current ?? new Application();
		var builtIn = new ResourceDictionary { Source = new Uri($"pack://application:,,,/OperaEtDies;component/{path}") };
		var theme = ThemeResources.Create(LoadShippedTheme(), ThemeSettings.GetFontFolder(JsonDataLoader.DefaultDataDirectory));

		// The tools never show the map, so their palette leaves its colors to Default.xaml.
		var keys = theme.Keys.OfType<string>().Where(x => !path.Contains("Tools", StringComparison.Ordinal) || !x.StartsWith("Map", StringComparison.Ordinal));
		Assert.That(keys.Where(x => !builtIn.Contains(x)), Is.Empty);
		Assert.That(keys.Where(x => builtIn.Contains(x) && builtIn[x].GetType() != theme[x].GetType()), Is.Empty);
	}

	private static ThemeSettings LoadShippedTheme() => ThemeSettings.Read(DataDocument.Read(JsonDataLoader.DefaultDataDirectory, []).Value!)!;
}
