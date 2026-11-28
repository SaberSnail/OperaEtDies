using System.IO;
using System.Reflection;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// How the game looks: its colors, fonts, and text sizes, from the theme section of the data. The colors and fonts are
/// named by the role they play, and fill the theme resources of the same names (see Themes/Default/Default.xaml).
/// </summary>
public sealed class ThemeSettings : IValidatedData
{
	/// <summary>
	/// The name of the data section the theme is read from.
	/// </summary>
	public const string SectionName = "theme";

	/// <summary>
	/// The folder, inside the data folder, that the theme's font files are found in.
	/// </summary>
	public const string FontFolderName = "Fonts";

	/// <summary>
	/// The smallest and largest base font size allowed.
	/// </summary>
	public const double MinFontSize = 6;
	public const double MaxFontSize = 72;

	/// <summary>
	/// The largest font scale allowed.
	/// </summary>
	public const double MaxFontScale = 10;

	/// <summary>
	/// Reads the theme from merged data on its own, before the rest of the game data. Returns null if the data has no theme,
	/// or it has problems, which are reported when the game data loads (see <see cref="GameData.Load(DataDocument, bool)"/>).
	/// </summary>
	public static ThemeSettings? Read(DataDocument document)
	{
		var errors = new List<DataLoadError>();
		var theme = document.ReadSection<ThemeSettings>(SectionName, errors);
		return errors.Count == 0 && theme is not null && !theme.Validate().Any() ? theme : null;
	}

	public required ThemeColors Colors { get; init; }

	public required ThemeFonts Fonts { get; init; }

	/// <summary>
	/// The size of ordinary text, in device-independent pixels. The other sizes are worked out from it.
	/// </summary>
	public required double FontSize { get; init; }

	public required ThemeFontScales FontScales { get; init; }

	/// <summary>
	/// Gets the full path of the folder the theme's font files are found in.
	/// </summary>
	public static string GetFontFolder(string dataDirectory) => Path.GetFullPath(Path.Combine(dataDirectory, FontFolderName));

	public IEnumerable<string> Validate()
	{
		foreach (var (role, value) in Colors.GetColors())
		{
			if (!PortraitColor.TryParseHex(value, out _))
				yield return OurResources.ThemeColorInvalid.FormatCurrentCulture(role, value);
		}

		foreach (var (role, family) in Fonts.GetFonts())
		{
			if (string.IsNullOrWhiteSpace(family))
				yield return OurResources.ThemeFontMissing.FormatCurrentCulture(role);
		}

		if (!(FontSize >= MinFontSize && FontSize <= MaxFontSize))
			yield return OurResources.ThemeFontSizeInvalid.FormatCurrentCulture(FontSize, MinFontSize, MaxFontSize);

		foreach (var (role, scale) in FontScales.GetScales())
		{
			if (!(scale > 0 && scale <= MaxFontScale))
				yield return OurResources.ThemeFontScaleInvalid.FormatCurrentCulture(role, scale, MaxFontScale);
		}
	}

	/// <summary>
	/// Gets the name of a role in the data, from the name of its property.
	/// </summary>
	internal static string GetRoleName(PropertyInfo property) => char.ToLowerInvariant(property.Name[0]) + property.Name[1..];

	internal static IEnumerable<(string Role, T Value)> GetValues<T>(object values) =>
		values.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(x => x.PropertyType == typeof(T))
			.Select(x => (GetRoleName(x), (T) x.GetValue(values)!));
}

/// <summary>
/// The theme's colors, by role. Each is written as #RRGGBB or #AARRGGBB, and fills the theme resources named after its
/// property, such as AccentColor and AccentBrush.
/// </summary>
public sealed class ThemeColors
{
	/// <summary>
	/// The background of the window, and of the center area.
	/// </summary>
	public required string Surface { get; init; }

	/// <summary>
	/// The background of the side columns and of panels set apart from the surface.
	/// </summary>
	public required string Panel { get; init; }

	/// <summary>
	/// The background of the title bar and toolbar.
	/// </summary>
	public required string Chrome { get; init; }

	/// <summary>
	/// The background of recessed areas, such as the portrait frame.
	/// </summary>
	public required string Inset { get; init; }

	/// <summary>
	/// The empty part of a bar or meter.
	/// </summary>
	public required string Track { get; init; }

