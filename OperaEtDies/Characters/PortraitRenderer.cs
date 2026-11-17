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

		var shapeSets = new Dictionary<string, ParsedShapeSet>(StringComparer.Ordinal);
		foreach (var set in data.ShapeSets)
			shapeSets.TryAdd(set.Id, new ParsedShapeSet(set, set.Shapes.Select(x => Parse(x, null)).ToList()));
		m_layers = data.Layers
			.Select(layer => new ParsedLayer(layer, layer.Shapes.Select(shape => Parse(shape, shape.Include is { } include ? shapeSets.GetValueOrDefault(include) : null)).ToList()))
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
	/// Gets the ids of the layers drawn for a character in a place wearing clothing, from bottom to top.
	/// </summary>
	public IReadOnlyList<string> GetLayerIds(CharacterTraits traits, PortraitPlace? place, PortraitClothing clothing)
	{
		var getValueId = GetValueIdFunc(traits);
		return m_layers.Where(x => x.Layer.IsDrawn(getValueId, place, clothing)).Select(x => x.Layer.Id).ToList();
	}

	/// <summary>
	/// Draws a character's portrait in a place, wearing clothing. A null place only draws the layers that are drawn
	/// anywhere.
	/// </summary>
	public DrawingImage Render(CharacterTraits traits, PortraitPlace? place, PortraitClothing clothing) => Render(traits, place, clothing, null, null);

	/// <summary>
	/// Draws a character's portrait in a place wearing clothing, with layers and shapes drawn or left out regardless of
	/// their conditions where the overrides say so. An override gets a layer's index (and a shape's index in its layer)
	/// and returns true to draw it, false to leave it out, or null to follow its conditions. See
	/// <see cref="IsShapeDrawn"/>.
	/// </summary>
	public DrawingImage Render(CharacterTraits traits, PortraitPlace? place, PortraitClothing clothing, Func<int, bool?>? layerOverride, Func<int, int, bool?>? shapeOverride)
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
			var isLayerDrawn = layer.Layer.IsDrawn(getValueId, place, clothing);
			var offset = offsets.GetValueOrDefault(layer.Layer.Offset ?? "");
			var index = layerIndex;
			bool IsDrawn(int shapeIndex) =>
				IsShapeDrawn(isLayerDrawn, CharacterCondition.IsMet(layer.Shapes[shapeIndex].Shape.When, getValueId), layerOverride?.Invoke(index), shapeOverride?.Invoke(index, shapeIndex));

			IReadOnlyDictionary<string, Color>? layerColors = null;
			Geometry? clip = null;
			for (var shapeIndex = 0; shapeIndex < layer.Shapes.Count; shapeIndex++)
			{
				if (IsDrawn(shapeIndex))
				{
					var shape = layer.Shapes[shapeIndex];
					layerColors ??= GetLayerColors(layer.Layer, clothing, colors, getValueId);
					if (shape.Include is { } set)
					{
						if (shape.Shape.Clip)
							clip ??= GetClip(layer, scales, offset, IsDrawn);
						portrait.Children.Add(CreateIncludeDrawing(set, shape.Shape.Clip ? clip : null, layerColors, getValueId, x => CombineTransforms(scales.GetValueOrDefault(x.Scale ?? set.Set.Scale ?? layer.Layer.Scale ?? ""), offset)));
					}
					else
					{
						portrait.Children.Add(CreateDrawing(shape, layerColors, CombineTransforms(scales.GetValueOrDefault(shape.Shape.Scale ?? layer.Layer.Scale ?? ""), offset)));
					}
				}
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
	/// Gets every shape of every layer as it's placed in a character's portrait in a place wearing clothing, from bottom
	/// to top, including the shapes that aren't drawn. A shape that includes a shape set is placed as the set's shapes
	/// together, already stretched and moved.
	/// </summary>
	public IReadOnlyList<PortraitShapePlacement> GetShapePlacements(CharacterTraits traits, PortraitPlace? place, PortraitClothing clothing)
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
			var isClothingMet = layer.Layer.IsClothingMet(clothing);
			var offset = offsets.GetValueOrDefault(layer.Layer.Offset ?? "");
			for (var shapeIndex = 0; shapeIndex < layer.Shapes.Count; shapeIndex++)
			{
				var shape = layer.Shapes[shapeIndex];
				var (geometry, matrix) = shape.Include is { } set
					? (CombineGeometries(set.Shapes.Select(x => Transform(x.Geometry, CombineTransforms(scales.GetValueOrDefault(x.Shape.Scale ?? set.Set.Scale ?? layer.Layer.Scale ?? ""), offset)))), Matrix.Identity)
					: (shape.Geometry, CombineTransforms(scales.GetValueOrDefault(shape.Shape.Scale ?? layer.Layer.Scale ?? ""), offset)?.Value ?? Matrix.Identity);
				placements.Add(new PortraitShapePlacement(layerIndex, shapeIndex, shape.Shape, geometry, matrix)
				{
					IsLayerConditionMet = isLayerConditionMet,
					IsPlaceMet = isPlaceMet,
					IsClothingMet = isClothingMet,
					IsShapeConditionMet = CharacterCondition.IsMet(shape.Shape.When, getValueId),
				});
			}
		}

		return placements;
	}

	/// <summary>
	/// Gets a character's portrait in a place wearing clothing, drawing it the first time it's asked for with those
	/// traits. Portraits are kept for as long as the traits object is.
	/// </summary>
	public DrawingImage GetPortrait(CharacterTraits traits, PortraitPlace? place, PortraitClothing clothing)
	{
		var portraits = m_portraits.GetValue(traits, _ => []);
		lock (portraits)
		{
			var key = new PortraitKey(place, clothing);
			if (!portraits.TryGetValue(key, out var portrait))
			{
				portrait = Render(traits, place, clothing);
				portraits.Add(key, portrait);
			}

			return portrait;
		}
	}

	private static ParsedShape Parse(PortraitShape shape, ParsedShapeSet? include) =>
		new(shape, shape.Path is { } path && PortraitShape.TryParsePath(path, out var geometry, out _) ? geometry : Geometry.Empty, include);

	private Func<string, string?> GetValueIdFunc(CharacterTraits traits) =>
		propertyId => m_properties.Find(propertyId)?.GetConditionValueId(traits.Values.GetValueOrDefault(propertyId));

	/// <summary>
	/// Works out every portrait color for a character, in order, since a color can use the colors before it.
	/// </summary>
	private Dictionary<string, Color> GetColors(Func<string, string?> getValueId)
	{
		var colors = new Dictionary<string, Color>(StringComparer.Ordinal);
		AddColors(m_colors, colors, getValueId);
		return colors;
	}

	/// <summary>
	/// Gets the colors a layer's shapes use: the portrait colors, then the colors of the piece of clothing it draws, by
	/// color name, then the layer's own colors, each replacing any with the same id before it.
	/// </summary>
	private static IReadOnlyDictionary<string, Color> GetLayerColors(PortraitLayer layer, PortraitClothing clothing, Dictionary<string, Color> colors, Func<string, string?> getValueId)
	{
		var piece = layer.FindPiece(clothing);
		if (piece is null && layer.Colors is null)
			return colors;

		var layerColors = new Dictionary<string, Color>(colors, StringComparer.Ordinal);
		foreach (var (name, hex) in piece?.Colors ?? new Dictionary<string, string>())
		{
			if (PortraitColor.TryParseHex(hex, out var color))
				layerColors[name] = color;
		}

		AddColors(layer.Colors ?? [], layerColors, getValueId);
		return layerColors;
	}

	/// <summary>
	/// Works out colors in order, adding each to the colors so later ones can use it.
	/// </summary>
	private static void AddColors(IEnumerable<PortraitColor> toAdd, Dictionary<string, Color> colors, Func<string, string?> getValueId)
	{
		foreach (var color in toAdd)
		{
			var reference = color.Variants?.FirstOrDefault(x => CharacterCondition.IsMet(x.When, getValueId))?.Color ?? color.Color;
			var value = Resolve(reference, colors);
			if (color.Mix is { } mix && color.Amount is { } amount)
				value = Blend(value, Resolve(mix, colors), amount);
			colors[color.Id] = value;
		}
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

	/// <summary>
	/// Gets the area a layer's drawn shapes fill, other than the shape sets it includes, as placed in the portrait.
	/// </summary>
	private static Geometry GetClip(ParsedLayer layer, IReadOnlyDictionary<string, Transform> scales, Transform? offset, Func<int, bool> isDrawn) =>
		CombineGeometries(layer.Shapes
			.Select((shape, index) => (shape, index))
			.Where(x => x.shape.Include is null && x.shape.Shape.Fill is not null && isDrawn(x.index))
			.Select(x => Transform(x.shape.Geometry, CombineTransforms(scales.GetValueOrDefault(x.shape.Shape.Scale ?? layer.Layer.Scale ?? ""), offset))));

	/// <summary>
	/// Combines geometries into one that covers everywhere any of them does.
	/// </summary>
	private static Geometry CombineGeometries(IEnumerable<Geometry> geometries)
	{
		var group = new GeometryGroup { FillRule = FillRule.Nonzero };
		foreach (var geometry in geometries)
			group.Children.Add(geometry);
		group.Freeze();
		return group;
	}

	private static Geometry Transform(Geometry geometry, Transform? transform)
	{
		if (transform is null)
			return geometry;

		var transformed = geometry.Clone();
		transformed.Transform = transform;
		transformed.Freeze();
		return transformed;
	}

	private static Color Resolve(string reference, IReadOnlyDictionary<string, Color> colors) =>
		PortraitColor.TryParseHex(reference, out var color) ? color : colors.GetValueOrDefault(reference, Colors.Magenta);

	private static Color Blend(Color from, Color to, double amount)
	{
		byte Mix(byte a, byte b) => (byte) Math.Round(a + ((b - a) * amount));
		return Color.FromArgb(Mix(from.A, to.A), Mix(from.R, to.R), Mix(from.G, to.G), Mix(from.B, to.B));
	}

	/// <summary>
	/// Draws the shapes of an included shape set whose conditions are met, in the including layer's colors, clipped
	/// to the given area if there is one.
	/// </summary>
	private static Drawing CreateIncludeDrawing(ParsedShapeSet set, Geometry? clip, IReadOnlyDictionary<string, Color> colors, Func<string, string?> getValueId, Func<PortraitShape, Transform?> getTransform)
	{
		var group = new DrawingGroup { ClipGeometry = clip };
		foreach (var shape in set.Shapes.Where(x => CharacterCondition.IsMet(x.Shape.When, getValueId)))
			group.Children.Add(CreateDrawing(shape, colors, getTransform(shape.Shape)));
		return group;
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
		Drawing drawing = new GeometryDrawing(fill, pen, Transform(parsed.Geometry, transform));

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

	private sealed record ParsedLayer(PortraitLayer Layer, IReadOnlyList<ParsedShape> Shapes);

	/// <summary>
	/// A shape with its parsed path, or the shape set it includes.
	/// </summary>
	private sealed record ParsedShape(PortraitShape Shape, Geometry Geometry, ParsedShapeSet? Include);

	private sealed record ParsedShapeSet(PortraitShapeSet Set, IReadOnlyList<ParsedShape> Shapes);

	private readonly record struct PortraitKey(PortraitPlace? Place, PortraitClothing Clothing);

	private readonly DataCatalog<CharacterProperty> m_properties;
	private readonly IReadOnlyList<PortraitColor> m_colors;
	private readonly IReadOnlyList<PortraitScale> m_scales;
	private readonly IReadOnlyList<PortraitOffset> m_offsets;
	private readonly IReadOnlyList<ParsedLayer> m_layers;
	private readonly Geometry m_bounds;
	private readonly ConditionalWeakTable<CharacterTraits, Dictionary<PortraitKey, DrawingImage>> m_portraits = new();
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
	/// Whether the character's clothing lets the layer be drawn: they're wearing clothing it draws, if it draws any, and
	/// nothing they wear hides its group.
	/// </summary>
	public bool IsClothingMet { get; init; } = true;

	/// <summary>
	/// Whether the character meets the shape's own condition.
	/// </summary>
	public bool IsShapeConditionMet { get; init; }

	public bool IsLayerDrawn => IsLayerConditionMet && IsPlaceMet && IsClothingMet;

	public bool IsDrawn => IsLayerDrawn && IsShapeConditionMet;
}
