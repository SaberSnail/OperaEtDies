using System.IO;
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
		Assert.That(gameData.Professions.Professions.Select(x => x.Id), Is.EqualTo(new[] { "smith", "miller" }));
		Assert.That(gameData.FirstNames.Male, Has.Count.EqualTo(24));
		Assert.That(gameData.FirstNames.Female, Has.Count.EqualTo(24));
		Assert.That(gameData.CharacterOptions.PlayerMinimumAge, Is.EqualTo(36));
		Assert.That(gameData.CharacterOptions.PlayerMaximumAge, Is.EqualTo(60));
		Assert.That(gameData.CharacterOptions.Heights.Select(x => x.Id), Is.EqualTo(new[] { "tall", "medium", "short" }));
		Assert.That(gameData.CharacterOptions.Weights.Select(x => x.Id), Is.EqualTo(new[] { "slim", "medium", "heavy" }));
	}

	[Test]
	public void ShippedNamesAreSplitEvenlyByFrequency()
	{
		var firstNames = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!.FirstNames;

		foreach (var names in new[] { firstNames.Male, firstNames.Female })
		{
			for (var frequency = FrequencyUtility.MinFrequency; frequency <= FrequencyUtility.MaxFrequency; frequency++)
				Assert.That(names.Count(x => x.Frequency == frequency), Is.EqualTo(8));
		}
	}

	[Test]
	public void ReportsErrorsFromEveryFile()
	{
		File.WriteAllText(Path.Combine(m_directory, ProfessionList.FileName), """{ "professions": [] }""");

		var result = GameData.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Select(x => Path.GetFileName(x.FilePath)), Is.EquivalentTo(new[] { ProfessionList.FileName, FirstNameList.FileName, CharacterOptions.FileName }));
	}

	[Test]
	public void InvalidProfessionsReportErrors()
	{
		File.WriteAllText(Path.Combine(m_directory, ProfessionList.FileName), """
			{
				"professions": [
					{ "id": "smith", "title": "Blacksmith", "defaultLastName": "Smith", "frequency": 3 },
					{ "id": "smith", "title": "", "defaultLastName": " ", "frequency": 0 },
					{ "id": "", "title": "Nobody", "defaultLastName": "Nobody", "frequency": 3 },
					null,
				]
			}
			""");

		var result = ProfessionList.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.DataListItemIdDuplicate.Replace("{0}", "smith", StringComparison.Ordinal).Replace("{1}", "professions", StringComparison.Ordinal),
			OurResources.DataListItemTitleMissing.Replace("{0}", "smith", StringComparison.Ordinal).Replace("{1}", "professions", StringComparison.Ordinal),
			OurResources.DataListItemIdMissing.Replace("{0}", "professions", StringComparison.Ordinal),
			OurResources.DataListItemEmpty.Replace("{0}", "professions", StringComparison.Ordinal),
			OurResources.DataListItemFrequencyInvalid.Replace("{0}", "smith", StringComparison.Ordinal).Replace("{1}", "professions", StringComparison.Ordinal).Replace("{2}", "0", StringComparison.Ordinal).Replace("{3}", "1", StringComparison.Ordinal).Replace("{4}", "3", StringComparison.Ordinal),
			OurResources.ProfessionDefaultLastNameMissing.Replace("{0}", "smith", StringComparison.Ordinal),
		}));
	}

	[Test]
	public void InvalidFirstNamesReportErrors()
	{
		File.WriteAllText(Path.Combine(m_directory, FirstNameList.FileName), """
			{
				"male": [ { "name": "John", "frequency": 4 }, { "name": "", "frequency": 1 } ],
				"female": []
			}
			""");

		var result = FirstNameList.Load(m_directory);

		Assert.That(result.Errors, Has.Count.EqualTo(3));
	}

	[Test]
	public void InvalidCharacterOptionsReportErrors()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterOptions.FileName), """
			{
				"playerMinimumAge": 0,
				"playerMaximumAge": -1,
				"heights": [],
				"weights": [ { "id": "slim", "title": "Slim", "frequency": 5 } ]
			}
			""");

		var result = CharacterOptions.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.PlayerMinimumAgeInvalid.Replace("{0}", "0", StringComparison.Ordinal),
			OurResources.PlayerMaximumAgeInvalid.Replace("{0}", "-1", StringComparison.Ordinal).Replace("{1}", "0", StringComparison.Ordinal),
			OurResources.DataListEmpty.Replace("{0}", "heights", StringComparison.Ordinal),
			OurResources.DataListItemFrequencyInvalid.Replace("{0}", "slim", StringComparison.Ordinal).Replace("{1}", "weights", StringComparison.Ordinal).Replace("{2}", "5", StringComparison.Ordinal).Replace("{3}", "1", StringComparison.Ordinal).Replace("{4}", "3", StringComparison.Ordinal),
		}));
	}

	[Test]
	public void MissingMaximumAgeReportsError()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterOptions.FileName), """
			{
				"playerMinimumAge": 36,
				"heights": [ { "id": "tall", "title": "Tall", "frequency": 2 } ],
				"weights": [ { "id": "slim", "title": "Slim", "frequency": 2 } ]
			}
			""");

		var result = CharacterOptions.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors, Has.Count.EqualTo(1));
	}

	private string m_directory = "";
}
