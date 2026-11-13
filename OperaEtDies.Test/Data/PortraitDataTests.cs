using System.IO;
using System.Windows;
using System.Windows.Media;
using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class PortraitDataTests
{
	[SetUp]
	public void SetUp()
	{
		m_directory = Path.Combine(Path.GetTempPath(), $"OperaEtDiesTest_{Guid.NewGuid():N}");
		Directory.CreateDirectory(m_directory);
	}

	[TearDown]
	public void TearDown()
	{
		Directory.Delete(m_directory, recursive: true);
	}

	[Test]
	public void ShippedPortraitsDrawEveryone()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		var renderer = gameData.PortraitRenderer!;

		for (var seed = 0; seed < 20; seed++)
		{
			var player = CharacterRandomizer.CreateRandomPlayer(gameData, new Random(seed));
			Assert.That(renderer.Render(player, PortraitPlace.InWorkplace("profession_baker")).IsFrozen, Is.True);
			Assert.That(renderer.GetLayerIds(player, PortraitPlace.Home), Is.Not.Empty);
		}
	}

	[Test]
	public void WithoutPortraitFileThereAreNoPortraits()
	{
		CopyShippedFiles(CharacterData.FileName, LocationList.FileName, ProfessionList.FileName, FamilySettings.FileName, CalendarSettings.FileName);

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors, Is.Empty);
		Assert.That(result.Value!.PortraitRenderer, Is.Null);
	}

	[Test]
	public void InvalidPortraitsReportErrors()
	{
		CopyShippedFiles(CharacterData.FileName, LocationList.FileName, ProfessionList.FileName, FamilySettings.FileName, CalendarSettings.FileName);
		File.WriteAllText(Path.Combine(m_directory, PortraitData.FileName), """
			{
				"width": 0,
				"height": 100,
				"colors": [
					{ "id": "skin", "color": "later" },
					{ "id": "later", "color": "#FFFFFF", "mix": "#000000" },
					{ "id": "shade", "color": "skin", "mix": "#000000", "amount": 2, "variants": [ { "when": { "values": { "nothing": [ "x" ] } }, "color": "#12345" } ] }
				],
				"scales": [
					{ "id": "wide", "x": 0, "variants": [] },
					{ "id": "wide", "variants": [ { "when": { "values": { "gender": [ "gender_male" ] } }, "y": -1 } ] }
				],
				"offsets": [
					{ "id": "up", "variants": [] },
					{ "id": "up", "y": -10 }
				],
				"layers": [
					{ "id": "a", "scale": "narrow", "offset": "down", "shapes": [ { "path": "M0,0 L10,10 Q" , "fill": "skin", "scale": "tall" } ] },
					{ "id": "a", "when": { "values": { "gender": [ "gender_unknown" ] } }, "shapes": [ { "path": "M0,0 H10", "opacity": 1.5 } ] },
					{ "id": "b", "shapes": [ { "path": "M0,0 H10", "stroke": "skin", "strokeWidth": 0, "gradient": "radial", "fillTo": "#000000" } ] },
					{ "id": "c", "shapes": [ { "path": "M0,0 H10", "fill": "skin", "fillTo": "#000000" } ] },
					{ "id": "d", "at": {}, "shapes": [ { "path": "M0,0 H10", "stroke": "#000000" } ] },
					{ "id": "e", "at": { "quarterKinds": [] }, "shapes": [ { "path": "M0,0 H10", "stroke": "#000000" } ] }
				]
			}
			""");

		var result = GameData.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		var messages = result.Errors.Where(x => Path.GetFileName(x.FilePath) == PortraitData.FileName).Select(x => x.Message).ToList();
		Assert.That(messages, Does.Contain(OurResources.DataNumberNotPositive.FormatCurrentCulture("width", 0)));
		Assert.That(messages, Does.Contain(OurResources.PortraitColorUnknown.FormatCurrentCulture("colors[skin].color", "later")));
		Assert.That(messages, Does.Contain(OurResources.PortraitColorMixIncomplete.FormatCurrentCulture("colors[later]")));
		Assert.That(messages, Does.Contain(OurResources.PortraitColorUnknown.FormatCurrentCulture("colors[shade].variants[0].color", "#12345")));
		Assert.That(messages, Does.Contain(OurResources.DataFractionInvalid.FormatCurrentCulture("colors[shade].amount", 2)));
		Assert.That(messages.Any(x => x.StartsWith(OurResources.PortraitPathInvalid.FormatCurrentCulture("layers[a].shapes[0].path", ""), StringComparison.Ordinal)), Is.True);
		Assert.That(messages, Does.Contain(OurResources.DataListItemIdDuplicate.FormatCurrentCulture("a", "layers")));
		Assert.That(messages, Does.Contain(OurResources.PortraitShapeNotDrawn.FormatCurrentCulture("layers[a].shapes[0]")));
		Assert.That(messages, Does.Contain(OurResources.DataFractionInvalid.FormatCurrentCulture("layers[a].shapes[0].opacity", 1.5)));
		Assert.That(messages, Does.Contain(OurResources.DataNumberNotPositive.FormatCurrentCulture("layers[b].shapes[0].strokeWidth", 0)));
		Assert.That(messages, Does.Contain(OurResources.PortraitGradientWithoutFill.FormatCurrentCulture("layers[b].shapes[0]")));
		Assert.That(messages, Does.Contain(OurResources.PortraitFillToWithoutGradient.FormatCurrentCulture("layers[c].shapes[0]")));
		Assert.That(messages, Does.Contain(OurResources.PortraitPlaceEmpty.FormatCurrentCulture("layers[d].at")));
		Assert.That(messages, Does.Contain(OurResources.DataListEmpty.FormatCurrentCulture("layers[e].at.quarterKinds")));
		Assert.That(messages, Does.Contain(OurResources.DataNumberNotPositive.FormatCurrentCulture("scales[wide].x", 0)));
		Assert.That(messages, Does.Contain(OurResources.DataListEmpty.FormatCurrentCulture("scales[wide].variants")));
		Assert.That(messages, Does.Contain(OurResources.DataNumberNotPositive.FormatCurrentCulture("scales[wide].variants[0].y", -1)));
		Assert.That(messages, Does.Contain(OurResources.DataListItemIdDuplicate.FormatCurrentCulture("wide", "scales")));
		Assert.That(messages, Does.Contain(OurResources.PortraitScaleUnknown.FormatCurrentCulture("layers[a].scale", "narrow")));
		Assert.That(messages, Does.Contain(OurResources.PortraitScaleUnknown.FormatCurrentCulture("layers[a].shapes[0].scale", "tall")));
		Assert.That(messages, Does.Contain(OurResources.DataListEmpty.FormatCurrentCulture("offsets[up].variants")));
		Assert.That(messages, Does.Contain(OurResources.DataListItemIdDuplicate.FormatCurrentCulture("up", "offsets")));
		Assert.That(messages, Does.Contain(OurResources.PortraitOffsetUnknown.FormatCurrentCulture("layers[a].offset", "down")));
	}

	[Test]
	public void ConditionsMustUseCharacterProperties()
	{
		CopyShippedFiles(CharacterData.FileName, LocationList.FileName, ProfessionList.FileName, FamilySettings.FileName, CalendarSettings.FileName);
		File.WriteAllText(Path.Combine(m_directory, PortraitData.FileName), """
			{
				"width": 10,
				"height": 10,
				"colors": [ { "id": "skin", "color": "#FFFFFF", "variants": [ { "when": { "values": { "nothing": [ "x" ] } }, "color": "#000000" } ] } ],
				"scales": [ { "id": "wide", "variants": [ { "when": { "values": { "gender": [ "gender_unknown" ] } }, "x": 2 } ] } ],
				"offsets": [ { "id": "up", "variants": [ { "when": { "values": { "height": [ "height_unknown" ] } }, "y": -10 } ] } ],
				"layers": [
					{ "id": "a", "when": { "values": { "gender": [ "gender_unknown" ] } }, "shapes": [ { "path": "M0,0 H10", "stroke": "skin", "when": { "hidden": [ "gender" ] } } ] }
				]
			}
			""");

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.ConditionPropertyWithoutValues.FormatCurrentCulture("colors[skin].variants[0].when.values", "nothing"),
			OurResources.ConditionValueUnknown.FormatCurrentCulture("scales[wide].variants[0].when.values.gender", "gender_unknown", "gender"),
			OurResources.ConditionValueUnknown.FormatCurrentCulture("offsets[up].variants[0].when.values.height", "height_unknown", "height"),
			OurResources.ConditionValueUnknown.FormatCurrentCulture("layers[a].when.values.gender", "gender_unknown", "gender"),
			OurResources.ConditionHiddenNotAllowed.FormatCurrentCulture("layers[a].shapes[0].when"),
		}));
	}

	[Test]
	public void PlacesMustBeQuarterKindsAndProfessions()
	{
		CopyShippedFiles(CharacterData.FileName, LocationList.FileName, ProfessionList.FileName, FamilySettings.FileName, CalendarSettings.FileName);
		File.WriteAllText(Path.Combine(m_directory, PortraitData.FileName), """
			{
				"width": 10,
				"height": 10,
				"layers": [
					{ "id": "a", "at": { "home": true, "quarterKinds": [ "square", "office" ], "workplaces": [ "profession_baker", "profession_knight" ] }, "shapes": [ { "path": "M0,0 H10", "stroke": "#000000" } ] }
				]
			}
			""");

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.PortraitQuarterKindUnknown.FormatCurrentCulture("layers[a].at.quarterKinds", "office"),
			OurResources.PortraitWorkplaceUnknown.FormatCurrentCulture("layers[a].at.workplaces", "profession_knight", "profession"),
		}));
	}

	[Test]
	public void DrawsTheLayersWhoseConditionsAreMet()
	{
		var renderer = CreateRenderer();
		var gameData = renderer.GameData;

		var man = TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40);
		var woman = TestData.CreateTraits(gameData, "female", "Joan", "Smith", "miller", "short", "slim", 60);

		var atWork = man.With("atWork", gameData.CharacterProperties.Get("atWork").Values!.First(x => x.Id == "atWork"));
		var smithy = PortraitPlace.InWorkplace("smith");

		Assert.That(renderer.Renderer.GetLayerIds(atWork, smithy), Is.EqualTo(new[] { "face", "beard", "forge", "apron" }));
		Assert.That(renderer.Renderer.GetLayerIds(man, smithy), Is.EqualTo(new[] { "face", "beard", "forge" }));
		Assert.That(renderer.Renderer.GetLayerIds(woman, smithy), Is.EqualTo(new[] { "face", "wrinkles", "forge" }));
		Assert.That(renderer.Renderer.GetLayerIds(woman, PortraitPlace.InWorkplace("miller")), Is.EqualTo(new[] { "face", "wrinkles" }));
		Assert.That(renderer.Renderer.GetLayerIds(woman, PortraitPlace.Home), Is.EqualTo(new[] { "face", "wrinkles", "hearth" }));
		Assert.That(renderer.Renderer.GetLayerIds(woman, PortraitPlace.InQuarter("lane")), Is.EqualTo(new[] { "face", "wrinkles", "street" }));
		Assert.That(renderer.Renderer.GetLayerIds(woman, null), Is.EqualTo(new[] { "face", "wrinkles" }));
	}

	[Test]
	public void ColorsUseVariantsAndMixes()
	{
		var renderer = CreateRenderer();
		var gameData = renderer.GameData;
		var man = TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40);
		var woman = TestData.CreateTraits(gameData, "female", "Joan", "Smith", "miller", "short", "slim", 60);

		// The face is the second drawing, after the one that fixes the bounds. The man's skin is the variant, and the
		// woman's is the usual color. Both are blended halfway with black.
		Assert.That(GetFill(renderer.Renderer.Render(man, null), 1), Is.EqualTo(Color.FromRgb(0x40, 0x20, 0x10)));
		Assert.That(GetFill(renderer.Renderer.Render(woman, null), 1), Is.EqualTo(Color.FromRgb(0x80, 0x80, 0x80)));
	}

	[Test]
	public void ScalesStretchShapesWithoutChangingOutlines()
	{
		var renderer = CreateRenderer();
		var gameData = renderer.GameData;
		var man = TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40);
		var woman = TestData.CreateTraits(gameData, "female", "Joan", "Smith", "miller", "short", "slim", 60);

		// The face uses the layer's scale, which doubles the man's width around x = 50 and leaves the woman alone. The
		// wrinkles use their own scale, which halves their height around y = 30 for everyone.
		var manDrawings = ((DrawingGroup) renderer.Renderer.Render(man, null).Drawing).Children;
		Assert.That(((GeometryDrawing) manDrawings[1]).Geometry.Bounds, Is.EqualTo(new Rect(-30, 10, 160, 100)));

		var womanDrawings = ((DrawingGroup) renderer.Renderer.Render(woman, null).Drawing).Children;
		Assert.That(((GeometryDrawing) womanDrawings[1]).Geometry.Bounds, Is.EqualTo(new Rect(10, 10, 80, 100)));
		var wrinkles = (GeometryDrawing) ((DrawingGroup) womanDrawings[2]).Children[0];
		Assert.That(wrinkles.Geometry.Bounds, Is.EqualTo(new Rect(20, 30, 60, 0)));
		Assert.That(wrinkles.Pen.Thickness, Is.EqualTo(2));
	}

	[Test]
	public void OffsetsMoveShapesAfterTheyreStretched()
	{
		var renderer = CreateRenderer();
		var gameData = renderer.GameData;
		var man = TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40);

		// The beard is halved in height around y = 30, from 80-110 to 55-70, then moved up 10 because the man is tall.
		var drawings = ((DrawingGroup) renderer.Renderer.Render(man, null).Drawing).Children;
		Assert.That(((GeometryDrawing) drawings[2]).Geometry.Bounds, Is.EqualTo(new Rect(20, 45, 60, 15)));
	}

	private static Color GetFill(DrawingImage image, int index) =>
		((SolidColorBrush) ((GeometryDrawing) ((DrawingGroup) image.Drawing).Children[index]).Brush).Color;

	private static (GameData GameData, PortraitRenderer Renderer) CreateRenderer()
	{
		var portraits = new PortraitData
		{
			Width = 100,
			Height = 120,
			Colors =
			[
				new PortraitColor { Id = "base", Color = "#FFFFFF", Variants = [new PortraitColorVariant { When = TestData.Condition("gender", "male"), Color = "#804020" }] },
				new PortraitColor { Id = "skin", Color = "base", Mix = "#000000", Amount = 0.5 },
			],
			Scales =
			[
				new PortraitScale { Id = "wide", OriginX = 50, Variants = [new PortraitScaleVariant { When = TestData.Condition("gender", "male"), X = 2 }] },
				new PortraitScale { Id = "short", OriginY = 30, Y = 0.5 },
			],
			Offsets =
			[
				new PortraitOffset { Id = "height", Variants = [new PortraitOffsetVariant { When = TestData.Condition("height", "tall"), Y = -10 }] },
			],
			Layers =
			[
				new PortraitLayer { Id = "face", Scale = "wide", Shapes = [new PortraitShape { Path = "M10,10 H90 V110 H10 Z", Fill = "skin" }] },
				new PortraitLayer { Id = "beard", When = TestData.Condition("gender", "male"), Scale = "short", Offset = "height", Shapes = [new PortraitShape { Path = "M20,80 H80 V110 H20 Z", Fill = "#202020" }] },
				new PortraitLayer { Id = "wrinkles", When = TestData.Condition("age", "old"), Scale = "wide", Shapes = [new PortraitShape { Path = "M20,30 H80", Stroke = "#000000", StrokeWidth = 2, Opacity = 0.5, Scale = "short" }] },
				new PortraitLayer { Id = "forge", At = new PortraitPlaceCondition { Workplaces = ["smith"] }, Shapes = [new PortraitShape { Path = "M0,0 H10", Stroke = "#FF8000" }] },
				new PortraitLayer { Id = "apron", When = TestData.Condition("atWork", "atWork"), Shapes = [new PortraitShape { Path = "M30,90 H70", Stroke = "#FFFFFF" }] },
				new PortraitLayer { Id = "hearth", At = new PortraitPlaceCondition { Home = true }, Shapes = [new PortraitShape { Path = "M0,0 H10", Stroke = "#FF0000" }] },
				new PortraitLayer { Id = "street", At = new PortraitPlaceCondition { QuarterKinds = ["lane", "square"] }, Shapes = [new PortraitShape { Path = "M0,0 H10", Stroke = "#808080" }] },
			],
		};
		var gameData = new GameData(TestData.CreateCharacterData(), TestData.CreateLocationList(), TestData.CreateProfessionList(), TestData.CreateFamilySettings(), TestData.CreateCalendarSettings(), portraits: portraits);
		return (gameData, gameData.PortraitRenderer!);
	}

	private void CopyShippedFiles(params string[] fileNames)
	{
		foreach (var fileName in fileNames)
			File.Copy(Path.Combine(JsonDataLoader.DefaultDataDirectory, fileName), Path.Combine(m_directory, fileName));
	}

	private string m_directory = "";
}
