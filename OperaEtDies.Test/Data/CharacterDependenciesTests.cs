using System.IO;
using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class CharacterDependenciesTests
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

	[TestCase("short", true)]
	[TestCase("long", false)]
	[TestCase(null, false)]
	public void NotValuesNeedsAValueThatIsNotListed(string? length, bool expected)
	{
		var condition = new CharacterCondition { NotValues = new Dictionary<string, IReadOnlyList<string>> { ["length"] = ["long", "veryLong"] } };

		Assert.That(CharacterCondition.IsMet(condition, x => x == "length" ? length : null), Is.EqualTo(expected));
	}

	[Test]
	public void ValuesAndNotValuesMustBothBeMet()
	{
		var condition = new CharacterCondition
		{
			Values = new Dictionary<string, IReadOnlyList<string>> { ["gender"] = ["female"] },
			NotValues = new Dictionary<string, IReadOnlyList<string>> { ["length"] = ["short"] },
		};
		string? GetValueId(string gender, string length, string id) => id == "gender" ? gender : length;

		Assert.That(CharacterCondition.IsMet(condition, x => GetValueId("female", "long", x)), Is.True);
		Assert.That(CharacterCondition.IsMet(condition, x => GetValueId("female", "short", x)), Is.False);
		Assert.That(CharacterCondition.IsMet(condition, x => GetValueId("male", "long", x)), Is.False);
	}

	[Test]
	public void InvalidNotValuesReportErrors()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
			{
				"templates": [ { "id": "fullName", "template": "{style}" }, { "id": "description", "template": "{style}" } ],
				"properties": [
					{
						"id": "style",
						"title": "Style",
						"type": "valueList",
						"when": { "notValues": { "length": [ "bald", "unknown" ], "age": [ "x" ] } },
						"values": [ { "id": "curly", "title": "Curly", "when": { "notValues": {} } } ]
					},
					{ "id": "length", "title": "Length", "type": "valueList", "values": [ { "id": "bald", "title": "Bald" }, { "id": "short", "title": "Short" } ] },
					{ "id": "age", "title": "Age", "type": "integer", "range": { "minimum": 1, "maximum": 5, "shape": { "peak": "low", "mostWithin": 2 } } }
				]
			}
			""");

		var result = CharacterData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.ConditionValueUnknown.FormatCurrentCulture("style.when.notValues.length", "unknown", "length"),
			OurResources.ConditionPropertyWithoutValues.FormatCurrentCulture("style.when.notValues", "age"),
			OurResources.DataListEmpty.FormatCurrentCulture("style.values.curly.when.notValues"),
		}));
	}

	[Test]
	public void PropertiesComeAfterTheirDependencies()
	{
		var properties = new[]
		{
			Property("name", keyedBy: "gender"),
			Property("style", conditionId: "length"),
			Property("length", conditionId: "gender"),
			Property("gender"),
			Property("height"),
		};

		Assert.That(CharacterDependencies.GetOrder(properties).Select(x => x.Id), Is.EqualTo(new[] { "gender", "name", "length", "style", "height" }));
		Assert.That(CharacterDependencies.FindCycle(properties), Is.Empty);
	}

	[Test]
	public void FollowersComeRightAfterTheirProperty()
	{
		var properties = new[]
		{
			Property("gender"),
			Property("firstName", keyedBy: "gender"),
			Property("height"),
			Property("profession", conditionId: "age"),
			Property("age"),
			Property("lastName", keyedBy: "profession", follows: "firstName"),
			Property("build"),
		};

		// The first name waits until the last name can be placed, after the profession and the age it depends on.
		Assert.That(CharacterDependencies.GetOrder(properties).Select(x => x.Id), Is.EqualTo(new[] { "gender", "height", "age", "profession", "firstName", "lastName", "build" }));
		Assert.That(CharacterDependencies.FindCycle(properties), Is.Empty);
	}

	[Test]
	public void FollowingAPropertyThatDependsOnTheFollowerIsACycle()
	{
		var properties = new[]
		{
			Property("a", conditionId: "b"),
			Property("b", follows: "a"),
			Property("c"),
		};

		Assert.That(CharacterDependencies.FindCycle(properties).Select(x => x.Id), Is.EqualTo(new[] { "a", "b" }));
		Assert.That(CharacterDependencies.GetOrder(properties).Select(x => x.Id), Is.EqualTo(new[] { "c", "a", "b" }));
	}

	[Test]
	public void InvalidFollowsReportErrors()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
			{
				"templates": [ { "id": "fullName", "template": "{a}" }, { "id": "description", "template": "{a}" } ],
				"properties": [
					{ "id": "a", "title": "A", "type": "valueList", "follows": "a", "values": [ { "id": "a1", "title": "A1" } ] },
					{ "id": "b", "title": "B", "type": "valueList", "follows": "nothing", "values": [ { "id": "b1", "title": "B1" } ] },
					{ "id": "c", "title": "C", "type": "valueList", "follows": "b", "values": [ { "id": "c1", "title": "C1" } ] },
					{ "id": "d", "title": "D", "type": "valueList", "follows": "b", "values": [ { "id": "d1", "title": "D1" } ], "player": { "values": [] } }
				]
			}
			""");

		var result = CharacterData.Load(m_directory);

		// An empty list of values for a role is allowed: that role never has the property.
		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.PropertyFollowsItself.FormatCurrentCulture("a"),
			OurResources.PropertyFollowsUnknown.FormatCurrentCulture("b", "nothing"),
			OurResources.PropertyFollowedTwice.FormatCurrentCulture("b", "\"c\", \"d\""),
		}));
	}

	[Test]
	public void CyclesAreFound()
	{
		var properties = new[]
		{
			Property("a", conditionId: "b"),
			Property("b", conditionId: "a"),
			Property("c", conditionId: "a"),
			Property("d", conditionId: "d"),
			Property("e"),
		};

		// "c" depends on the cycle, but isn't part of it.
		Assert.That(CharacterDependencies.FindCycle(properties).Select(x => x.Id), Is.EqualTo(new[] { "a", "b", "d" }));
		Assert.That(CharacterDependencies.GetOrder(properties).Select(x => x.Id), Is.EqualTo(new[] { "e", "a", "b", "c", "d" }));
	}

	[Test]
	public void PropertiesCanDependOnLaterProperties()
	{
		var gameData = new GameData(
			new CharacterData
			{
				Templates = TestData.CreateTemplates("{name}", "{name}"),
				Properties =
				[
					new CharacterProperty
					{
						Id = "name",
						Title = "Name",
						Type = CharacterPropertyType.String,
						Defaults = new StringDefaults
						{
							KeyedBy = "gender",
							ValuesByKey = new Dictionary<string, IReadOnlyList<WeightedString>>
							{
								["male"] = [new WeightedString { Value = "John" }],
								["female"] = [new WeightedString { Value = "Alice" }],
							},
						},
					},
					new CharacterProperty
					{
						Id = "beard",
						Title = "Beard",
						Type = CharacterPropertyType.ValueList,
						When = TestData.Condition("gender", "male"),
						Values = [new CharacterPropertyOption { Id = "full", Title = "Full" }],
					},
					new CharacterProperty
					{
						Id = "gender",
						Title = "Gender",
						Type = CharacterPropertyType.ValueList,
						Values = [new CharacterPropertyOption { Id = "male", Title = "Male" }, new CharacterPropertyOption { Id = "female", Title = "Female" }],
					},
				],
			},
			TestData.CreateLocationList(), TestData.CreateProfessionList(), TestData.CreateFamilySettings(), TestData.CreateCalendarSettings());

		for (var seed = 0; seed < 20; seed++)
		{
			var traits = new CharacterRandomizer(gameData, new Random(seed), CharacterRole.Npc).GetTraits();
			var isMale = traits.GetValueId("gender") == "male";

			Assert.That(traits.GetString("name"), Is.EqualTo(isMale ? "John" : "Alice"));
			Assert.That(traits.HasValue("beard"), Is.EqualTo(isMale));
		}
	}

	[Test]
	public void CircularDependenciesReportAnError()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
			{
				"templates": [ { "id": "fullName", "template": "{a}" }, { "id": "description", "template": "{a}" } ],
				"properties": [
					{ "id": "a", "title": "A", "type": "valueList", "values": [ { "id": "a1", "title": "A1", "when": { "values": { "b": [ "b1" ] } } }, { "id": "a2", "title": "A2" } ] },
					{ "id": "b", "title": "B", "type": "valueList", "when": { "notValues": { "a": [ "a2" ] } }, "values": [ { "id": "b1", "title": "B1" } ] }
				]
			}
			""");

		var result = CharacterData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EqualTo(new[] { OurResources.PropertyDependencyCycle.FormatCurrentCulture("\"a\", \"b\"") }));
	}

	[TestCase("{a} {b} .", "A.")]
	[TestCase("{a} {b}, then {a} !", "A, then A!")]
	public void SpacesBeforePunctuationAreRemoved(string text, string expected)
	{
		var templates = new TextTemplateSet(new Dictionary<string, IReadOnlyList<TextTemplateVariant>>
		{
			["text"] = [new TextTemplateVariant(null, TextTemplate.Parse(text))],
		});

		Assert.That(templates.Format("text", x => x.Id == "a" ? "A" : "", _ => null), Is.EqualTo(expected));
	}

	private static CharacterProperty Property(string id, string? conditionId = null, string? keyedBy = null, string? follows = null) => new()
	{
		Id = id,
		Follows = follows,
		Title = id,
		Type = keyedBy is null ? CharacterPropertyType.ValueList : CharacterPropertyType.String,
		When = conditionId is null ? null : TestData.Condition(conditionId, "x"),
		Defaults = keyedBy is null ? null : new StringDefaults { KeyedBy = keyedBy, ValuesByKey = new Dictionary<string, IReadOnlyList<WeightedString>>() },
	};

	private string m_directory = "";
}
