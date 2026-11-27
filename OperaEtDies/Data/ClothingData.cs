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

	/// <summary>
	/// How comfortable people are with how covered they are, which decides what their modesty rule calls for.
	/// </summary>
	public ModestySettings Modesty { get; init; } = ModestySettings.None;

	public ClothingSettings Settings { get; init; } = ClothingSettings.None;

	/// <summary>
	/// Gets the conditions of the outfits, with where each one is.
	/// </summary>
	internal IEnumerable<(string Path, CharacterCondition Condition)> GetConditions() =>
		Outfits.OfType<Outfit>().Where(x => x.When is not null).Select(x => ($"outfits[{x.Id}].when", x.When!))
			.Concat(Outfits.OfType<Outfit>().SelectMany(outfit => (outfit.Clothing ?? []).SelectMany((entry, index) => (entry?.Options ?? []).Select((option, optionIndex) => (option, Path: $"outfits[{outfit.Id}].clothing[{index}].options[{optionIndex}].when")))
				.Where(x => x.option?.When is not null).Select(x => (x.Path, x.option.When!))))
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

		errors.AddRange((Modesty ?? ModestySettings.None).Validate());
		foreach (var rule in ModestyRules.Where(IsIdentified))
			errors.AddRange(ValidateRule(rule, slotIds));

		return errors;
	}

	/// <summary>
	/// Checks a modesty rule: each slot it lists must be a clothing slot, with at least one minimum, in order of comfort,
	/// each comfort from 0 to <see cref="ModestySettings.MaxComfort"/> and more than the one before it, and each minimum from
	/// 0 to <see cref="ClothingItem.MaxCoverage"/>.
	/// </summary>
	private static IEnumerable<string> ValidateRule(ModestyRule rule, IReadOnlySet<string> slotIds)
	{
		var path = $"modestyRules[{rule.Id}].slots";
		foreach (var (slot, minimums) in rule.Slots ?? new Dictionary<string, IReadOnlyList<ModestyMinimum>>())
		{
			if (!slotIds.Contains(slot))
				yield return OurResources.DataIdUnknown.FormatCurrentCulture(path, slot, "clothingSlots");
			if (minimums is not { Count: > 0 })
			{
				yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.{slot}");
			}
			else
			{
				for (var index = 0; index < minimums.Count; index++)
				{
					var itemPath = $"{path}.{slot}[{index}]";
					if (minimums[index] is not { } minimum)
					{
						yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.{slot}");
					}
					else
					{
						foreach (var error in ModestySettings.ValidateRange($"{itemPath}.comfort", minimum.Comfort, 0).Concat(ValidateRange($"{itemPath}.minimum", minimum.Minimum, 0)))
							yield return error;
						if (index > 0 && minimums[index - 1] is { } previous && minimum.Comfort <= previous.Comfort)
							yield return OurResources.ModestyComfortOutOfOrder.FormatCurrentCulture($"{path}.{slot}", minimum.Comfort, previous.Comfort);
					}
				}
			}
		}
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
		// A complete work outfit, night clothes, or an outfit for wearing nothing can be nothing at all.
		var entries = outfit.Clothing ?? [];
		if (entries.Count == 0 && !(outfit.Complete || outfit.Kind is OutfitKind.Night or OutfitKind.Nothing))
			yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.clothing");

		// The clothing each entry can hold, which can't take the same slot in the same layer as any other entry's.
		var entryItems = new List<List<ClothingItem>>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		for (var index = 0; index < entries.Count; index++)
		{
			var entryItemList = new List<ClothingItem>();
			entryItems.Add(entryItemList);
			if (entries[index] is not { } entry)
			{
				yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.clothing");
			}
			else
			{
				var isChoice = entry.Options is not null;
				var options = entry.GetOptions();
				var optionsPath = $"{path}.clothing[{index}].options";
				if (options.Count == 0)
					yield return OurResources.DataListEmpty.FormatCurrentCulture(optionsPath);
				for (var optionIndex = 0; optionIndex < options.Count; optionIndex++)
				{
					var clothingPath = isChoice ? $"{optionsPath}[{optionIndex}].clothing" : $"{path}.clothing";
					if (options[optionIndex] is not { } option)
					{
						yield return OurResources.DataListItemEmpty.FormatCurrentCulture(optionsPath);
					}
					else if (option.Clothing is { } itemId)
					{
						if (string.IsNullOrWhiteSpace(itemId))
							yield return isChoice ? OurResources.DataValueMissing.FormatCurrentCulture(clothingPath) : OurResources.DataListItemBlank.FormatCurrentCulture(clothingPath);
						else if (!seen.Add(itemId))
							yield return OurResources.DataListValueDuplicate.FormatCurrentCulture(itemId, $"{path}.clothing");
						else if (!items.TryGetValue(itemId, out var item))
							yield return OurResources.DataIdUnknown.FormatCurrentCulture(clothingPath, itemId, "clothing");
						else
							entryItemList.Add(item);
					}
					else if (!isChoice)
					{
						yield return OurResources.DataListItemEmpty.FormatCurrentCulture(clothingPath);
					}
				}
			}
		}

		for (var first = 0; first < entryItems.Count; first++)
		{
			for (var second = first + 1; second < entryItems.Count; second++)
			{
				foreach (var item in entryItems[first])
				{
					foreach (var other in entryItems[second].Where(item.Overlaps))
					{
						var slot = (item.Slots ?? []).First(x => (other.Slots ?? []).Contains(x, StringComparer.Ordinal));
						yield return OurResources.OutfitSlotTaken.FormatCurrentCulture(path, item.Id, other.Id, slot, item.Layer);
					}
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
