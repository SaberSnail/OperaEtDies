using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GoldenAnvil.Utility.Calendar;
using GoldenAnvil.Utility.Logging;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Town;

namespace OperaEtDies.Reports;

/// <summary>
/// Generates villages the same way a new game does, without showing any UI, and writes what was generated as
/// tab-separated records so the results can be analyzed. Each record starts with its type, and the first lines name
/// the columns of each type. Warnings and errors logged while loading and generating are written as records too.
/// </summary>
public sealed class VillageReport : ILogDestination
{
	/// <summary>
	/// The command line argument that runs the report, followed by how many villages to generate.
	/// </summary>
	public const string Argument = "--generate-villages";

	/// <summary>
	/// The optional command line argument for the seed of the first village. Each village after it uses the next seed.
	/// </summary>
	public const string SeedArgument = "--seed";

	/// <summary>
	/// The optional command line argument for a folder to write each person's portrait to.
	/// </summary>
	public const string PortraitsArgument = "--portraits";

	/// <summary>
	/// The optional command line argument to draw everyone's portrait at work in their own workplace, instead of where
	/// they start.
	/// </summary>
	public const string PortraitsAtWorkArgument = "--portraits-at-work";

	/// <summary>
	/// The optional command line argument to save each village and load it again before writing it, to check that
	/// saving and loading keep everything the report shows.
	/// </summary>
	public const string ThroughSaveArgument = "--through-save";

	public static bool IsRequested(IReadOnlyList<string> args) => args.Contains(Argument, StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Runs the report with the given command line arguments, and returns the exit code: 0 on success, 1 if the game
	/// data couldn't be loaded, and 2 if the arguments are invalid. Logging must not have been set up yet, since the
	/// report captures it. Writing portraits must be done on an STA thread.
	/// </summary>
	public static int Run(IReadOnlyList<string> args, bool areModsEnabled, TextWriter output, TextWriter error)
	{
		int exitCode;
		if (TryParseArguments(args, out var count, out var firstSeed, out var portraitFolder, out var argumentError))
		{
			var report = new VillageReport(output, portraitFolder, args.Contains(PortraitsAtWorkArgument, StringComparer.OrdinalIgnoreCase), args.Contains(ThroughSaveArgument, StringComparer.OrdinalIgnoreCase));
			LogManager.Initialize(report);
			exitCode = report.Generate(count, firstSeed, areModsEnabled, error);
		}
		else
		{
			error.WriteLine(argumentError);
			exitCode = 2;
		}

		output.Flush();
		error.Flush();
		return exitCode;
	}

	public void LogMessage(LogSeverity severity, string source, string message)
	{
		if (severity != LogSeverity.Info)
			WriteRecord("log", m_village, severity, source, message);
	}

	private VillageReport(TextWriter output, string? portraitFolder, bool arePortraitsAtWork, bool isThroughSave)
	{
		m_isThroughSave = isThroughSave;
		m_output = output;
		m_portraitFolder = portraitFolder;
		m_arePortraitsAtWork = arePortraitsAtWork;
	}

	private static bool TryParseArguments(IReadOnlyList<string> args, out int count, out int firstSeed, out string? portraitFolder, out string error)
	{
		count = 0;
		firstSeed = Random.Shared.Next();
		portraitFolder = null;
		error = "";

		for (var index = 0; index < args.Count; index++)
		{
			var isCount = string.Equals(args[index], Argument, StringComparison.OrdinalIgnoreCase);
			var isSeed = string.Equals(args[index], SeedArgument, StringComparison.OrdinalIgnoreCase);
			if (string.Equals(args[index], PortraitsArgument, StringComparison.OrdinalIgnoreCase))
			{
				index++;
				if (index < args.Count && !string.IsNullOrWhiteSpace(args[index]))
					portraitFolder = args[index];
				else
					error = $"{PortraitsArgument} must be followed by a folder.";
			}
			else if (isCount || isSeed)
			{
				index++;
				var text = index < args.Count ? args[index] : "";
				if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || (isCount && value < 1))
					error = isCount ? $"{Argument} must be followed by a number of villages of at least 1." : $"{SeedArgument} must be followed by a whole number.";
				else if (isCount)
					count = value;
				else
					firstSeed = value;
			}
		}

		return error.Length == 0;
	}

	private int Generate(int count, int firstSeed, bool areModsEnabled, TextWriter error)
	{
		var (content, _, _) = AppModel.LoadContent(areModsEnabled);
		var gameData = content.GameDataResult.Value;
		var exitCode = 0;
		if (gameData is null)
		{
			foreach (var loadError in content.Errors)
				error.WriteLine(loadError.ToString());
			exitCode = 1;
		}
		else
		{
			if (m_portraitFolder is not null)
				Directory.CreateDirectory(m_portraitFolder);

			WriteVillages(gameData, count, firstSeed);
		}

		return exitCode;
	}

