using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using OperaEtDies.Data;

namespace OperaEtDies.Characters;

/// <summary>
/// Draws character portraits from the layers in the portrait data. The paths are parsed once, and the portraits it
/// draws are frozen, so they can be used from any thread.
/// </summary>
public sealed class PortraitRenderer
{
	/// <summary>
	/// Creates a renderer for portrait data that was validated when it loaded, using the character properties to
	/// check conditions.
	/// </summary>
	public PortraitRenderer(PortraitData data, DataCatalog<CharacterProperty> properties)
	{
		m_properties = properties;
		m_colors = data.Colors;
		m_scales = data.Scales;
		Size = new Size(data.Width, data.Height);
		m_layers = data.Layers
			.Select(layer => new ParsedLayer(layer, layer.Shapes.Select(shape => new ParsedShape(shape, PortraitShape.TryParsePath(shape.Path, out var geometry, out _) ? geometry : Geometry.Empty)).ToList()))
			.ToList();

		var bounds = new RectangleGeometry(new Rect(Size));
		bounds.Freeze();
		m_bounds = bounds;
	}

	/// <summary>
	/// The size of a portrait, in the units used by the layers' paths.
	/// </summary>
	public Size Size { get; }

	/// <summary>
	/// Gets the ids of the layers drawn for a character in a place, from bottom to top.
	/// </summary>
	public IReadOnlyList<string> GetLayerIds(CharacterTraits traits, PortraitPlace? place)
	{
		var getValueId = GetValueIdFunc(traits);
		return m_layers.Where(x => x.Layer.IsDrawn(getValueId, place)).Select(x => x.Layer.Id).ToList();
	}

	/// <summary>
	/// Draws a character's portrait in a place. A null place only draws the layers
	/// that are drawn anywhere.
	/// </summary>
	public DrawingImage Render(CharacterTraits traits, PortraitPlace? place)
	{
		var getValueId = GetValueIdFunc(traits);
		var colors = GetColors(getValueId);
		var transforms = GetTransforms(getValueId);

		var portrait = new DrawingGroup { ClipGeometry = m_bounds };

		// Fixes the drawing's bounds to the full portrait, whatever its layers cover.
		portrait.Children.Add(new GeometryDrawing(Brushes.Transparent, null, m_bounds));
		foreach (var layer in m_layers.Where(x => x.Layer.IsDrawn(getValueId, place)))
		{
			foreach (var shape in layer.Shapes.Where(x => CharacterCondition.IsMet(x.Shape.When, getValueId)))
				portrait.Children.Add(CreateDrawing(shape, colors, transforms.GetValueOrDefault(shape.Shape.Scale ?? layer.Layer.Scale ?? "")));
		}

		var image = new DrawingImage(portrait);
		image.Freeze();
		return image;
	}

	private Func<string, string?> GetValueIdFunc(CharacterTraits traits) =>
		propertyId => m_properties.Find(propertyId)?.GetConditionValueId(traits.Values.GetValueOrDefault(propertyId));

	/// <summary>
	/// Works out every portrait color for a character, in order, since a color can use the colors before it.
	/// </summary>
	private Dictionary<string, Color> GetColors(Func<string, string?> getValueId)
	{
		var colors = new Dictionary<string, Color>(StringComparer.Ordinal);
		foreach (var color in m_colors)
		{
			var reference = color.Variants?.FirstOrDefault(x => CharacterCondition.IsMet(x.When, getValueId))?.Color ?? color.Color;
			var value = Resolve(reference, colors);
			if (color.Mix is { } mix && color.Amount is { } amount)
				value = Blend(value, Resolve(mix, colors), amount);
			colors[color.Id] = value;
		}

		return colors;
	}

	/// <summary>
	/// Works out the stretch of every portrait scale for a character, leaving out the ones that don't stretch anything.
	/// </summary>
	private Dictionary<string, Transform> GetTransforms(Func<string, string?> getValueId)
	{
		var transforms = new Dictionary<string, Transform>(StringComparer.Ordinal);
		foreach (var scale in m_scales)
		{
			var variant = scale.Variants?.FirstOrDefault(x => CharacterCondition.IsMet(x.When, getValueId));
			var (x, y) = variant is null ? (scale.X, scale.Y) : (variant.X, variant.Y);
			if (x != 1 || y != 1)
			{
				var transform = new ScaleTransform(x, y, scale.OriginX, scale.OriginY);
				transform.Freeze();
				transforms[scale.Id] = transform;
			}
		}

		return transforms;
	}

