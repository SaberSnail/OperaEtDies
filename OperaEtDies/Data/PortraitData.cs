using System.IO;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// How character portraits are drawn, loaded from Portraits.json: the colors and stretches portraits use, and the layers drawn from
/// a character's properties, bottom to top.
/// </summary>
public sealed class PortraitData : IValidatedData
{
	public const string FileName = "Portraits.json";

	/// <summary>
	/// Loads the file from the data folder, with the changes from the mod folders applied in order. Returns null if the
	/// data folder has no portrait file, in which case characters have no portraits.
	/// </summary>
	public static DataLoadResult<PortraitData>? Load(string dataDirectory, IReadOnlyList<string> modDirectories)
	{
		var filePath = Path.Combine(dataDirectory, FileName);
		return File.Exists(filePath) ? JsonDataLoader.Load<PortraitData>(filePath, modDirectories, []) : null;
	}

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
	/// The layers of a portrait, from bottom to top.
	/// </summary>
	public required IReadOnlyList<PortraitLayer> Layers { get; init; }

	/// <summary>
	/// Gets every condition in the file, with the path to it, so they can be checked against the character properties.
	/// </summary>
	public IEnumerable<(string Path, CharacterCondition Condition)> GetConditions()
	{
		foreach (var color in Colors.Where(x => x is not null))
		{
			foreach (var (variant, index) in (color.Variants ?? []).Select((x, i) => (x, i)).Where(x => x.x?.When is not null))
				yield return ($"colors[{color.Id}].variants[{index}].when", variant.When);
		}

		foreach (var scale in Scales.Where(x => x is not null))
		{
			foreach (var (variant, index) in (scale.Variants ?? []).Select((x, i) => (x, i)).Where(x => x.x?.When is not null))
				yield return ($"scales[{scale.Id}].variants[{index}].when", variant.When);
		}

		foreach (var layer in Layers.Where(x => x is not null))
		{
			if (layer.When is { } layerWhen)
				yield return ($"layers[{layer.Id}].when", layerWhen);
			foreach (var (shape, index) in (layer.Shapes ?? []).Select((x, i) => (x, i)).Where(x => x.x?.When is not null))
				yield return ($"layers[{layer.Id}].shapes[{index}].when", shape.When!);
		}
	}

