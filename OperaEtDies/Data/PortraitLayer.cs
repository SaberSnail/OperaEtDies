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
	/// Whether the layer is drawn for a character in a place, given a function that gets the value id of a value list
	/// property. A null place only draws the layers that are drawn anywhere.
	/// </summary>
	public bool IsDrawn(Func<string, string?> getValueId, PortraitPlace? place) =>
		(At is null || At.IsMet(place)) && CharacterCondition.IsMet(When, getValueId);

	/// <summary>
	/// The shapes in the layer, from bottom to top.
	/// </summary>
	public required IReadOnlyList<PortraitShape> Shapes { get; init; }
}

/// <summary>
/// A shape in a portrait layer, filled and outlined with portrait colors.
/// </summary>
public sealed class PortraitShape
{
	/// <summary>
	/// The outline of the shape, in WPF path markup (the same as SVG path data).
	/// </summary>
	public required string Path { get; init; }

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
