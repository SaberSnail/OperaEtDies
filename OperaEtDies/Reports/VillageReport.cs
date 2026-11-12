using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
			var report = new VillageReport(output, portraitFolder, args.Contains(PortraitsAtWorkArgument, StringComparer.OrdinalIgnoreCase));
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

	private VillageReport(TextWriter output, string? portraitFolder, bool arePortraitsAtWork)
	{
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

			WriteHeaders(gameData);
			for (var village = 0; village < count; village++)
			{
				var seed = unchecked(firstSeed + village);
				m_village = village;

				// The player and the village come from the same random numbers, as they would in a new game.
				var random = new Random(seed);
				var player = CharacterRandomizer.CreateRandomPlayer(gameData, random);
				var gameState = GameStateViewModel.CreateNewGame(gameData, player, random);
				WriteVillage(gameData, village, seed, gameState);
			}
		}

		return exitCode;
	}

	private void WriteHeaders(GameData gameData)
	{
		WriteRecord("#village", "village", "seed", "households", "people", "businesses", "quarters", "houses", "businessLocations", "reusedTemplates", "shape", "landmarks", "paths", "deadEnds", "landmarkDeadEnds", "maxExits", "features", "crossings");
		WriteRecord("#household", "village", "household", "lastName", "members", "wealth", "fortune", "wealthLevel", "house", "quarter", "quarterKind");
		WriteRecord(["#person", "village", "household", "member", "role", "isPlayer", "work", "businessProfession", "fullName", "location", "place", "portraitLayers", .. gameData.CharacterProperties.Select(x => x.Id)]);
		WriteRecord("#business", "village", "household", "profession", "ownerRole", "helpers", "location", "quarter", "quarterKind");
		WriteRecord("#location", "village", "id", "template", "type", "quarterKind", "wealth", "connections", "landmark", "x", "y", "width", "height", "borders");
		WriteRecord("#exit", "village", "from", "to", "direction", "path", "crossing");
		WriteRecord("#feature", "village", "id", "type", "x", "y", "width", "height", "bordering");
		WriteRecord("#log", "village", "severity", "source", "message");
	}

	private void WriteVillage(GameData gameData, int village, int seed, GameStateViewModel gameState)
	{
		var town = gameState.Town;
		var households = town.Households.Select((household, index) => (household, index)).ToDictionary(x => x.household, x => x.index);
		var quarters = town.Locations.Where(x => x.Template is QuarterTemplate).ToList();

		// Paths between quarters are the exits with a direction. Each path has an exit at both ends.
		var pathCounts = quarters.ToDictionary(x => x, x => x.Exits.Count(exit => exit.Direction is not null));
		WriteRecord("village", village, seed, town.Households.Count, town.Households.Sum(x => x.Members.Count), town.Businesses.Count,
			quarters.Count, town.Locations.Count(x => x.Template is HouseTemplate), town.Locations.Count(x => x.Template is BusinessTemplate),
			town.Locations.Count(x => x.Id != x.Template.Id), JsonNamingPolicy.CamelCase.ConvertName(town.Shape.ToString()),
			quarters.Count(x => ((QuarterTemplate) x.Template).IsLandmark), pathCounts.Values.Sum() / 2, pathCounts.Count(x => x.Value == 1),
			pathCounts.Count(x => x.Value == 1 && ((QuarterTemplate) x.Key.Template).IsLandmark), pathCounts.Values.DefaultIfEmpty().Max(),
			string.Join(" ", town.Features.Select(x => x.Feature.Id)), quarters.Sum(x => x.Exits.Count(exit => exit.IsCrossing)) / 2);

		for (var index = 0; index < town.Households.Count; index++)
		{
			var household = town.Households[index];
			var quarter = household.House?.Quarter;
			WriteRecord("household", village, index, household.LastName, household.Members.Count, household.Wealth, household.Fortune, household.WealthLevel.GetDataName(),
				household.House?.Template.Id, quarter?.Template.Id, (quarter?.Template as QuarterTemplate)?.Kind);

			for (var member = 0; member < household.Members.Count; member++)
			{
				var person = household.Members[member];
				var personViewModel = gameState.Families[index].Members[member];
				var work = person.Business is null ? "none" : person.IsBusinessOwner ? "owner" : "helper";
				var startLocation = personViewModel.Location;
				var startPlace = personViewModel.PortraitPlace;
				var startTraits = personViewModel.Traits;

				// Portraits are drawn where the person starts, or at work in their own workplace.
				if (m_arePortraitsAtWork)
				{
					personViewModel.Location = personViewModel.Workplace;
					personViewModel.IsAtWork = true;
				}

				var portraitLayers = gameData.PortraitRenderer?.GetLayerIds(personViewModel.Traits, personViewModel.PortraitPlace) ?? [];
				WriteRecord(["person", village, index, member, person.Role, person.IsPlayer, work, person.Business?.Profession.Id,
					startTraits.GetFullName(gameData.CharacterTemplates), startLocation.Id, startPlace, string.Join(" ", portraitLayers),
					.. gameData.CharacterProperties.Select(x => FormatValue(startTraits.Values.GetValueOrDefault(x.Id)))]);

				if (m_portraitFolder is not null && gameData.PortraitRenderer is { } portraitRenderer)
					WritePortrait(portraitRenderer, personViewModel.Traits, personViewModel.PortraitPlace, Path.Combine(m_portraitFolder, FormattableString.Invariant($"{village}-{index}-{member}.png")));
			}
		}

		foreach (var business in town.Businesses)
		{
			var quarter = business.Location?.Quarter;
			WriteRecord("business", village, households[business.Owner.Household], business.Profession.Id, business.Owner.Role, business.Helpers.Count,
				business.Location?.Template.Id, quarter?.Template.Id, (quarter?.Template as QuarterTemplate)?.Kind);
		}

		foreach (var location in town.Locations)
		{
			var (type, wealth) = location.Template switch
			{
				QuarterTemplate quarter => ("quarter", quarter.Wealth),
				HouseTemplate house => ("house", house.Wealth),
				_ => ("business", (WealthLevel?) null),
			};
			var kind = (location.Quarter?.Template as QuarterTemplate)?.Kind;
			var bounds = location.Bounds;
			WriteRecord("location", village, location.Id, location.Template.Id, type, kind, wealth?.GetDataName(), location.Connections.Count,
				(location.Template as QuarterTemplate)?.IsLandmark, bounds?.X, bounds?.Y, bounds?.Width, bounds?.Height,
				string.Join(" ", location.Surroundings.Select(x => $"{x.Feature.Id}:{x.Direction}")));
			foreach (var exit in location.Exits.Where(x => x.Direction is not null))
				WriteRecord("exit", village, location.Id, exit.Location.Id, exit.Direction, exit.Path, exit.IsCrossing);
		}

		foreach (var feature in town.Features)
		{
			var bounds = feature.Bounds;
			WriteRecord("feature", village, feature.Feature.Id, JsonNamingPolicy.CamelCase.ConvertName(feature.Feature.Type.ToString()), bounds.X, bounds.Y, bounds.Width, bounds.Height,
				town.Locations.Count(x => x.Surroundings.Any(surrounding => surrounding.Feature == feature.Feature)));
		}
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
	private int? m_village;
}
