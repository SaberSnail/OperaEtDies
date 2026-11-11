using System.IO;
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
		File.WriteAllText(Path.Combine(m_dataDirectory, ProfessionList.FileName), """
			{
				// Comments are allowed.
				"professions": [
					{ "id": "smith", "title": "Smith", "wealth": "high", "worksFromHome": true },
				],
			}
			""");
		File.WriteAllText(Path.Combine(m_dataDirectory, GameInfo.FileName), """{ "title": "Title", "description": "Description." }""");
	}

	[TearDown]
	public void TearDown()
	{
		Directory.Delete(m_directory, recursive: true);
	}

	[Test]
	public void AppliesModsInOrder()
	{
		var first = WriteMod("First", ProfessionList.FileName, """
			[
				{ "op": "insert", "path": "professions", "value": { "id": "baker", "title": "Baker", "wealth": "low", "worksFromHome": true } },
			]
			""");
		var second = WriteMod("Second", ProfessionList.FileName, """[ { "op": "set", "path": "professions[baker].wealth", "value": "medium" } ]""");

		var result = ProfessionList.Load(m_dataDirectory, [first, second]);

		Assert.That(result.Errors, Is.Empty);
		Assert.That(result.Value!.Professions.Select(x => x.Id), Is.EqualTo(new[] { "smith", "baker" }));
		Assert.That(result.Value.Professions[1].Wealth!.Value.Level, Is.EqualTo(WealthLevel.Medium));
	}

	[Test]
	public void ModsWithoutTheFileAreSkipped()
	{
		var mod = WriteMod("Mod", CharacterData.FileName, "[]");

		var result = ProfessionList.Load(m_dataDirectory, [mod]);

		Assert.That(result.Succeeded, Is.True);
	}

	[Test]
	public void OperationErrorsNameTheMod()
	{
		var mod = WriteMod("Broken", ProfessionList.FileName, """[ { "op": "remove", "path": "professions[missing]" } ]""");

		var result = ProfessionList.Load(m_dataDirectory, [mod]);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Single().ToString(), Does.StartWith("Professions.json (mod \"Broken\"): "));
	}

	[Test]
	public void InvalidModJsonIsReported()
	{
		var mod = WriteMod("Broken", ProfessionList.FileName, "[ {");

		var result = ProfessionList.Load(m_dataDirectory, [mod]);

		Assert.That(result.Errors.Single().Source, Is.EqualTo("mod \"Broken\""));
	}

	[Test]
	public void ValidationErrorsAfterModsAreMarked()
	{
		var mod = WriteMod("Mod", ProfessionList.FileName, """[ { "op": "remove", "path": "professions[smith]" } ]""");

		var result = ProfessionList.Load(m_dataDirectory, [mod]);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Select(x => x.Source), Is.All.EqualTo(OurResources.DataLoadErrorWithMods));
	}

	[Test]
	public void GameInfoTitleAndIconAreProtected()
	{
		var mod = WriteMod("Mod", GameInfo.FileName, """[ { "op": "set", "path": "icon", "value": "Other.ico" } ]""");

		Assert.That(GameInfo.Load(m_dataDirectory, [mod]).Succeeded, Is.False);
	}

	[Test]
	public void GameInfoCanChangeOtherwise()
	{
		var mod = WriteMod("Mod", GameInfo.FileName, """[ { "op": "set", "path": "description", "value": "Modded." } ]""");

		Assert.That(GameInfo.Load(m_dataDirectory, [mod]).Value!.Description, Is.EqualTo("Modded."));
	}

	[Test]
	public void MissingModFoldersAreErrors()
	{
		var errors = ModFolders.Check([Path.Combine(m_directory, "Missing"), m_dataDirectory]);

		Assert.That(errors, Has.Count.EqualTo(1));
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
