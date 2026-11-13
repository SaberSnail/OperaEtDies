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
		m_offsets = data.Offsets;
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
	public DrawingImage Render(CharacterTraits traits, PortraitPlace? place) => Render(traits, place, null, null);

	/// <summary>
	/// Draws a character's portrait in a place, with layers and shapes drawn or left out regardless of their conditions
	/// where the overrides say so. An override gets a layer's index (and a shape's index in its layer) and returns
	/// true to draw it, false to leave it out, or null to follow its conditions. See <see cref="IsShapeDrawn"/>.
	/// </summary>
	public DrawingImage Render(CharacterTraits traits, PortraitPlace? place, Func<int, bool?>? layerOverride, Func<int, int, bool?>? shapeOverride)
	{
		var getValueId = GetValueIdFunc(traits);
		var colors = GetColors(getValueId);
		var scales = GetScales(getValueId);
		var offsets = GetOffsets(getValueId);

		var portrait = new DrawingGroup { ClipGeometry = m_bounds };

		// Fixes the drawing's bounds to the full portrait, whatever its layers cover.
		portrait.Children.Add(new GeometryDrawing(Brushes.Transparent, null, m_bounds));
		for (var layerIndex = 0; layerIndex < m_layers.Count; layerIndex++)
		{
			var layer = m_layers[layerIndex];
			var isLayerDrawn = layer.Layer.IsDrawn(getValueId, place);
			var offset = offsets.GetValueOrDefault(layer.Layer.Offset ?? "");
			for (var shapeIndex = 0; shapeIndex < layer.Shapes.Count; shapeIndex++)
			{
				var shape = layer.Shapes[shapeIndex];
				if (IsShapeDrawn(isLayerDrawn, CharacterCondition.IsMet(shape.Shape.When, getValueId), layerOverride?.Invoke(layerIndex), shapeOverride?.Invoke(layerIndex, shapeIndex)))
					portrait.Children.Add(CreateDrawing(shape, colors, CombineTransforms(scales.GetValueOrDefault(shape.Shape.Scale ?? layer.Layer.Scale ?? ""), offset)));
			}
		}

		var image = new DrawingImage(portrait);
		image.Freeze();
		return image;
	}

	/// <summary>
	/// Whether a shape is drawn, given whether its layer is drawn and its own condition is met, and any overrides. A
	/// shape's override wins over its layer's, and a layer's override replaces whether the layer is drawn, but its
	/// shapes still need their own conditions.
	/// </summary>
	public static bool IsShapeDrawn(bool isLayerDrawn, bool isShapeConditionMet, bool? layerOverride, bool? shapeOverride) =>
		shapeOverride ?? ((layerOverride ?? isLayerDrawn) && isShapeConditionMet);

	/// <summary>
	/// Works out every portrait color for a character, by id.
	/// </summary>
	public IReadOnlyDictionary<string, Color> GetColors(CharacterTraits traits) => GetColors(GetValueIdFunc(traits));

	/// <summary>
	/// Gets every shape of every layer as it's placed in a character's portrait in a place, from bottom to top,
	/// including the shapes that aren't drawn.
	/// </summary>
	public IReadOnlyList<PortraitShapePlacement> GetShapePlacements(CharacterTraits traits, PortraitPlace? place)
	{
		var getValueId = GetValueIdFunc(traits);
		var scales = GetScales(getValueId);
		var offsets = GetOffsets(getValueId);
		var placements = new List<PortraitShapePlacement>();
		for (var layerIndex = 0; layerIndex < m_layers.Count; layerIndex++)
		{
			var layer = m_layers[layerIndex];
			var isLayerConditionMet = CharacterCondition.IsMet(layer.Layer.When, getValueId);
			var isPlaceMet = layer.Layer.At is null || layer.Layer.At.IsMet(place);
			var offset = offsets.GetValueOrDefault(layer.Layer.Offset ?? "");
			for (var shapeIndex = 0; shapeIndex < layer.Shapes.Count; shapeIndex++)
			{
				var shape = layer.Shapes[shapeIndex];
				var transform = CombineTransforms(scales.GetValueOrDefault(shape.Shape.Scale ?? layer.Layer.Scale ?? ""), offset);
				placements.Add(new PortraitShapePlacement(layerIndex, shapeIndex, shape.Shape, shape.Geometry, transform?.Value ?? Matrix.Identity)
				{
					IsLayerConditionMet = isLayerConditionMet,
					IsPlaceMet = isPlaceMet,
					IsShapeConditionMet = CharacterCondition.IsMet(shape.Shape.When, getValueId),
				});
			}
		}

		return placements;
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
	private Dictionary<string, Transform> GetScales(Func<string, string?> getValueId)
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

	/// <summary>
	/// Works out the shift of every portrait offset for a character, leaving out the ones that don't move anything.
	/// </summary>
	private Dictionary<string, Transform> GetOffsets(Func<string, string?> getValueId)
	{
		var transforms = new Dictionary<string, Transform>(StringComparer.Ordinal);
		foreach (var offset in m_offsets)
		{
			var variant = offset.Variants?.FirstOrDefault(x => CharacterCondition.IsMet(x.When, getValueId));
			var (x, y) = variant is null ? (offset.X, offset.Y) : (variant.X, variant.Y);
			if (x != 0 || y != 0)
			{
				var transform = new TranslateTransform(x, y);
				transform.Freeze();
				transforms[offset.Id] = transform;
			}
		}

		return transforms;
	}

	/// <summary>
	/// Stretches by the scale, then shifts by the offset, either of which may be missing.
	/// </summary>
	private static Transform? CombineTransforms(Transform? scale, Transform? offset)
	{
		if (scale is null || offset is null)
			return scale ?? offset;

		var group = new TransformGroup { Children = { scale, offset } };
		group.Freeze();
		return group;
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
	private readonly IReadOnlyList<PortraitOffset> m_offsets;
	private readonly IReadOnlyList<ParsedLayer> m_layers;
	private readonly Geometry m_bounds;
	private readonly ConditionalWeakTable<CharacterTraits, Dictionary<ValueTuple<PortraitPlace?>, DrawingImage>> m_portraits = new();
}

/// <summary>
/// Where a shape of a portrait layer is placed in a character's portrait: its geometry before it's moved, how it's
/// stretched and moved, and which of the conditions for drawing it are met.
/// </summary>
public sealed record PortraitShapePlacement(int LayerIndex, int ShapeIndex, PortraitShape Shape, Geometry Geometry, Matrix Transform)
{
	/// <summary>
	/// Whether the character meets the layer's condition.
	/// </summary>
	public bool IsLayerConditionMet { get; init; }

	/// <summary>
	/// Whether the character is in one of the places the layer is drawn in.
	/// </summary>
	public bool IsPlaceMet { get; init; }

	/// <summary>
	/// Whether the character meets the shape's own condition.
	/// </summary>
	public bool IsShapeConditionMet { get; init; }

	public bool IsLayerDrawn => IsLayerConditionMet && IsPlaceMet;

	public bool IsDrawn => IsLayerDrawn && IsShapeConditionMet;
}
