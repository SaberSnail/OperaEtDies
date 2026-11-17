using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// How character portraits are drawn, from the portrait sections of the data: the colors, stretches, and shifts portraits use, and the layers drawn from
/// a character's properties, bottom to top.
/// </summary>
public sealed class PortraitData : IValidatedData
{
	/// <summary>
	/// The width of a portrait, in the units used by the layers' paths.
	/// </summary>
	public required double Width { get; init; }

	/// <summary>
	/// The height of a portrait, in the units used by the layers' paths.
	/// </summary>
	public required double Height { get; init; }

	/// <summary>
	/// The named colors the layers can use, in the order they're worked out.
	/// </summary>
	public IReadOnlyList<PortraitColor> Colors { get; init; } = [];

	/// <summary>
	/// The named stretches the layers can use.
	/// </summary>
	public IReadOnlyList<PortraitScale> Scales { get; init; } = [];

	/// <summary>
	/// The named shifts the layers can use.
	/// </summary>
	public IReadOnlyList<PortraitOffset> Offsets { get; init; } = [];

	/// <summary>
	/// The layers of a portrait, from bottom to top.
	/// </summary>
	public required IReadOnlyList<PortraitLayer> Layers { get; init; }

	/// <summary>
	/// The shape sets layers can include.
	/// </summary>
	public IReadOnlyList<PortraitShapeSet> ShapeSets { get; init; } = [];

	/// <summary>
	/// Gets every color a layer's shapes use that isn't a hex color, a portrait color, or one of the layer's own colors,
	/// with the path to it and the clothing the layer draws, so they can be checked against the colors of that clothing.
	/// Layers that don't draw clothing have none, since those are errors on their own.
	/// </summary>
	public IEnumerable<(string Path, IReadOnlyList<string> Clothing, string Color)> GetClothingColors()
	{
		var colorIds = Colors.Where(x => x?.Id is not null).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		var shapeSets = GetShapeSets();
		foreach (var layer in Layers.Where(x => x?.Id is not null && x.Clothing is not null))
		{
			foreach (var (path, color, localIds) in GetColorReferences(layer, shapeSets).Where(x => !IsKnownColor(x.Color, colorIds, x.LocalIds)))
				yield return (path, layer.Clothing!, color);
		}
	}

	/// <summary>
	/// Gets every condition in the file, with the path to it, so they can be checked against the character properties.
	/// </summary>
	public IEnumerable<(string Path, CharacterCondition Condition)> GetConditions()
	{
		foreach (var color in Colors.Where(x => x is not null))
		{
			foreach (var (variant, index) in (color.Variants ?? []).Select((x, i) => (x, i)).Where(x => x.x?.When is not null))
				yield return ($"portraitColors[{color.Id}].variants[{index}].when", variant.When);
		}

		foreach (var scale in Scales.Where(x => x is not null))
		{
			foreach (var (variant, index) in (scale.Variants ?? []).Select((x, i) => (x, i)).Where(x => x.x?.When is not null))
				yield return ($"portraitScales[{scale.Id}].variants[{index}].when", variant.When);
		}

		foreach (var offset in Offsets.Where(x => x is not null))
		{
			foreach (var (variant, index) in (offset.Variants ?? []).Select((x, i) => (x, i)).Where(x => x.x?.When is not null))
				yield return ($"portraitOffsets[{offset.Id}].variants[{index}].when", variant.When);
		}

		foreach (var layer in Layers.Where(x => x is not null))
		{
			if (layer.When is { } layerWhen)
				yield return ($"portraitLayers[{layer.Id}].when", layerWhen);
			foreach (var (shape, index) in (layer.Shapes ?? []).Select((x, i) => (x, i)).Where(x => x.x?.When is not null))
				yield return ($"portraitLayers[{layer.Id}].shapes[{index}].when", shape.When!);
			foreach (var color in (layer.Colors ?? []).Where(x => x is not null))
			{
				foreach (var (variant, index) in (color.Variants ?? []).Select((x, i) => (x, i)).Where(x => x.x?.When is not null))
					yield return ($"portraitLayers[{layer.Id}].colors[{color.Id}].variants[{index}].when", variant.When);
			}
		}

		foreach (var set in ShapeSets.Where(x => x is not null))
		{
			foreach (var (shape, index) in (set.Shapes ?? []).Select((x, i) => (x, i)).Where(x => x.x?.When is not null))
				yield return ($"portraitShapeSets[{set.Id}].shapes[{index}].when", shape.When!);
		}
	}

