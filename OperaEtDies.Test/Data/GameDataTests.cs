using System.IO;
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
		Assert.That(gameData.Professions.Select(x => x.Id).Append(families.Homemaker).Append(families.Infirm), Is.EquivalentTo(gameData.CharacterProperties.Get(families.Properties.Profession).Values!.Select(x => x.Id)));
		Assert.That(gameData.BusinessTemplates, Is.Not.Empty);
		Assert.That(gameData.QuarterTemplates.Select(x => x.Id).Concat(gameData.HouseTemplates.Select(x => x.Id)).Concat(gameData.BusinessTemplates.Select(x => x.Id)), Is.Unique);
		foreach (var profession in gameData.Professions)
			Assert.That(gameData.GetBusinessTemplates(profession.Id), profession.WorksFromHome ? Is.Empty : Is.Not.Empty, profession.Id);
	}

	[Test]
	public void ReportsErrorsFromEveryFile()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """{ "templates": [], "properties": [] }""");

		var result = GameData.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Select(x => Path.GetFileName(x.FilePath)).Distinct(), Is.EquivalentTo(new[] { CharacterData.FileName, LocationList.FileName, ProfessionList.FileName, FamilySettings.FileName }));
	}

	[Test]
	public void InvalidCharacterPropertiesReportErrors()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
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

		var result = CharacterData.Load(m_directory);

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
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
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

		var result = CharacterData.Load(m_directory);

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
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
			{
				"templates": [ { "id": "other", "template": "{age}" } ],
				"properties": [ { "id": "age", "title": "Age", "type": "integer", "range": { "minimum": 1, "maximum": 5, "shape": { "peak": "low", "mostWithin": 2 } } } ]
			}
			""");

		var result = CharacterData.Load(m_directory);

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
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
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

		var result = CharacterData.Load(m_directory);

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
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
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

		var result = CharacterData.Load(m_directory);

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
	public void TemplateTextMustExistOnEveryValue()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
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

		var result = CharacterData.Load(m_directory);

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
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
			{
				"templates": [
					{ "id": "fullName", "template": "{age}" },
					{ "id": "description", "variants": [ { "when": { "hidden": [ "nobody", "age", "age" ] }, "template": "" }, { "when": { "hidden": [] }, "template": "{age}" } ] }
				],
				"properties": [
					{ "id": "gender", "title": "Gender", "type": "valueList", "values": [ { "id": "male", "title": "Male", "prominence": 0 } ] },
					{ "id": "age", "title": "Age", "type": "integer", "range": { "minimum": 1, "maximum": 5, "shape": { "peak": "low", "mostWithin": 2 } } }
				],
				"obscurers": [
					{ "id": "mask", "hides": { "nobody": 2, "age": 4, "*": 1 } },
					{ "id": "mask", "hides": {} },
					{ "id": "", "hides": { "age": 1 } }
				]
			}
			""");

		var result = CharacterData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.ProminenceInvalid.FormatCurrentCulture("male", "gender.values", 0, 1, 3),
			OurResources.ObscurerPropertyUnknown.FormatCurrentCulture("mask", "nobody"),
			OurResources.ObscurerStrengthInvalid.FormatCurrentCulture("mask", "age", 4, 1, 3),
			OurResources.DataListItemIdDuplicate.FormatCurrentCulture("mask", "obscurers"),
			OurResources.DataListEmpty.FormatCurrentCulture("mask.hides"),
			OurResources.DataListItemIdMissing.FormatCurrentCulture("obscurers"),
			OurResources.TemplateHiddenPropertyUnknown.FormatCurrentCulture("description.variants[0].when.hidden", "nobody"),
			OurResources.DataListItemIdDuplicate.FormatCurrentCulture("age", "description.variants[0].when.hidden"),
			OurResources.DataListEmpty.FormatCurrentCulture("description.variants[1].when.hidden"),
		}));
	}

	[Test]
	public void TemplatesMustHandleObscuredValues()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
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
						"type": "valueList",
						"values": [ { "id": "full", "title": "Full" }, { "id": "stubble", "title": "Stubble", "prominence": 1 } ]
					},
					{ "id": "hat", "title": "Hat", "type": "valueList", "values": [ { "id": "tall", "title": "Tall", "prominence": 3 } ] }
				],
				"obscurers": [ { "id": "mask", "hides": { "beard": 1, "hat": 2 } } ]
			}
			""");

		var result = CharacterData.Load(m_directory);

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
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
			{ "templates": [ { "id": "fullName", "template": "{a}" }, { "id": "description", "template": "{a}" } ], "properties": [ { "id": "a", "title": "A", "type": "boolean" } ] }
			""");

		var result = CharacterData.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors, Has.Count.EqualTo(1));
	}

	[TestCase("plain", "plain")]
	[TestCase("{a} and {b.c}", "a and b.c")]
	[TestCase("{{literal}} {a}", "{literal} a")]
	[TestCase("{b.c:first_upper} {a}", "B.c a")]
	[TestCase("{x:first_upper}", "X")]
	public void TemplatesFillInPlaceholders(string text, string expected)
	{
		Assert.That(TextTemplate.TryParse(text, out var template), Is.True);
		Assert.That(template!.Format(x => x.Id + (x.Field is null ? "" : "." + x.Field)), Is.EqualTo(expected));
	}

	[TestCase("{")]
	[TestCase("}")]
	[TestCase("{}")]
	[TestCase("{a.}")]
	[TestCase("{a.b.c}")]
	[TestCase("{a{b}")]
	[TestCase("{a:}")]
	[TestCase("{:first_upper}")]
	[TestCase("{a:b:c}")]
	public void MalformedTemplatesFailToParse(string text)
	{
		Assert.That(TextTemplate.TryParse(text, out _), Is.False);
	}

	[Test]
	public void CatalogsLookUpItemsById()
	{
		var gameData = TestData.CreateGameData();

		Assert.That(gameData.CharacterProperties.Get("profession").Title, Is.EqualTo("Profession"));
		Assert.That(gameData.CharacterProperties.Get("height"), Is.SameAs(gameData.CharacterProperties[6]));
		Assert.That(gameData.QuarterTemplates.Get("square").Businesses, Is.EqualTo(3));
		Assert.That(gameData.HouseTemplates.Get("hall").Wealth, Is.EqualTo(WealthLevel.High));
		Assert.That(gameData.Professions.Get("smith").MaxBusinesses, Is.EqualTo(1));
		Assert.That(gameData.Professions.Find("nowhere"), Is.Null);
		Assert.That(() => gameData.Professions.Get("nowhere"), Throws.TypeOf<KeyNotFoundException>());
	}

	[Test]
	public void InvalidLocationsReportErrors()
	{
		File.WriteAllText(Path.Combine(m_directory, LocationList.FileName), """
			{
				"quarterKinds": [
					{ "id": "lane", "area": "village", "path": "lane" },
					{ "id": "lane", "area": "village", "path": "lane" },
					{ "id": "plaza", "area": "village", "unique": true, "path": " " }
				],
				"landmarkCount": { "minimum": -1, "maximum": 2, "shape": { "peak": "low", "mostWithin": 1 } },
				"layouts": [ { "shape": "radial", "center": "lane" } ],
				"houseTitle": "The {lastName Home",
				"quarters": [
					{
						"id": "lane",
						"title": "Lane",
						"shortDescription": "",
						"mediumDescription": " ",
						"longDescription": [],
						"kind": "nowhere",
						"houses": { "minimum": 3, "maximum": 2 }
					},
					{
						"id": "square",
						"title": "Square",
						"shortDescription": "Short.",
						"mediumDescription": "Medium.",
						"longDescription": [ "One.", " " ],
						"kind": "lane",
						"size": { "length": 0, "width": 1 },
						"wealth": "veryLow"
					}
				],
				"houses": [
					{ "id": "square", "title": "House", "shortDescription": "Short.", "mediumDescription": "Medium.", "longDescription": [ "One." ], "wealth": "low" }
				],
				"businesses": [
					{ "id": "square", "title": "Shop", "shortDescription": "Short.", "mediumDescription": "Medium.", "longDescription": [ "One." ], "profession": "smith", "quarterKinds": [ "lane" ] },
					{ "id": "forge", "title": "", "shortDescription": "Short.", "mediumDescription": "", "longDescription": [ "One." ], "profession": " ", "quarterKinds": [] },
					{ "id": "smithy", "title": "Smithy", "shortDescription": "Short.", "mediumDescription": "Medium.", "longDescription": [ "One." ], "profession": "smith", "quarterKinds": [ "nowhere" ] }
				]
			}
			""");

		var result = LocationList.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.DataListItemIdDuplicate.FormatCurrentCulture("lane", "quarterKinds"),
			OurResources.DataValueMissing.FormatCurrentCulture("quarterKinds[plaza].path"),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("landmarkCount.minimum", -1, 0),
			OurResources.LayoutCenterInvalid.FormatCurrentCulture("radial", "lane"),
			OurResources.QuarterKindUniqueMissing.FormatCurrentCulture("plaza"),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("quarters[square].size.length", 0, 1),
			OurResources.LocationDescriptionMissing.FormatCurrentCulture("lane", "shortDescription"),
			OurResources.LocationDescriptionMissing.FormatCurrentCulture("lane", "mediumDescription"),
			OurResources.LocationDescriptionMissing.FormatCurrentCulture("lane", "longDescription"),
			OurResources.QuarterKindUnknown.FormatCurrentCulture("lane", "nowhere"),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("quarters[lane].houses.maximum", 2, 3),
			OurResources.QuarterHousesWithoutWealth.FormatCurrentCulture("lane"),
			OurResources.LocationDescriptionParagraphBlank.FormatCurrentCulture("square"),
			OurResources.QuarterWealthWithoutHouses.FormatCurrentCulture("square"),
			OurResources.WealthLevelNotAllowed.FormatCurrentCulture("quarters[square].wealth", "veryLow"),
			OurResources.HouseWealthMissing.FormatCurrentCulture("medium"),
			OurResources.HouseWealthMissing.FormatCurrentCulture("high"),
			OurResources.QuarterWealthMissing.FormatCurrentCulture("low"),
			OurResources.QuarterWealthMissing.FormatCurrentCulture("medium"),
			OurResources.QuarterWealthMissing.FormatCurrentCulture("high"),
			OurResources.BusinessQuarterMissing.FormatCurrentCulture("square"),
			OurResources.DataListItemTitleMissing.FormatCurrentCulture("forge", "businesses"),
			OurResources.LocationDescriptionMissing.FormatCurrentCulture("forge", "mediumDescription"),
			OurResources.DataValueMissing.FormatCurrentCulture("businesses[forge].profession"),
			OurResources.DataListEmpty.FormatCurrentCulture("businesses[forge].quarterKinds"),
			OurResources.QuarterKindUnknown.FormatCurrentCulture("smithy", "nowhere"),
			OurResources.LocationIdDuplicate.FormatCurrentCulture("square"),
			OurResources.TemplateMalformed.FormatCurrentCulture("houseTitle"),
			OurResources.HouseTitleNotAllowed.FormatCurrentCulture("square"),
		}));
	}

	[Test]
	public void InvalidFeaturesReportErrors()
	{
		CopyShippedFiles(LocationList.FileName);
		var path = Path.Combine(m_directory, LocationList.FileName);
		File.WriteAllText(path, File.ReadAllText(path)
			.Replace("\"crossings\": [ \"bridge\", \"ford\" ],", "\"size\": { \"length\": 2, \"width\": 1 },", StringComparison.Ordinal)
			.Replace("\"borders\": \"river\"", "\"borders\": \"lake\"", StringComparison.Ordinal)
			.Replace("\"To the {direction}, a reedy pond reflects the sky.\"", "\"To the {place}, a reedy pond reflects the sky.\"", StringComparison.Ordinal)
			.Replace("\"id\": \"square\", \"area\": \"village\", \"unique\": true,", "\"id\": \"square\", \"area\": \"village\", \"unique\": true, \"borders\": \"hill\",", StringComparison.Ordinal));

		var result = LocationList.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.FeatureSettingMissing.FormatCurrentCulture("river", "crossings", "river"),
			OurResources.FeatureSettingNotAllowed.FormatCurrentCulture("river", "size", "river"),
			OurResources.FeatureTextInvalid.FormatCurrentCulture("features[pond].text", "To the {place}, a reedy pond reflects the sky."),
			OurResources.FeatureUnknown.FormatCurrentCulture("quarterKinds[riverside].borders", "lake"),
			OurResources.QuarterKindUniqueBorders.FormatCurrentCulture("square"),
		}));
	}

	[Test]
	public void InvalidProfessionsReportErrors()
	{
		File.WriteAllText(Path.Combine(m_directory, ProfessionList.FileName), """
			{
				"professions": [
					{ "id": "smith", "title": "Smith", "wealth": "high", "maxBusinesses": 0, "helpers": -1, "forHouseholdElders": true },
					{ "id": "smith", "title": "Smith", "wealth": "low", "worksFromHome": true }
				]
			}
			""");

		var result = ProfessionList.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.DataNumberTooSmall.FormatCurrentCulture("professions[smith].maxBusinesses", 0, 1),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("professions[smith].helpers", -1, 0),
			OurResources.ProfessionElderTradeNotFromHome.FormatCurrentCulture("smith"),
			OurResources.DataListItemIdDuplicate.FormatCurrentCulture("smith", "professions"),
		}));
	}

	[Test]
	public void InvalidFamilySettingsReportErrors()
	{
		File.WriteAllText(Path.Combine(m_directory, FamilySettings.FileName), """
			{
				"properties": { "gender": "gender", "age": "", "profession": "profession", "lastName": "lastName", "workRole": "", "householdRole": "" },
				"male": "male",
				"female": " ",
				"homemaker": "",
				"infirm": "infirm",
				"independent": "workRole_independent",
				"helper": " ",
				"head": "householdRole_head",
				"member": "",
				"familyCount": { "minimum": -1, "maximum": 5, "shape": { "peak": "middle", "mostWithin": 1 } },
				"femaleHeadChance": 101,
				"femaleHeadMinWealth": "low",
				"headAge": { "minimum": 50, "maximum": 20, "shape": { "peak": "middle", "mostWithin": 9 } },
				"wifeChance": 80,
				"wifeAgeDifference": { "minimum": 4, "maximum": -10, "shape": { "peak": "middle", "mostWithin": 3 } },
				"householdElderChance": -1,
				"femaleHouseholdElderChance": 70,
				"householdElderAgeGap": { "peak": "low", "mostWithin": 0 },
				"householdElderMinAge": { "male": -1, "female": 50 },
				"infirmChance": { "age_old": 150 },
				"householdElderHomemakerChance": 50,
				"minParentAge": 18,
				"maxMotherAge": 17,
				"maxFatherAge": 55,
				"childCount": { "minimum": 0, "maximum": 2, "shape": { "peak": "middle", "mostWithin": 1 } },
				"childAge": { "peak": "middle", "mostWithin": 5 },
				"siblingAgeSpread": 0,
				"sonChance": 5,
				"childHomemakerChance": 50,
				"helperChance": 80,
				"helperWealth": 1.5,
				"helperWealthFalloff": 0.7,
				"homemakerWealth": 0.5,
				"infirmWealth": -0.5,
				"householdFortune": { "minimum": -10, "maximum": 140, "shape": { "peak": "middle", "mostWithin": 12 } },
				"headAgeWealth": { "age_young": -5 },
				"wealthThresholds": { "medium": 10, "high": 5 }
			}
			""");

		var result = FamilySettings.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.DataValueMissing.FormatCurrentCulture("properties.age"),
			OurResources.DataValueMissing.FormatCurrentCulture("female"),
			OurResources.DataValueMissing.FormatCurrentCulture("homemaker"),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("familyCount.minimum", -1, 0),
			OurResources.IntegerRangeInvalid.FormatCurrentCulture("headAge", 20, 50),
			OurResources.IntegerRangeInvalid.FormatCurrentCulture("wifeAgeDifference", -10, 4),
			OurResources.ShapeMostWithinInvalid.FormatCurrentCulture("householdElderAgeGap.mostWithin", 0),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("householdElderMinAge.male", -1, 0),
			OurResources.DataNumberNotPositive.FormatCurrentCulture("siblingAgeSpread", 0),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("maxMotherAge", 17, 18),
			OurResources.DataChanceInvalid.FormatCurrentCulture("femaleHeadChance", 101),
			OurResources.DataChanceInvalid.FormatCurrentCulture("householdElderChance", -1),
			OurResources.DataChanceInvalid.FormatCurrentCulture("infirmChance.age_old", 150),
			OurResources.DataFractionInvalid.FormatCurrentCulture("helperWealth", 1.5),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("householdFortune.minimum", -10, 0),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("headAgeWealth.age_young", -5, 0),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("wealthThresholds.high", 5, 10),
			OurResources.DataValueMissing.FormatCurrentCulture("properties.workRole"),
			OurResources.DataValueMissing.FormatCurrentCulture("helper"),
			OurResources.DataValueMissing.FormatCurrentCulture("member"),
			OurResources.DataValueMissing.FormatCurrentCulture("properties.householdRole"),
		}));
	}

	[Test]
	public void ProfessionsMustMatchTheOtherFiles()
	{
		CopyShippedFiles(CharacterData.FileName, LocationList.FileName, ProfessionList.FileName, FamilySettings.FileName);
		var professionsPath = Path.Combine(m_directory, ProfessionList.FileName);
		File.WriteAllText(professionsPath, File.ReadAllText(professionsPath)
			.Replace("\"wealth\": \"high-\",\n\t\t\t\"maxBusinesses\": 1,\n\t\t\t\"helperWhen\": { \"values\": { \"gender\": [ \"gender_male\", \"gender_female\" ] } }", "\"wealth\": \"high-\", \"worksFromHome\": true, \"helperWhen\": { \"values\": { \"nothing\": [ \"nothing\" ] } }", StringComparison.Ordinal)
			.Replace("\"defaults\": { \"lastName\": [ { \"value\": \"Barber\" } ] },\n\t\t\t\"wealth\": \"medium\",", "\"defaults\": { \"lastName\": [ { \"value\": \"Barber\" } ] },", StringComparison.Ordinal)
			.Replace("\"id\": \"profession_homemaker\",", "\"id\": \"profession_homemaker\", \"wealth\": \"low\",", StringComparison.Ordinal)
			.Replace("\"wealth\": \"veryLow\",\n\t\t\t\"worksFromHome\": true", "\"wealth\": \"veryLow\"", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Where(x => Path.GetFileName(x.FilePath) == ProfessionList.FileName).Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.ProfessionLocationsMissing.FormatCurrentCulture("profession_spinster"),
			OurResources.ProfessionLocationsMissing.FormatCurrentCulture("profession_dairymaid"),
			OurResources.ConditionPropertyWithoutValues.FormatCurrentCulture("professions[profession_smith].helperWhen.values", "nothing"),
			OurResources.ProfessionWealthMissing.FormatCurrentCulture("profession_barber"),
			OurResources.ProfessionNotBusiness.FormatCurrentCulture("profession_homemaker", "homemaker"),
		}));
		Assert.That(result.Errors.Where(x => Path.GetFileName(x.FilePath) == LocationList.FileName).Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.BusinessProfessionWorksFromHome.FormatCurrentCulture("profession_smith_forge", "profession_smith"),
			OurResources.BusinessProfessionWorksFromHome.FormatCurrentCulture("profession_smith_smithy", "profession_smith"),
		}));
	}

	[Test]
	public void ProfessionPropertyTakesItsValuesFromProfessions()
	{
		CopyShippedFiles(CharacterData.FileName, LocationList.FileName, ProfessionList.FileName, FamilySettings.FileName);
		var professionsPath = Path.Combine(m_directory, ProfessionList.FileName);
		File.WriteAllText(professionsPath, File.ReadAllText(professionsPath)
			.Replace("\"title\": \"Blacksmith\",", "\"title\": \"Smith of Smiths\",", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors, Is.Empty);
		var profession = result.Value!.CharacterProperties.Get(result.Value.Families.Properties.Profession);
		Assert.That(profession.Values!.Select(x => x.Id), Is.EqualTo(ProfessionList.Load(m_directory).Value!.Professions.Select(x => x.Id)));
		Assert.That(profession.Values!.Single(x => x.Id == "profession_smith").Title, Is.EqualTo("Smith of Smiths"));
		Assert.That(result.Value.Professions.Select(x => x.Id), Does.Not.Contain("profession_homemaker").And.Not.Contain("profession_infirm"));
	}

	[Test]
	public void ProfessionsProvideKeyedDefaults()
	{
		CopyShippedFiles(CharacterData.FileName, LocationList.FileName, ProfessionList.FileName, FamilySettings.FileName);
		var professionsPath = Path.Combine(m_directory, ProfessionList.FileName);
		var original = File.ReadAllText(professionsPath);
		File.WriteAllText(professionsPath, original
			.Replace("\"defaults\": { \"lastName\": [ { \"value\": \"Barber\" } ] },", "\"defaults\": { \"lastName\": [ { \"value\": \"Barber\" } ], \"height\": [ { \"value\": \"Tall\" } ] },", StringComparison.Ordinal));

		var invalid = GameData.Load(m_directory);

		Assert.That(invalid.Errors.Select(x => x.Message), Is.EqualTo(new[] { OurResources.ProfessionDefaultsPropertyInvalid.FormatCurrentCulture("profession_barber", "height", "profession") }));

		File.WriteAllText(professionsPath, original);
		var result = GameData.Load(m_directory);

		// A profession without last names of its own uses the other last names.
		Assert.That(result.Errors, Is.Empty);
		var defaults = result.Value!.CharacterProperties.Get(result.Value.Families.Properties.LastName).GetDefaults(CharacterRole.Npc)!;
		Assert.That(defaults.GetValues("profession_smith")!.Select(x => x.Value), Is.EqualTo(new[] { "Smith" }));
		Assert.That(defaults.GetValues("profession_homemaker"), Is.SameAs(defaults.OtherValues));
	}

	[Test]
	public void ProfessionsCanChangeFrequencyByCondition()
	{
		CopyShippedFiles(CharacterData.FileName, LocationList.FileName, ProfessionList.FileName, FamilySettings.FileName);
		var professionsPath = Path.Combine(m_directory, ProfessionList.FileName);
		File.WriteAllText(professionsPath, File.ReadAllText(professionsPath)
			.Replace("\"title\": \"Blacksmith\",", "\"title\": \"Blacksmith\", \"frequencyWhen\": [ { \"when\": { \"values\": { \"age\": [ \"age_veryOld\" ] } }, \"frequency\": \"veryRare\" } ],", StringComparison.Ordinal)
			.Replace("\"title\": \"Barber\",", "\"title\": \"Barber\", \"frequencyWhen\": [ { \"when\": { \"values\": { \"nothing\": [ \"x\" ] } }, \"frequency\": \"rare\" } ],", StringComparison.Ordinal));

		var invalid = GameData.Load(m_directory);

		// Conditions on professions are checked against the character properties.
		Assert.That(invalid.Errors.Select(x => x.Message), Has.Some.Contains("\"nothing\""));

		File.WriteAllText(professionsPath, File.ReadAllText(professionsPath)
			.Replace(" \"frequencyWhen\": [ { \"when\": { \"values\": { \"nothing\": [ \"x\" ] } }, \"frequency\": \"rare\" } ],", "", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors, Is.Empty);
		var property = result.Value!.CharacterProperties.Get(result.Value.Families.Properties.Profession);
		var smith = property.Values!.Single(x => x.Id == "profession_smith");
		Assert.That(smith.GetFrequency(x => x == "age" ? "age_veryOld" : null), Is.EqualTo(new FrequencyLevel(Frequency.VeryRare)));
		Assert.That(smith.GetFrequency(x => x == "age" ? "age_adult" : null), Is.EqualTo(new FrequencyLevel(Frequency.Common)));
		Assert.That(CharacterDependencies.GetDependencyIds(property), Does.Contain("age"));
	}

	[Test]
	public void InvalidValuesFromReportsErrors()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """
			{
				"templates": [ { "id": "fullName", "template": "{a}" }, { "id": "description", "template": "{a}" } ],
				"properties": [
					{ "id": "a", "title": "A", "type": "valueList", "valuesFrom": "professions" },
					{ "id": "b", "title": "B", "type": "valueList", "valuesFrom": "planets" },
					{ "id": "c", "title": "C", "type": "valueList", "valuesFrom": "professions", "values": [ { "id": "c1", "title": "C1" } ] },
					{ "id": "d", "title": "D", "type": "string", "valuesFrom": "professions" }
				]
			}
			""");

		var professions = new[] { new Profession { Id = "p1", Title = "P1" } };
		var result = CharacterData.Load(m_directory, [], professions);
		var withoutProfessions = CharacterData.Load(m_directory, [], null);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.ValuesFromUnknown.FormatCurrentCulture("b", "planets", "professions"),
			OurResources.ValuesAndValuesFrom.FormatCurrentCulture("c"),
			OurResources.CharacterSettingNotAllowed.FormatCurrentCulture("d", "valuesFrom"),
		}));
		Assert.That(withoutProfessions.Errors.Select(x => x.Message), Does.Contain(OurResources.ValuesFromNotLoaded.FormatCurrentCulture("a", "professions")));
	}

	[Test]
	public void FamilySettingsMustMatchCharacters()
	{
		CopyShippedFiles(CharacterData.FileName, LocationList.FileName, ProfessionList.FileName, FamilySettings.FileName);
		var familiesPath = Path.Combine(m_directory, FamilySettings.FileName);
		File.WriteAllText(familiesPath, File.ReadAllText(familiesPath)
			.Replace("\"age\": \"age\"", "\"age\": \"firstName\"", StringComparison.Ordinal)
			.Replace("\"female\": \"gender_female\"", "\"female\": \"gender_other\"", StringComparison.Ordinal)
			.Replace("\"helper\": \"workRole_helper\"", "\"helper\": \"workRole_nothing\"", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.FamilyPropertyInvalid.FormatCurrentCulture("properties.age", "firstName", "integer"),
			OurResources.FamilyValueUnknown.FormatCurrentCulture("female", "gender_other", "gender"),
			OurResources.FamilyValueUnknown.FormatCurrentCulture("helper", "workRole_nothing", "workRole"),
		}));
	}

	[Test]
	public void FamilySettingsMustMatchAgesAndProfessions()
	{
		CopyShippedFiles(CharacterData.FileName, LocationList.FileName, ProfessionList.FileName, FamilySettings.FileName);
		var familiesPath = Path.Combine(m_directory, FamilySettings.FileName);
		File.WriteAllText(familiesPath, File.ReadAllText(familiesPath)
			.Replace("\"infirm\": \"profession_infirm\"", "\"infirm\": \"profession_nothing\"", StringComparison.Ordinal)
			.Replace("\"age_veryOld\":", "\"age_ancient\":", StringComparison.Ordinal)
			.Replace("\"headAge\": { \"minimum\": 20,", "\"headAge\": { \"minimum\": 10,", StringComparison.Ordinal));

		var result = GameData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.FamilyValueUnknown.FormatCurrentCulture("infirm", "profession_nothing", "profession"),
			OurResources.FamilyAgeGroupUnknown.FormatCurrentCulture("infirmChance", "age_ancient", "age"),
			OurResources.FamilyHeadAgeOutOfRange.FormatCurrentCulture(10, 65, "age", 18, 80),
			// With a different infirm value, the infirm profession counts as a business.
			OurResources.ProfessionWealthMissing.FormatCurrentCulture("profession_infirm"),
			OurResources.ProfessionLocationsMissing.FormatCurrentCulture("profession_infirm"),
		}));
	}

	[Test]
	public void PlayerAgeComesFromHeadAges()
	{
		CopyShippedFiles(CharacterData.FileName, LocationList.FileName, ProfessionList.FileName, FamilySettings.FileName);
		var charactersPath = Path.Combine(m_directory, CharacterData.FileName);
		File.WriteAllText(charactersPath, File.ReadAllText(charactersPath)
			.Replace("\"range\": { \"minimum\": 18, \"maximum\": 80,", "\"player\": { \"range\": { \"minimum\": 36, \"maximum\": 50, \"shape\": { \"peak\": \"low\", \"mostWithin\": 10 } } }, \"range\": { \"minimum\": 18, \"maximum\": 80,", StringComparison.Ordinal));
		var familiesPath = Path.Combine(m_directory, FamilySettings.FileName);
		File.WriteAllText(familiesPath, File.ReadAllText(familiesPath)
			.Replace("\"headAge\": { \"minimum\": 20, \"maximum\": 65,", "\"headAge\": { \"minimum\": 20, \"maximum\": 30,", StringComparison.Ordinal));

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
		var age = gameData.CharacterProperties.Get(gameData.Families.Properties.Age);

		var range = gameData.GetRange(age, CharacterRole.Player);

		Assert.That(range.Minimum, Is.EqualTo(36));
		Assert.That(range.Maximum, Is.EqualTo(gameData.Families.HeadAge.Maximum));
	}

	private void CopyShippedFiles(params string[] fileNames)
	{
		foreach (var fileName in fileNames)
			File.Copy(Path.Combine(JsonDataLoader.DefaultDataDirectory, fileName), Path.Combine(m_directory, fileName));
	}

	private string m_directory = "";
}
