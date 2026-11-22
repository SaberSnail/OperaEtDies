using System.Text.Json;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// The clothing in the data: the slots and layers it's worn in, how bulky it can be, the colors it can be, the articles of
/// clothing, the outfits people are given, and how well covered they must be.
/// </summary>
public sealed class ClothingData
{
	/// <summary>
	/// No clothing at all.
	/// </summary>
	public static ClothingData None { get; } = new();

	public IReadOnlyList<ClothingSlot> Slots { get; init; } = [];

	/// <summary>
	/// The clothing layers, from the innermost out.
	/// </summary>
	public IReadOnlyList<ClothingLayer> Layers { get; init; } = [];

	public IReadOnlyList<ClothingBulk> Bulks { get; init; } = [];

	public IReadOnlyList<ClothingColor> Colors { get; init; } = [];

	public IReadOnlyList<ClothingItem> Items { get; init; } = [];

	public IReadOnlyList<Outfit> Outfits { get; init; } = [];

	/// <summary>
	/// How well covered people must be. Each person follows the first rule whose condition they meet.
	/// </summary>
	public IReadOnlyList<ModestyRule> ModestyRules { get; init; } = [];

	public ClothingSettings Settings { get; init; } = ClothingSettings.None;

	/// <summary>
	/// Gets the conditions of the outfits, with where each one is.
	/// </summary>
	internal IEnumerable<(string Path, CharacterCondition Condition)> GetConditions() =>
		Outfits.OfType<Outfit>().Where(x => x.When is not null).Select(x => ($"outfits[{x.Id}].when", x.When!))
			.Concat(ModestyRules.OfType<ModestyRule>().Where(x => x.When is not null).Select(x => ($"modestyRules[{x.Id}].when", x.When!)));

	/// <summary>
	/// Checks the clothing on its own. The conditions of its outfits and modesty rules are checked against the rest of the
	/// data separately.
	/// </summary>
	public IEnumerable<string> Validate()
	{
		var errors = new List<string>();
		errors.AddRange(DataValidation.ValidateIdentifiedItems("clothingSlots", Slots, x => x.Id, x => x.Title, allowEmpty: true));
		errors.AddRange(DataValidation.ValidateIdentifiedItems("clothingLayers", Layers, x => x.Id, x => x.Title, allowEmpty: true));
		errors.AddRange(DataValidation.ValidateIdentifiedItems("clothingBulks", Bulks, x => x.Id, x => x.Title, allowEmpty: true));
		foreach (var bulk in Bulks.Where(IsIdentified))
			errors.AddRange(ValidateRange($"clothingBulks[{bulk.Id}].modesty", bulk.Modesty, -ClothingItem.MaxCoverage));
		errors.AddRange(ValidateRange($"{ClothingSettings.SectionName}.hidesClothingAt", (Settings ?? ClothingSettings.None).HidesClothingAt, 0));
		errors.AddRange(DataValidation.ValidateIdentifiedItems("clothingColors", Colors, x => x.Id, x => x.Name, allowEmpty: true));
		errors.AddRange(DataValidation.ValidateIdentifiedItems("clothing", Items, x => x.Id, x => x.Title, allowEmpty: true));
		errors.AddRange(DataValidation.ValidateIdentifiedItems("outfits", Outfits, x => x.Id, null, allowEmpty: true));
		errors.AddRange(DataValidation.ValidateIdentifiedItems("modestyRules", ModestyRules, x => x.Id, null, allowEmpty: true));

		foreach (var color in Colors.Where(IsIdentified))
		{
			if (!PortraitColor.TryParseHex(color.Color ?? "", out _) || color.Color!.Length != 7)
				errors.Add(OurResources.ClothingColorInvalid.FormatCurrentCulture($"clothingColors[{color.Id}].color", color.Color));
			if (string.IsNullOrWhiteSpace(color.Article))
				errors.Add(OurResources.DataValueMissing.FormatCurrentCulture($"clothingColors[{color.Id}].article"));
		}

		// Each piece of clothing with colors gets them from the clothing colors.
		if (Colors.Count == 0 && Items.Any(x => x?.Colors is { Count: > 0 }))
			errors.Add(OurResources.DataListEmpty.FormatCurrentCulture("clothingColors"));

		var slotIds = Slots.Where(IsIdentified).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		var layerIds = Layers.Where(IsIdentified).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		var bulkIds = Bulks.Where(IsIdentified).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		foreach (var item in Items.Where(IsIdentified))
			errors.AddRange(ValidateItem(item, slotIds, layerIds, bulkIds));

		var items = new Dictionary<string, ClothingItem>(StringComparer.Ordinal);
		foreach (var item in Items.Where(IsIdentified))
			items.TryAdd(item.Id, item);
		foreach (var outfit in Outfits.Where(IsIdentified))
			errors.AddRange(ValidateOutfit(outfit, items));

		foreach (var rule in ModestyRules.Where(IsIdentified))
		{
			foreach (var level in Enum.GetValues<ModestyLevel>())
				errors.AddRange(ValidateSlotValues($"modestyRules[{rule.Id}].{JsonNamingPolicy.CamelCase.ConvertName(level.ToString())}", rule.GetMinimums(level) ?? new Dictionary<string, int>(), slotIds, 0));
		}

		return errors;
	}

