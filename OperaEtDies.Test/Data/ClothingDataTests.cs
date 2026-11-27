using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class ClothingDataTests
{
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
			new Outfit { Id = "bareDay", Kind = OutfitKind.Everyday, Clothing = [] },
			new Outfit { Id = "bareNight", Kind = OutfitKind.Night, Clothing = [] },
		};

		Assert.That(new ClothingData { Slots = clothing.Slots, Layers = clothing.Layers, Colors = clothing.Colors, Items = clothing.Items, Outfits = outfits }.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.OutfitSlotTaken.FormatCurrentCulture("outfits[doubled]", "tunic", "gown", "torso", "clothes"),
			OurResources.DataIdUnknown.FormatCurrentCulture("outfits[missing].clothing", "cape", "clothing"),
			OurResources.OutfitCompleteNotWork.FormatCurrentCulture("outfits[allDay].complete"),
			OurResources.DataListEmpty.FormatCurrentCulture("outfits[empty].clothing"),
			OurResources.DataListEmpty.FormatCurrentCulture("outfits[bareDay].clothing"),
		}));
	}

	[Test]
	public void OutfitOptionsMustBeValid()
	{
		var clothing = CreateClothing();
		static OutfitClothing Options(params string?[] ids) => new() { Options = ids.Select(x => new OutfitClothingOption { Clothing = x }).ToList() };
		var outfits = new[]
		{
			// Options in one entry are alternatives, so they can share a slot, but not with another entry's options.
			new Outfit { Id = "choice", Kind = OutfitKind.Everyday, Clothing = [Options("tunic", "gown", null), "hat"] },
			new Outfit { Id = "clash", Kind = OutfitKind.Everyday, Clothing = ["tunic", Options("hat", "gown")] },
			new Outfit { Id = "twice", Kind = OutfitKind.Everyday, Clothing = ["hat", Options("hat", null)] },
			new Outfit { Id = "unknown", Kind = OutfitKind.Everyday, Clothing = [Options("cape")] },
			new Outfit { Id = "none", Kind = OutfitKind.Everyday, Clothing = [Options()] },
		};

		Assert.That(new ClothingData { Slots = clothing.Slots, Layers = clothing.Layers, Colors = clothing.Colors, Items = clothing.Items, Outfits = outfits }.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.OutfitSlotTaken.FormatCurrentCulture("outfits[clash]", "tunic", "gown", "torso", "clothes"),
			OurResources.DataListValueDuplicate.FormatCurrentCulture("hat", "outfits[twice].clothing"),
			OurResources.DataIdUnknown.FormatCurrentCulture("outfits[unknown].clothing[0].options[0].clothing", "cape", "clothing"),
			OurResources.DataListEmpty.FormatCurrentCulture("outfits[none].clothing[0].options"),
		}));
	}

	[Test]
	public void OutfitClothingIsAnIdOrOptions()
	{
		var outfit = System.Text.Json.JsonSerializer.Deserialize<Outfit>(
			"""{ "id": "o", "kind": "everyday", "clothing": [ "tunic", { "comments": "A hat or not.", "options": [ { }, { "clothing": "hat", "frequency": "rare", "when": { "values": { "gender": "gender_male" } } } ] } ] }""",
			JsonDataLoader.Options)!;
		Assert.That(outfit.Clothing[0].Clothing, Is.EqualTo("tunic"));
		Assert.That(outfit.Clothing[1].Options!.Select(x => x.Clothing), Is.EqualTo(new[] { null, "hat" }));
		Assert.That(outfit.Clothing[1].Options![1].Frequency.Tier, Is.EqualTo(global::OperaEtDies.Utility.Random.Frequency.Rare));

		// A single entry needn't be in a list.
		var single = System.Text.Json.JsonSerializer.Deserialize<Outfit>("""{ "id": "o", "kind": "work", "clothing": "apron" }""", JsonDataLoader.Options)!;
		Assert.That(single.Clothing.Select(x => x.Clothing), Is.EqualTo(new[] { "apron" }));
		Assert.That(() => System.Text.Json.JsonSerializer.Deserialize<Outfit>("""{ "id": "o", "kind": "work", "clothing": [ 3 ] }""", JsonDataLoader.Options), Throws.InstanceOf<System.Text.Json.JsonException>());

		// Options are chosen only for those who meet their conditions, and an entry with none they meet holds nothing.
		var options = outfit.GetOptions(_ => "gender_female");
		Assert.That(options[1].Select(x => x.Clothing), Is.EqualTo(new string?[] { null }));
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
		var rule = new ModestyRule
		{
			Id = "rule",
			Slots = new Dictionary<string, IReadOnlyList<ModestyMinimum>>
			{
				["torso"] = [new ModestyMinimum { Comfort = 10, Minimum = 5 }, new ModestyMinimum { Comfort = 10, Minimum = 11 }],
				["wings"] = [new ModestyMinimum { Comfort = 0, Minimum = 3 }],
				["legs"] = [],
			},
		};
		var modesty = new ModestySettings { Company = new ModestyCompany { Spouse = 101 }, Asleep = -1 };

		Assert.That(new ClothingData { Slots = clothing.Slots, Layers = clothing.Layers, Bulks = [bulk], ModestyRules = [rule], Modesty = modesty, Settings = new ClothingSettings { HidesClothingAt = 12 } }.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.DataNumberTooSmall.FormatCurrentCulture("clothingBulks[sheer].modesty", -11, -10),
			OurResources.DataNumberTooLarge.FormatCurrentCulture("clothingSettings.hidesClothingAt", 12, 10),
			OurResources.DataNumberTooLarge.FormatCurrentCulture("modestySettings.company.spouse", 101, 100),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("modestySettings.asleep", -1, 0),
			OurResources.DataNumberTooLarge.FormatCurrentCulture("modestyRules[rule].slots.torso[1].minimum", 11, 10),
			OurResources.ModestyComfortOutOfOrder.FormatCurrentCulture("modestyRules[rule].slots.torso", 10, 10),
			OurResources.DataIdUnknown.FormatCurrentCulture("modestyRules[rule].slots", "wings", "clothingSlots"),
			OurResources.DataListEmpty.FormatCurrentCulture("modestyRules[rule].slots.legs"),
		}));
	}

	[Test]
	public void BoldnessMustBeAnIntegerProperty()
	{
		var clothing = CreateClothing();
		IReadOnlyList<string> Validate(string property) =>
			GameDataValidation.Validate(TestData.CreateCharacterData(), TestData.CreateLocationList(), TestData.CreateProfessionList(), TestData.CreateFamilySettings(),
				TestData.CreateRelationshipSettings(), TestData.CreateCalendarSettings(), ScheduleSettings.None,
				clothing: new ClothingData { Slots = clothing.Slots, Layers = clothing.Layers, Modesty = new ModestySettings { Boldness = new ModestyBoldness { Property = property } } }).ToList();

		Assert.That(Validate("height"), Does.Contain(OurResources.PropertyNotInteger.FormatCurrentCulture("modestySettings.boldness.property", "height")));
		Assert.That(Validate("nerve"), Does.Contain(OurResources.DataIdUnknown.FormatCurrentCulture("modestySettings.boldness.property", "nerve", "properties")));
		Assert.That(Validate("age").Where(x => x.Contains("boldness", StringComparison.Ordinal)), Is.Empty);
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
