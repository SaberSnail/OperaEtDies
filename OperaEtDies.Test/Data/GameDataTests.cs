using System.IO;
using GoldenAnvil.Utility;
using OperaEtDies.Data;

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

		Assert.That(gameData.Locations, Is.Not.Empty);
		Assert.That(gameData.Locations.Select(x => x.Id), Has.All.Not.Empty);
		Assert.That(gameData.Locations.Select(x => x.Id), Is.Unique);
		Assert.That(gameData.Locations, Does.Contain(gameData.StartingLocation));
	}

	[Test]
	public void ReportsErrorsFromEveryFile()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterData.FileName), """{ "templates": [], "properties": [] }""");

		var result = GameData.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Select(x => Path.GetFileName(x.FilePath)).Distinct(), Is.EquivalentTo(new[] { CharacterData.FileName, LocationList.FileName }));
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

		Assert.That(gameData.Locations.Get("lane").Connections, Is.EqualTo(new[] { "home", "cottage" }));
		Assert.That(gameData.Locations.Find("nowhere"), Is.Null);
		Assert.That(() => gameData.Locations.Get("nowhere"), Throws.TypeOf<KeyNotFoundException>());
		Assert.That(gameData.CharacterProperties.Get("profession").Title, Is.EqualTo("Profession"));
		Assert.That(gameData.CharacterProperties.Get("height"), Is.SameAs(gameData.CharacterProperties[4]));
		Assert.That(gameData.StartingLocation.Id, Is.EqualTo("home"));
	}

	[Test]
	public void InvalidLocationsReportErrors()
	{
		File.WriteAllText(Path.Combine(m_directory, LocationList.FileName), """
			{
				"startingLocation": "nowhere",
				"locations": [
					{
						"id": "home",
						"title": "Home",
						"shortDescription": "",
						"mediumDescription": " ",
						"longDescription": [],
						"connections": [ "home", "nowhere", "" ]
					},
					{
						"id": "lane",
						"title": "Lane",
						"shortDescription": "Short.",
						"mediumDescription": "Medium.",
						"longDescription": [ "One.", " " ]
					}
				]
			}
			""");

		var result = LocationList.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.LocationDescriptionMissing.Replace("{0}", "home", StringComparison.Ordinal).Replace("{1}", "shortDescription", StringComparison.Ordinal),
			OurResources.LocationDescriptionMissing.Replace("{0}", "home", StringComparison.Ordinal).Replace("{1}", "mediumDescription", StringComparison.Ordinal),
			OurResources.LocationDescriptionMissing.Replace("{0}", "home", StringComparison.Ordinal).Replace("{1}", "longDescription", StringComparison.Ordinal),
			OurResources.LocationConnectionToSelf.Replace("{0}", "home", StringComparison.Ordinal),
			OurResources.LocationConnectionUnknown.Replace("{0}", "home", StringComparison.Ordinal).Replace("{1}", "nowhere", StringComparison.Ordinal),
			OurResources.LocationConnectionBlank.Replace("{0}", "home", StringComparison.Ordinal),
			OurResources.LocationDescriptionParagraphBlank.Replace("{0}", "lane", StringComparison.Ordinal),
			OurResources.StartingLocationUnknown.Replace("{0}", "nowhere", StringComparison.Ordinal),
		}));
	}

	private string m_directory = "";
}
