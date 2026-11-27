using System.Text.Json.Serialization;
using GoldenAnvil.Utility;
using System.Windows.Media;

namespace OperaEtDies.Data;

/// <summary>
/// A small picture from the icons section of the data, such as the one for a need, referred to by its id wherever it's
/// used. It's a line drawing on a 24 by 24 grid, stroked in the theme's colors, so another palette needs no new art.
/// </summary>
public sealed class GameIcon : IIdentified
{
	/// <summary>
	/// The width and height of the grid an icon is drawn on.
	/// </summary>
	public const double GridSize = 24;

	public required string Id { get; init; }

	/// <summary>
	/// The lines of the icon, in WPF path markup (the same as SVG path data), on a 24 by 24 grid.
	/// </summary>
	public required string Path { get; init; }

	/// <summary>
	/// Whether the art was drawn by AI. The game doesn't use it; it's for the authors of the data.
	/// </summary>
	public required bool Ai { get; init; }

	/// <summary>
	/// The icon's lines, parsed from <see cref="Path"/>, or an empty geometry if it isn't valid (validation reports it).
	/// </summary>
	[JsonIgnore]
	public Geometry Geometry => m_geometry ??= PortraitShape.TryParsePath(Path, out var geometry, out _) ? geometry : Geometry.Empty;

	/// <summary>
	/// Checks the icons: each has a unique id and valid path markup.
	/// </summary>
	public static IEnumerable<string> Validate(IReadOnlyList<GameIcon?> icons)
	{
		foreach (var error in DataValidation.ValidateIdentifiedItems(SectionName, icons, x => x.Id, null, allowEmpty: true))
			yield return error;

		for (var index = 0; index < icons.Count; index++)
		{
			if (icons[index] is { } icon && !PortraitShape.TryParsePath(icon.Path ?? "", out _, out var error))
				yield return OurResources.PortraitPathInvalid.FormatCurrentCulture($"{SectionName}[{index}].path", error);
		}
	}

	/// <summary>
	/// The name of the data section the icons are read from.
	/// </summary>
	public const string SectionName = "icons";

	private Geometry? m_geometry;
}
