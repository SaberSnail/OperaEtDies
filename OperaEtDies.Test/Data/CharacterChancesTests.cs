using System.Globalization;
using System.IO;
using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class CharacterChancesTests
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
	public void ShippedDataHasNoLowChanceValues()
	{
		var gameInfo = GameInfo.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory, gameInfo.FrequencyChances).Value!;

		Assert.That(gameData.GetLowChanceWarnings(), Is.Empty);
	}

	[Test]
	public void LowShapeFollowsABellCurve()
	{
		var chances = new WeightShape { Peak = ShapePeak.Low, MostWithin = 10 }.GetChances(55);

		Assert.That(chances.Sum(), Is.EqualTo(100).Within(0.0001));
		Assert.That(chances[0], Is.EqualTo(7.67).Within(0.01));
		Assert.That(chances[2], Is.EqualTo(7.52).Within(0.01));
		Assert.That(chances[10], Is.EqualTo(4.65).Within(0.01));
		Assert.That(chances, Is.Ordered.Descending);
	}

	[Test]
	public void HighShapeMirrorsLowShape()
	{
		var low = new WeightShape { Peak = ShapePeak.Low, MostWithin = 4 }.GetChances(12);
		var high = new WeightShape { Peak = ShapePeak.High, MostWithin = 4 }.GetChances(12);

		Assert.That(high, Is.EqualTo(low.Reverse()).Within(0.0000001));
	}

	[TestCase(3, 1.0, new[] { 27.4, 45.2, 27.4 })]
	[TestCase(3, 0.5, new[] { 10.65, 78.7, 10.65 })]
	[TestCase(4, 1.0, new[] { 13.45, 36.55, 36.55, 13.45 })]
	public void MiddleShapePeaksInTheMiddle(int count, double mostWithin, double[] expected)
	{
		var chances = new WeightShape { Peak = ShapePeak.Middle, MostWithin = mostWithin }.GetChances(count);

		Assert.That(chances, Is.EqualTo(expected).Within(0.1));
	}

	[TestCase("low+", 10)]
	[TestCase("low++", 20)]
	[TestCase("middle-", 40)]
	[TestCase("middle--", 30)]
	[TestCase("high-", 90)]
	public void PeakStepsMoveThePeakByATenthOfTheRange(string text, int expectedPeak)
	{
		Assert.That(PeakLevel.TryParse(text, out var peak), Is.True);
		var chances = new WeightShape { Peak = peak!.Value, MostWithin = 5 }.GetChances(101);

		Assert.That(chances.ToList().IndexOf(chances.Max()), Is.EqualTo(expectedPeak));
	}

	[TestCase("")]
	[TestCase("lowest")]
	[TestCase("middle+++")]
	[TestCase("high+-")]
	public void InvalidPeaksDontParse(string text)
	{
		Assert.That(PeakLevel.TryParse(text, out _), Is.False);
	}

	[Test]
	public void RangeStartingLaterKeepsTheCurve()
	{
		var range = new IntegerRange { Minimum = 20, Maximum = 60, Shape = new WeightShape { Peak = new PeakLevel(ShapePeak.Middle, -2), MostWithin = 8 } };
		var full = range.Shape.GetChances(41);

		var later = range.StartingAt(36);
		var chances = later.Shape.GetChances(later.Maximum - later.Minimum + 1);

		// The ages that are left keep their weights compared to each other.
		Assert.That(later.Minimum, Is.EqualTo(36));
		var kept = full.Skip(16).ToList();
		Assert.That(chances, Is.EqualTo(kept.Select(x => x / kept.Sum() * 100)).Within(0.0001));
	}

	[Test]
	public void ValueListsCanUseAShape()
	{
		var gameData = new GameData(
			new CharacterData
			{
				Templates = TestData.CreateTemplates("{size}", "{size}"),
				Properties =
				[
					new CharacterProperty
					{
						Id = "size",
						Title = "Size",
						Type = CharacterPropertyType.ValueList,
						Values = [Option("small"), Option("medium"), Option("large")],
						Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 0.5 },
					},
				],
			},
			TestData.CreateLocationList(), TestData.CreateProfessionList(), TestData.CreateFamilySettings());
		var randomizer = new CharacterRandomizer(gameData, new Random(0), CharacterRole.Npc);

		var mediumCount = 0;
		for (var i = 0; i < 2000; i++)
		{
			randomizer.RandomizeAll();
			if (randomizer.GetTraits().GetOption("size").Id == "medium")
				mediumCount++;
		}

		// Expected is 78.7%.
		Assert.That(mediumCount, Is.InRange(1500, 1650));
	}

	[Test]
	public void InvalidShapesReportErrors()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
			{
				"lowChanceWarning": 100,
				"templates": [ { "id": "fullName", "template": "{age}" }, { "id": "description", "template": "{age}" } ],
				"properties": [
					{
						"id": "age",
						"title": "Age",
						"type": "integer",
						"range": { "minimum": 1, "maximum": 5, "shape": { "peak": "low", "mostWithin": -1 } },
						"shape": { "peak": "low", "mostWithin": 1 }
					},
					{
						"id": "size",
						"title": "Size",
						"type": "valueList",
						"values": [ { "id": "small", "title": "Small" }, { "id": "large", "title": "Large", "frequency": "rare" } ],
						"shape": { "peak": "high+", "mostWithin": 0 }
					}
				]
			}
			""");

		var result = CharacterData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.ShapeMostWithinInvalid.FormatCurrentCulture("age.range.shape.mostWithin", -1),
			OurResources.CharacterSettingNotAllowed.FormatCurrentCulture("age", "shape"),
			OurResources.ShapePeakOutOfRange.FormatCurrentCulture("size.shape.peak", "high+"),
			OurResources.ShapeMostWithinInvalid.FormatCurrentCulture("size.shape.mostWithin", 0),
			OurResources.ShapeFrequencyNotAllowed.FormatCurrentCulture("large", "size"),
			OurResources.LowChanceWarningInvalid.FormatCurrentCulture(100),
		}));
	}

	[Test]
	public void LowChanceValuesAreReported()
	{
		var numberShape = new WeightShape { Peak = ShapePeak.Low, MostWithin = 1 };
		var gameData = new GameData(
			new CharacterData
			{
				LowChanceWarning = 5,
				Templates = TestData.CreateTemplates("{name}", "{name}"),
				Properties =
				[
					new CharacterProperty
					{
						Id = "color",
						Title = "Color",
						Type = CharacterPropertyType.ValueList,
						Values = [Option("red"), Option("blue", Frequency.Uncommon), Option("green", Frequency.Rare)],
					},
					new CharacterProperty
					{
						Id = "number",
						Title = "Number",
						Type = CharacterPropertyType.Integer,
						Range = new IntegerRange { Minimum = 1, Maximum = 9, Shape = numberShape },
					},
					new CharacterProperty
					{
						Id = "name",
						Title = "Name",
						Type = CharacterPropertyType.String,
						Defaults = new StringDefaults { Values = [new WeightedString { Value = "Ann" }, new WeightedString { Value = "Bea", Frequency = Frequency.Rare }] },
					},
				],
			},
			TestData.CreateLocationList(), TestData.CreateProfessionList(), TestData.CreateFamilySettings());

		// Numbers 4 to 9 are at or below 5%, and 9 is the least likely.
		var lowestNumberChance = numberShape.GetChances(9)[8].ToString("G3", CultureInfo.CurrentCulture);
		Assert.That(gameData.GetLowChanceWarnings(), Is.EquivalentTo(new[] { "player", "npc" }.SelectMany(role => new[]
		{
			OurResources.LowChanceValue.FormatCurrentCulture("green", "color", "5", role, "", "5"),
			OurResources.LowChanceIntegers.FormatCurrentCulture(role, 6, "number", "5", 9, lowestNumberChance),
			OurResources.LowChanceDefault.FormatCurrentCulture("Bea", "name", "5", role, "", "5"),
		})));
	}

	[Test]
	public void LowChanceWarningsNameTheConditionValues()
	{
		var conditional = TestData.CreateConditionalGameData();
		var gameData = new GameData(
			new CharacterData { LowChanceWarning = 20, Templates = TestData.CreateTemplates("{gender}", "{gender}"), Properties = conditional.CharacterProperties },
			TestData.CreateLocationList(), TestData.CreateProfessionList(), TestData.CreateFamilySettings());

		// No beard is uncommon, so it has a 20% chance among men. Wiry is uncommon too, but men can't have the slender
		// build, so wiry has a third of the chance among them, and isn't reported.
		var male = OurResources.LowChanceCondition.FormatCurrentCulture(OurResources.ConditionCombinationValue.FormatCurrentCulture("gender", "male"));
		Assert.That(gameData.GetLowChanceWarnings(), Is.EquivalentTo(new[] { "player", "npc" }.SelectMany(role => new[]
		{
			OurResources.LowChanceValue.FormatCurrentCulture("none", "beard", "20", role, male, "20"),
		})));
	}

	[Test]
	public void NoThresholdMeansNoWarnings()
	{
		Assert.That(TestData.CreateGameData().GetLowChanceWarnings(), Is.Empty);
	}

	private static CharacterPropertyOption Option(string id, Frequency frequency = Frequency.Common) => new() { Id = id, Title = id, Frequency = frequency };

	private string m_directory = "";
}