	/// <summary>
	/// Gets every kind of quarter a layer is drawn in, with the path to it, so they can be checked against the quarter
	/// kinds.
	/// </summary>
	public IEnumerable<(string Path, string QuarterKind)> GetQuarterKinds() =>
		Layers.Where(x => x?.At?.QuarterKinds is not null)
			.SelectMany(layer => layer.At!.QuarterKinds!.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => ($"portraitLayers[{layer.Id}].at.quarterKinds", x)));

	/// <summary>
	/// Gets the profession of every workplace a layer is drawn in, with the path to it, so they can be checked against
	/// the profession property.
	/// </summary>
	public IEnumerable<(string Path, string Profession)> GetWorkplaces() =>
		Layers.Where(x => x?.At?.Workplaces is not null)
			.SelectMany(layer => layer.At!.Workplaces!.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => ($"portraitLayers[{layer.Id}].at.workplaces", x)));

	public IEnumerable<string> Validate()
	{
		var errors = new List<string>();
		if (Width <= 0)
			errors.Add(OurResources.DataNumberNotPositive.FormatCurrentCulture("portraits.width", Width));
		if (Height <= 0)
			errors.Add(OurResources.DataNumberNotPositive.FormatCurrentCulture("portraits.height", Height));

		// A color can only use the colors before it, so they can be worked out in order.
		var colorIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var color in Colors)
		{
			if (color is null)
			{
				errors.Add(OurResources.DataListItemEmpty.FormatCurrentCulture("portraitColors"));
			}
			else if (string.IsNullOrWhiteSpace(color.Id))
			{
				errors.Add(OurResources.DataListItemIdMissing.FormatCurrentCulture("portraitColors"));
			}
			else
			{
				errors.AddRange(ValidateColor(color, colorIds));
				if (!colorIds.Add(color.Id))
					errors.Add(OurResources.DataListItemIdDuplicate.FormatCurrentCulture(color.Id, "portraitColors"));
			}
		}

		var scaleIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var scale in Scales)
		{
			if (scale is null)
			{
				errors.Add(OurResources.DataListItemEmpty.FormatCurrentCulture("portraitScales"));
			}
			else if (string.IsNullOrWhiteSpace(scale.Id))
			{
				errors.Add(OurResources.DataListItemIdMissing.FormatCurrentCulture("portraitScales"));
			}
			else
			{
				errors.AddRange(ValidateScale(scale));
				if (!scaleIds.Add(scale.Id))
					errors.Add(OurResources.DataListItemIdDuplicate.FormatCurrentCulture(scale.Id, "portraitScales"));
			}
		}

		var offsetIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var offset in Offsets)
		{
			if (offset is null)
			{
				errors.Add(OurResources.DataListItemEmpty.FormatCurrentCulture("portraitOffsets"));
			}
			else if (string.IsNullOrWhiteSpace(offset.Id))
			{
				errors.Add(OurResources.DataListItemIdMissing.FormatCurrentCulture("portraitOffsets"));
			}
			else
			{
				errors.AddRange(ValidateOffset(offset));
				if (!offsetIds.Add(offset.Id))
					errors.Add(OurResources.DataListItemIdDuplicate.FormatCurrentCulture(offset.Id, "portraitOffsets"));
			}
		}

		var shapeSetIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var set in ShapeSets)
		{
			if (set is null)
			{
				errors.Add(OurResources.DataListItemEmpty.FormatCurrentCulture("portraitShapeSets"));
			}
			else if (string.IsNullOrWhiteSpace(set.Id))
			{
				errors.Add(OurResources.DataListItemIdMissing.FormatCurrentCulture("portraitShapeSets"));
			}
			else
			{
				if (!shapeSetIds.Add(set.Id))
					errors.Add(OurResources.DataListItemIdDuplicate.FormatCurrentCulture(set.Id, "portraitShapeSets"));
				errors.AddRange(ValidateShapeSet(set, scaleIds));
			}
		}

		if (Layers.Count == 0)
			errors.Add(OurResources.DataListEmpty.FormatCurrentCulture("portraitLayers"));

		var shapeSets = GetShapeSets();
		var layerIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var layer in Layers)
		{
			if (layer is null)
			{
				errors.Add(OurResources.DataListItemEmpty.FormatCurrentCulture("portraitLayers"));
			}
			else if (string.IsNullOrWhiteSpace(layer.Id))
			{
				errors.Add(OurResources.DataListItemIdMissing.FormatCurrentCulture("portraitLayers"));
			}
			else
			{
				if (!layerIds.Add(layer.Id))
					errors.Add(OurResources.DataListItemIdDuplicate.FormatCurrentCulture(layer.Id, "portraitLayers"));
				errors.AddRange(ValidateLayer(layer, colorIds, scaleIds, offsetIds, shapeSets));
			}
		}

		return errors;
	}

	private static IEnumerable<string> ValidateColor(PortraitColor color, IReadOnlySet<string> earlierColorIds) =>
		ValidateColor($"portraitColors[{color.Id}]", color, earlierColorIds.Contains);

	/// <summary>
	/// Checks a color, given whether a color reference it uses is known.
	/// </summary>
	private static IEnumerable<string> ValidateColor(string path, PortraitColor color, Func<string, bool> isKnownColor)
	{
		foreach (var error in ValidateColorReference($"{path}.color", color.Color, isKnownColor, isRequired: true))
			yield return error;

		if (color.Variants is { } variants)
		{
			if (variants.Count == 0)
				yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.variants");

			for (var index = 0; index < variants.Count; index++)
			{
				var variantPath = $"{path}.variants[{index}]";
				if (variants[index] is not { } variant)
				{
					yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.variants");
				}
				else
				{
					if (variant.When is null)
						yield return OurResources.DataValueMissing.FormatCurrentCulture($"{variantPath}.when");
					foreach (var error in ValidateColorReference($"{variantPath}.color", variant.Color, isKnownColor, isRequired: true))
						yield return error;
				}
			}
		}

		if ((color.Mix is null) != (color.Amount is null))
			yield return OurResources.PortraitColorMixIncomplete.FormatCurrentCulture(path);
		foreach (var error in ValidateColorReference($"{path}.mix", color.Mix, isKnownColor, isRequired: false))
			yield return error;
		if (color.Amount is < 0 or > 1)
			yield return OurResources.DataFractionInvalid.FormatCurrentCulture($"{path}.amount", color.Amount);
	}

	private static IEnumerable<string> ValidateLayer(PortraitLayer layer, IReadOnlySet<string> colorIds, IReadOnlySet<string> scaleIds, IReadOnlySet<string> offsetIds, IReadOnlyDictionary<string, PortraitShapeSet> shapeSets)
	{
		var path = $"portraitLayers[{layer.Id}]";
		if (layer.Clothing is { } clothing)
		{
			foreach (var error in DataValidation.ValidateStrings($"{path}.clothing", clothing))
				yield return error;
		}

		if (layer.HiddenBy is { } hiddenBy)
		{
			if (hiddenBy.Regions is not { Count: > 0 })
				yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.hiddenBy.regions");
			if (hiddenBy.Coverage < 1)
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.hiddenBy.coverage", hiddenBy.Coverage, 1);
			else if (hiddenBy.Coverage > ClothingItem.MaxCoverage)
				yield return OurResources.DataNumberTooLarge.FormatCurrentCulture($"{path}.hiddenBy.coverage", hiddenBy.Coverage, ClothingItem.MaxCoverage);
		}

		// A layer's own colors can use the colors before them. The colors of the clothing it draws are checked against the
		// clothing data, so they're taken to be known here.
		var localIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var color in layer.Colors ?? [])
		{
			if (color is null)
			{
				yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.colors");
			}
			else if (string.IsNullOrWhiteSpace(color.Id))
			{
				yield return OurResources.DataListItemIdMissing.FormatCurrentCulture($"{path}.colors");
			}
			else
			{
				foreach (var error in ValidateColor($"{path}.colors[{color.Id}]", color, x => true))
					yield return error;
				if (!localIds.Add(color.Id))
					yield return OurResources.DataListItemIdDuplicate.FormatCurrentCulture(color.Id, $"{path}.colors");
			}
		}

		if (layer.Clothing is null)
		{
			foreach (var (colorPath, color, _) in GetColorReferences(layer, shapeSets).Where(x => !IsKnownColor(x.Color, colorIds, x.LocalIds)))
				yield return OurResources.PortraitColorUnknown.FormatCurrentCulture(colorPath, color);
		}

		foreach (var error in ValidateScaleReference($"{path}.scale", layer.Scale, scaleIds))
			yield return error;
		if (layer.Offset is { } offset && !offsetIds.Contains(offset))
			yield return OurResources.PortraitOffsetUnknown.FormatCurrentCulture($"{path}.offset", offset);

		if (layer.At is { } at)
		{
			if (at.QuarterKinds is not null)
			{
				foreach (var error in DataValidation.ValidateStrings($"{path}.at.quarterKinds", at.QuarterKinds))
					yield return error;
			}

			if (at.Workplaces is not null)
			{
				foreach (var error in DataValidation.ValidateStrings($"{path}.at.workplaces", at.Workplaces))
					yield return error;
			}

			if (!at.Home && at.QuarterKinds is null && at.Workplaces is null)
				yield return OurResources.PortraitPlaceEmpty.FormatCurrentCulture($"{path}.at");
		}

		if (layer.Shapes is null || layer.Shapes.Count == 0)
		{
			yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.shapes");
		}
		else
		{
			for (var index = 0; index < layer.Shapes.Count; index++)
			{
				if (layer.Shapes[index] is not { } shape)
				{
					yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.shapes");
				}
				else
				{
					foreach (var error in ValidateShape($"{path}.shapes[{index}]", shape, scaleIds, shapeSets))
						yield return error;
				}
			}
		}
	}

	private static IEnumerable<string> ValidateShapeSet(PortraitShapeSet set, IReadOnlySet<string> scaleIds)
	{
		var path = $"portraitShapeSets[{set.Id}]";
		foreach (var error in ValidateScaleReference($"{path}.scale", set.Scale, scaleIds))
			yield return error;

		if (set.Shapes is null || set.Shapes.Count == 0)
		{
			yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.shapes");
		}
		else
		{
			for (var index = 0; index < set.Shapes.Count; index++)
			{
				if (set.Shapes[index] is not { } shape)
				{
					yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.shapes");
				}
				else if (shape.Include is not null)
				{
					yield return OurResources.PortraitShapeSetNested.FormatCurrentCulture($"{path}.shapes[{index}]");
				}
				else
				{
					foreach (var error in ValidateShape($"{path}.shapes[{index}]", shape, scaleIds, null))
						yield return error;
				}
			}
		}
	}

	/// <summary>
	/// Checks a shape, other than its colors, which depend on the layer it's drawn in. Shapes in shape sets are checked
	/// without the shape sets, since they can't include them.
	/// </summary>
	private static IEnumerable<string> ValidateShape(string path, PortraitShape shape, IReadOnlySet<string> scaleIds, IReadOnlyDictionary<string, PortraitShapeSet>? shapeSets) =>
		shape.Include is { } include ? ValidateInclude(path, shape, include, shapeSets) : ValidateDrawnShape(path, shape, scaleIds);

	/// <summary>
	/// Checks a shape that includes a shape set, which can only have a condition and be clipped.
	/// </summary>
	private static IEnumerable<string> ValidateInclude(string path, PortraitShape shape, string include, IReadOnlyDictionary<string, PortraitShapeSet>? shapeSets)
	{
		if (shape.Path is not null || shape.Fill is not null || shape.Stroke is not null || shape.Gradient is not null || shape.FillTo is not null || shape.StrokeWidth is not null ||
			shape.Opacity is not null || shape.Scale is not null)
		{
			yield return OurResources.PortraitIncludeDrawn.FormatCurrentCulture(path);
		}

		if (shapeSets is not null && !shapeSets.ContainsKey(include))
			yield return OurResources.DataIdUnknown.FormatCurrentCulture($"{path}.include", include, "portraitShapeSets");
	}

	private static IEnumerable<string> ValidateDrawnShape(string path, PortraitShape shape, IReadOnlySet<string> scaleIds)
	{
		if (shape.Clip)
			yield return OurResources.PortraitClipWithoutInclude.FormatCurrentCulture(path);
		if (string.IsNullOrWhiteSpace(shape.Path))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.path");
		else if (!PortraitShape.TryParsePath(shape.Path, out _, out var pathError))
			yield return OurResources.PortraitPathInvalid.FormatCurrentCulture($"{path}.path", pathError);

		if (shape.Fill is null && shape.Stroke is null)
			yield return OurResources.PortraitShapeNotDrawn.FormatCurrentCulture(path);
		if (shape.Gradient is not null && shape.Fill is null)
			yield return OurResources.PortraitGradientWithoutFill.FormatCurrentCulture(path);
		if (shape.FillTo is not null && shape.Gradient is null)
			yield return OurResources.PortraitFillToWithoutGradient.FormatCurrentCulture(path);

		if (shape.StrokeWidth is <= 0)
			yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.strokeWidth", shape.StrokeWidth);
		if (shape.Opacity is < 0 or > 1)
			yield return OurResources.DataFractionInvalid.FormatCurrentCulture($"{path}.opacity", shape.Opacity);
		foreach (var error in ValidateScaleReference($"{path}.scale", shape.Scale, scaleIds))
			yield return error;
	}

	private static IEnumerable<string> ValidateScale(PortraitScale scale)
	{
		var path = $"portraitScales[{scale.Id}]";
		foreach (var error in ValidateStretch(path, scale.X, scale.Y))
			yield return error;

		if (scale.Variants is { } variants)
		{
			if (variants.Count == 0)
				yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.variants");

			for (var index = 0; index < variants.Count; index++)
			{
				var variantPath = $"{path}.variants[{index}]";
				if (variants[index] is not { } variant)
				{
					yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.variants");
				}
				else
				{
					if (variant.When is null)
						yield return OurResources.DataValueMissing.FormatCurrentCulture($"{variantPath}.when");
					foreach (var error in ValidateStretch(variantPath, variant.X, variant.Y))
						yield return error;
				}
			}
		}
	}

	private static IEnumerable<string> ValidateOffset(PortraitOffset offset)
	{
		if (offset.Variants is not { } variants)
			yield break;

		var path = $"portraitOffsets[{offset.Id}]";
		if (variants.Count == 0)
			yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.variants");

		for (var index = 0; index < variants.Count; index++)
		{
			if (variants[index] is not { } variant)
				yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.variants");
			else if (variant.When is null)
				yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.variants[{index}].when");
		}
	}

	private static IEnumerable<string> ValidateStretch(string path, double x, double y)
	{
		if (x <= 0)
			yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.x", x);
		if (y <= 0)
			yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.y", y);
	}

	private static IEnumerable<string> ValidateScaleReference(string path, string? reference, IReadOnlySet<string> scaleIds)
	{
		if (reference is not null && !scaleIds.Contains(reference))
			yield return OurResources.PortraitScaleUnknown.FormatCurrentCulture(path, reference);
	}

	private static IEnumerable<string> ValidateColorReference(string path, string? reference, Func<string, bool> isKnownColor, bool isRequired)
	{
		if (reference is null)
		{
			if (isRequired)
				yield return OurResources.DataValueMissing.FormatCurrentCulture(path);
		}
		else if (!PortraitColor.TryParseHex(reference, out _) && !isKnownColor(reference))
		{
			yield return OurResources.PortraitColorUnknown.FormatCurrentCulture(path, reference);
		}
	}

	private static bool IsKnownColor(string reference, IReadOnlySet<string> colorIds, IReadOnlySet<string> localIds) =>
		PortraitColor.TryParseHex(reference, out _) || colorIds.Contains(reference) || localIds.Contains(reference);

	/// <summary>
	/// Gets the shape sets by id, using the first of any with the same id.
	/// </summary>
	private Dictionary<string, PortraitShapeSet> GetShapeSets()
	{
		var shapeSets = new Dictionary<string, PortraitShapeSet>(StringComparer.Ordinal);
		foreach (var set in ShapeSets.Where(x => !string.IsNullOrWhiteSpace(x?.Id)))
			shapeSets.TryAdd(set.Id, set);
		return shapeSets;
	}

	/// <summary>
	/// Gets every color reference a layer uses, in its own colors, its shapes, and the shapes of the shape sets it
	/// includes, with the ids of the layer's own colors it can use there: the ones before it for a layer color, and all of
	/// them for a shape.
	/// </summary>
	private static IEnumerable<(string Path, string Color, IReadOnlySet<string> LocalIds)> GetColorReferences(PortraitLayer layer, IReadOnlyDictionary<string, PortraitShapeSet> shapeSets)
	{
		var path = $"portraitLayers[{layer.Id}]";
		var localIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var color in (layer.Colors ?? []).Where(x => !string.IsNullOrWhiteSpace(x?.Id)))
		{
			var earlierIds = localIds.ToHashSet(StringComparer.Ordinal);
			var colorPath = $"{path}.colors[{color.Id}]";
			if (color.Color is not null)
				yield return ($"{colorPath}.color", color.Color, earlierIds);
			foreach (var (variant, index) in (color.Variants ?? []).Select((x, i) => (x, i)).Where(x => x.x?.Color is not null))
				yield return ($"{colorPath}.variants[{index}].color", variant.Color, earlierIds);
			if (color.Mix is not null)
				yield return ($"{colorPath}.mix", color.Mix, earlierIds);
			localIds.Add(color.Id);
		}

		for (var index = 0; index < (layer.Shapes?.Count ?? 0); index++)
		{
			var shapePath = $"{path}.shapes[{index}]";
			var shape = layer.Shapes![index];
			if (shape?.Include is { } include)
			{
				var set = shapeSets.GetValueOrDefault(include);
				for (var setIndex = 0; setIndex < (set?.Shapes?.Count ?? 0); setIndex++)
				{
					if (set!.Shapes[setIndex] is { } setShape)
					{
						foreach (var (field, color) in GetShapeColors(setShape))
							yield return ($"{shapePath}.include: portraitShapeSets[{include}].shapes[{setIndex}].{field}", color, localIds);
					}
				}
			}
			else if (shape is not null)
			{
				foreach (var (field, color) in GetShapeColors(shape))
					yield return ($"{shapePath}.{field}", color, localIds);
			}
		}
	}

	private static IEnumerable<(string Field, string Color)> GetShapeColors(PortraitShape shape)
	{
		if (shape.Fill is not null)
			yield return ("fill", shape.Fill);
		if (shape.FillTo is not null)
			yield return ("fillTo", shape.FillTo);
		if (shape.Stroke is not null)
			yield return ("stroke", shape.Stroke);
	}
}