	/// <summary>
	/// The filled part of a bar or meter.
	/// </summary>
	public required string Meter { get; init; }

	/// <summary>
	/// The sun on the day dial, which shows the time of day.
	/// </summary>
	public required string Sun { get; init; }

	/// <summary>
	/// The moon on the day dial at night.
	/// </summary>
	public required string Moon { get; init; }

	/// <summary>
	/// Ordinary text, and icons drawn in the same color.
	/// </summary>
	public required string Text { get; init; }

	/// <summary>
	/// Secondary text, such as labels, hints, and notes.
	/// </summary>
	public required string TextMuted { get; init; }

	/// <summary>
	/// The text of disabled controls.
	/// </summary>
	public required string TextDisabled { get; init; }

	/// <summary>
	/// Text on a dark background, such as a tooltip.
	/// </summary>
	public required string TextOnDark { get; init; }

	/// <summary>
	/// Text on the accent color.
	/// </summary>
	public required string TextOnAccent { get; init; }

	/// <summary>
	/// Rules, borders, and dividers.
	/// </summary>
	public required string Rule { get; init; }

	/// <summary>
	/// Stronger rules and borders, such as those of controls under the mouse.
	/// </summary>
	public required string RuleStrong { get; init; }

	/// <summary>
	/// The accent, for drop caps, small-caps labels, and things to notice.
	/// </summary>
	public required string Accent { get; init; }

	/// <summary>
	/// Danger and errors.
	/// </summary>
	public required string Threat { get; init; }

	/// <summary>
	/// The background of controls, such as buttons.
	/// </summary>
	public required string Control { get; init; }

	public required string ControlHover { get; init; }

	public required string ControlPressed { get; init; }

	public required string ControlDisabled { get; init; }

	/// <summary>
	/// Quarters in the village on the map.
	/// </summary>
	public required string MapVillage { get; init; }

	/// <summary>
	/// Quarters in the outskirts on the map.
	/// </summary>
	public required string MapOutskirts { get; init; }

	/// <summary>
	/// Landmark quarters on the map.
	/// </summary>
	public required string MapLandmark { get; init; }

	/// <summary>
	/// The borders of quarters on the map.
	/// </summary>
	public required string MapBorder { get; init; }

	/// <summary>
	/// The player's quarter on the map.
	/// </summary>
	public required string MapCurrent { get; init; }

	/// <summary>
	/// The paths between quarters on the map.
	/// </summary>
	public required string MapPath { get; init; }

	/// <summary>
	/// The river on the map.
	/// </summary>
	public required string MapRiver { get; init; }

	/// <summary>
	/// Areas on the map, such as woods.
	/// </summary>
	public required string MapArea { get; init; }

	/// <summary>
	/// Gets each color with the name of its role in the data.
	/// </summary>
	public IEnumerable<(string Role, string Value)> GetColors() => ThemeSettings.GetValues<string>(this);
}

/// <summary>
/// The theme's font families, by role. Each is a family name, or several separated by commas, the first available of
/// which is used. Families are found in the data folder's Fonts folder, then among the installed fonts.
/// </summary>
public sealed class ThemeFonts
{
	/// <summary>
	/// The game's story: descriptions, conversations, and the log.
	/// </summary>
	public required string Narrative { get; init; }

	/// <summary>
	/// Everything else: labels, buttons, and lists.
	/// </summary>
	public required string Interface { get; init; }

	/// <summary>
	/// Gets each font family with the name of its role in the data.
	/// </summary>
	public IEnumerable<(string Role, string Family)> GetFonts() => ThemeSettings.GetValues<string>(this);
}

/// <summary>
/// The sizes of text other than ordinary text, each as a multiple of the theme's font size.
/// </summary>
public sealed class ThemeFontScales
{
	/// <summary>
	/// Small text, such as notes.
	/// </summary>
	public required double Small { get; init; }

	/// <summary>
	/// Larger text, such as headings within a screen.
	/// </summary>
	public required double Large { get; init; }

	/// <summary>
	/// Titles, such as the name of a place or a screen.
	/// </summary>
	public required double Title { get; init; }

	/// <summary>
	/// Small-caps labels.
	/// </summary>
	public required double Caption { get; init; }

	/// <summary>
	/// Gets each scale with the name of its role in the data.
	/// </summary>
	public IEnumerable<(string Role, double Scale)> GetScales() => ThemeSettings.GetValues<double>(this);
}