	/// <summary>
	/// Generates villages with the game data and writes them to the output, without capturing logging, for tests.
	/// </summary>
	internal static void Write(GameData gameData, int count, int firstSeed, bool isThroughSave, TextWriter output) =>
		new VillageReport(output, portraitFolder: null, arePortraitsAtWork: false, isThroughSave).WriteVillages(gameData, count, firstSeed);

	private void WriteVillages(GameData gameData, int count, int firstSeed)
	{
		WriteHeaders(gameData);
		for (var village = 0; village < count; village++)
		{
			var seed = unchecked(firstSeed + village);
			m_village = village;

			// The player and the village come from the same random numbers, as they would in a new game.
			var random = new Random(seed);
			var player = CharacterRandomizer.CreateRandomPlayer(gameData, random);
			var town = TownGenerator.Generate(gameData, player, random);
			var world = GameWorld.CreateFromTown(gameData, town, random);
			try
			{
				if (m_isThroughSave)
					world = SaveAndLoad(gameData, world, random);
				WriteVillage(gameData, village, seed, town, world);
			}
			catch (SaveLoadException e)
			{
				WriteRecord("log", village, LogSeverity.Error, nameof(VillageReport), $"The village couldn't be loaded after saving it: {e.Message}");
			}
		}
	}

	/// <summary>
	/// Saves the world and loads it again, as saving and loading a game does, so the report shows what a loaded game has.
	/// </summary>
	private static GameWorld SaveAndLoad(GameData gameData, GameWorld world, Random random)
	{
		var settings = new SaveSettings(nameof(VillageReport), "", "", [], "");
		var json = SaveFile.Create(world, settings, DateTimeOffset.UnixEpoch).Serialize();
		return SaveFile.Deserialize(json).Load(gameData, settings.GameTitle, random);
	}

	private void WriteHeaders(GameData gameData)
	{
		WriteRecord("#village", "village", "seed", "households", "people", "businesses", "quarters", "houses", "businessLocations", "reusedTemplates", "shape", "landmarks", "paths", "deadEnds", "landmarkDeadEnds", "maxExits", "features", "crossings", "time", "season");
		WriteRecord("#household", "village", "household", "lastName", "members", "wealth", "fortune", "wealthLevel", "house", "quarter", "quarterKind");
		WriteRecord(["#person", "village", "household", "member", "role", "isPlayer", "work", "businessProfession", "fullName", "location", "place", "portraitLayers", .. gameData.CharacterProperties.Select(x => x.Id)]);
		WriteRecord("#business", "village", "household", "profession", "ownerRole", "helpers", "location", "quarter", "quarterKind");
		WriteRecord("#location", "village", "id", "template", "type", "quarterKind", "wealth", "connections", "landmark", "x", "y", "width", "height", "borders");
		WriteRecord("#exit", "village", "from", "to", "direction", "path", "crossing");
		WriteRecord("#feature", "village", "id", "type", "x", "y", "width", "height", "bordering");
		WriteRecord("#log", "village", "severity", "source", "message");
	}

