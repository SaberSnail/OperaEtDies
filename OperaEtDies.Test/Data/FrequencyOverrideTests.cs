using System.IO;
using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class FrequencyOverrideTests
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

	[TestCase(20, "young")]
	[TestCase(29, "young")]
	[TestCase(30, "old")]
	[TestCase(70, "old")]
	[TestCase(10, null)]
	public void NumbersBelongToGroups(int age, string? expected)
	{
		var property = CreateGameData().CharacterProperties.Get("age");

		Assert.That(property.GetConditionValueId(age), Is.EqualTo(expected));
	}

	[TestCase(20, 0.05)]
	[TestCase(60, 0.5)]
	public void OverridesChangeFrequencies(int age, double expectedGreyShare)
	{
		var gameData = CreateGameData();
		var randomizer = new CharacterRandomizer(gameData, new Random(0), CharacterRole.Npc);
		randomizer["age"].Lock(age);

		var greyCount = 0;
		const int count = 4000;
		for (var i = 0; i < count; i++)
		{
			randomizer.RandomizeAll();
			if (randomizer.GetTraits().GetOption("hair").Id == "grey")
				greyCount++;
		}

		Assert.That((double) greyCount / count, Is.EqualTo(expectedGreyShare).Within(0.03));
	}

	[Test]
	public void ChangingADriverKeepsAValueThatIsStillAllowed()
	{
		var gameData = CreateGameData();
		var randomizer = new CharacterRandomizer(gameData, new Random(0), CharacterRole.Npc);
		var grey = gameData.CharacterProperties.Get("hair").Values!.Single(x => x.Id == "grey");

		randomizer["age"].Value = 60;
		randomizer["hair"].Value = grey;
		randomizer["age"].Value = 20;

		Assert.That(randomizer["hair"].Value, Is.SameAs(grey));
	}

	[Test]
	public void OverridesCreateDependencies()
	{
		var properties = CreateGameData().CharacterProperties;

		Assert.That(CharacterDependencies.GetDependencyIds(properties.Get("hair")), Is.EqualTo(new[] { "age" }));
		Assert.That(CharacterDependencies.GetOrder(properties).Select(x => x.Id), Is.EqualTo(new[] { "age", "hair" }));
	}

	[Test]
	public void LowChanceWarningsUseOverrides()
	{
		var gameData = CreateGameData(lowChanceWarning: 5);

		// Grey is rare only for young characters. Brown is never that rare. Individual ages are also below the threshold.
		var young = OurResources.LowChanceCondition.FormatCurrentCulture(OurResources.ConditionCombinationValue.FormatCurrentCulture("age", "young"));
		Assert.That(gameData.GetLowChanceWarnings().Where(x => !x.Contains("\"age\" have", StringComparison.Ordinal)), Is.EquivalentTo(new[] { "player", "npc" }.Select(role =>
			OurResources.LowChanceValue.FormatCurrentCulture("grey", "hair", "5", role, young, "5"))));
	}

	[Test]
	public void ConditionsCanUseGroups()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
			{
				"templates": [ { "id": "fullName", "template": "{age}" }, { "id": "description", "template": "{age}" } ],
				"properties": [
					{
						"id": "age",
						"title": "Age",
						"type": "integer",
						"range": { "minimum": 10, "maximum": 70, "shape": { "peak": "low", "mostWithin": 20 } },
						"groups": [ { "id": "young", "minimum": 18, "maximum": 29 }, { "id": "old", "minimum": 30 } ]
					},
					{
						"id": "hair",
						"title": "Hair",
						"type": "valueList",
						"values": [ { "id": "grey", "title": "Grey", "when": { "values": { "age": [ "old" ] } } } ]
					}
				]
			}
			""");

		var result = CharacterData.Load(m_directory);

		// Ages 10 to 17 are in no group, and young characters have no hair values either.
		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[] { "player", "npc" }.SelectMany(role => new[]
		{
			OurResources.ConditionPropertyNoValues.FormatCurrentCulture("hair", role, OurResources.ConditionCombinationValue.FormatCurrentCulture("age", "young")),
			OurResources.ConditionPropertyNoValues.FormatCurrentCulture("hair", role, OurResources.ConditionCombinationNoValue.FormatCurrentCulture("age")),
		})));
	}

	[TestCase("uncommon+", true)]
	[TestCase("common--", true)]
	[TestCase("often", false)]
	[TestCase("rare+-", false)]
	public void DataFilesReadFrequencySteps(string frequency, bool isValid)
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), $$"""
			{
				"templates": [ { "id": "fullName", "template": "{hair}" }, { "id": "description", "template": "{hair}" } ],
				"properties": [
					{
						"id": "hair",
						"title": "Hair",
						"type": "valueList",
						"values": [ { "id": "brown", "title": "Brown" }, { "id": "grey", "title": "Grey", "frequency": "{{frequency}}" } ]
					}
				]
			}
			""");

		var result = CharacterData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), isValid ? Is.Empty : Has.Exactly(1).Contains(OurResources.FrequencyLevelInvalid));
	}

	[Test]
	public void InvalidGroupsAndOverridesReportErrors()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
			{
				"templates": [ { "id": "fullName", "template": "{age}" }, { "id": "description", "variants": [ { "when": { "values": { "age": [ "young" ] } }, "template": "{age}" } ] } ],
				"properties": [
					{
						"id": "age",
						"title": "Age",
						"type": "integer",
						"range": { "minimum": 10, "maximum": 70, "shape": { "peak": "low", "mostWithin": 20 } },
						"groups": [ { "id": "young", "maximum": 30 }, { "id": "old", "minimum": 30 }, { "id": "odd", "minimum": 50, "maximum": 40 }, { "id": "young" } ]
					},
					{
						"id": "hair",
						"title": "Hair",
						"type": "valueList",
						"groups": [ { "id": "x" } ],
						"values": [
							{ "id": "grey", "title": "Grey", "frequencyWhen": [] },
							{ "id": "brown", "title": "Brown", "frequencyWhen": [ { "when": { "values": { "age": [ "middle" ] } }, "frequency": "rare" } ] }
						]
					},
					{
						"id": "eyes",
						"title": "Eyes",
						"type": "valueList",
						"shape": { "peak": "middle", "mostWithin": 1 },
						"values": [ { "id": "blue", "title": "Blue", "frequencyWhen": [ { "when": { "values": { "hair": [ "grey" ] } }, "frequency": "common" } ] } ]
					}
				]
			}
			""");

		var result = CharacterData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.IntegerGroupsOverlap.FormatCurrentCulture("young", "old", "age"),
			OurResources.IntegerRangeInvalid.FormatCurrentCulture("age.groups.odd", 40, 50),
			OurResources.DataListItemIdDuplicate.FormatCurrentCulture("young", "age.groups"),
			OurResources.IntegerGroupsOverlap.FormatCurrentCulture("young", "young", "age"),
			OurResources.IntegerGroupsOverlap.FormatCurrentCulture("old", "young", "age"),
			OurResources.CharacterSettingNotAllowed.FormatCurrentCulture("hair", "groups"),
			OurResources.DataListEmpty.FormatCurrentCulture("hair.values.grey.frequencyWhen"),
			OurResources.ConditionValueUnknown.FormatCurrentCulture("hair.values.brown.frequencyWhen[0].when.values.age", "middle", "age"),
			OurResources.ShapeFrequencyNotAllowed.FormatCurrentCulture("blue", "eyes"),
			OurResources.ConditionPropertyInvalid.FormatCurrentCulture("description.variants[0].when.values", "age"),
		}));
	}

	/// <summary>
	/// Creates game data where grey hair is rare for young characters and common for old ones, and brown hair is
	/// always common.
	/// </summary>
	private static GameData CreateGameData(double? lowChanceWarning = null)
	{
		CharacterCondition Age(string group) => new() { Values = new Dictionary<string, IReadOnlyList<string>> { ["age"] = [group] } };
		return new GameData(
			new CharacterData
			{
				LowChanceWarning = lowChanceWarning,
				Templates = TestData.CreateTemplates("{hair}", "{hair}"),
				Properties =
				[
					new CharacterProperty
					{
						Id = "hair",
						Title = "Hair",
						Type = CharacterPropertyType.ValueList,
						Values =
						[
							new CharacterPropertyOption { Id = "brown", Title = "Brown" },
							new CharacterPropertyOption
							{
								Id = "grey",
								Title = "Grey",
								Frequency = Frequency.Rare,
								FrequencyWhen = [new FrequencyOverride { When = Age("old"), Frequency = Frequency.Common }],
							},
						],
					},
					new CharacterProperty
					{
						Id = "age",
						Title = "Age",
						Type = CharacterPropertyType.Integer,
						Range = new IntegerRange { Minimum = 18, Maximum = 70, Shape = new WeightShape { Peak = ShapePeak.Low, MostWithin = 20 } },
						Groups = [new IntegerGroup { Id = "young", Minimum = 18, Maximum = 29 }, new IntegerGroup { Id = "old", Minimum = 30 }],
					},
				],
			},
			TestData.CreateLocationList());
	}

	private string m_directory = "";
}
