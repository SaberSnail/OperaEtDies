using System.IO;
using System.Reflection;
using System.Text.Json;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class ThemeSettingsTests
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
	public void LoadsShippedTheme()
	{
		var document = DataDocument.Read(JsonDataLoader.DefaultDataDirectory, []);

		Assert.That(document.Errors, Is.Empty);
		var theme = ThemeSettings.Read(document.Value!);
		Assert.That(theme, Is.Not.Null);
		Assert.That(theme!.Validate(), Is.Empty);
	}

	[Test]
	public void ShippedFontsAreInTheFontFolder()
	{
		var theme = ThemeSettings.Read(DataDocument.Read(JsonDataLoader.DefaultDataDirectory, []).Value!)!;
		var folder = ThemeSettings.GetFontFolder(JsonDataLoader.DefaultDataDirectory);
		var families = System.Windows.Media.Fonts.GetFontFamilies(folder + Path.DirectorySeparatorChar)
			.SelectMany(x => x.FamilyNames.Values)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		// The first family of each font is the one meant to be used; the rest are fallbacks.
		foreach (var (role, family) in theme.Fonts.GetFonts())
			Assert.That(families, Does.Contain(family.Split(',')[0].Trim()), role);
	}

	[Test]
	public void ThemeIsOptional()
	{
		WriteData(new { game = new { title = "Test Title" } });

		Assert.That(ThemeSettings.Read(ReadDocument()), Is.Null);
	}

	[Test]
	public void ValidThemeHasNoErrors()
	{
		WriteData(new { theme = CreateTheme() });

		var theme = ThemeSettings.Read(ReadDocument());

		Assert.That(theme, Is.Not.Null);
		Assert.That(theme!.Colors.Accent, Is.EqualTo("#102030"));
		Assert.That(theme.FontSize, Is.EqualTo(16));
	}

	[Test]
	public void MissingColorIsAnError()
	{
		var theme = CreateTheme();
		((Dictionary<string, object>) theme["colors"]).Remove("accent");
		WriteData(new { theme });

		var errors = new List<DataLoadError>();
		ReadDocument().ReadSection<ThemeSettings>(ThemeSettings.SectionName, errors);

		Assert.That(errors, Is.Not.Empty);
	}

	[TestCase("#12345")]
	[TestCase("102030")]
	[TestCase("#GG2030")]
	[TestCase("red")]
	public void InvalidColorIsAnError(string color)
	{
		var theme = CreateTheme();
		((Dictionary<string, object>) theme["colors"])["accent"] = color;
		WriteData(new { theme });

		var errors = ReadAndValidate();

		Assert.That(errors, Has.Count.EqualTo(1));
		Assert.That(errors[0], Does.Contain("accent").And.Contain(color));
	}

	[Test]
	public void AlphaColorIsValid()
	{
		var theme = CreateTheme();
		((Dictionary<string, object>) theme["colors"])["accent"] = "#80102030";
		WriteData(new { theme });

		Assert.That(ReadAndValidate(), Is.Empty);
	}

	[TestCase("")]
	[TestCase(" ")]
	public void BlankFontIsAnError(string family)
	{
		var theme = CreateTheme();
		((Dictionary<string, object>) theme["fonts"])["narrative"] = family;
		WriteData(new { theme });

		var errors = ReadAndValidate();

		Assert.That(errors, Has.Count.EqualTo(1));
		Assert.That(errors[0], Does.Contain("narrative"));
	}

	[TestCase(5.9)]
	[TestCase(72.1)]
	[TestCase(0)]
	public void FontSizeOutOfRangeIsAnError(double size)
	{
		var theme = CreateTheme();
		theme["fontSize"] = size;
		WriteData(new { theme });

		Assert.That(ReadAndValidate(), Has.Count.EqualTo(1));
	}

	[TestCase(0)]
	[TestCase(-1)]
	[TestCase(10.1)]
	public void FontScaleOutOfRangeIsAnError(double scale)
	{
		var theme = CreateTheme();
		((Dictionary<string, object>) theme["fontScales"])["title"] = scale;
		WriteData(new { theme });

		var errors = ReadAndValidate();

		Assert.That(errors, Has.Count.EqualTo(1));
		Assert.That(errors[0], Does.Contain("title"));
	}

	[Test]
	public void ThemeErrorsStopTheGameDataFromLoading()
	{
		var theme = CreateTheme();
		((Dictionary<string, object>) theme["colors"])["accent"] = "red";
		var document = DataDocument.Read(JsonDataLoader.DefaultDataDirectory, []).Value!;
		document.Root[ThemeSettings.SectionName] = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(theme));

		var result = GameData.Load(document, includePortraits: false);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Select(x => x.Message), Has.Some.Contain("accent"));
	}

	[Test]
	public void ThemeIsReadWithTheGameData()
	{
		var result = GameData.Load(DataDocument.Read(JsonDataLoader.DefaultDataDirectory, []).Value!, includePortraits: false);

		Assert.That(result.Errors, Is.Empty);
		Assert.That(result.Value!.Theme, Is.Not.Null);
	}

	/// <summary>
	/// Creates a valid theme, with every color, font, and scale, as data that can be changed before it's written.
	/// </summary>
	private static Dictionary<string, object> CreateTheme() => new()
	{
		["colors"] = GetRoles<ThemeColors>().ToDictionary(x => x, x => (object) (x == "accent" ? "#102030" : "#000000")),
		["fonts"] = GetRoles<ThemeFonts>().ToDictionary(x => x, _ => (object) "Georgia"),
		["fontSize"] = 16.0,
		["fontScales"] = GetRoles<ThemeFontScales>().ToDictionary(x => x, _ => (object) 1.5),
	};

	private static IEnumerable<string> GetRoles<T>() =>
		typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(x => char.ToLowerInvariant(x.Name[0]) + x.Name[1..]);

	private void WriteData(object data) => File.WriteAllText(Path.Combine(m_directory, "Data.json"), JsonSerializer.Serialize(data));

	private DataDocument ReadDocument()
	{
		var document = DataDocument.Read(m_directory, []);
		Assert.That(document.Errors, Is.Empty);
		return document.Value!;
	}

	private IReadOnlyList<string> ReadAndValidate()
	{
		var errors = new List<DataLoadError>();
		var theme = ReadDocument().ReadSection<ThemeSettings>(ThemeSettings.SectionName, errors);
		Assert.That(errors, Is.Empty);
		return theme!.Validate().ToList();
	}

	private string m_directory = null!;
}