	/// <summary>
	/// Writes the records for one village. Everything comes from the world, except the details only generation has (the
	/// households' wealth and fortune, and the layout shape), which come from the generated town. The town's households
	/// are in the same order as the world's families.
	/// </summary>
	private void WriteVillage(GameData gameData, int village, int seed, GeneratedTown town, GameWorld world)
	{
		var families = world.Families.Select((family, index) => (family, index)).ToDictionary(x => x.family, x => x.index);
		var quarters = world.Locations.Where(x => x.Template is QuarterTemplate).ToList();
		var names = GetLocationNames(world.Locations);

		// Paths between quarters are the exits with a direction. Each path has an exit at both ends.
		var pathCounts = quarters.ToDictionary(x => x, x => x.Exits.Count(exit => exit.Direction is not null));
		WriteRecord("village", village, seed, world.Families.Count, world.People.Count, world.Businesses.Count,
			quarters.Count, world.Locations.Count(x => x.Template is HouseTemplate), world.Locations.Count(x => x.Template is BusinessTemplate),
			world.Locations.Count - world.Locations.Select(x => x.Template).Distinct().Count(), JsonNamingPolicy.CamelCase.ConvertName(town.Shape.ToString()),
			quarters.Count(x => x.IsLandmark), pathCounts.Values.Sum() / 2, pathCounts.Count(x => x.Value == 1),
			pathCounts.Count(x => x.Value == 1 && x.Key.IsLandmark), pathCounts.Values.DefaultIfEmpty().Max(),
			string.Join(" ", world.Features.Select(x => x.Template.Id)), quarters.Sum(x => x.Exits.Count(exit => exit.IsCrossing)) / 2,
			gameData.Calendar.FormatTime(world.Time, TimeFormat.Long), gameData.Calendar.GetSeason(world.Time).Id);

		for (var index = 0; index < world.Families.Count; index++)
		{
			var family = world.Families[index];
			var household = town.Households[index];
			var quarter = family.Home.Quarter;
			WriteRecord("household", village, index, family.LastName, family.Members.Count, household.Wealth, household.Fortune, family.WealthLevel.GetDataName(),
				family.Home.Template.Id, quarter?.Template.Id, quarter?.QuarterKind);

			for (var member = 0; member < family.Members.Count; member++)
			{
				var person = family.Members[member];
				var work = person.Business is null ? "none" : person.IsBusinessOwner ? "owner" : "helper";
				var startLocation = person.Location;
				var startPlace = person.PortraitPlace;
				var startTraits = person.Traits;

				// Portraits are drawn where the person starts, or at work in their own workplace.
				if (m_arePortraitsAtWork)
				{
					person.Location = person.Workplace;
					person.IsAtWork = true;
				}

				var portraitLayers = gameData.PortraitRenderer?.GetLayerIds(person.Traits, person.PortraitPlace) ?? [];
				WriteRecord(["person", village, index, member, person.Role, person.IsPlayer, work, person.Business?.Profession.Id,
					startTraits.GetFullName(gameData.CharacterTemplates), names[startLocation], startPlace, string.Join(" ", portraitLayers),
					.. gameData.CharacterProperties.Select(x => FormatValue(startTraits.Values.GetValueOrDefault(x.Id)))]);

				if (m_portraitFolder is not null && gameData.PortraitRenderer is { } portraitRenderer)
					WritePortrait(portraitRenderer, person.Traits, person.PortraitPlace, Path.Combine(m_portraitFolder, FormattableString.Invariant($"{village}-{index}-{member}.png")));
			}
		}

		foreach (var business in world.Businesses)
		{
			var quarter = business.Location?.Quarter;
			WriteRecord("business", village, families[business.Owner.Family], business.Profession.Id, business.Owner.Role, business.Helpers.Count,
				business.Location?.Template.Id, quarter?.Template.Id, quarter?.QuarterKind);
		}

		foreach (var location in world.Locations)
		{
			var (type, wealth) = location.Template switch
			{
				QuarterTemplate quarter => ("quarter", quarter.Wealth),
				HouseTemplate house => ("house", house.Wealth),
				_ => ("business", (WealthLevel?) null),
			};
			var bounds = location.Bounds;
			WriteRecord("location", village, names[location], location.Template.Id, type, location.QuarterKind, wealth?.GetDataName(), location.Connections.Count,
				(location.Template as QuarterTemplate)?.IsLandmark, bounds?.X, bounds?.Y, bounds?.Width, bounds?.Height,
				string.Join(" ", location.Surroundings.Select(x => $"{x.Feature.Template.Id}:{x.Direction}")));
			foreach (var exit in location.Exits.Where(x => x.Direction is not null))
				WriteRecord("exit", village, names[location], names[exit.Location], exit.Direction, exit.Path, exit.IsCrossing);
		}

		foreach (var feature in world.Features)
		{
			var bounds = feature.Bounds;
			WriteRecord("feature", village, feature.Template.Id, JsonNamingPolicy.CamelCase.ConvertName(feature.Template.Type.ToString()), bounds.X, bounds.Y, bounds.Width, bounds.Height,
				world.Locations.Count(x => x.Surroundings.Any(surrounding => surrounding.Feature == feature)));
		}
	}

	/// <summary>
	/// Names each location after its template, with a number added for each use of a template after the first, so the
	/// names are readable and the same each time a village is generated. World ids aren't used, since they're a black box.
	/// </summary>
	private static Dictionary<Location, string> GetLocationNames(IReadOnlyList<Location> locations)
	{
		var uses = new Dictionary<string, int>(StringComparer.Ordinal);
		var names = new Dictionary<Location, string>();
		foreach (var location in locations)
		{
			var id = location.Template.Id;
			var count = uses[id] = uses.GetValueOrDefault(id) + 1;
			names.Add(location, count == 1 ? id : $"{id}_{count}");
		}

		return names;
	}

	private static object? FormatValue(object? value) => value is CharacterPropertyOption option ? option.Id : value;

	/// <summary>
	/// Draws a character's portrait at its full size and saves it as a PNG file.
	/// </summary>
	private static void WritePortrait(PortraitRenderer renderer, CharacterTraits traits, PortraitPlace? place, string filePath)
	{
		var size = renderer.Size;
		var visual = new DrawingVisual();
		using (var context = visual.RenderOpen())
			context.DrawImage(renderer.Render(traits, place), new Rect(size));

		var bitmap = new RenderTargetBitmap((int) Math.Ceiling(size.Width), (int) Math.Ceiling(size.Height), 96, 96, PixelFormats.Pbgra32);
		bitmap.Render(visual);
		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using var stream = File.Create(filePath);
		encoder.Save(stream);
	}

	private void WriteRecord(params object?[] values) =>
		m_output.WriteLine(string.Join('\t', values.Select(x => (Convert.ToString(x, CultureInfo.InvariantCulture) ?? "").ReplaceLineEndings(" ").Replace('\t', ' '))));

	private readonly TextWriter m_output;
	private readonly string? m_portraitFolder;
	private readonly bool m_arePortraitsAtWork;
	private readonly bool m_isThroughSave;
	private int? m_village;
}
