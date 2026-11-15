using System.IO;
using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class ModLoadingTests
{
	[SetUp]
	public void SetUp()
	{
		m_directory = Path.Combine(Path.GetTempPath(), $"OperaEtDiesTest_{Guid.NewGuid():N}");
		m_dataDirectory = Path.Combine(m_directory, "Data");
		Directory.CreateDirectory(m_dataDirectory);
		File.WriteAllText(Path.Combine(m_dataDirectory, "Trades.json"), """
			{
				// Comments are allowed.
				"professions": [
					{ "id": "smith", "title": "Smith", "business": { "wealth": "high", "worksFromHome": true } },
				],
			}
			""");
		File.WriteAllText(Path.Combine(m_dataDirectory, "Game.json"), """{ "game": { "title": "Title", "description": { "text": "Description.", "ai": true } } }""");
	}

	[TearDown]
	public void TearDown()
	{
		Directory.Delete(m_directory, recursive: true);
	}

	[Test]
	public void ListsAreJoinedAcrossFiles()
	{
		Directory.CreateDirectory(Path.Combine(m_dataDirectory, "More"));
		File.WriteAllText(Path.Combine(m_dataDirectory, "More", "Baker.json"), """{ "professions": [ { "id": "baker", "title": "Baker" } ] }""");

		var professions = ReadProfessions(DataDocument.Read(m_dataDirectory, []));

		Assert.That(professions.Select(x => x.Id), Is.EqualTo(new[] { "baker", "smith" }));
	}

	[Test]
	public void SettingsAreJoinedAcrossFiles()
	{
		File.WriteAllText(Path.Combine(m_dataDirectory, "Icon.json"), """{ "game": { "icon": "Icon.ico" } }""");

		var document = DataDocument.Read(m_dataDirectory, []);

		Assert.That(GameInfo.Read(document.Value!).Value!.Icon, Is.EqualTo("Icon.ico"));
	}

	[Test]
	public void DuplicateIdsAcrossFilesAreErrors()
	{
		File.WriteAllText(Path.Combine(m_dataDirectory, "Other.json"), """{ "professions": [ { "id": "smith", "title": "Smith" } ] }""");

		var document = DataDocument.Read(m_dataDirectory, []);

		Assert.That(document.Succeeded, Is.False);
		Assert.That(Path.GetFileName(document.Errors.Single().FilePath), Is.EqualTo("Trades.json"));
	}

	[Test]
	public void DuplicateSettingsAcrossFilesAreErrors()
	{
		File.WriteAllText(Path.Combine(m_dataDirectory, "Other.json"), """{ "game": { "title": "Other" } }""");

		Assert.That(DataDocument.Read(m_dataDirectory, []).Succeeded, Is.False);
	}

	[Test]
	public void ErrorsNameTheFileTheItemCameFrom()
	{
		var document = DataDocument.Read(m_dataDirectory, []).Value!;

		var error = document.CreateError(OurResources.ProfessionLocationsMissing.FormatCurrentCulture("smith"));

		Assert.That(Path.GetFileName(error.FilePath), Is.EqualTo("Trades.json"));
	}

	[Test]
	public void AppliesModsInOrder()
	{
		var first = WriteMod("First", "Any.json", """
			[
				{ "op": "insert", "path": "professions", "value": { "id": "baker", "title": "Baker", "business": { "wealth": "low", "worksFromHome": true } } },
			]
			""");
		var second = WriteMod("Second", "Other.json", """[ { "op": "set", "path": "professions[baker].business.wealth", "value": "medium" } ]""");

		var professions = ReadProfessions(DataDocument.Read(m_dataDirectory, [first, second]));

		Assert.That(professions.Select(x => x.Id), Is.EqualTo(new[] { "smith", "baker" }));
		Assert.That(professions[1].Wealth!.Value.Level, Is.EqualTo(WealthLevel.Medium));
	}

	[Test]
	public void ModDataIsMergedIn()
	{
		var mod = WriteMod("Mod", "Baker.json", """{ "professions": [ { "id": "baker", "title": "Baker" } ] }""");

		var professions = ReadProfessions(DataDocument.Read(m_dataDirectory, [mod]));

		Assert.That(professions.Select(x => x.Id), Is.EqualTo(new[] { "smith", "baker" }));
	}

	[Test]
	public void OperationErrorsNameTheMod()
	{
		var mod = WriteMod("Broken", "Mod.json", """[ { "op": "remove", "path": "professions[missing]" } ]""");

		var result = DataDocument.Read(m_dataDirectory, [mod]);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Single().ToString(), Does.StartWith("Mod.json (mod \"Broken\"): "));
	}

	[Test]
	public void InvalidModJsonIsReported()
	{
		var mod = WriteMod("Broken", "Mod.json", "[ {");

		var result = DataDocument.Read(m_dataDirectory, [mod]);

		Assert.That(result.Errors.Single().Source, Is.EqualTo("mod \"Broken\""));
	}

	[Test]
	public void ValidationErrorsAfterModsAreMarked()
	{
		var mod = WriteMod("Mod", "Mod.json", """[ { "op": "remove", "path": "professions[smith]" } ]""");

		var result = GameData.Load(m_dataDirectory, [mod]);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Select(x => x.Source), Is.All.EqualTo(OurResources.DataLoadErrorWithMods));
	}

	[TestCase("""[ { "op": "set", "path": "game.icon", "value": "Other.ico" } ]""")]
	[TestCase("""[ { "op": "remove", "path": "game" } ]""")]
	[TestCase("""{ "game": { "title": "Other" } }""")]
	public void GameTitleAndIconAreProtected(string contents)
	{
		var mod = WriteMod("Mod", "Mod.json", contents);

		Assert.That(DataDocument.Read(m_dataDirectory, [mod]).Succeeded, Is.False);
	}

	[Test]
	public void GameInfoCanChangeOtherwise()
	{
		var mod = WriteMod("Mod", "Mod.json", """[ { "op": "set", "path": "game.description.text", "value": "Modded." } ]""");

		var document = DataDocument.Read(m_dataDirectory, [mod]);

		Assert.That(GameInfo.Read(document.Value!).Value!.Description.Text, Is.EqualTo("Modded."));
	}

	[Test]
	public void MissingModFoldersAreErrors()
	{
		var errors = ModFolders.Check([Path.Combine(m_directory, "Missing"), m_dataDirectory]);

		Assert.That(errors, Has.Count.EqualTo(1));
	}

	private static IReadOnlyList<Profession> ReadProfessions(DataLoadResult<DataDocument> document)
	{
		Assert.That(document.Errors, Is.Empty);
		var errors = new List<DataLoadError>();
		var professions = document.Value!.ReadSection<List<Profession>>("professions", errors);
		Assert.That(errors, Is.Empty);
		return professions!;
	}

	private string WriteMod(string name, string fileName, string contents)
	{
		var directory = Path.Combine(m_directory, name);
		Directory.CreateDirectory(directory);
		File.WriteAllText(Path.Combine(directory, fileName), contents);
		return directory;
	}

	private string m_directory = "";
	private string m_dataDirectory = "";
}
