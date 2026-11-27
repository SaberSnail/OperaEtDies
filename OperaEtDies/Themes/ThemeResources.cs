using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using OperaEtDies.Data;

namespace OperaEtDies.Themes;

/// <summary>
/// Turns the theme from the data into the theme resources defined in Themes/Default/Default.xaml. Merged after those,
/// the resources replace them, and everything drawn with them as dynamic resources changes to match.
/// </summary>
public static class ThemeResources
{
	/// <summary>
	/// Creates the resources for a theme: a Color and a Brush for each of its colors, a FontFamily for each of its fonts, and
	/// the font sizes. Font families are looked for in the font folder first, then among the installed fonts. The theme
	/// must have been validated.
	/// </summary>
	public static ResourceDictionary Create(ThemeSettings theme, string fontFolder)
	{
		var resources = new ResourceDictionary();
		foreach (var (role, value) in theme.Colors.GetColors())
		{
			var color = PortraitColor.TryParseHex(value, out var parsed) ? parsed : Colors.Magenta;
			var brush = new SolidColorBrush(color);
			brush.Freeze();
			resources[GetKey(role, "Color")] = color;
			resources[GetKey(role, "Brush")] = brush;
		}

		var fontFolderUri = new Uri(Path.TrimEndingDirectorySeparator(Path.GetFullPath(fontFolder)) + Path.DirectorySeparatorChar);
		foreach (var (role, family) in theme.Fonts.GetFonts())
			resources[GetKey(role, "FontFamily")] = new FontFamily(fontFolderUri, GetFamilySource(family));

		resources[DefaultFontSizeKey] = theme.FontSize;
		foreach (var (role, scale) in theme.FontScales.GetScales())
			resources[GetKey(role, "FontSize")] = theme.FontSize * scale;

		return resources;
	}

	/// <summary>
	/// The resource key of the size of ordinary text, which the other sizes are multiples of.
	/// </summary>
	public const string DefaultFontSizeKey = "DefaultFontSize";

	/// <summary>
	/// The resource keys of every text size: ordinary text, and one for each of the theme's font scales.
	/// </summary>
	public static IReadOnlyList<string> FontSizeKeys { get; } =
	[
		DefaultFontSizeKey,
		.. typeof(ThemeFontScales).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.PropertyType == typeof(double)).Select(x => GetKey(x.Name, "FontSize")),
	];

	/// <summary>
	/// Creates the text sizes for the player's text size setting: each size found (from the theme, or the built-in theme
	/// without one) multiplied by the scale. Merged after the theme, they replace its sizes.
	/// </summary>
	public static ResourceDictionary CreateScaledFontSizes(Func<string, object?> findFontSize, double scale)
	{
		ArgumentNullException.ThrowIfNull(findFontSize);
		var resources = new ResourceDictionary();
		foreach (var key in FontSizeKeys)
		{
			if (findFontSize(key) is double size)
				resources[key] = size * scale;
		}

		return resources;
	}

	/// <summary>
	/// Gets the key of a theme resource from the name of its role in the data, such as AccentBrush for the "accent" color.
	/// </summary>
	public static string GetKey(string role, string suffix) => char.ToUpperInvariant(role[0]) + role[1..] + suffix;

	/// <summary>
	/// Gets the font family source for a list of families separated by commas, looking for each in the font folder before
	/// the installed fonts.
	/// </summary>
	internal static string GetFamilySource(string families) =>
		string.Join(", ", families.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).SelectMany(x => new[] { $"./#{x}", x }));
}
