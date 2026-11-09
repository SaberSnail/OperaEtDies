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
		Assert.That(gameData.CharacterProperties.Select(x => x.Id), Is.EqualTo(new[] { "gender", "profession", "firstName", "lastName", "height", "weight", "age" }));
		Assert.That(gameData.CharacterProperties.Get("profession").Values!.Select(x => x.Id), Is.EqualTo(new[] { "smith", "miller" }));
		Assert.That(gameData.CharacterProperties.Get("age").GetRange(CharacterRole.Player).Minimum, Is.EqualTo(36));
		Assert.That(gameData.CharacterProperties.Get("age").GetRange(CharacterRole.Npc).Minimum, Is.EqualTo(16));
		Assert.That(gameData.StartingLocation.Id, Is.EqualTo("player-house"));
		Assert.That(gameData.Locations.Select(x => x.Id), Is.EqualTo(new[] { "player-house", "well-lane", "thatched-cottage", "timber-house" }));
	}

	[Test]
	public void ShippedNamesAreSplitEvenlyByFrequency()
	{
		var firstNames = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!.CharacterProperties.Get("firstName").Defaults!.ValuesByKey!;

		Assert.That(firstNames.Keys, Is.EquivalentTo(new[] { "male", "female" }));
		foreach (var names in firstNames.Values)
		{
			for (var frequency = FrequencyUtility.MinFrequency; frequency <= FrequencyUtility.MaxFrequency; frequency++)
				Assert.That(names.Count(x => x.Frequency == frequency), Is.EqualTo(8));
		}
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
							{ "id": "male", "title": "Male", "frequency": 3, "text": { "pronoun": "He" } },
							{ "id": "female", "title": "Female", "frequency": 5, "text": { "pronoun": " " } }
						],
						"range": { "minimum": 1, "maximum": 2, "shape": "low" },
						"player": { "values": [ "male", "other", "male" ] }
					},
					{ "id": "age", "title": "Age", "type": "integer", "range": { "minimum": 10, "maximum": 5, "shape": "middle" }, "player": { "defaults": { "values": [] } } },
					{ "id": "height", "title": "Height", "type": "integer", "npc": { "range": { "minimum": 1, "maximum": 5, "shape": "high" } } },
					{
						"id": "firstName",
						"title": "First Name",
						"type": "string",
						"defaults": { "keyedBy": "gender", "valuesByKey": { "male": [ { "value": "John", "frequency": 3 } ], "other": [ { "value": "", "frequency": 3 } ] } }
					},
					{ "id": "lastName", "title": "Last Name", "type": "string", "defaults": { "keyedBy": "nickname", "valuesByKey": {} } },
					{ "id": "nickname", "title": "Nickname", "type": "valueList" },
					{ "id": "motto", "title": "Motto", "type": "string", "defaults": { "keyedBy": "gender", "values": [ { "value": "Hi", "frequency": 3 } ] } }
				]
			}
			""");

		var result = CharacterData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.DataListItemFrequencyInvalid.FormatCurrentCulture("female", "gender.values", 5, 1, 3),
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
			OurResources.StringDefaultsKeyedByInvalid.FormatCurrentCulture("lastName.defaults", "nickname"),
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
					{ "id": "gender", "title": "Gender", "type": "valueList", "values": [ { "id": "male", "title": "Male", "frequency": 3, "text": { "pronoun": "he" } } ] },
					{ "id": "age", "title": "Age", "type": "integer", "range": { "minimum": 1, "maximum": 5, "shape": "low" } }
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
				"properties": [ { "id": "age", "title": "Age", "type": "integer", "range": { "minimum": 1, "maximum": 5, "shape": "low" } } ]
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
		var templates = new TextTemplateSet(new Dictionary<string, TextTemplate>
		{
			["greeting"] = TextTemplate.Parse("{name:first_upper} says hi."),
			["name"] = TextTemplate.Parse("{title} {first}"),
		});

		Assert.That(templates.Format("greeting", x => x.Id == "title" ? "sir" : "Hugh"), Is.EqualTo("Sir Hugh says hi."));
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
						"values": [ { "id": "male", "title": "Male", "frequency": 3, "text": { "pronoun": "He" } }, { "id": "female", "title": "Female", "frequency": 3 } ]
					}
				]
			}
			""");

		var result = CharacterData.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EqualTo(new[] { OurResources.TemplateFieldMissing.FormatCurrentCulture("fullName", "gender", "pronoun", "female") }));
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
	public void ShippedLocationsHaveThreeParagraphLongDescriptions()
	{
		var locations = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!.Locations;

		foreach (var location in locations)
			Assert.That(location.LongDescription, Has.Count.EqualTo(3), location.Id);
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
