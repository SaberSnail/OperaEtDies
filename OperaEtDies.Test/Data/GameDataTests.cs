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
		Assert.That(gameData.CharacterOptions.Heights.Select(x => x.Id), Is.EqualTo(new[] { "tall", "medium", "short" }));
		Assert.That(gameData.CharacterOptions.Weights.Select(x => x.Id), Is.EqualTo(new[] { "slim", "medium", "heavy" }));
		Assert.That(gameData.CharacterOptions.GetDefaultHeight().Id, Is.EqualTo("medium"));
		Assert.That(gameData.CharacterOptions.GetDefaultWeight().Id, Is.EqualTo("medium"));
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
					{ "id": "smith", "title": "Blacksmith", "defaultLastName": "Smith" },
					{ "id": "smith", "title": "", "defaultLastName": " " },
					{ "id": "", "title": "Nobody", "defaultLastName": "Nobody" },
					null,
				]
			}
			""");

		var result = ProfessionList.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.DataListItemIdDuplicate.Replace("{0}", "smith").Replace("{1}", "professions"),
			OurResources.DataListItemTitleMissing.Replace("{0}", "smith").Replace("{1}", "professions"),
			OurResources.DataListItemIdMissing.Replace("{0}", "professions"),
			OurResources.DataListItemEmpty.Replace("{0}", "professions"),
			OurResources.ProfessionDefaultLastNameMissing.Replace("{0}", "smith"),
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
				"heights": [],
				"defaultHeight": "medium",
				"weights": [ { "id": "slim", "title": "Slim" } ],
				"defaultWeight": "heavy"
			}
			""");

		var result = CharacterOptions.Load(m_directory);

		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.PlayerMinimumAgeInvalid.Replace("{0}", "0"),
			OurResources.DataListEmpty.Replace("{0}", "heights"),
			OurResources.DataDefaultIdNotFound.Replace("{0}", "defaultHeight").Replace("{1}", "medium").Replace("{2}", "heights"),
			OurResources.DataDefaultIdNotFound.Replace("{0}", "defaultWeight").Replace("{1}", "heavy").Replace("{2}", "weights"),
		}));
	}

	[Test]
	public void MissingDefaultHeightReportsError()
	{
		File.WriteAllText(Path.Combine(m_directory, CharacterOptions.FileName), """
			{
				"playerMinimumAge": 36,
				"heights": [ { "id": "tall", "title": "Tall" } ],
				"weights": [ { "id": "slim", "title": "Slim" } ],
				"defaultWeight": "slim"
			}
			""");

		var result = CharacterOptions.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors, Has.Count.EqualTo(1));
	}

	private string m_directory = "";
}
