using System.Windows;
using System.Windows.Media;
using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class PortraitClothingTests
{
	[Test]
	public void ClothingLayersAreOnlyDrawnWhileTheirClothingIsWorn()
	{
		var (renderer, traits) = CreateRenderer();

		Assert.That(renderer.GetLayerIds(traits, null, PortraitClothing.None), Is.EqualTo(new[] { "body", "hair" }));
		Assert.That(renderer.GetLayerIds(traits, null, Wearing(s_tunic, "#FF0000")), Is.EqualTo(new[] { "body", "hair", "tunic" }));
		Assert.That(renderer.GetLayerIds(traits, null, Wearing(s_hat)), Is.EqualTo(new[] { "body", "hat" }));

		// A band doesn't cover the scalp enough to hide the hair.
		Assert.That(renderer.GetLayerIds(traits, null, Wearing(s_band)), Is.EqualTo(new[] { "body", "hair" }));
	}

	[Test]
	public void ClothingLayersUseTheColorsOfTheFirstOfTheirClothingWorn()
	{
		var (renderer, traits) = CreateRenderer();

		// The tunic layer's fill is the piece's main color, and its outline is its own color, worked out from it.
		var tunic = GetDrawings(renderer.Render(traits, null, Wearing(s_tunic, "#FF0000")));
		Assert.That(GetFill(tunic[3]), Is.EqualTo(Color.FromRgb(0xFF, 0, 0)));
		Assert.That(((SolidColorBrush) ((GeometryDrawing) tunic[4]).Pen.Brush).Color, Is.EqualTo(Color.FromRgb(0x80, 0, 0)));

		// Without the tunic, the layer uses the gown's colors.
		var gown = GetDrawings(renderer.Render(traits, null, Wearing(s_gown, "#0000FF")));
		Assert.That(GetFill(gown[3]), Is.EqualTo(Color.FromRgb(0, 0, 0xFF)));
	}

	[Test]
	public void IncludedShapeSetsAreClippedToTheLayersFilledShapes()
	{
		var (renderer, traits) = CreateRenderer();

		var drawings = GetDrawings(renderer.Render(traits, null, Wearing(s_tunic, "#FF0000")));
		var included = (DrawingGroup) drawings[5];
		Assert.That(included.ClipGeometry.Bounds, Is.EqualTo(new Rect(10, 10, 80, 80)));
		Assert.That(((GeometryDrawing) included.Children[0]).Geometry.Bounds, Is.EqualTo(new Rect(0, 50, 100, 10)));
		Assert.That(GetFill(included.Children[0]), Is.EqualTo(Color.FromRgb(0x80, 0, 0)));
	}

	[Test]
	public void LayersCutAwayTheLayersUnderThemThatClothingOverTheirRegionsCanHide()
	{
		// A band that covers too little of the scalp to hide the hair cuts the hair away above its line instead, but not the
		// body, which clothing can't hide.
		var (renderer, traits) = CreateRenderer([new PortraitLayer { Ai = true, Id = "bandLayer", Clothing = ["band"], Cut = new PortraitCut { Path = "M0,0 H100 V5 H0 Z", Regions = ["scalp"] }, Shapes = [new PortraitShape { Path = "M0,4 H100 V6 H0 Z", Fill = "#806040" }] }]);

		var bare = GetDrawings(renderer.Render(traits, null, new PortraitClothing([])));
		Assert.That(bare.OfType<DrawingGroup>(), Is.Empty);

		var drawings = GetDrawings(renderer.Render(traits, null, Wearing(s_band)));
		var hair = drawings.OfType<DrawingGroup>().Single();
		Assert.That(GetFill(hair.Children[0]), Is.EqualTo(Color.FromRgb(0x11, 0x11, 0x11)));
		Assert.That((hair.ClipGeometry.FillContains(new Point(50, 2)), hair.ClipGeometry.FillContains(new Point(50, 8))), Is.EqualTo((false, true)));
		Assert.That(drawings.OfType<GeometryDrawing>().Any(x => x.Brush is SolidColorBrush { Color: { R: 0, G: 0, B: 0 } }), Is.True);
	}

	[Test]
	public void LayersOnTheCharacterAreOnlyDrawnOverTheLayersUnderThemDrawnAnywhere()
	{
		// Dust is only drawn over the body and hair, not the background, which is only drawn in some places.
		var (renderer, traits) = CreateRenderer(
		[
			new PortraitLayer { Ai = true, Id = "background", At = new PortraitPlaceCondition { Home = true }, Shapes = [new PortraitShape { Path = "M0,0 H100 V100 H0 Z", Fill = "#336699" }] },
			new PortraitLayer { Ai = true, Id = "dust", OnCharacter = true, Shapes = [new PortraitShape { Path = "M0,0 H100 V100 H0 Z", Fill = "#FFFFFF" }] },
		],
		body: "M0,0 H50 V100 H0 Z");

		var dust = GetDrawings(renderer.Render(traits, PortraitPlace.Home, new PortraitClothing([]))).OfType<DrawingGroup>().Single();
		Assert.That((dust.ClipGeometry.FillContains(new Point(25, 50)), dust.ClipGeometry.FillContains(new Point(75, 50))), Is.EqualTo((true, false)));
	}

	[Test]
	public void LayersOnTheCharacterAreNeverDrawnOverTheBackdrop()
	{
		// The sky is drawn everywhere, but it's behind the character, so dust isn't drawn over it.
		var (renderer, traits) = CreateRenderer(
		[
			new PortraitLayer { Ai = true, Id = "sky", Backdrop = true, When = new PortraitCondition { Skies = ["night"] }, Shapes = [new PortraitShape { Path = "M0,0 H100 V100 H0 Z", Fill = "#223355" }] },
			new PortraitLayer { Ai = true, Id = "dust", OnCharacter = true, Shapes = [new PortraitShape { Path = "M0,0 H100 V100 H0 Z", Fill = "#FFFFFF" }] },
		],
		body: "M0,0 H50 V100 H0 Z");

		var dust = GetDrawings(renderer.Render(traits, PortraitPlace.Home.WithSky("night"), new PortraitClothing([]))).OfType<DrawingGroup>().Single();
		Assert.That((dust.ClipGeometry.FillContains(new Point(25, 50)), dust.ClipGeometry.FillContains(new Point(75, 50))), Is.EqualTo((true, false)));
	}

	[Test]
	public void LayersWithSkyConditionsAreOnlyDrawnUnderThem()
	{
		var (renderer, traits) = CreateRenderer([new PortraitLayer { Ai = true, Id = "stars", When = new PortraitCondition { Skies = ["night"] }, Shapes = [new PortraitShape { Path = "M0,0 H1 V1 Z", Fill = "#FFFFFF" }] }]);
		var clothing = new PortraitClothing([]);

		Assert.Multiple(() =>
		{
			Assert.That(renderer.GetLayerIds(traits, PortraitPlace.Home.WithSky("night"), clothing), Does.Contain("stars"));
			Assert.That(renderer.GetLayerIds(traits, PortraitPlace.Home.WithSky("day"), clothing), Does.Not.Contain("stars"));
			Assert.That(renderer.GetLayerIds(traits, PortraitPlace.Home, clothing), Does.Not.Contain("stars"));
			Assert.That(renderer.GetLayerIds(traits, null, clothing), Does.Not.Contain("stars"));
		});
	}

	[Test]
	public void LayerConditionSkiesMustBeCalendarSkies()
	{
		var portraits = new PortraitData
		{
			Width = 100,
			Height = 100,
			Layers = [new PortraitLayer { Ai = true, Id = "sky", When = new PortraitCondition { Skies = ["night", "noon"] }, Shapes = [new PortraitShape { Path = "M0,0 H1 V1 Z", Fill = "#000000" }] }],
		};

		var errors = GameDataValidation.ValidatePortraits(portraits, new Dictionary<string, CharacterProperty>(), new HashSet<string>(), new HashSet<string> { "night" }, null, ClothingData.None);
		Assert.That(errors, Is.EqualTo(new[] { OurResources.PortraitSkyUnknown.FormatCurrentCulture("portraitLayers[sky].when.skies", "noon") }));
	}

	[Test]
	public void CutsMustBeValid()
	{
		var portraits = new PortraitData
		{
			Width = 100,
			Height = 100,
			Layers =
			[
				new PortraitLayer { Ai = true, Id = "badPath", Cut = new PortraitCut { Path = "Q", Regions = ["scalp"] }, Shapes = [new PortraitShape { Path = "M0,0 H1 V1 Z", Fill = "#000000" }] },
				new PortraitLayer { Ai = true, Id = "noRegions", Cut = new PortraitCut { Path = "M0,0 H1 V1 Z", Regions = [] }, Shapes = [new PortraitShape { Path = "M0,0 H1 V1 Z", Fill = "#000000" }] },
			],
		};

		var errors = portraits.Validate().ToList();
		Assert.That(errors.Where(x => x.Contains("portraitLayers[badPath].cut.path", StringComparison.Ordinal)), Is.Not.Empty);
		Assert.That(errors, Does.Contain(OurResources.DataListEmpty.FormatCurrentCulture("portraitLayers[noRegions].cut.regions")));
	}

	[Test]
	public void PortraitClothingIsEqualForTheSameClothingInTheSameColors()
	{
		var red = Wearing(s_tunic, "#FF0000");
		var alsoRed = Wearing(s_tunic, "#FF0000");
		Assert.That(red, Is.EqualTo(alsoRed));
		Assert.That(red.GetHashCode(), Is.EqualTo(alsoRed.GetHashCode()));
		Assert.That(red, Is.Not.EqualTo(Wearing(s_tunic, "#00FF00")));
		Assert.That(red, Is.Not.EqualTo(PortraitClothing.None));
	}

	[Test]
	public void IncludesAndClippingMustBeValid()
	{
		var portraits = new PortraitData
		{
			Width = 10,
			Height = 10,
			ShapeSets =
			[
				new PortraitShapeSet { Ai = true, Id = "set", Shapes = [new PortraitShape { Include = "set" }] },
			],
			Layers =
			[
				new PortraitLayer
				{
					Ai = true,
					Id = "a",
					Shapes =
					[
						new PortraitShape { Include = "missing" },
						new PortraitShape { Include = "set", Fill = "#000000" },
						new PortraitShape { Path = "M0,0 H10", Stroke = "#000000", Clip = true },
						new PortraitShape { Path = "M0,0 H10", Stroke = "main" },
					],
				},
				new PortraitLayer { Ai = true, Id = "b", Clothing = ["tunic"], Colors = [new PortraitColor { Id = "shade", Color = "main", Mix = "#000000", Amount = 0.5 }], Shapes = [new PortraitShape { Path = "M0,0 H10", Stroke = "shade", Fill = "main" }] },
			],
		};

		Assert.That(portraits.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.PortraitShapeSetNested.FormatCurrentCulture("portraitShapeSets[set].shapes[0]"),
			OurResources.DataIdUnknown.FormatCurrentCulture("portraitLayers[a].shapes[0].include", "missing", "portraitShapeSets"),
			OurResources.PortraitIncludeDrawn.FormatCurrentCulture("portraitLayers[a].shapes[1]"),
			OurResources.PortraitClipWithoutInclude.FormatCurrentCulture("portraitLayers[a].shapes[2]"),
			OurResources.PortraitColorUnknown.FormatCurrentCulture("portraitLayers[a].shapes[3].stroke", "main"),
		}));
	}

	[Test]
	public void ClothingLayersMustUseClothingAndItsColors()
	{
		var portraits = new PortraitData
		{
			Width = 10,
			Height = 10,
			ShapeSets = [new PortraitShapeSet { Ai = true, Id = "set", Shapes = [new PortraitShape { Path = "M0,0 H10", Stroke = "trim" }] }],
			Layers =
			[
				new PortraitLayer { Ai = true, Id = "a", Clothing = ["tunic", "gown", "cape"], Shapes = [new PortraitShape { Path = "M0,0 H10", Fill = "main" }, new PortraitShape { Include = "set" }] },
				new PortraitLayer { Ai = true, Id = "b", HiddenBy = new PortraitHiddenBy { Regions = ["scalp", "wings"], Coverage = 5 }, Shapes = [new PortraitShape { Path = "M0,0 H10", Stroke = "#000000" }] },
			],
		};
		Assert.That(portraits.Validate(), Is.Empty);

		var errors = GameDataValidation.ValidatePortraits(portraits, new Dictionary<string, CharacterProperty>(), new HashSet<string>(), new HashSet<string>(), null,
			new ClothingData { Slots = [new ClothingSlot { Id = "scalp", Title = "Scalp" }, new ClothingSlot { Id = "torso", Title = "Torso" }], Items = [s_tunic, s_gown, s_hat] });
		Assert.That(errors, Is.EquivalentTo(new[]
		{
			OurResources.DataIdUnknown.FormatCurrentCulture("portraitLayers[b].hiddenBy.regions", "wings", "clothingSlots"),
			OurResources.DataIdUnknown.FormatCurrentCulture("portraitLayers[a].clothing", "cape", "clothing"),
			OurResources.PortraitClothingColorUnknown.FormatCurrentCulture("portraitLayers[a].shapes[1].include: portraitShapeSets[set].shapes[0].stroke", "trim", "gown"),
		}));
	}

	private static PortraitClothing Wearing(ClothingItem clothing, params string[] colors) =>
		new([new PortraitClothingPiece(clothing, clothing.Colors.Zip(colors).ToDictionary(x => x.First, x => x.Second, StringComparer.Ordinal))]);

	private static DrawingCollection GetDrawings(DrawingImage image) => ((DrawingGroup) image.Drawing).Children;

	private static Color GetFill(Drawing drawing) => ((SolidColorBrush) ((GeometryDrawing) drawing).Brush).Color;

	private static (PortraitRenderer Renderer, CharacterTraits Traits) CreateRenderer() => CreateRenderer([]);

	/// <summary>
	/// Creates a renderer with a body (filling the given path), hair, a tunic, and a hat, then the given layers.
	/// </summary>
	private static (PortraitRenderer Renderer, CharacterTraits Traits) CreateRenderer(IReadOnlyList<PortraitLayer> extra, string body = "M0,0 H100 V100 H0 Z")
	{
		var portraits = new PortraitData
		{
			Width = 100,
			Height = 100,
			ShapeSets = [new PortraitShapeSet { Ai = true, Id = "belt", Shapes = [new PortraitShape { Path = "M0,50 H100 V60 H0 Z", Fill = "shade" }] }],
			Layers =
			[
				new PortraitLayer { Ai = true, Id = "body", Shapes = [new PortraitShape { Path = body, Fill = "#000000" }] },
				new PortraitLayer { Ai = true, Id = "hair", HiddenBy = new PortraitHiddenBy { Regions = ["scalp"], Coverage = 5 }, Shapes = [new PortraitShape { Path = "M0,0 H100 V10 H0 Z", Fill = "#111111" }] },
				new PortraitLayer
				{
					Ai = true,
					Id = "tunic",
					Clothing = ["tunic", "gown"],
					Colors = [new PortraitColor { Id = "shade", Color = "main", Mix = "#000000", Amount = 0.5 }],
					Shapes =
					[
						new PortraitShape { Path = "M10,10 H90 V90 H10 Z", Fill = "main" },
						new PortraitShape { Path = "M10,10 H90", Stroke = "shade" },
						new PortraitShape { Include = "belt", Clip = true },
					],
				},
				new PortraitLayer { Ai = true, Id = "hat", Clothing = ["hat"], Shapes = [new PortraitShape { Path = "M20,0 H80 V10 H20 Z", Fill = "#806040" }] },
				.. extra,
			],
		};

		var gameData = new GameData(TestData.CreateCharacterData(), TestData.CreateLocationList(), TestData.CreateProfessionList(), TestData.CreateFamilySettings(), TestData.CreateRelationshipSettings(), TestData.CreateCalendarSettings(), portraits: portraits);
		var traits = TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40);
		return (gameData.PortraitRenderer!, traits);
	}

	private static readonly ClothingItem s_tunic = new() { Id = "tunic", Title = "Tunic", Slots = ["torso"], Layer = "clothes", Colors = ["main", "trim"], Description = "{main.a} tunic", Text = new LongText { Text = "A tunic.", Ai = true } };
	private static readonly ClothingItem s_gown = new() { Id = "gown", Title = "Gown", Slots = ["torso"], Layer = "clothes", Colors = ["main"], Description = "{main.a} gown", Text = new LongText { Text = "A gown.", Ai = true } };
	private static readonly ClothingItem s_hat = new() { Id = "hat", Title = "Hat", Slots = ["scalp"], Layer = "clothes", Description = "a hat", Text = new LongText { Text = "A hat.", Ai = true }, Coverage = new Dictionary<string, int> { ["scalp"] = 5 } };
	private static readonly ClothingItem s_band = new() { Id = "band", Title = "Band", Slots = ["scalp"], Layer = "clothes", Description = "a band", Text = new LongText { Text = "A band.", Ai = true }, Coverage = new Dictionary<string, int> { ["scalp"] = 4 } };
}
