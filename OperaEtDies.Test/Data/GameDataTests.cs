using System.IO;
using System.Text.Json;
using GoldenAnvil.Utility;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class GameDataTests
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
	public void LoadsShippedGameData()
	{
		var result = GameData.Load(JsonDataLoader.DefaultDataDirectory);

		Assert.That(result.Errors, Is.Empty);
		var gameData = result.Value!;

		Assert.That(gameData.CharacterProperties, Is.Not.Empty);
		Assert.That(gameData.CharacterProperties.Select(x => x.Id), Has.All.Not.Empty);
		Assert.That(gameData.CharacterProperties.Select(x => x.Id), Is.Unique);
		Assert.That(gameData.CharacterProperties.Where(x => x.Type == CharacterPropertyType.ValueList), Has.All.Property(nameof(CharacterProperty.Values)).Not.Empty);
		Assert.That(gameData.CharacterTemplates.Contains(CharacterData.FullNameTemplateId), Is.True);
		Assert.That(gameData.CharacterTemplates.Contains(CharacterData.DescriptionTemplateId), Is.True);
		Assert.That(gameData.CharacterLowChanceWarning, Is.Not.Null);

		Assert.That(gameData.QuarterTemplates, Is.Not.Empty);
		Assert.That(gameData.HouseTemplates, Is.Not.Empty);
		// Every profession value is a business, except homemaker and infirm.
		var families = gameData.Families;
		Assert.That(gameData.Professions.Select(x => x.Id).Append(families.Roles.Homemaker).Append(families.Roles.Infirm), Is.EquivalentTo(gameData.CharacterProperties.Get(families.Roles.Properties.Profession).Values!.Select(x => x.Id)));
		Assert.That(gameData.BusinessTemplates, Is.Not.Empty);
		Assert.That(gameData.QuarterTemplates.Select(x => x.Id).Concat(gameData.HouseTemplates.Select(x => x.Id)).Concat(gameData.BusinessTemplates.Select(x => x.Id)), Is.Unique);
		foreach (var profession in gameData.Professions)
			Assert.That(gameData.GetBusinessTemplates(profession.Id), profession.WorksFromHome ? Is.Empty : Is.Not.Empty, profession.Id);
	}

	[Test]
	public void ReportsMissingSections()
	{
		File.WriteAllText(Path.Combine(m_directory, "Characters.json"), """{ "templates": [], "properties": [] }""");

		var result = GameData.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[] { "game", "village", "families", "relationships", "calendar" }.Select(x => OurResources.DataSectionMissing.FormatCurrentCulture(x))));
	}

	[Test]
	public void InvalidCharacterPropertiesReportErrors()
	{
		var result = TestData.LoadCharacterData("""
			{
				"templates": [ { "id": "fullName", "template": "{firstName}" }, { "id": "description", "template": "{age}" } ],
				"properties": [
					{
						"id": "gender",
						"title": "Gender",
						"type": "valueList",
						"values": [
							{ "id": "male", "title": "Male", "text": { "pronoun": "He" } },
							{ "id": "female", "title": "Female", "frequency": "uncommon", "text": { "pronoun": " " } }
						],
						"range": { "minimum": 1, "maximum": 2, "shape": { "peak": "low", "mostWithin": 2 } },
						"player": { "values": [ "male", "other", "male" ] }
					},
					{ "id": "age", "title": "Age", "type": "integer", "range": { "minimum": 10, "maximum": 5, "shape": { "peak": "middle", "mostWithin": 2 } }, "player": { "defaults": { "values": [] } } },
					{ "id": "height", "title": "Height", "type": "integer", "npc": { "range": { "minimum": 1, "maximum": 5, "shape": { "peak": "high", "mostWithin": 2 } } } },
					{
						"id": "firstName",
						"title": "First Name",
						"type": "string",
						"defaults": { "keyedBy": "gender", "valuesByKey": { "male": [ { "value": "John" } ], "other": [ { "value": "" } ] } }
					},
					{ "id": "lastName", "title": "Last Name", "type": "string", "defaults": { "keyedBy": "missing", "valuesByKey": {} } },
					{ "id": "nickname", "title": "Nickname", "type": "valueList" },
					{ "id": "motto", "title": "Motto", "type": "string", "defaults": { "keyedBy": "gender", "values": [ { "value": "Hi" } ] } }
				]
			}
			""");

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.CharacterOptionTextBlank.FormatCurrentCulture("female", "gender", "pronoun"),
			OurResources.CharacterSettingNotAllowed.FormatCurrentCulture("gender", "range"),
			OurResources.RoleValueUnknown.FormatCurrentCulture("gender.player.values", "other"),
			OurResources.DataListItemIdDuplicate.FormatCurrentCulture("male", "gender.player.values"),
			OurResources.IntegerRangeInvalid.FormatCurrentCulture("age.range", 5, 10),
			OurResources.CharacterSettingNotAllowed.FormatCurrentCulture("age", "player.defaults"),
			OurResources.CharacterSettingMissing.FormatCurrentCulture("height", "range"),
			OurResources.StringDefaultsKeyUnknown.FormatCurrentCulture("firstName.defaults", "other", "gender"),
			OurResources.WeightedValueMissing.FormatCurrentCulture("firstName.defaults.valuesByKey.other"),
			OurResources.StringDefaultsKeyMissing.FormatCurrentCulture("firstName.defaults", "female", "gender"),
			OurResources.StringDefaultsKeyedByInvalid.FormatCurrentCulture("lastName.defaults", "missing"),
			OurResources.CharacterSettingMissing.FormatCurrentCulture("nickname", "values"),
			OurResources.StringDefaultsFormInvalid.FormatCurrentCulture("motto.defaults"),
		}));
	}

	[Test]
	public void InvalidTemplatesReportErrors()
	{
		var result = TestData.LoadCharacterData("""
			{
				"templates": [
					{ "id": "fullName", "template": "{nobody} {age.text} {gender.pronoun:shout} {description.x}" },
					{ "id": "description", "template": "Broken }" },
					{ "id": "age", "template": "Clash" },
					{ "id": "loopA", "template": "{loopB}" },
					{ "id": "loopB", "template": "{loopA} {fullName}" },
					{ "id": "blank", "template": " " },
					{ "id": "loopA", "template": "Duplicate" },
					{ "id": "", "template": "No id" }
				],
				"properties": [
					{ "id": "gender", "title": "Gender", "type": "valueList", "values": [ { "id": "male", "title": "Male", "text": { "pronoun": "he" } } ] },
					{ "id": "age", "title": "Age", "type": "integer", "range": { "minimum": 1, "maximum": 5, "shape": { "peak": "low", "mostWithin": 2 } } }
				]
			}
			""");

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.TemplateIdIsProperty.FormatCurrentCulture("age"),
			OurResources.DataListItemIdDuplicate.FormatCurrentCulture("loopA", "templates"),
			OurResources.DataListItemIdMissing.FormatCurrentCulture("templates"),
			OurResources.TemplateBlank.FormatCurrentCulture("blank"),
			OurResources.TemplateMalformed.FormatCurrentCulture("description"),
			OurResources.TemplatePropertyUnknown.FormatCurrentCulture("fullName", "nobody"),
			OurResources.TemplateFieldNotAllowed.FormatCurrentCulture("fullName", "age", "text"),
			OurResources.TemplateFormatUnknown.FormatCurrentCulture("fullName", "shout"),
			OurResources.TemplateFieldNotAllowed.FormatCurrentCulture("fullName", "description", "x"),
			OurResources.TemplateCycle.FormatCurrentCulture("loopA"),
			OurResources.TemplateCycle.FormatCurrentCulture("loopB"),
		}));
	}

	[Test]
	public void RequiredTemplatesMustExist()
	{
		var result = TestData.LoadCharacterData("""
			{
				"templates": [ { "id": "other", "template": "{age}" } ],
				"properties": [ { "id": "age", "title": "Age", "type": "integer", "range": { "minimum": 1, "maximum": 5, "shape": { "peak": "low", "mostWithin": 2 } } } ]
			}
			""");

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.TemplateRequired.FormatCurrentCulture(CharacterData.FullNameTemplateId),
			OurResources.TemplateRequired.FormatCurrentCulture(CharacterData.DescriptionTemplateId),
		}));
	}

	[Test]
	public void TemplatesCanUseOtherTemplates()
	{
		var templates = new TextTemplateSet(new Dictionary<string, IReadOnlyList<TextTemplateVariant>>
		{
			["greeting"] = [new TextTemplateVariant(null, TextTemplate.Parse("{name:first_upper} says hi."))],
			["name"] = [new TextTemplateVariant(null, TextTemplate.Parse("{title} {first}"))],
		});

		Assert.That(templates.Format("greeting", x => x.Id == "title" ? "sir" : "Hugh", _ => null), Is.EqualTo("Sir Hugh says hi."));
	}

	[TestCase("knight", "Sir Hugh")]
	[TestCase("smith", "Goodman Hugh")]
	[TestCase(null, "Hugh")]
	public void TemplatesUseTheFirstVariantWhoseConditionIsMet(string? rank, string expected)
	{
		var templates = new TextTemplateSet(new Dictionary<string, IReadOnlyList<TextTemplateVariant>>
		{
			["name"] =
			[
				new TextTemplateVariant(TestData.Condition("rank", "knight"), TextTemplate.Parse("Sir {first}")),
				new TextTemplateVariant(TestData.Condition("rank", "knight", "smith"), TextTemplate.Parse("Goodman {first}")),
				new TextTemplateVariant(null, TextTemplate.Parse("{first}")),
			],
		});

		Assert.That(templates.Format("name", _ => "Hugh", x => x == "rank" && rank is not null ? new CharacterPropertyOption { Id = rank, Title = rank } : null), Is.EqualTo(expected));
	}

	[Test]
	public void InvalidConditionsReportErrors()
	{
		var result = TestData.LoadCharacterData("""
			{
				"templates": [
					{ "id": "fullName", "template": "{age}", "variants": [ { "template": "{age}" } ] },
					{
						"id": "description",
						"variants": [
							{ "when": { "values": { "age": [ "x" ], "gender": [ "other", "" ] } }, "template": "{age}" },
							{ "when": {}, "template": "{age}" },
							{ "when": { "values": {} }, "template": "{age}" }
						]
					},
					{ "id": "empty", "variants": [] },
					{ "id": "missing" }
				],
				"properties": [
					{ "id": "age", "title": "Age", "type": "integer", "range": { "minimum": 1, "maximum": 5, "shape": { "peak": "low", "mostWithin": 2 } }, "when": { "values": { "gender": [ "male" ] } } },
					{
						"id": "gender",
						"title": "Gender",
						"type": "valueList",
						"when": { "values": { "gender": [ "male" ] }, "hidden": [ "age" ] },
						"values": [ { "id": "male", "title": "Male", "when": { "values": { "age": [ "male" ], "build": [] } } } ]
					}
				]
			}
			""");

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.PropertyDependencyCycle.FormatCurrentCulture("\"age\", \"gender\""),
			OurResources.ConditionHiddenNotAllowed.FormatCurrentCulture("gender.when"),
			OurResources.ConditionPropertyWithoutValues.FormatCurrentCulture("gender.values.male.when.values", "age"),
			OurResources.ConditionPropertyWithoutValues.FormatCurrentCulture("gender.values.male.when.values", "build"),
			OurResources.DataListEmpty.FormatCurrentCulture("gender.values.male.when.values.build"),
			OurResources.TemplateFormInvalid.FormatCurrentCulture("fullName"),
			OurResources.ConditionPropertyInvalid.FormatCurrentCulture("description.variants[0].when.values", "age"),
			OurResources.ConditionValueUnknown.FormatCurrentCulture("description.variants[0].when.values.gender", "other", "gender"),
			OurResources.DataListItemIdMissing.FormatCurrentCulture("description.variants[0].when.values.gender"),
			OurResources.ConditionEmpty.FormatCurrentCulture("description.variants[1].when"),
			OurResources.DataListEmpty.FormatCurrentCulture("description.variants[2].when.values"),
			OurResources.DataListEmpty.FormatCurrentCulture("empty.variants"),
			OurResources.TemplateFormInvalid.FormatCurrentCulture("missing"),
		}));
	}

	[Test]
	public void ConditionCombinationsReportErrors()
	{
		var result = TestData.LoadCharacterData("""
			{
				"templates": [
					{ "id": "fullName", "template": "{beard}" },
					{ "id": "description", "variants": [ { "when": { "values": { "gender": [ "male" ] } }, "template": "{build}" } ] }
				],
				"properties": [
					{
						"id": "gender",
						"title": "Gender",
						"type": "valueList",
						"values": [ { "id": "male", "title": "Male" }, { "id": "female", "title": "Female" } ],
						"player": { "values": [ "male" ] }
					},
					{ "id": "beard", "title": "Beard", "type": "valueList", "when": { "values": { "gender": [ "male" ] } }, "values": [ { "id": "full", "title": "Full" } ] },
					{ "id": "build", "title": "Build", "type": "valueList", "values": [ { "id": "brawny", "title": "Brawny", "when": { "values": { "gender": [ "male" ] } } } ] }
				]
			}
			""");

		// The player is always male, so only non-player characters can be female.
		var female = OurResources.ConditionCombinationValue.FormatCurrentCulture("gender", "female");
		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.ConditionPropertyNoValues.FormatCurrentCulture("build", "npc", female),
			OurResources.TemplatePropertyMissing.FormatCurrentCulture("fullName", "beard", "npc", female),
			OurResources.TemplateVariantMissing.FormatCurrentCulture("description", "npc", female),
		}));
	}

	[Test]
	public void TooManyCombinationsNamesWhatWasChecked()
	{
		// Three properties with 30 values each make 27000 combinations. Only conditions that decide which values are
		// allowed count, so the frequency overrides on "weighted" don't need to be checked.
		static string Values(string prefix) => string.Join(", ", Enumerable.Range(0, 30).Select(x => $$"""{ "id": "{{prefix}}{{x}}", "title": "{{prefix}}{{x}}" }"""));
		const string all = """{ "values": { "a": [ "a0" ], "b": [ "b0" ], "c": [ "c0" ] } }""";
		var result = TestData.LoadCharacterData($$"""
			{
				"templates": [ { "id": "fullName", "template": "{a}" }, { "id": "description", "template": "{a}" } ],
				"properties": [
					{ "id": "a", "title": "A", "type": "valueList", "values": [ {{Values("a")}} ] },
					{ "id": "b", "title": "B", "type": "valueList", "values": [ {{Values("b")}} ] },
					{ "id": "c", "title": "C", "type": "valueList", "values": [ {{Values("c")}} ] },
					{ "id": "limited", "title": "Limited", "type": "valueList", "values": [ { "id": "x", "title": "X", "when": {{all}} }, { "id": "y", "title": "Y" } ] },
					{ "id": "weighted", "title": "Weighted", "type": "valueList", "values": [ { "id": "x", "title": "X", "frequencyWhen": [ { "when": {{all}}, "frequency": "rare" } ] } ] }
				]
			}
			""");

		var sizes = "a (30), b (30), c (30)";
		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.ConditionCombinationsTooManyForProperty.FormatCurrentCulture("limited", "player", CharacterData.MaxConditionCombinations, sizes),
			OurResources.ConditionCombinationsTooManyForProperty.FormatCurrentCulture("limited", "npc", CharacterData.MaxConditionCombinations, sizes),
		}));
	}

	[Test]
	public void TemplateTextMustExistOnEveryValue()
	{
		var result = TestData.LoadCharacterData("""
			{
				"templates": [ { "id": "fullName", "template": "{gender.pronoun} {gender.pronoun}" }, { "id": "description", "template": "{gender}" } ],
				"properties": [
					{
						"id": "gender",
						"title": "Gender",
						"type": "valueList",
						"values": [ { "id": "male", "title": "Male", "text": { "pronoun": "He" } }, { "id": "female", "title": "Female" } ]
					}
				]
			}
			""");

		Assert.That(result.Errors.Select(x => x.Message), Is.EqualTo(new[] { OurResources.TemplateFieldMissing.FormatCurrentCulture("fullName", "gender", "pronoun", "female") }));
	}

	[TestCase(true, "A man walks by.")]
	[TestCase(false, "A man with a beard walks by.")]
	public void HiddenVariantsCanLeaveOutText(bool isBeardHidden, string expected)
	{
		var beard = new CharacterPropertyOption { Id = "full", Title = "Full" };
		var templates = new TextTemplateSet(new Dictionary<string, IReadOnlyList<TextTemplateVariant>>
		{
			["sentence"] = [new TextTemplateVariant(null, TextTemplate.Parse(" A man {beardClause} walks by. "))],
			["beardClause"] =
			[
				new TextTemplateVariant(new CharacterCondition { Hidden = ["beard"] }, TextTemplate.Parse("")),
				new TextTemplateVariant(null, TextTemplate.Parse("with a {beard}")),
			],
		});

		Assert.That(templates.Format("sentence", _ => "beard", x => x == "beard" && !isBeardHidden ? beard : null), Is.EqualTo(expected));
	}

	[Test]
	public void InvalidObscuringReportsErrors()
	{
		var result = TestData.LoadCharacterData("""
			{
				"templates": [
					{ "id": "fullName", "template": "{age}" },
					{ "id": "description", "variants": [ { "when": { "hidden": [ "nobody", "age", "age" ] }, "template": "" }, { "when": { "hidden": [] }, "template": "{age}" } ] }
				],
				"properties": [
					{ "id": "gender", "title": "Gender", "type": "valueList", "regions": [ "torso" ], "values": [ { "id": "male", "title": "Male", "prominence": 0 } ] },
					{ "id": "eyes", "title": "Eyes", "type": "valueList", "values": [ { "id": "blue", "title": "Blue", "prominence": 5 } ] },
					{ "id": "age", "title": "Age", "type": "integer", "range": { "minimum": 1, "maximum": 5, "shape": { "peak": "low", "mostWithin": 2 } } }
				]
			}
			""");

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.ProminenceInvalid.FormatCurrentCulture("male", "gender.values", 0, 1, 10),
			OurResources.ProminenceWithoutRegions.FormatCurrentCulture("blue", "eyes.values", "eyes"),
			OurResources.TemplateHiddenPropertyUnknown.FormatCurrentCulture("description.variants[0].when.hidden", "nobody"),
			OurResources.DataListItemIdDuplicate.FormatCurrentCulture("age", "description.variants[0].when.hidden"),
			OurResources.DataListEmpty.FormatCurrentCulture("description.variants[1].when.hidden"),
		}));
	}

	[Test]
	public void TemplatesMustHandleObscuredValues()
	{
		var result = TestData.LoadCharacterData("""
			{
				"templates": [
					{ "id": "fullName", "template": "{name}" },
					{ "id": "description", "template": "{beard} {hat}" }
				],
				"properties": [
					{ "id": "name", "title": "Name", "type": "string" },
					{
						"id": "beard",
						"title": "Beard",
						"regions": [ "face" ],
						"type": "valueList",
						"values": [ { "id": "full", "title": "Full" }, { "id": "stubble", "title": "Stubble", "prominence": 1 } ]
					},
					{ "id": "hat", "title": "Hat", "type": "valueList", "regions": [ "scalp" ], "values": [ { "id": "tall", "title": "Tall", "prominence": 3 } ] }
				],
				"clothing": [ { "obscures": { "beard": 1, "hat": 2 } } ]
			}
			""");

		// Stubble can be hidden, so the template must handle a hidden beard. No hat is prominent enough to hide.
		var hidden = OurResources.ConditionCombinationHidden.FormatCurrentCulture("beard");
		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.TemplatePropertyMissing.FormatCurrentCulture("description", "beard", "player", hidden),
			OurResources.TemplatePropertyMissing.FormatCurrentCulture("description", "beard", "npc", hidden),
		}));
	}

	[Test]
	public void UnknownPropertyTypeReportsError()
	{
		var result = TestData.LoadCharacterData("""
			{ "templates": [ { "id": "fullName", "template": "{a}" }, { "id": "description", "template": "{a}" } ], "properties": [ { "id": "a", "title": "A", "type": "boolean" } ] }
			""");

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors, Has.Count.EqualTo(1));
	}

	[TestCase("plain", "plain")]
	[TestCase("{a} and {b.c}", "a and b.c")]
	[TestCase("{{literal}} {a}", "{literal} a")]
	[TestCase("{b.c:first_upper} {a}", "B.c a")]
	[TestCase("{x:first_upper}", "X")]
	[TestCase("{a.b.c}", "a.b.c")]
	public void TemplatesFillInPlaceholders(string text, string expected)
	{
		Assert.That(TextTemplate.TryParse(text, out var template), Is.True);
		Assert.That(template!.Format(x => x.Id + (x.Field is null ? "" : "." + x.Field)), Is.EqualTo(expected));
	}

	[TestCase("{")]
	[TestCase("}")]
	[TestCase("{}")]
	[TestCase("{a.}")]
	[TestCase("{a{b}")]
	[TestCase("{a:}")]
	[TestCase("{:first_upper}")]
	[TestCase("{a:b:c}")]
	public void MalformedTemplatesFailToParse(string text)
	{
		Assert.That(TextTemplate.TryParse(text, out _), Is.False);
	}

	[Test]
	public void InvalidLocationsReportErrors()
	{
		var errors = ValidateLocations("""
			{
				"village": {
					"familyCount": { "minimum": -1, "maximum": 5 },
					"buildingTravelTime": -1,
					"landmarkCount": { "minimum": -1, "maximum": 2, "shape": { "peak": "low", "mostWithin": 1 } },
					"houseTitle": "The {lastName Home"
				},
				"quarterKinds": [
					{ "id": "lane", "area": "village", "path": "lane", "travelTime": 2 },
					{ "id": "lane", "area": "village", "path": "lane", "travelTime": 2 },
					{ "id": "plaza", "area": "village", "count": { "min": 1, "max": 1 }, "path": " ", "travelTime": -1 },
					{ "id": "yard", "area": "village", "count": { "min": -1, "max": 0 }, "path": "lane", "travelTime": 2 },
					{ "id": "court", "area": "village", "count": { "min": 3, "max": 2 }, "path": "lane", "travelTime": 2 }
				],
				"layouts": [ { "id": "layout", "shape": "radial", "center": "lane" } ],
				"quarters": [
					{
						"id": "lane",
						"title": "Lane",
						"description": { "short": { "text": "", "ai": true }, "medium": { "text": " ", "ai": true }, "long": [] },
						"kind": "nowhere",
						"houses": { "minimum": 3, "maximum": 2, "wealth": "veryLow" }
					},
					{
						"id": "square",
						"title": "Square",
						"description": { "short": { "text": "Short.", "ai": true }, "medium": { "text": "Medium.", "ai": true }, "long": [ { "text": "One.", "ai": true }, { "text": " ", "ai": false } ] },
						"kind": "lane",
						"size": { "length": 0, "width": 1 }
					}
				],
				"houses": [
					{ "id": "square", "title": "House", "description": { "short": { "text": "Short.", "ai": true }, "medium": { "text": "Medium.", "ai": true }, "long": [ { "text": "One.", "ai": true } ] }, "wealth": "low" }
				],
				"businesses": [
					{ "id": "square", "title": "Shop", "description": { "short": { "text": "Short.", "ai": true }, "medium": { "text": "Medium.", "ai": true }, "long": [ { "text": "One.", "ai": true } ] }, "profession": "smith", "quarterKinds": "lane" },
					{ "id": "forge", "title": "", "description": { "short": { "text": "Short.", "ai": true }, "medium": { "text": "", "ai": true }, "long": [ { "text": "One.", "ai": true } ] }, "profession": " ", "quarterKinds": [] },
					{ "id": "smithy", "title": "Smithy", "description": { "short": { "text": "Short.", "ai": true }, "medium": { "text": "Medium.", "ai": true }, "long": [ { "text": "One.", "ai": true } ] }, "profession": "smith", "quarterKinds": [ "nowhere" ] }
				]
			}
			""");

		Assert.That(errors, Is.EquivalentTo(new[]
		{
			OurResources.DataListItemIdDuplicate.FormatCurrentCulture("lane", "quarterKinds"),
			OurResources.DataValueMissing.FormatCurrentCulture("quarterKinds[plaza].path"),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("village.buildingTravelTime", -1, 0),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("quarterKinds[plaza].travelTime", -1, 0),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("quarterKinds[yard].count.min", -1, 0),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("quarterKinds[yard].count.max", 0, 1),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("quarterKinds[court].count.max", 2, 3),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("village.familyCount.minimum", -1, 0),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("village.landmarkCount.minimum", -1, 0),
			OurResources.LayoutCenterInvalid.FormatCurrentCulture("radial", "lane"),
			OurResources.QuarterKindMinimumMissing.FormatCurrentCulture("plaza"),
			OurResources.QuarterKindMinimumMissing.FormatCurrentCulture("court"),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("quarters[square].size.length", 0, 1),
			OurResources.LocationDescriptionMissing.FormatCurrentCulture("lane", "description.short"),
			OurResources.LocationDescriptionMissing.FormatCurrentCulture("lane", "description.medium"),
			OurResources.LocationDescriptionMissing.FormatCurrentCulture("lane", "description.long"),
			OurResources.QuarterKindUnknown.FormatCurrentCulture("lane", "nowhere"),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("quarters[lane].houses.maximum", 2, 3),
			OurResources.WealthLevelNotAllowed.FormatCurrentCulture("quarters[lane].houses.wealth", "veryLow"),
			OurResources.LocationDescriptionParagraphBlank.FormatCurrentCulture("square"),
			OurResources.HouseWealthMissing.FormatCurrentCulture("medium"),
			OurResources.HouseWealthMissing.FormatCurrentCulture("high"),
			OurResources.QuarterWealthMissing.FormatCurrentCulture("low"),
			OurResources.QuarterWealthMissing.FormatCurrentCulture("medium"),
			OurResources.QuarterWealthMissing.FormatCurrentCulture("high"),
			OurResources.BusinessQuarterMissing.FormatCurrentCulture("square"),
			OurResources.DataListItemTitleMissing.FormatCurrentCulture("forge", "businesses"),
			OurResources.LocationDescriptionMissing.FormatCurrentCulture("forge", "description.medium"),
			OurResources.DataValueMissing.FormatCurrentCulture("businesses[forge].profession"),
			OurResources.DataListEmpty.FormatCurrentCulture("businesses[forge].quarterKinds"),
			OurResources.QuarterKindUnknown.FormatCurrentCulture("smithy", "nowhere"),
			OurResources.LocationIdDuplicate.FormatCurrentCulture("square"),
			OurResources.TemplateMalformed.FormatCurrentCulture("village.houseTitle"),
			OurResources.HouseTitleNotAllowed.FormatCurrentCulture("square"),
		}));
	}

	[Test]
	public void InvalidFeaturesReportErrors()
	{
		CopyShippedData();
		ModifyShipped("Locations.json", x => x
			.Replace("\"crossings\": [ \"bridge\", \"ford\" ],", "\"size\": { \"length\": 2, \"width\": 1 },", StringComparison.Ordinal)
			.Replace("\"borders\": \"river\"", "\"borders\": \"lake\"", StringComparison.Ordinal)
			.Replace("\"To the {direction}, a reedy pond reflects the sky.\"", "\"To the {place}, a reedy pond reflects the sky.\"", StringComparison.Ordinal)
			.Replace("\"id\": \"square\", \"area\": \"village\",", "\"id\": \"square\", \"area\": \"village\", \"borders\": \"hill\",", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.FeatureSettingMissing.FormatCurrentCulture("river", "crossings", "river"),
			OurResources.FeatureSettingNotAllowed.FormatCurrentCulture("river", "size", "river"),
			OurResources.FeatureTextInvalid.FormatCurrentCulture("features[pond].text", "To the {place}, a reedy pond reflects the sky."),
			OurResources.FeatureUnknown.FormatCurrentCulture("quarterKinds[riverside].borders", "lake"),
			OurResources.QuarterKindMinimumBorders.FormatCurrentCulture("square"),
		}));
		Assert.That(result.Errors.Select(x => Path.GetFileName(x.FilePath)), Is.All.EqualTo("Locations.json"));
	}

	[Test]
	public void OnlyKindsWithAMinimumCanBeOptional()
	{
		CopyShippedData();
		ModifyShipped("Locations.json", x => x
			.Replace("\"optional\": { \"with\": \"river\", \"chance\": 50 }", "\"optional\": { \"with\": \"lake\", \"chance\": 150 }", StringComparison.Ordinal)
			.Replace("\"id\": \"lane\", \"area\": \"village\",", "\"id\": \"lane\", \"area\": \"village\", \"optional\": { \"with\": \"river\" },", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.FeatureUnknown.FormatCurrentCulture("quarterKinds[well].optional.with", "lake"),
			OurResources.DataChanceInvalid.FormatCurrentCulture("quarterKinds[well].optional.chance", 150),
			OurResources.QuarterKindOptionalNoMinimum.FormatCurrentCulture("lane"),
		}));
	}

	[Test]
	public void InvalidProfessionsReportErrors()
	{
		var professions = new ProfessionList
		{
			Professions = Read<List<Profession>>("""
				[
					{ "id": "smith", "title": "Smith", "business": { "wealth": "high", "maxBusinesses": 0, "helpers": -1, "forHouseholdElders": true } },
					{ "id": "smith", "title": "Smith", "business": { "wealth": "low" } },
					{ "id": "brewer", "title": "Brewer", "business": { "wealth": "low", "hours": { "start": 20, "end": 20, "days": "day1" } } },
					{ "id": "keeper", "title": "Keeper", "role": "homemaker" },
					{ "id": "sick", "title": "Sick", "role": "infirm", "player": false, "business": { "wealth": "low" } }
				]
				"""),
		};

		Assert.That(professions.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.DataNumberTooSmall.FormatCurrentCulture("professions[smith].business.maxBusinesses", 0, 1),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("professions[smith].business.helpers", -1, 0),
			OurResources.DataListItemIdDuplicate.FormatCurrentCulture("smith", "professions"),
			OurResources.HoursInvalid.FormatCurrentCulture("professions[brewer].business.hours", 20, 20),
			OurResources.HoursSettingNotAllowed.FormatCurrentCulture("professions[brewer].business.hours.days"),
			OurResources.ProfessionPlayerNotBusiness.FormatCurrentCulture("keeper"),
			OurResources.ProfessionNotBusiness.FormatCurrentCulture("sick", "infirm"),
		}));
	}

	[Test]
	public void InvalidFamilySettingsReportErrors()
	{
		var families = Read<FamilySettings>("""
			{
				"head": { "femaleChance": 101, "femaleMinWealth": "low", "age": { "minimum": 50, "maximum": 20, "shape": { "peak": "middle", "mostWithin": 9 } } },
				"wife": { "chance": 80, "ageDifference": { "minimum": 4, "maximum": -10, "shape": { "peak": "middle", "mostWithin": 3 } } },
				"children": { "count": { "minimum": 0, "maximum": 2 }, "age": { "peak": "middle", "mostWithin": 5 }, "siblingAgeSpread": 0, "sonChance": 5, "homemakerChance": 50 },
				"elder": { "chance": -1, "femaleChance": 70, "ageGap": { "peak": "low", "mostWithin": 0 }, "minAge": { "male": -1, "female": 50 }, "infirmChance": { "age_old": 150 }, "homemakerChance": 50 },
				"parentAge": { "minimum": 18, "maxMother": 17, "maxFather": 55 },
				"helperChance": 80,
				"wealth": {
					"helper": 1.5, "helperFalloff": 0.7, "homemaker": 0.5, "infirm": -0.5,
					"fortune": { "minimum": -10, "maximum": 140, "shape": { "peak": "middle", "mostWithin": 12 } },
					"headAge": { "age_young": -5 },
					"thresholds": { "medium": 10, "high": 5 }
				}
			}
			""");
		var relationships = Read<RelationshipSettings>("""
			{
				"score": { "minimum": -5, "maximum": 120, "shape": { "peak": "middle", "mostWithin": 10 } }, "familyBonus": -1, "spouseBonus": -2, "elderBonus": 5, "elderInLawBonus": 5, "neighborBonus": 5, "friendScore": 101,
				"levels": [ { "minimum": 10, "title": "Cold" }, { "minimum": 5, "title": "" } ],
				"playerRelations": [ { "when": { "values": { "gender": "male" } }, "title": " " } ]
			}
			""");

		Assert.That(families.Validate().Concat(relationships.Validate()), Is.EquivalentTo(new[]
		{
			OurResources.DataNumberTooSmall.FormatCurrentCulture("relationships.score.minimum", -5, 0),
			OurResources.DataNumberTooLarge.FormatCurrentCulture("relationships.score.maximum", 120, 100),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("relationships.familyBonus", -1, 0),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("relationships.spouseBonus", -2, 0),
			OurResources.DataNumberTooLarge.FormatCurrentCulture("relationships.friendScore", 101, 100),
			OurResources.RelationshipLevelOutOfOrder.FormatCurrentCulture("relationships.levels[0]", 10),
			OurResources.DataValueMissing.FormatCurrentCulture("relationships.levels[1].title"),
			OurResources.RelationshipLevelOutOfOrder.FormatCurrentCulture("relationships.levels[1]", 5),
			OurResources.DataValueMissing.FormatCurrentCulture("relationships.playerRelations[0].title"),
			OurResources.IntegerRangeInvalid.FormatCurrentCulture("families.head.age", 20, 50),
			OurResources.IntegerRangeInvalid.FormatCurrentCulture("families.wife.ageDifference", -10, 4),
			OurResources.ShapeMostWithinInvalid.FormatCurrentCulture("families.elder.ageGap.mostWithin", 0),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("families.elder.minAge.male", -1, 0),
			OurResources.DataNumberNotPositive.FormatCurrentCulture("families.children.siblingAgeSpread", 0),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("families.parentAge.maxMother", 17, 18),
			OurResources.DataChanceInvalid.FormatCurrentCulture("families.head.femaleChance", 101),
			OurResources.DataChanceInvalid.FormatCurrentCulture("families.elder.chance", -1),
			OurResources.DataChanceInvalid.FormatCurrentCulture("families.elder.infirmChance.age_old", 150),
			OurResources.DataFractionInvalid.FormatCurrentCulture("families.wealth.helper", 1.5),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("families.wealth.fortune.minimum", -10, 0),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("families.wealth.headAge.age_young", -5, 0),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("families.wealth.thresholds.high", 5, 10),
		}));
	}

	[Test]
	public void ProfessionsMustMatchTheRestOfTheData()
	{
		CopyShippedData();
		ModifyShipped("Professions.json", x => x
			.Replace("\"wealth\": \"high-\",\n\t\t\t\t\"maxBusinesses\": 1", "\"wealth\": \"high-\", \"forHouseholdElders\": true, \"helperWhen\": { \"values\": { \"nothing\": [ \"nothing\" ] } }", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Where(x => Path.GetFileName(x.FilePath) == "Professions.json").Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.ProfessionElderTradeNotFromHome.FormatCurrentCulture("profession_smith"),
			OurResources.ConditionPropertyWithoutValues.FormatCurrentCulture("professions[profession_smith].business.helperWhen.values", "nothing"),
		}));
	}

	[Test]
	public void WhoCanOnlySayMemberOrOtherForFamily()
	{
		CopyShippedData();
		ModifyShipped("Professions.json", x => x
			.Replace("\"who\": { \"values\": { \"gender\": [ \"gender_female\" ], \"age\": [ \"age_young\", \"age_adult\" ] }, \"notValues\": { \"family\": [ \"member\" ] } }", "\"who\": { \"values\": { \"family\": [ \"other\" ] }, \"notValues\": { \"family\": [ \"cousin\" ] } }", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		// "other" is fine, and family isn't a property, so a condition only on family has nothing to check against them.
		const string path = "professions[profession_midwife].business.steps[1].steps[0].options[1]";
		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.StepWhoFamilyInvalid.FormatCurrentCulture($"{path}.steps[0].who.notValues.family", "cousin"),
			OurResources.StepWhoFamilyInvalid.FormatCurrentCulture($"{path}.steps[1].who.notValues.family", "cousin"),
		}));
	}

	[Test]
	public void ProfessionPropertyTakesItsValuesFromProfessions()
	{
		CopyShippedData();
		ModifyShipped("Professions.json", x => x
			.Replace("\"title\": \"Blacksmith\",", "\"title\": \"Smith of Smiths\",", StringComparison.Ordinal)
			.Replace("\"id\": \"profession_barber\",", "\"id\": \"profession_barber\", \"player\": false,", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors, Is.Empty);
		var gameData = result.Value!;
		var profession = gameData.CharacterProperties.Get(gameData.Families.Roles.Properties.Profession);
		Assert.That(profession.Values!.Single(x => x.Id == "profession_smith").Title, Is.EqualTo("Smith of Smiths"));
		Assert.That(gameData.Professions.Select(x => x.Id), Does.Not.Contain("profession_homemaker").And.Not.Contain("profession_infirm"));

		// Only businesses the player is allowed are offered to the player.
		var playerOptions = profession.GetOptions(CharacterRole.Player).Select(x => x.Id).ToList();
		Assert.That(playerOptions, Does.Not.Contain("profession_barber").And.Not.Contain("profession_homemaker").And.Contain("profession_smith"));
		Assert.That(profession.GetOptions(CharacterRole.Npc).Select(x => x.Id), Does.Contain("profession_barber"));
	}

	[Test]
	public void ProfessionPropertyCantHaveItsOwnValues()
	{
		CopyShippedData();
		ModifyShipped("Characters.json", x => x
			.Replace("\"role\": \"profession\",", "\"role\": \"profession\", \"values\": [ { \"id\": \"x\", \"title\": \"X\" } ],", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Has.Member(OurResources.ProfessionPropertyValuesNotAllowed.FormatCurrentCulture("profession")));
	}

	[Test]
	public void ProfessionsProvideKeyedDefaults()
	{
		CopyShippedData();
		var original = File.ReadAllText(Path.Combine(m_directory, "Professions.json"));
		ModifyShipped("Professions.json", x => x
			.Replace("\"defaults\": { \"lastName\": [ { \"value\": \"Barber\" } ] },", "\"defaults\": { \"lastName\": [ { \"value\": \"Barber\" } ], \"height\": [ { \"value\": \"Tall\" } ] },", StringComparison.Ordinal));

		var invalid = GameData.Load(m_directory);

		Assert.That(invalid.Errors.Select(x => x.Message), Is.EqualTo(new[] { OurResources.ProfessionDefaultsPropertyInvalid.FormatCurrentCulture("profession_barber", "height", "profession") }));

		File.WriteAllText(Path.Combine(m_directory, "Professions.json"), original);
		var result = GameData.Load(m_directory);

		// A profession without last names of its own uses the other last names.
		Assert.That(result.Errors, Is.Empty);
		var defaults = result.Value!.CharacterProperties.Get(result.Value.Families.Roles.Properties.LastName).GetDefaults(CharacterRole.Npc)!;
		Assert.That(defaults.GetValues("profession_smith")!.Select(x => x.Value), Is.EqualTo(new[] { "Smith" }));
		Assert.That(defaults.GetValues("profession_homemaker"), Is.SameAs(defaults.OtherValues));
	}

	[Test]
	public void ProfessionsCanChangeFrequencyByCondition()
	{
		CopyShippedData();
		ModifyShipped("Professions.json", x => x
			.Replace("\"title\": \"Blacksmith\",", "\"title\": \"Blacksmith\", \"frequencyWhen\": [ { \"when\": { \"values\": { \"age\": [ \"age_veryOld\" ] } }, \"frequency\": \"veryRare\" } ],", StringComparison.Ordinal)
			.Replace("\"title\": \"Barber\",", "\"title\": \"Barber\", \"frequencyWhen\": [ { \"when\": { \"values\": { \"nothing\": [ \"x\" ] } }, \"frequency\": \"rare\" } ],", StringComparison.Ordinal));

		var invalid = GameData.Load(m_directory);

		// Conditions on professions are checked against the character properties.
		Assert.That(invalid.Errors.Select(x => x.Message), Has.Some.Contains("\"nothing\""));

		ModifyShipped("Professions.json", x => x
			.Replace(" \"frequencyWhen\": [ { \"when\": { \"values\": { \"nothing\": [ \"x\" ] } }, \"frequency\": \"rare\" } ],", "", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors, Is.Empty);
		var property = result.Value!.CharacterProperties.Get(result.Value.Families.Roles.Properties.Profession);
		var smith = property.Values!.Single(x => x.Id == "profession_smith");
		Assert.That(smith.GetFrequency(x => x == "age" ? "age_veryOld" : null), Is.EqualTo(new FrequencyLevel(Frequency.VeryRare)));
		Assert.That(smith.GetFrequency(x => x == "age" ? "age_adult" : null), Is.EqualTo(new FrequencyLevel(Frequency.Common)));
		Assert.That(CharacterDependencies.GetDependencyIds(property), Does.Contain("age"));
	}

	[Test]
	public void ConditionsCanNameValueGroups()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		var build = gameData.CharacterProperties.Get("build");
		var fat = build.Values!.Single(x => x.Id == "build_fat");

		// The group stands for every profession in it.
		Assert.That(fat.GetFrequency(x => x == "profession" ? "profession_baker" : null), Is.EqualTo(new FrequencyLevel(Frequency.Uncommon)));
		Assert.That(fat.GetFrequency(x => x == "profession" ? "profession_smith" : null), Is.EqualTo(new FrequencyLevel(Frequency.Rare, 1)));
	}

	[Test]
	public void ValueGroupsMustNotBeValueIds()
	{
		var result = TestData.LoadCharacterData("""
			{
				"templates": [ { "id": "fullName", "template": "{hair}" }, { "id": "description", "template": "{hair}" } ],
				"properties": [
					{ "id": "hair", "title": "Hair", "type": "valueList", "values": [ { "id": "brown", "title": "Brown", "groups": [ "dark", "grey", " " ] }, { "id": "grey", "title": "Grey" } ] }
				]
			}
			""");

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.ValueGroupIsValue.FormatCurrentCulture("properties[hair].values[brown].groups", "grey", "hair"),
			OurResources.DataListItemBlank.FormatCurrentCulture("properties[hair].values[brown].groups"),
		}));
	}

	[Test]
	public void RolesMustBeValid()
	{
		CopyShippedData();
		ModifyShipped("Characters.json", x => x
			.Replace("\"role\": \"age\",", "", StringComparison.Ordinal)
			.Replace("\"role\": \"female\", ", "", StringComparison.Ordinal)
			.Replace("\"role\": \"headName\",", "\"role\": \"nickname\",", StringComparison.Ordinal)
			.Replace("\"id\": \"height\",", "\"id\": \"height\", \"role\": \"married\",", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.SupersetOf(new[]
		{
			OurResources.RoleMissing.FormatCurrentCulture("properties", "age"),
			OurResources.RoleMissing.FormatCurrentCulture("properties[gender].values", "female"),
			OurResources.RoleUnknown.FormatCurrentCulture("properties[headName]", "nickname", string.Join(", ", new[] { "gender", "age", "profession", "lastName", "workRole", "householdRole", "headRelation", "headName", "atWork", "married" }.Select(x => $"\"{x}\""))),
			OurResources.RoleMissing.FormatCurrentCulture("properties", "headName"),
			OurResources.RoleDuplicate.FormatCurrentCulture("properties", "married", "\"married\", \"height\""),
		}));
	}

	[Test]
	public void FamilySettingsMustMatchAgesAndProfessions()
	{
		CopyShippedData();
		ModifyShipped("Families.json", x => x
			.Replace("\"age_veryOld\":", "\"age_ancient\":", StringComparison.Ordinal)
			.Replace("\"minimum\": 20, \"maximum\": 65,", "\"minimum\": 10, \"maximum\": 65,", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.FamilyAgeGroupUnknown.FormatCurrentCulture("families.elder.infirmChance", "age_ancient", "age"),
			OurResources.FamilyHeadAgeOutOfRange.FormatCurrentCulture(10, 65, "age", 18, 80),
		}));
	}

	[Test]
	public void PlayerAgeComesFromHeadAges()
	{
		CopyShippedData();
		ModifyShipped("Characters.json", x => x
			.Replace("\"range\": { \"minimum\": 18, \"maximum\": 80,", "\"player\": { \"range\": { \"minimum\": 36, \"maximum\": 50, \"shape\": { \"peak\": \"low\", \"mostWithin\": 10 } } }, \"range\": { \"minimum\": 18, \"maximum\": 80,", StringComparison.Ordinal));
		ModifyShipped("Families.json", x => x
			.Replace("\"minimum\": 20, \"maximum\": 65,", "\"minimum\": 20, \"maximum\": 30,", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		// The player must be old enough to have an adult child: the youngest age (18) plus the youngest parent age (18).
		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.FamilyAgePlayerRangeNotAllowed.FormatCurrentCulture("age"),
			OurResources.FamilyHeadAgeTooYoung.FormatCurrentCulture(36),
		}));
	}

	[Test]
	public void ShippedPlayerAgesFollowTheHeadAges()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		var age = gameData.CharacterProperties.Get(gameData.Families.Roles.Properties.Age);

		var range = gameData.GetRange(age, CharacterRole.Player);

		Assert.That(range.Minimum, Is.EqualTo(36));
		Assert.That(range.Maximum, Is.EqualTo(gameData.Families.Head.Age.Maximum));
	}

	[Test]
	public void UnknownPropertiesAreErrors()
	{
		CopyShippedData();
		ModifyShipped("Families.json", x => x.Replace("\"helperChance\": 85,", "\"helperChance\": 85, \"helperChanse\": 85,", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors.Single().Message, Does.Contain("helperChanse"));
		Assert.That(Path.GetFileName(result.Errors.Single().FilePath), Is.EqualTo("Families.json"));
	}

	private void CopyShippedData()
	{
		foreach (var path in Directory.GetFiles(JsonDataLoader.DefaultDataDirectory, "*.json"))
			File.Copy(path, Path.Combine(m_directory, Path.GetFileName(path)));
	}

	private void ModifyShipped(string fileName, Func<string, string> modify)
	{
		var path = Path.Combine(m_directory, fileName);
		var text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
		var modified = modify(text);
		Assert.That(modified, Is.Not.EqualTo(text), "The data didn't change.");
		File.WriteAllText(path, modified);
	}

	/// <summary>
	/// Reads JSON as the given type, the same way the data is read.
	/// </summary>
	private static T Read<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, JsonDataLoader.Options)!;

	/// <summary>
	/// Reads the village and location sections from JSON and checks them on their own.
	/// </summary>
	private static IReadOnlyList<string> ValidateLocations(string json)
	{
		var root = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
		IReadOnlyList<T> List<T>(string name) => root.TryGetPropertyValue(name, out var node) ? node.Deserialize<List<T>>(JsonDataLoader.Options)! : [];
		var locations = new LocationList
		{
			Village = root["village"].Deserialize<VillageSettings>(JsonDataLoader.Options)!,
			QuarterKinds = List<QuarterKind>("quarterKinds"),
			Layouts = List<TownLayoutChoice>("layouts"),
			Features = List<MapFeature>("features"),
			Quarters = List<QuarterTemplate>("quarters"),
			Houses = List<HouseTemplate>("houses"),
			Businesses = List<BusinessTemplate>("businesses"),
		};
		return locations.Validate().ToList();
	}

	private string m_directory = "";
}
