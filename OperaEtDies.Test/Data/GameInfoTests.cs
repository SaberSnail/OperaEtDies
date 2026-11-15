using System.IO;
using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class GameInfoTests
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
	public void LoadsShippedGameInfo()
	{
		var result = GameInfo.Load(JsonDataLoader.DefaultDataDirectory);

		Assert.That(result.Errors, Is.Empty);
		Assert.That(result.Value!.Title, Is.Not.Empty);
		Assert.That(result.Value.Description.Text, Is.Not.Empty);
		if (result.Value.GetIconPath(JsonDataLoader.DefaultDataDirectory) is { } iconPath)
			Assert.That(File.Exists(iconPath), Is.True);
	}

	[Test]
	public void IconIsOptional()
	{
		WriteGameInfo("""{ "title": "Test Title", "description": { "text": "Test description.", "ai": true } }""");

		var result = GameInfo.Load(m_directory);

		Assert.That(result.Succeeded, Is.True);
		Assert.That(result.Value!.Icon, Is.Null);
		Assert.That(result.Value.GetIconPath(m_directory), Is.Null);
	}

	[Test]
	public void IconPathIsRelativeToDataDirectory()
	{
		WriteGameInfo("""{ "title": "Test Title", "description": { "text": "Test description.", "ai": true }, "icon": "Images/Icon.ico" }""");

		var result = GameInfo.Load(m_directory);

		Assert.That(result.Succeeded, Is.True);
		Assert.That(result.Value!.GetIconPath(m_directory), Is.EqualTo(Path.Combine(m_directory, "Images", "Icon.ico")));
	}

	[TestCase("")]
	[TestCase(" ")]
	[TestCase(@"C:\\Icon.ico")]
	[TestCase(@"\\Icon.ico")]
	public void InvalidIconPathReportsError(string icon)
	{
		WriteGameInfo($$"""{ "title": "Test Title", "description": { "text": "Test description.", "ai": true }, "icon": "{{icon}}" }""");

		var result = GameInfo.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Single().Message, Is.EqualTo(OurResources.GameInfoIconInvalid.FormatCurrentCulture(icon.Replace(@"\\", @"\", StringComparison.Ordinal))));
	}

	[Test]
	public void LoadsValidFileWithCommentsAndTrailingCommas()
	{
		WriteGameInfo("""
			{
				// A comment
				"title": "Test Title",
				"description": { "text": "Test description.", "ai": true },
			}
			""");

		var result = GameInfo.Load(m_directory);

		Assert.That(result.Succeeded, Is.True);
		Assert.That(result.Value!.Title, Is.EqualTo("Test Title"));
		Assert.That(result.Value.Description.Text, Is.EqualTo("Test description."));
	}

	[Test]
	public void MissingFileReportsError()
	{
		var result = GameInfo.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Single().Message, Is.EqualTo(OurResources.DataSectionMissing.FormatCurrentCulture("game")));
	}

	[Test]
	public void MissingDirectoryReportsError()
	{
		var result = GameInfo.Load(Path.Combine(m_directory, "DoesNotExist"));

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Single().Message, Is.EqualTo(OurResources.DataFolderNotFound.FormatCurrentCulture(Path.Combine(m_directory, "DoesNotExist"))));
	}

	[TestCase("{ \"title\": ")]
	[TestCase("{ \"title\": \"Test Title\" }")]
	[TestCase("{ \"title\": null, \"description\": \"Test description.\" }")]
	[TestCase("[]")]
	public void InvalidJsonReportsError(string json)
	{
		WriteGameInfo(json);

		var result = GameInfo.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors, Has.Count.EqualTo(1));
	}

	[Test]
	public void NullJsonReportsEmptyError()
	{
		File.WriteAllText(Path.Combine(m_directory, "Game.json"), "null");

		var result = GameInfo.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Single().Message, Is.EqualTo(OurResources.DataFileEmpty));
	}

	[Test]
	public void BlankValuesReportValidationErrors()
	{
		WriteGameInfo("""{ "title": " ", "description": { "text": "", "ai": true } }""");

		var result = GameInfo.Load(m_directory);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Select(x => x.Message), Is.EquivalentTo(new[] { OurResources.GameInfoTitleMissing, OurResources.GameInfoDescriptionMissing }));
	}

	private void WriteGameInfo(string json) => File.WriteAllText(Path.Combine(m_directory, "Game.json"), $"{{ \"game\": {json} }}");

	private string m_directory = "";
}