	/// <summary>
	/// Gets every kind of quarter a layer is drawn in, with the path to it, so they can be checked against the quarter
	/// kinds.
	/// </summary>
	public IEnumerable<(string Path, string QuarterKind)> GetQuarterKinds() =>
		Layers.Where(x => x?.At?.QuarterKinds is not null)
			.SelectMany(layer => layer.At!.QuarterKinds!.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => ($"layers[{layer.Id}].at.quarterKinds", x)));

	/// <summary>
	/// Gets the profession of every workplace a layer is drawn in, with the path to it, so they can be checked against
	/// the profession property.
	/// </summary>
	public IEnumerable<(string Path, string Profession)> GetWorkplaces() =>
		Layers.Where(x => x?.At?.Workplaces is not null)
			.SelectMany(layer => layer.At!.Workplaces!.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => ($"layers[{layer.Id}].at.workplaces", x)));

	public IEnumerable<string> Validate()
	{
		var errors = new List<string>();
		if (Width <= 0)
			errors.Add(OurResources.DataNumberNotPositive.FormatCurrentCulture("width", Width));
		if (Height <= 0)
			errors.Add(OurResources.DataNumberNotPositive.FormatCurrentCulture("height", Height));

		// A color can only use the colors before it, so they can be worked out in order.
		var colorIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var color in Colors)
		{
			if (color is null)
			{
				errors.Add(OurResources.DataListItemEmpty.FormatCurrentCulture("colors"));
			}
			else if (string.IsNullOrWhiteSpace(color.Id))
			{
				errors.Add(OurResources.DataListItemIdMissing.FormatCurrentCulture("colors"));
			}
			else
			{
				errors.AddRange(ValidateColor(color, colorIds));
				if (!colorIds.Add(color.Id))
					errors.Add(OurResources.DataListItemIdDuplicate.FormatCurrentCulture(color.Id, "colors"));
			}
		}

		var scaleIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var scale in Scales)
		{
			if (scale is null)
			{
				errors.Add(OurResources.DataListItemEmpty.FormatCurrentCulture("scales"));
			}
			else if (string.IsNullOrWhiteSpace(scale.Id))
			{
				errors.Add(OurResources.DataListItemIdMissing.FormatCurrentCulture("scales"));
			}
			else
			{
				errors.AddRange(ValidateScale(scale));
				if (!scaleIds.Add(scale.Id))
					errors.Add(OurResources.DataListItemIdDuplicate.FormatCurrentCulture(scale.Id, "scales"));
			}
		}

		if (Layers.Count == 0)
			errors.Add(OurResources.DataListEmpty.FormatCurrentCulture("layers"));

		var layerIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var layer in Layers)
		{
			if (layer is null)
			{
				errors.Add(OurResources.DataListItemEmpty.FormatCurrentCulture("layers"));
			}
			else if (string.IsNullOrWhiteSpace(layer.Id))
			{
				errors.Add(OurResources.DataListItemIdMissing.FormatCurrentCulture("layers"));
			}
			else
			{
				if (!layerIds.Add(layer.Id))
					errors.Add(OurResources.DataListItemIdDuplicate.FormatCurrentCulture(layer.Id, "layers"));
				errors.AddRange(ValidateLayer(layer, colorIds, scaleIds));
			}
		}

		return errors;
	}

	private static IEnumerable<string> ValidateColor(PortraitColor color, IReadOnlySet<string> earlierColorIds)
	{
		var path = $"colors[{color.Id}]";
		foreach (var error in ValidateColorReference($"{path}.color", color.Color, earlierColorIds, isRequired: true))
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
					foreach (var error in ValidateColorReference($"{variantPath}.color", variant.Color, earlierColorIds, isRequired: true))
						yield return error;
				}
			}
		}

		if ((color.Mix is null) != (color.Amount is null))
			yield return OurResources.PortraitColorMixIncomplete.FormatCurrentCulture(path);
		foreach (var error in ValidateColorReference($"{path}.mix", color.Mix, earlierColorIds, isRequired: false))
			yield return error;
		if (color.Amount is < 0 or > 1)
			yield return OurResources.DataFractionInvalid.FormatCurrentCulture($"{path}.amount", color.Amount);
	}

	private static IEnumerable<string> ValidateLayer(PortraitLayer layer, IReadOnlySet<string> colorIds, IReadOnlySet<string> scaleIds)
	{
		var path = $"layers[{layer.Id}]";
		foreach (var error in ValidateScaleReference($"{path}.scale", layer.Scale, scaleIds))
			yield return error;

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
					foreach (var error in ValidateShape($"{path}.shapes[{index}]", shape, colorIds, scaleIds))
						yield return error;
				}
			}
		}
	}

	private static IEnumerable<string> ValidateShape(string path, PortraitShape shape, IReadOnlySet<string> colorIds, IReadOnlySet<string> scaleIds)
	{
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

		foreach (var error in ValidateColorReference($"{path}.fill", shape.Fill, colorIds, isRequired: false))
			yield return error;
		foreach (var error in ValidateColorReference($"{path}.fillTo", shape.FillTo, colorIds, isRequired: false))
			yield return error;
		foreach (var error in ValidateColorReference($"{path}.stroke", shape.Stroke, colorIds, isRequired: false))
			yield return error;

		if (shape.StrokeWidth is <= 0)
			yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.strokeWidth", shape.StrokeWidth);
		if (shape.Opacity is < 0 or > 1)
			yield return OurResources.DataFractionInvalid.FormatCurrentCulture($"{path}.opacity", shape.Opacity);
		foreach (var error in ValidateScaleReference($"{path}.scale", shape.Scale, scaleIds))
			yield return error;
	}

	private static IEnumerable<string> ValidateScale(PortraitScale scale)
	{
		var path = $"scales[{scale.Id}]";
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

	private static IEnumerable<string> ValidateColorReference(string path, string? reference, IReadOnlySet<string> colorIds, bool isRequired)
	{
		if (reference is null)
		{
			if (isRequired)
				yield return OurResources.DataValueMissing.FormatCurrentCulture(path);
		}
		else if (!PortraitColor.TryParseHex(reference, out _) && !colorIds.Contains(reference))
		{
			yield return OurResources.PortraitColorUnknown.FormatCurrentCulture(path, reference);
		}
	}
}
