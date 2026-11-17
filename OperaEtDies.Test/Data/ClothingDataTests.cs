using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class ClothingDataTests
{
	[Test]
	public void LoadsShippedClothing()
	{
		var result = GameData.Load(JsonDataLoader.DefaultDataDirectory);

		Assert.That(result.Errors, Is.Empty);
		Assert.That(result.Value!.Clothing, Is.Not.Empty);
		Assert.That(result.Value.Outfits.Select(x => x.Kind).Distinct(), Is.EquivalentTo(Enum.GetValues<OutfitKind>()));
	}

	[Test]
	public void ValidClothingHasNoErrors()
	{
		Assert.That(CreateClothing().Validate(), Is.Empty);
	}

	[Test]
	public void DescriptionsUseColorsAndArticles()
	{
		var clothing = CreateClothing();
		var colors = clothing.Colors.ToDictionary(x => x.Id);

		Assert.That(clothing.Items[0].FormatDescription(x => x == "main" ? colors["ochre"] : colors["red"]), Is.EqualTo("an ochre tunic with red trim"));
	}

	[Test]
	public void ItemsMustBeValid()
	{
		var item = new ClothingItem
		{
			Id = "odd",
			Title = "Odd",
			Slots = ["head", "wings", "head"],
			Layer = "cloud",
			Colors = ["main", "main"],
			Description = "{main.the} {trim:shout}",
			Text = new LongText { Text = " ", Ai = true },
			Bulk = "heavy",
			Coverage = new Dictionary<string, int> { ["torso"] = 11, ["wings"] = 2 },
		};
		var malformed = new ClothingItem { Id = "malformed", Title = "Malformed", Slots = ["head"], Layer = "clothes", Description = "a {hat", Text = Text() };

		Assert.That(With(item, malformed).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.DataListValueDuplicate.FormatCurrentCulture("head", "clothing[odd].slots"),
			OurResources.DataIdUnknown.FormatCurrentCulture("clothing[odd].slots", "wings", "clothingSlots"),
			OurResources.DataIdUnknown.FormatCurrentCulture("clothing[odd].layer", "cloud", "clothingLayers"),
			OurResources.DataListValueDuplicate.FormatCurrentCulture("main", "clothing[odd].colors"),
			OurResources.ClothingDescriptionFieldUnknown.FormatCurrentCulture("clothing[odd].description", "main", "the", "a"),
			OurResources.ClothingDescriptionColorUnknown.FormatCurrentCulture("clothing[odd].description", "trim"),
			OurResources.TemplateFormatUnknown.FormatCurrentCulture("clothing[odd].description", "shout"),
			OurResources.DataValueMissing.FormatCurrentCulture("clothing[odd].text"),
			OurResources.DataIdUnknown.FormatCurrentCulture("clothing[odd].bulk", "heavy", "clothingBulks"),
			OurResources.SlotValueOutOfRange.FormatCurrentCulture("clothing[odd].coverage", "torso", 11, 0, 10),
			OurResources.DataIdUnknown.FormatCurrentCulture("clothing[odd].coverage", "wings", "clothingSlots"),
			OurResources.TemplateMalformed.FormatCurrentCulture("clothing[malformed].description"),
		}));
	}

	[Test]
	public void ColorsMustBeValid()
	{
		var clothing = new ClothingData
		{
			Slots = CreateClothing().Slots,
			Layers = CreateClothing().Layers,
			Colors =
			[
				new ClothingColor { Id = "pale", Name = "pale", Color = "#80FFFFFF" },
				new ClothingColor { Id = "odd", Name = "odd", Color = "blue", Article = " " },
			],
		};

		Assert.That(clothing.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.ClothingColorInvalid.FormatCurrentCulture("clothingColors[pale].color", "#80FFFFFF"),
			OurResources.ClothingColorInvalid.FormatCurrentCulture("clothingColors[odd].color", "blue"),
			OurResources.DataValueMissing.FormatCurrentCulture("clothingColors[odd].article"),
		}));
	}

	[Test]
	public void OutfitsMustNotShareSlots()
	{
		var clothing = CreateClothing();
		var outfits = new[]
		{
			new Outfit { Id = "doubled", Kind = OutfitKind.Everyday, Clothing = ["tunic", "gown", "hat"] },
			new Outfit { Id = "missing", Kind = OutfitKind.Work, Clothing = ["cape"] },
			new Outfit { Id = "layered", Kind = OutfitKind.Everyday, Clothing = ["tunic", "cloak", "hat"] },
			new Outfit { Id = "allDay", Kind = OutfitKind.Everyday, Complete = true, Clothing = ["hat"] },
			new Outfit { Id = "empty", Kind = OutfitKind.Work, Clothing = [] },
			new Outfit { Id = "unclothed", Kind = OutfitKind.Work, Complete = true, Clothing = [] },
		};

		Assert.That(new ClothingData { Slots = clothing.Slots, Layers = clothing.Layers, Colors = clothing.Colors, Items = clothing.Items, Outfits = outfits }.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.OutfitSlotTaken.FormatCurrentCulture("outfits[doubled]", "tunic", "gown", "torso", "clothes"),
			OurResources.DataIdUnknown.FormatCurrentCulture("outfits[missing].clothing", "cape", "clothing"),
			OurResources.OutfitCompleteNotWork.FormatCurrentCulture("outfits[allDay].complete"),
			OurResources.DataListEmpty.FormatCurrentCulture("outfits[empty].clothing"),
		}));
	}

	[Test]
	public void RegionsAndConditionsMustExist()
	{
		var clothing = CreateClothing();
		var item = new ClothingItem { Id = "mask", Title = "Mask", Slots = ["head"], Layer = "clothes", Description = "a mask", Text = Text() };
		var outfit = new Outfit { Id = "odd", Kind = OutfitKind.Night, When = new CharacterCondition { Values = new Dictionary<string, IReadOnlyList<string>> { ["mood"] = ["happy"] } }, Clothing = ["mask"] };
		var data = new ClothingData { Slots = clothing.Slots, Layers = clothing.Layers, Colors = clothing.Colors, Items = [item], Outfits = [outfit] };

		var characters = TestData.CreateCharacterData();
		characters = new CharacterData { Templates = characters.Templates, Properties = [.. characters.Properties.Select(x => x.Id == "height" ? new CharacterProperty { Id = x.Id, Title = x.Title, Type = x.Type, Values = x.Values, Regions = ["torso", "tail"] } : x)] };
		var errors = GameDataValidation.Validate(characters, TestData.CreateLocationList(), TestData.CreateProfessionList(), TestData.CreateFamilySettings(),
			TestData.CreateRelationshipSettings(), TestData.CreateCalendarSettings(), ScheduleSettings.None, clothing: data);

		Assert.That(errors, Does.Contain(OurResources.DataIdUnknown.FormatCurrentCulture("properties[height].regions", "tail", "clothingSlots")));
		Assert.That(errors.Where(x => x.Contains("outfits[odd].when", StringComparison.Ordinal)), Is.Not.Empty);
	}

	[Test]
	public void ModestyAndBulkMustBeValid()
	{
		var clothing = CreateClothing();
		var bulk = new ClothingBulk { Id = "sheer", Title = "Sheer", Modesty = -11 };
		var rule = new ModestyRule { Id = "rule", Outside = new Dictionary<string, int> { ["torso"] = -1, ["wings"] = 3 } };

		Assert.That(new ClothingData { Slots = clothing.Slots, Layers = clothing.Layers, Bulks = [bulk], ModestyRules = [rule], Settings = new ClothingSettings { HidesClothingAt = 12 } }.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.DataNumberTooSmall.FormatCurrentCulture("clothingBulks[sheer].modesty", -11, -10),
			OurResources.DataNumberTooLarge.FormatCurrentCulture("clothingSettings.hidesClothingAt", 12, 10),
			OurResources.SlotValueOutOfRange.FormatCurrentCulture("modestyRules[rule].outside", "torso", -1, 0, 10),
			OurResources.DataIdUnknown.FormatCurrentCulture("modestyRules[rule].outside", "wings", "clothingSlots"),
		}));
	}

	[Test]
	public void ProfessionModestyMustNameRulesAndSlots()
	{
		var clothing = CreateClothing();
		var rule = new ModestyRule { Id = "rule", Outside = new Dictionary<string, int> { ["torso"] = 5 } };
		var data = new ClothingData { Slots = clothing.Slots, Layers = clothing.Layers, ModestyRules = [rule] };
		var professions = TestData.CreateProfessionList();
		var modesty = new Dictionary<string, IReadOnlyDictionary<string, int>>
		{
			["rule"] = new Dictionary<string, int> { ["torso"] = 2, ["legs"] = -11, ["wings"] = 1 },
			["other"] = new Dictionary<string, int> { ["torso"] = 1 },
		};
		professions = new ProfessionList { Professions = [.. professions.Professions.Select(x => x.Id == "smith" ? new Profession { Id = x.Id, Title = x.Title, Business = x.Business, Modesty = modesty } : x)] };

		var errors = GameDataValidation.Validate(TestData.CreateCharacterData(), TestData.CreateLocationList(), professions, TestData.CreateFamilySettings(),
			TestData.CreateRelationshipSettings(), TestData.CreateCalendarSettings(), ScheduleSettings.None, clothing: data);

		Assert.That(errors.Where(x => x.Contains("professions[smith].modesty", StringComparison.Ordinal)), Is.EquivalentTo(new[]
		{
			OurResources.SlotValueOutOfRange.FormatCurrentCulture("professions[smith].modesty.rule", "legs", -11, -10, 10),
			OurResources.DataIdUnknown.FormatCurrentCulture("professions[smith].modesty.rule", "wings", "clothingSlots"),
			OurResources.DataIdUnknown.FormatCurrentCulture("professions[smith].modesty", "other", "modestyRules"),
		}));
	}

	private static ClothingData CreateClothing() => new()
	{
		Slots = [new ClothingSlot { Id = "head", Title = "Head" }, new ClothingSlot { Id = "torso", Title = "Torso" }, new ClothingSlot { Id = "legs", Title = "Legs" }],
		Layers = [new ClothingLayer { Id = "clothes", Title = "Clothes" }, new ClothingLayer { Id = "outer", Title = "Outerwear" }],
		Colors =
		[
			new ClothingColor { Id = "red", Name = "red", Color = "#AA2020" },
			new ClothingColor { Id = "ochre", Name = "ochre", Article = "an", Color = "#C08A2E" },
		],
		Items =
		[
			new ClothingItem { Id = "tunic", Title = "Tunic", Slots = ["torso"], Layer = "clothes", Colors = ["main", "trim"], Description = "{main.a} tunic with {trim} trim", Text = Text() },
			new ClothingItem { Id = "gown", Title = "Gown", Slots = ["torso", "legs"], Layer = "clothes", Colors = ["main"], Description = "{main.a} gown", Text = Text() },
			new ClothingItem { Id = "hat", Title = "Hat", Slots = ["head"], Layer = "clothes", Description = "a straw hat", Text = Text() },
			new ClothingItem { Id = "cloak", Title = "Cloak", Slots = ["torso"], Layer = "outer", Description = "a cloak", Text = Text() },
		],
	};

	private static ClothingData With(params ClothingItem[] items)
	{
		var clothing = CreateClothing();
		return new ClothingData { Slots = clothing.Slots, Layers = clothing.Layers, Colors = clothing.Colors, Items = items };
	}

	private static LongText Text() => new() { Text = "Some clothing.", Ai = true };
}
