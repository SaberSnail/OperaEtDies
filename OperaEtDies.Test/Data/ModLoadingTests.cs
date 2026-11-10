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
		File.WriteAllText(Path.Combine(m_dataDirectory, LocationList.FileName), """
			{
				// Comments are allowed.
				"startingLocation": "square",
				"locations": [
					{ "id": "square", "title": "Square", "shortDescription": "A square.", "mediumDescription": "A square.", "longDescription": [ "A square." ] },
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
		var first = WriteMod("First", LocationList.FileName, """
			[
				{ "op": "insert", "path": "locations", "value": { "id": "inn", "title": "Inn", "shortDescription": "An inn.", "mediumDescription": "An inn.", "longDescription": [ "An inn." ] } },
			]
			""");
		var second = WriteMod("Second", LocationList.FileName, """[ { "op": "set", "path": "startingLocation", "value": "inn" } ]""");

		var result = LocationList.Load(m_dataDirectory, [first, second]);

		Assert.That(result.Errors, Is.Empty);
		Assert.That(result.Value!.Locations.Select(x => x.Id), Is.EqualTo(new[] { "square", "inn" }));
		Assert.That(result.Value.StartingLocation, Is.EqualTo("inn"));
	}

	[Test]
	public void ModsWithoutTheFileAreSkipped()
	{
		var mod = WriteMod("Mod", CharacterData.FileName, "[]");

		var result = LocationList.Load(m_dataDirectory, [mod]);

		Assert.That(result.Succeeded, Is.True);
	}

	[Test]
	public void OperationErrorsNameTheMod()
	{
		var mod = WriteMod("Broken", LocationList.FileName, """[ { "op": "remove", "path": "locations[missing]" } ]""");

		var result = LocationList.Load(m_dataDirectory, [mod]);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Single().ToString(), Does.StartWith("Locations.json (mod \"Broken\"): "));
	}

	[Test]
	public void InvalidModJsonIsReported()
	{
		var mod = WriteMod("Broken", LocationList.FileName, "[ {");

		var result = LocationList.Load(m_dataDirectory, [mod]);

		Assert.That(result.Errors.Single().Source, Is.EqualTo("mod \"Broken\""));
	}

	[Test]
	public void ValidationErrorsAfterModsAreMarked()
	{
		var mod = WriteMod("Mod", LocationList.FileName, """[ { "op": "remove", "path": "locations[square]" } ]""");

		var result = LocationList.Load(m_dataDirectory, [mod]);

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
