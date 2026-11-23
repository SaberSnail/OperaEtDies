using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GoldenAnvil.Utility.Calendar;
using GoldenAnvil.Utility.Logging;
using OperaEtDies.Characters;
using OperaEtDies.Controls.Log;
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
	/// The optional command line argument for how many days to run each village for after creating it, writing what
	/// people do as they go.
	/// </summary>
	public const string SimulateDaysArgument = "--simulate-days";

	/// <summary>
	/// The optional command line argument to save each village and load it again before writing it, to check that
	/// saving and loading keep everything the report shows.
	/// </summary>
	public const string ThroughSaveArgument = "--through-save";

	public static bool IsRequested(IReadOnlyList<string> args) => args.Contains(Argument, StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Runs the report with the given command line arguments, and returns the exit code: 0 on success, 1 if the game
	/// data couldn't be loaded, and 2 if the arguments are invalid. Logging must not have been set up yet, since the
	/// report captures it. Writing portraits must be done on an STA thread. The given mod folders are loaded instead of the mod
	/// list if there are any.
	/// </summary>
	public static int Run(IReadOnlyList<string> args, bool areModsEnabled, IReadOnlyList<string>? modDirectories, TextWriter output, TextWriter error)
	{
		int exitCode;
		if (TryParseArguments(args, out var count, out var firstSeed, out var portraitFolder, out var simulateDays, out var argumentError))
		{
			var report = new VillageReport(output, portraitFolder, args.Contains(PortraitsAtWorkArgument, StringComparer.OrdinalIgnoreCase), args.Contains(ThroughSaveArgument, StringComparer.OrdinalIgnoreCase), simulateDays);
			LogManager.Initialize(report);
			exitCode = report.Generate(count, firstSeed, areModsEnabled, modDirectories, error);
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

	private VillageReport(TextWriter output, string? portraitFolder, bool arePortraitsAtWork, bool isThroughSave, int simulateDays)
	{
		m_simulateDays = simulateDays;
		m_isThroughSave = isThroughSave;
		m_output = output;
		m_portraitFolder = portraitFolder;
		m_arePortraitsAtWork = arePortraitsAtWork;
	}

	private static bool TryParseArguments(IReadOnlyList<string> args, out int count, out int firstSeed, out string? portraitFolder, out int simulateDays, out string error)
	{
		simulateDays = 0;
		count = 0;
		firstSeed = Random.Shared.Next();
		portraitFolder = null;
		error = "";

		for (var index = 0; index < args.Count; index++)
		{
			var isCount = string.Equals(args[index], Argument, StringComparison.OrdinalIgnoreCase);
			var isSeed = string.Equals(args[index], SeedArgument, StringComparison.OrdinalIgnoreCase);
			var isDays = string.Equals(args[index], SimulateDaysArgument, StringComparison.OrdinalIgnoreCase);
			if (string.Equals(args[index], PortraitsArgument, StringComparison.OrdinalIgnoreCase))
			{
				index++;
				if (index < args.Count && !string.IsNullOrWhiteSpace(args[index]))
					portraitFolder = args[index];
				else
					error = $"{PortraitsArgument} must be followed by a folder.";
			}
			else if (isDays)
			{
				index++;
				var text = index < args.Count ? args[index] : "";
				if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) && days >= 0)
					simulateDays = days;
				else
					error = $"{SimulateDaysArgument} must be followed by a number of days of at least 0.";
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

	private int Generate(int count, int firstSeed, bool areModsEnabled, IReadOnlyList<string>? modDirectories, TextWriter error)
	{
		var (content, _, _) = AppModel.LoadContent(areModsEnabled, modDirectories);
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
	internal static void Write(GameData gameData, int count, int firstSeed, bool isThroughSave, TextWriter output, int simulateDays = 0) =>
		new VillageReport(output, portraitFolder: null, arePortraitsAtWork: false, isThroughSave, simulateDays).WriteVillages(gameData, count, firstSeed);

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
			// No one plays, so the player lives their life like everyone else.
			var world = GameWorld.CreateFromTown(gameData, town, random);
			world.IsHeadless = true;
			if (m_simulateDays > 0)
				Simulate(village, world);

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
	/// Runs the world for the report's number of days, writing an activity record each time someone starts a task.
	/// </summary>
	private void Simulate(int village, GameWorld world)
	{
		var calendar = world.GameData.Calendar;
		var names = GetLocationNames(world.Locations);
		var members = world.Families.SelectMany((family, index) => family.Members.Select((person, member) => (person, index, member))).ToDictionary(x => x.person);
		void OnTaskStarted(object? sender, PersonTaskEventArgs e)
		{
			var (person, household, member) = members[e.Person];
			WriteRecord("activity", village, calendar.FormatTime(e.Time, TimeFormat.Long), calendar.GetDayOfWeek(e.Time).Id, household, member, person.Goal?.Id, person.Current?.Step, GetTaskName(e.Task),
				person.TaskEnd is { } end ? calendar.GetTotalMinutes(end - e.Time) : null,
				names[person.Location], e.Task is TravelTask travel ? names[travel.Destination] : null);
		}

		void OnStepFailed(object? sender, StepFailedEventArgs e)
		{
			var (person, household, member) = members[e.Person];
			WriteRecord("failure", village, calendar.FormatTime(world.Time, TimeFormat.Long), household, member, e.Sequence.Need.Id, e.Step, JsonNamingPolicy.CamelCase.ConvertName(e.Sequence.Need.Steps[e.Step].Kind.ToString()),
				JsonNamingPolicy.CamelCase.ConvertName(e.Cause.ToString()), names[person.Location]);
		}

		world.TaskStarted += OnTaskStarted;
		world.StepFailed += OnStepFailed;
		world.AdvanceTime(calendar.CreateTimeOffset(m_simulateDays, 0, 0));
		world.TaskStarted -= OnTaskStarted;
		world.StepFailed -= OnStepFailed;
	}

	/// <summary>
	/// Saves the world and loads it again, as saving and loading a game does, so the report shows what a loaded game has.
	/// </summary>
	private static GameWorld SaveAndLoad(GameData gameData, GameWorld world, Random random)
	{
		var settings = new SaveSettings(nameof(VillageReport), "", "", [], "");
		var json = SaveFile.Create(world, settings, DateTimeOffset.UnixEpoch).Serialize();
		var loaded = SaveFile.Deserialize(json).Load(gameData, settings.GameTitle, random);
		loaded.IsHeadless = world.IsHeadless;
		return loaded;
	}

	private void WriteHeaders(GameData gameData)
	{
		WriteRecord("#village", "village", "seed", "households", "people", "businesses", "quarters", "houses", "businessLocations", "reusedTemplates", "shape", "landmarks", "paths", "deadEnds", "landmarkDeadEnds", "maxExits", "features", "crossings", "time", "season");
		WriteRecord("#household", "village", "household", "lastName", "members", "wealth", "fortune", "wealthLevel", "house", "quarter", "quarterKind");
		WriteRecord(["#person", "village", "household", "member", "role", "isPlayer", "work", "businessProfession", "fullName", "description", "location", "place", "portraitLayers", "goal", "task", "taskEnd", "step", "onHold", "bundles", "remembered", "replacedClothes", "friends",
			.. gameData.Schedules.Needs.Select(x => x.Id), .. gameData.CharacterProperties.Select(x => x.Id)]);
		WriteRecord("#activity", "village", "time", "day", "household", "member", "goal", "step", "task", "minutes", "location", "destination");
		WriteRecord("#failure", "village", "time", "household", "member", "need", "step", "kind", "cause", "location");
		WriteRecord("#relationship", "village", "household", "member", "otherHousehold", "otherMember", "score", "isFriend", "familyTie", "isNeighbor", "lastTalked");
		WriteRecord("#appointment", "village", "household", "member", "clientHousehold", "clientMember", "need", "step", "place", "time", "workerOnTheWay", "clientOnTheWay", "started");
		WriteRecord("#job", "village", "household", "member", "clientHousehold", "clientMember", "need", "place", "minutes", "minutesDone");
		WriteRecord("#event", "village", "time", "kind", "household", "member", "otherHousehold", "otherMember", "location", "place", "need", "step", "cause", "text");
		WriteRecord("#sighting", "village", "household", "member", "otherHousehold", "otherMember", "location", "time", "activity", "need", "doing", "destination", "source", "toldGoal", "toldDestination", "toldDoing");
		WriteRecord("#business", "village", "household", "profession", "ownerRole", "helpers", "location", "quarter", "quarterKind");
		WriteRecord("#clothing", "village", "household", "member", "clothing", "colors", "outfit", "kind", "place", "description", "readyAt", "delayActivity");
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
		var relationships = gameData.Relationships;

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
				var task = person.CurrentTask is { } currentTask ? GetTaskName(currentTask) : null;
				var taskEnd = person.TaskEnd is { } end ? gameData.Calendar.FormatTime(end, TimeFormat.Long) : null;
				var needs = gameData.Schedules.Needs.Select(x => x.Kind == NeedKind.Level ? person.GetLevel(x).ToString("0.##", CultureInfo.InvariantCulture) : (object) person.GetProgress(x)).ToList();
				var goal = person.Goal?.Id;

				var description = person.Description;

				// Portraits are drawn where the person starts, or at work in their own workplace, dressed for work.
				if (m_arePortraitsAtWork)
				{
					person.Location = person.Workplace;
					person.IsAtWork = true;
					person.Clothing.TakeOffAll(person.Home.Items);
					ClothingGenerator.DressAtStart(person, isAtWork: true);
				}

				var portraitLayers = gameData.PortraitRenderer?.GetLayerIds(person.Traits, person.PortraitPlace, person.Clothing.GetPortraitClothing()) ?? [];
				WriteRecord(["person", village, index, member, person.Role, person.IsPlayer, work, person.Business?.Profession.Id,
					startTraits.GetFullName(gameData.CharacterTemplates), description, names[startLocation], startPlace, string.Join(" ", portraitLayers), goal, task, taskEnd, person.Current?.Step, person.Sequences.Count < 2 ? null : string.Join(" ", person.Sequences.Where(x => x != person.Current).Select(x => $"{x.Need.Id}@{x.Step}{(x.HasSetOff ? null : $"~{x.SetOff}")}")),
					person.Sequences.SelectMany(x => x.Bundles).ToList() is { Count: > 0 } bundles ? string.Join(" ", bundles.Select(x => $"{x.Key}={x.Value.Items.Count}")) : null, person.Sequences.SelectMany(x => x.RememberedPlaces).ToList() is { Count: > 0 } remembered ? string.Join(" ", remembered.Select(x => $"{x.Key}={names[x.Value]}")) : null, person.ReplacedClothes.Count == 0 ? null : string.Join(" ", person.ReplacedClothes.Select(x => $"{JsonNamingPolicy.CamelCase.ConvertName(x.Key.Kind.ToString())}={x.Value.Count}")), person.Relationships.Values.Count(x => relationships.IsFriend(x.Score)), .. needs,
					.. gameData.CharacterProperties.Select(x => FormatValue(startTraits.Values.GetValueOrDefault(x.Id)))]);

				if (m_portraitFolder is not null && gameData.PortraitRenderer is { } portraitRenderer)
					WritePortrait(portraitRenderer, person.Traits, person.PortraitPlace, person.Clothing.GetPortraitClothing(), Path.Combine(m_portraitFolder, FormattableString.Invariant($"{village}-{index}-{member}.png")));
			}
		}

		// People are named by their household and member numbers, as in the person records.
		foreach (var relationship in world.Relationships)
		{
			var (first, second) = (relationship.First, relationship.Second);
			WriteRecord("relationship", village, families[first.Family], first.Family.Members.ToList().IndexOf(first), families[second.Family], second.Family.Members.ToList().IndexOf(second),
				relationship.Score, relationships.IsFriend(relationship.Score), GetFamilyTie(gameData, first, second), first.Home.Quarter is { } quarter && quarter == second.Home.Quarter,
				relationship.LastTalked is { } lastTalked ? gameData.Calendar.FormatTime(lastTalked, TimeFormat.Long) : null);
		}

		// The appointments still to keep, with the worker first.
		foreach (var appointment in world.Appointments)
		{
			var (worker, client) = (appointment.Worker, appointment.Client);
			WriteRecord("appointment", village, families[worker.Family], worker.Family.Members.ToList().IndexOf(worker), families[client.Family], client.Family.Members.ToList().IndexOf(client),
				appointment.Need.Id, appointment.Step, names[appointment.Place], gameData.Calendar.FormatTime(appointment.Time, TimeFormat.Long), appointment.IsOnTheWay(worker), appointment.IsOnTheWay(client), appointment.IsStarted);
		}

		foreach (var job in world.Jobs)
		{
			var (worker, client) = (job.Worker, job.Client);
			WriteRecord("job", village, families[worker.Family], worker.Family.Members.ToList().IndexOf(worker), families[client.Family], client.Family.Members.ToList().IndexOf(client),
				job.Need.Id, names[job.Place], job.Minutes, job.MinutesDone);
		}

		// What each person last saw of everyone else, with whoever told them, if anyone, as "household-member".
		var members = world.Families.SelectMany((family, index) => family.Members.Select((person, member) => (person, index, member))).ToDictionary(x => x.person, x => (x.index, x.member));
		foreach (var person in world.People)
		{
			foreach (var (other, sighting) in person.Sightings.OrderBy(x => members[x.Key]))
			{
				WriteRecord("sighting", village, members[person].index, members[person].member, members[other].index, members[other].member, names[sighting.Location],
					gameData.Calendar.FormatTime(sighting.Time, TimeFormat.Long), JsonNamingPolicy.CamelCase.ConvertName(sighting.Activity.ToString()), sighting.Need?.Id, sighting.Doing,
					sighting.Destination is { } destination ? names[destination] : null,
					sighting.Source is { } source ? FormattableString.Invariant($"{members[source].index}-{members[source].member}") : null,
					sighting.Plan is null ? null : sighting.Plan.Goal?.Id ?? "idle", sighting.Plan?.Destination is { } told ? names[told] : null, sighting.Plan?.Doing);
			}
		}

		// What the player saw happen where they were, as the event log has it.
		foreach (var entry in world.EventLog.Entries)
		{
			WriteRecord("event", village, gameData.Calendar.FormatTime(entry.Time, TimeFormat.Long), JsonNamingPolicy.CamelCase.ConvertName(entry.Kind.ToString()),
				members[entry.Person].index, members[entry.Person].member, entry.Other is { } other ? members[other].index : null, entry.Other is { } otherMember ? members[otherMember].member : null,
				names[entry.Location], entry.Place is { } place ? names[place] : null, entry.Need?.Id, entry.Step, entry.Cause is { } cause ? JsonNamingPolicy.CamelCase.ConvertName(cause.ToString()) : null,
				LogEntryText.ToPlainText(LogEntryText.Format(entry)));
		}

		// Each piece of clothing, with its owner, the first outfit it's part of (accessories can be part of several), and where it is: worn, carried, or in a location.
		var outfits = world.People.SelectMany(x => x.Outfits).SelectMany(outfit => outfit.Pieces.Select(piece => (piece, outfit))).GroupBy(x => x.piece).ToDictionary(x => x.Key, x => x.First().outfit);
		foreach (var piece in world.Objects.OfType<ClothingPiece>())
		{
			var outfit = outfits.GetValueOrDefault(piece);
			var place = piece.Wearer is not null ? "worn" : piece.Container is { } container && world.People.FirstOrDefault(x => x.Inventory == container) is not null ? "carried" :
				world.Locations.FirstOrDefault(x => x.Items == piece.Container) is { } location ? names[location] : null;
			WriteRecord("clothing", village, piece.Owner is { } owner ? members[owner].index : null, piece.Owner is { } member ? members[member].member : null, piece.Clothing.Id,
				string.Join(" ", piece.Colors.Select(x => $"{x.Key}={x.Value.Id}")), outfit?.Template?.Id, outfit is null ? null : JsonNamingPolicy.CamelCase.ConvertName(outfit.Kind.ToString()),
				place, piece.Description, piece.ReadyAt is { } readyAt ? gameData.Calendar.FormatTime(readyAt, TimeFormat.Long) : null, piece.DelayActivity);
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
	/// <summary>
	/// Gets how two people are related if they're in the same household, such as "spouse", or null if they aren't.
	/// </summary>
	private static string? GetFamilyTie(GameData gameData, Person first, Person second)
	{
		var families = gameData.Families;
		var headRelation = families.Roles.Properties.HeadRelation;
		return first.Family != second.Family ? null : JsonNamingPolicy.CamelCase.ConvertName(families.Roles.HeadRelations.GetFamilyTie(first.Traits.GetValueId(headRelation), second.Traits.GetValueId(headRelation)).ToString());
	}

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

	private static string GetTaskName(PersonTask task) => task switch
	{
		TravelTask => "travel",
		BusyTask => "busy",
		TalkTask => "talk",
		WaitTask => "wait",
		FollowTask => "follow",
		MeetTask => "meet",
		AtWorkTask { IsAtWork: true } => "startWork",
		ChangeClothesTask { IsTakingOff: false } => "changeClothes",
		ChangeClothesTask => "changeBack",
		_ => "stopWork",
	};

	private static object? FormatValue(object? value) => value is CharacterPropertyOption option ? option.Id : value;

	/// <summary>
	/// Draws a character's portrait at its full size and saves it as a PNG file.
	/// </summary>
	private static void WritePortrait(PortraitRenderer renderer, CharacterTraits traits, PortraitPlace? place, PortraitClothing clothing, string filePath)
	{
		var size = renderer.Size;
		var visual = new DrawingVisual();
		using (var context = visual.RenderOpen())
			context.DrawImage(renderer.Render(traits, place, clothing), new Rect(size));

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
	private readonly int m_simulateDays;
	private int? m_village;
}
