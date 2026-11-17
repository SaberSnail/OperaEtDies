using System.Windows.Media;

namespace OperaEtDies.Data;

/// <summary>
/// One layer of a character portrait, drawn when its condition is met.
/// </summary>
public sealed class PortraitLayer : IIdentified
{
	public required string Id { get; init; }

	/// <summary>
	/// A condition on the character's properties that must be met for the layer to be drawn.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// The places the layer is drawn in, or null to draw it anywhere.
	/// </summary>
	public PortraitPlaceCondition? At { get; init; }

	/// <summary>
	/// The id of the portrait scale that stretches the layer's shapes, except shapes that name their own.
	/// </summary>
	public string? Scale { get; init; }

	/// <summary>
	/// The id of the portrait offset that moves the layer's shapes, after they're stretched.
	/// </summary>
	public string? Offset { get; init; }

	/// <summary>
	/// The ids of the articles of clothing the layer draws. The layer is only drawn when one of them is worn, and its
	/// shapes can use the colors of the first one in the list that's worn, by color name.
	/// </summary>
	public IReadOnlyList<string>? Clothing { get; init; }

	/// <summary>
	/// How much worn clothing must cover which regions of the body to hide the layer, such as the hair under a hood, or
	/// null if clothing never hides it.
	/// </summary>
	public PortraitHiddenBy? HiddenBy { get; init; }

	/// <summary>
	/// Colors only the layer uses, worked out in order after the portrait colors and the colors of the clothing it
	/// draws, which they can use. In the layer, a layer color takes the place of a clothing or portrait color with the
	/// same id, and a clothing color takes the place of a portrait color.
	/// </summary>
	public IReadOnlyList<PortraitColor>? Colors { get; init; }

	/// <summary>
	/// Whether the layer is drawn for a character in a place wearing clothing, given a function that gets the value id
	/// of a value list property. A null place only draws the layers that are drawn anywhere.
	/// </summary>
	public bool IsDrawn(Func<string, string?> getValueId, PortraitPlace? place, PortraitClothing clothing) =>
		(At is null || At.IsMet(place)) && CharacterCondition.IsMet(When, getValueId) && IsClothingMet(clothing);

	/// <summary>
	/// Whether the clothing allows the layer to be drawn: one of the layer's articles of clothing is worn, if it has
	/// any, and its group isn't hidden.
	/// </summary>
	public bool IsClothingMet(PortraitClothing clothing) =>
		(Clothing is null || FindPiece(clothing) is not null) && (HiddenBy is null || !HiddenBy.IsHidden(clothing));

	/// <summary>
	/// Gets the worn piece of clothing whose colors the layer uses: the first of its articles of clothing that's worn,
	/// or null if it draws no clothing or none of it is worn.
	/// </summary>
	public PortraitClothingPiece? FindPiece(PortraitClothing clothing) =>
		Clothing?.Select(clothing.Find).FirstOrDefault(x => x is not null);

	/// <summary>
	/// The shapes in the layer, from bottom to top.
	/// </summary>
	public required IReadOnlyList<PortraitShape> Shapes { get; init; }

	/// <summary>
	/// Whether the art was drawn by AI. The game doesn't use it; it's for the authors of the data.
	/// </summary>
	public required bool Ai { get; init; }
}

/// <summary>
/// A shape in a portrait layer, filled and outlined with portrait colors.
/// </summary>
public sealed class PortraitShape
{
	/// <summary>
	/// The outline of the shape, in WPF path markup (the same as SVG path data). Every shape has either a path or
	/// <see cref="Include"/>.
	/// </summary>
	public string? Path { get; init; }

	/// <summary>
	/// The id of a portrait shape set whose shapes are drawn in place of this one, in the layer's colors.
	/// </summary>
	public string? Include { get; init; }

	/// <summary>
	/// For an included shape set, whether its shapes are only drawn where they're over the layer's other filled shapes.
	/// </summary>
	public bool Clip { get; init; }

	/// <summary>
	/// A condition on the character's properties that must be met for the shape to be drawn.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// The color the shape is filled with: a hex color, or the id of a portrait color.
	/// </summary>
	public string? Fill { get; init; }

	/// <summary>
	/// Fills the shape with a gradient from <see cref="Fill"/> to <see cref="FillTo"/>, across the shape's bounds.
	/// </summary>
	public PortraitGradient? Gradient { get; init; }

	/// <summary>
	/// The color the gradient ends with. Without it, the gradient fades the fill color out.
	/// </summary>
	public string? FillTo { get; init; }

	/// <summary>
	/// The color of the shape's outline: a hex color, or the id of a portrait color.
	/// </summary>
	public string? Stroke { get; init; }

	/// <summary>
	/// The width of the outline. Defaults to 1.
	/// </summary>
	public double? StrokeWidth { get; init; }

	/// <summary>
	/// How opaque the shape is, from 0 (invisible) to 1 (solid, the default).
	/// </summary>
	public double? Opacity { get; init; }

	/// <summary>
	/// The id of the portrait scale that stretches the shape, used in place of the layer's.
	/// </summary>
	public string? Scale { get; init; }

	/// <summary>
	/// Parses path markup into a frozen geometry, or returns the reason it isn't valid.
	/// </summary>
	public static bool TryParsePath(string path, out Geometry geometry, out string error)
	{
		try
		{
			geometry = Geometry.Parse(path);
			geometry.Freeze();
			error = "";
			return true;
		}
		catch (Exception e) when (e is FormatException or InvalidOperationException or ArgumentException)
		{
			geometry = Geometry.Empty;
			error = e.Message;
			return false;
		}
	}
}

/// <summary>
/// The direction a portrait shape's gradient runs.
/// </summary>
public enum PortraitGradient
{
	/// <summary>
	/// From the top of the shape to the bottom.
	/// </summary>
	Vertical,

	/// <summary>
	/// From the left of the shape to the right.
	/// </summary>
	Horizontal,

	/// <summary>
	/// From the center of the shape out to its edges.
	/// </summary>
	Radial,
}

/// <summary>
/// When worn clothing hides a portrait layer: when it covers every one of the regions at least as much as the coverage.
/// </summary>
public sealed class PortraitHiddenBy
{
	/// <summary>
	/// The ids of the clothing slots (regions of the body) that must be covered.
	/// </summary>
	public required IReadOnlyList<string> Regions { get; init; }

	/// <summary>
	/// How much each region must be covered, from 1 to <see cref="ClothingItem.MaxCoverage"/>.
	/// </summary>
	public required int Coverage { get; init; }

	/// <summary>
	/// Whether the clothing covers every region enough to hide the layer.
	/// </summary>
	public bool IsHidden(PortraitClothing clothing) => Regions.All(x => clothing.GetCoverage(x) >= Coverage);
}