/// <summary>
/// The portraits section of the data: the size of a portrait.
/// </summary>
public sealed class PortraitSection
{
	/// <summary>
	/// The width of a portrait, in the units used by the layers' paths.
	/// </summary>
	public required double Width { get; init; }

	/// <summary>
	/// The height of a portrait, in the units used by the layers' paths.
	/// </summary>
	public required double Height { get; init; }
}

/// <summary>
/// Reads portrait data from the portrait sections of data, for tools that edit it on its own.
/// </summary>
public static class PortraitDataReader
{
	/// <summary>
	/// The names of the data sections that hold portrait data.
	/// </summary>
	public static IReadOnlyList<string> SectionNames { get; } = ["portraits", "portraitColors", "portraitScales", "portraitOffsets", "portraitShapeSets", "portraitLayers"];

	/// <summary>
	/// Reads and checks the portrait sections of the given data. Problems are reported against the given file.
	/// </summary>
	public static DataLoadResult<PortraitData> Read(System.Text.Json.Nodes.JsonObject root, string filePath)
	{
		var errors = new List<DataLoadError>();
		T? Section<T>(string name)
			where T : class
		{
			try
			{
				return root.TryGetPropertyValue(name, out var node) ? System.Text.Json.JsonSerializer.Deserialize<T>(node, JsonDataLoader.Options) : null;
			}
			catch (Exception e) when (e is System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
			{
				errors.Add(new DataLoadError(filePath, OurResources.DataFileInvalidJson.FormatCurrentCulture(e.Message)));
				return null;
			}
		}

		var size = Section<PortraitSection>("portraits");
		if (size is null && errors.Count == 0)
			errors.Add(new DataLoadError(filePath, OurResources.DataSectionMissing.FormatCurrentCulture("portraits")));
		var portraits = size is null ? null : new PortraitData
		{
			Width = size.Width,
			Height = size.Height,
			Colors = Section<List<PortraitColor>>("portraitColors") ?? [],
			Scales = Section<List<PortraitScale>>("portraitScales") ?? [],
			Offsets = Section<List<PortraitOffset>>("portraitOffsets") ?? [],
			Layers = Section<List<PortraitLayer>>("portraitLayers") ?? [],
			ShapeSets = Section<List<PortraitShapeSet>>("portraitShapeSets") ?? [],
		};
		if (portraits is not null && errors.Count == 0)
			errors.AddRange(portraits.Validate().Select(x => new DataLoadError(filePath, x)));
		return errors.Count != 0 ? DataLoadResult<PortraitData>.Failure(errors) : DataLoadResult<PortraitData>.Success(portraits!);
	}
}