	private static Color Resolve(string reference, IReadOnlyDictionary<string, Color> colors) =>
		PortraitColor.TryParseHex(reference, out var color) ? color : colors.GetValueOrDefault(reference, Colors.Magenta);

	private static Color Blend(Color from, Color to, double amount)
	{
		byte Mix(byte a, byte b) => (byte) Math.Round(a + ((b - a) * amount));
		return Color.FromArgb(Mix(from.A, to.A), Mix(from.R, to.R), Mix(from.G, to.G), Mix(from.B, to.B));
	}

	private static Drawing CreateDrawing(ParsedShape parsed, IReadOnlyDictionary<string, Color> colors, Transform? transform)
	{
		var shape = parsed.Shape;
		var fill = shape.Fill is { } fillColor ? CreateFill(shape, Resolve(fillColor, colors), colors) : null;
		var pen = shape.Stroke is { } strokeColor
			? new Pen(new SolidColorBrush(Resolve(strokeColor, colors)), shape.StrokeWidth ?? 1)
			{
				StartLineCap = PenLineCap.Round,
				EndLineCap = PenLineCap.Round,
				LineJoin = PenLineJoin.Round,
			}
			: null;

		// Transforming the geometry rather than the drawing keeps the outline's width.
		var geometry = parsed.Geometry;
		if (transform is not null)
		{
			geometry = geometry.Clone();
			geometry.Transform = transform;
			geometry.Freeze();
		}

		Drawing drawing = new GeometryDrawing(fill, pen, geometry);

		// Group opacity keeps a shape's outline from showing through its own fill.
		if (shape.Opacity is { } opacity and < 1)
			drawing = new DrawingGroup { Opacity = opacity, Children = { drawing } };

		return drawing;
	}

	private static Brush CreateFill(PortraitShape shape, Color from, IReadOnlyDictionary<string, Color> colors)
	{
		// A gradient without an end color fades the fill color out, keeping its hue so it doesn't darken as it fades.
		var to = shape.FillTo is { } fillTo ? Resolve(fillTo, colors) : Color.FromArgb(0, from.R, from.G, from.B);
		return shape.Gradient switch
		{
			PortraitGradient.Vertical => new LinearGradientBrush(from, to, new Point(0, 0), new Point(0, 1)),
			PortraitGradient.Horizontal => new LinearGradientBrush(from, to, new Point(0, 0), new Point(1, 0)),
			PortraitGradient.Radial => new RadialGradientBrush(from, to),
			_ => new SolidColorBrush(from),
		};
	}

	/// <summary>
	/// Gets a character's portrait in a place, drawing it the first time it's asked for with those traits. Portraits
	/// are kept for as long as the traits object is.
	/// </summary>
	public DrawingImage GetPortrait(CharacterTraits traits, PortraitPlace? place)
	{
		var portraits = m_portraits.GetValue(traits, _ => []);
		lock (portraits)
		{
			if (!portraits.TryGetValue(new ValueTuple<PortraitPlace?>(place), out var portrait))
			{
				portrait = Render(traits, place);
				portraits.Add(new ValueTuple<PortraitPlace?>(place), portrait);
			}

			return portrait;
		}
	}

	private sealed record ParsedLayer(PortraitLayer Layer, IReadOnlyList<ParsedShape> Shapes);

	private sealed record ParsedShape(PortraitShape Shape, Geometry Geometry);

	private readonly DataCatalog<CharacterProperty> m_properties;
	private readonly IReadOnlyList<PortraitColor> m_colors;
	private readonly IReadOnlyList<PortraitScale> m_scales;
	private readonly IReadOnlyList<ParsedLayer> m_layers;
	private readonly Geometry m_bounds;
	private readonly ConditionalWeakTable<CharacterTraits, Dictionary<ValueTuple<PortraitPlace?>, DrawingImage>> m_portraits = new();
}