	private static IEnumerable<string> ValidateItem(ClothingItem item, IReadOnlySet<string> slotIds, IReadOnlySet<string> layerIds, IReadOnlySet<string> bulkIds)
	{
		var path = $"clothing[{item.Id}]";
		foreach (var error in DataValidation.ValidateStrings($"{path}.slots", item.Slots ?? []))
			yield return error;
		foreach (var slot in (item.Slots ?? []).Where(x => !string.IsNullOrWhiteSpace(x) && !slotIds.Contains(x)))
			yield return OurResources.DataIdUnknown.FormatCurrentCulture($"{path}.slots", slot, "clothingSlots");

		if (string.IsNullOrWhiteSpace(item.Layer))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.layer");
		else if (!layerIds.Contains(item.Layer))
			yield return OurResources.DataIdUnknown.FormatCurrentCulture($"{path}.layer", item.Layer, "clothingLayers");

		if (item.Bulk is { } bulk && !bulkIds.Contains(bulk))
			yield return OurResources.DataIdUnknown.FormatCurrentCulture($"{path}.bulk", bulk, "clothingBulks");

		var colors = item.Colors ?? [];
		if (colors.Count != 0)
		{
			foreach (var error in DataValidation.ValidateStrings($"{path}.colors", colors))
				yield return error;
		}

		if (string.IsNullOrWhiteSpace(item.Description))
		{
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.description");
		}
		else if (!TextTemplate.TryParse(item.Description, out var description))
		{
			yield return OurResources.TemplateMalformed.FormatCurrentCulture($"{path}.description");
		}
		else
		{
			foreach (var placeholder in description.Placeholders)
			{
				if (!colors.Contains(placeholder.Id, StringComparer.Ordinal))
					yield return OurResources.ClothingDescriptionColorUnknown.FormatCurrentCulture($"{path}.description", placeholder.Id);
				if (placeholder.Field is { } field && field != ClothingItem.ArticleField)
					yield return OurResources.ClothingDescriptionFieldUnknown.FormatCurrentCulture($"{path}.description", placeholder.Id, field, ClothingItem.ArticleField);
				if (placeholder.Format is { } format && !TextTemplate.IsKnownFormat(format))
					yield return OurResources.TemplateFormatUnknown.FormatCurrentCulture($"{path}.description", format);
			}
		}

		if (string.IsNullOrWhiteSpace(item.Text?.Text))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.text");

		foreach (var error in ValidateSlotValues($"{path}.coverage", item.Coverage ?? new Dictionary<string, int>(), slotIds, 0))
			yield return error;
	}

	private static IEnumerable<string> ValidateOutfit(Outfit outfit, IReadOnlyDictionary<string, ClothingItem> items)
	{
		var path = $"outfits[{outfit.Id}]";
		if (outfit.Complete && !DataRules.IsAllowed(outfit, nameof(Outfit.Complete)))
			yield return OurResources.OutfitCompleteNotWork.FormatCurrentCulture($"{path}.complete");
		// A complete work outfit, or one for wearing nothing, can be nothing at all.
		if (!(outfit.Complete || outfit.Kind == OutfitKind.Nothing) || outfit.Clothing is { Count: > 0 })
		{
			foreach (var error in DataValidation.ValidateStrings($"{path}.clothing", outfit.Clothing ?? []))
				yield return error;
		}

		// Which piece of the outfit takes each slot in each layer, so two pieces in the same place can be reported.
		var slotItems = new Dictionary<(string Slot, string? Layer), string>();
		foreach (var itemId in (outfit.Clothing ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
		{
			if (!items.TryGetValue(itemId, out var item))
			{
				yield return OurResources.DataIdUnknown.FormatCurrentCulture($"{path}.clothing", itemId, "clothing");
			}
			else
			{
				foreach (var slot in (item.Slots ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
				{
					if (slotItems.TryGetValue((slot, item.Layer), out var otherId))
						yield return OurResources.OutfitSlotTaken.FormatCurrentCulture(path, otherId, itemId, slot, item.Layer);
					else
						slotItems.Add((slot, item.Layer), itemId);
				}
			}
		}
	}

	/// <summary>
	/// Checks values keyed by clothing slot id, such as coverage or modesty: each slot must be one of the given ones, and
	/// each value from the given minimum to <see cref="ClothingItem.MaxCoverage"/>.
	/// </summary>
	internal static IEnumerable<string> ValidateSlotValues(string path, IReadOnlyDictionary<string, int> values, IReadOnlySet<string> slotIds, int minimum)
	{
		foreach (var (slot, value) in values)
		{
			if (!slotIds.Contains(slot))
				yield return OurResources.DataIdUnknown.FormatCurrentCulture(path, slot, "clothingSlots");
			if (value < minimum || value > ClothingItem.MaxCoverage)
				yield return OurResources.SlotValueOutOfRange.FormatCurrentCulture(path, slot, value, minimum, ClothingItem.MaxCoverage);
		}
	}

	/// <summary>
	/// Checks that a number is from the given minimum to <see cref="ClothingItem.MaxCoverage"/>.
	/// </summary>
	private static IEnumerable<string> ValidateRange(string path, int value, int minimum)
	{
		if (value < minimum)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture(path, value, minimum);
		else if (value > ClothingItem.MaxCoverage)
			yield return OurResources.DataNumberTooLarge.FormatCurrentCulture(path, value, ClothingItem.MaxCoverage);
	}

	private static bool IsIdentified(IIdentified? item) => item is not null && !string.IsNullOrWhiteSpace(item.Id);
}
