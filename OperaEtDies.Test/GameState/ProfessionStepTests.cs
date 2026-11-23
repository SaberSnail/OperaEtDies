using GoldenAnvil.Utility;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Town;
using static OperaEtDies.Test.TestData;

namespace OperaEtDies.Test.GameState;

/// <summary>
/// Professions with steps of their own, which replace the work need's steps for the people in their businesses, and
/// steps that wander.
/// </summary>
[TestFixture]
public sealed class ProfessionStepTests
{
	[Test]
	public void ProfessionStepsReplaceTheWorkNeedsStepsForTheirBusinesses()
	{
		var gameData = CreateGameData(withSchedules: true);
		gameData.Schedules.ProfessionSteps = new Dictionary<string, IReadOnlyList<Step>>(StringComparer.Ordinal) { ["miller"] = MillerSteps() };
		var work = gameData.Schedules.Needs.Single(x => x.Id == "work");

		// The work need starts by choosing the steps of the person's business's profession, or its own.
		Assert.That(work.Steps[0] is { Kind: StepKind.Choose, First: true }, Is.True);
		Assert.That(work.GetOptions(0).Select(x => x.Option.When?.BusinessProfession), Is.EqualTo(new[] { "miller", null }));
		Assert.That((work.IsWork, work.Place), Is.EqualTo((true, (NeedPlace?) NeedPlace.Workplace)));
		Assert.That(work.GetActivityAt(_ => true, "miller"), Is.EqualTo("Grinding for a neighbor"));
		Assert.That(work.GetActivityAt(_ => true, "smith"), Is.EqualTo("Work"));
	}

	[Test]
	public void ProfessionsCanSayWhatTheirWorkersAreDoing()
	{
		var gameData = CreateGameData(withSchedules: true);
		gameData.Schedules.ProfessionActivities = new Dictionary<string, string>(StringComparer.Ordinal) { ["smith"] = "Smithing" };
		var work = gameData.Schedules.Needs.Single(x => x.Id == "work");
		var upkeep = gameData.Schedules.Needs.Single(x => x.Id == "upkeep");
		var step = work.Steps.ToList().FindIndex(x => x.Kind == StepKind.Do);

		// Only work shows the profession's activity, and a step's own activity comes first.
		Assert.That((work.GetActivity(step, "smith"), work.GetActivity(step, "miller"), work.GetActivity(step)), Is.EqualTo(("Smithing", "Work", "Work")));
		Assert.That(upkeep.GetActivity(1, "smith"), Is.EqualTo("Upkeep"));
		var laundry = gameData.Schedules.Needs.Single(x => x.Id == "laundry");
		Assert.That(laundry.GetActivity(5, "smith"), Is.EqualTo("Hanging out the washing"));
	}

	[Test]
	public void WorkersWithProfessionStepsWorkWhereTheirStepsSay()
	{
		var (world, work) = CreateWorld(new Dictionary<string, IReadOnlyList<Step>>(StringComparer.Ordinal) { ["miller"] = MillerSteps() }, x => x.Business?.Profession.Id == "miller");
		var miller = world.Npcs.First(x => x.Business?.Profession.Id == "miller");
		var smith = world.Npcs.FirstOrDefault(x => x.Business?.Profession.Id == "smith");
		var places = new Dictionary<Person, Location>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Task is BusyTask && e.Person.Goal == work && !places.ContainsKey(e.Person))
				places[e.Person] = e.Person.Location;
		};

		foreach (var person in new[] { miller, smith }.OfType<Person>())
		{
			person.SetProgress(work, 0);
			person.SetPlan(work, [], startStep: 0, goalMinutes: 60);
		}
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 2, 0));

		// The miller works in another household's home, and a smith at their workplace, as usual.
		Assert.That(places.TryGetValue(miller, out var millerPlace), Is.True);
		Assert.That((millerPlace!.IsHouse, millerPlace == miller.Home), Is.EqualTo((true, false)));
		if (smith is not null)
			Assert.That(places.GetValueOrDefault(smith), Is.SameAs(smith.Workplace));
	}

	[Test]
	public async Task ThePlayersWorkActionFollowsTheirProfessionsStepsAsync()
	{
		var (world, work) = CreateWorld(new Dictionary<string, IReadOnlyList<Step>>(StringComparer.Ordinal) { ["smith"] = MillerSteps() }, x => x.Business is not null && x.IsPlayer);
		var player = world.Player;
		var house = world.Families.Select(x => x.Home).First(x => x != player.Home);

		// The player (a smith here) follows the profession's steps: in someone else's home, grinding there is next.
		player.Location = house;
		await world.StartPlayerStepsAsync(work, isAutomatic: false);
		Assert.That(player.Current?.CurrentStep?.Activity, Is.EqualTo("Grinding for a neighbor"));
	}

	[Test]
	public void WanderingMovesBetweenQuartersOfItsKinds()
	{
		var need = new Need
		{
			Id = "stroll",
			Title = "Stroll",
			StepSettings =
			[
				new Step { Kind = StepKind.Wander, Place = Quarters(false, "square", "landmark"), Minutes = Amount(90), StopMinutes = new IntegerRange { Minimum = 10, Maximum = 10 } },
				new Step { Kind = StepKind.Do, Minutes = Amount(5) },
			],
			Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 },
		};
		var gameData = CreateGameData(withSchedules: true);
		gameData.Schedules.Needs = [.. gameData.Schedules.Needs, need];
		var random = new Random(0);
		var world = GameWorld.CreateFromTown(gameData, TownGenerator.Generate(gameData, CreatePlayerTraits(gameData), random), random);
		world.IsHeadless = true;
		var person = world.Npcs[0];
		var stops = new List<(Location Location, int Minutes)>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is BusyTask { Minutes: { } minutes, ResumeMinutes: null } && person.Goal == need)
				stops.Add((person.Location, minutes));
		};

		person.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 3, 0));

		// They stay ten minutes at each of several quarters of the kinds, then carry on with the next step.
		var wanders = stops.Take(stops.Count - 1).ToList();
		Assert.That(wanders.Count, Is.GreaterThan(1));
		Assert.That(wanders.Select(x => x.Location.QuarterKind), Is.All.AnyOf("square", "landmark"));
		Assert.That(wanders.Select(x => x.Minutes), Is.All.LessThanOrEqualTo(10));
		Assert.That(stops[^1].Minutes, Is.EqualTo(5));
		Assert.That(person.Current?.Until, Is.Null);
	}

	[Test]
	public void ProfessionStepsAndWanderingMustBeValid()
	{
		var settings = CreateScheduleSettings();
		settings.ProfessionSteps = new Dictionary<string, IReadOnlyList<Step>>(StringComparer.Ordinal)
		{
			["miller"] = [new Step { Kind = StepKind.Wander, Place = Home }],
			["idle"] = [new Step { Kind = StepKind.Do, Minutes = Amount(5) }],
		};

		Assert.That(settings.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.StepWanderPlaceInvalid.FormatCurrentCulture("professions[miller].business.steps[0].place"),
			OurResources.StepSettingMissing.FormatCurrentCulture("professions[miller].business.steps[0].minutes", "wander"),
			OurResources.StepSettingMissing.FormatCurrentCulture("professions[miller].business.steps[0].stopMinutes", "wander"),
			OurResources.ProfessionStepsWithoutDo.FormatCurrentCulture("miller"),
			OurResources.ProfessionStepsWithoutMeetsQuota.FormatCurrentCulture("miller", "work"),
			OurResources.ProfessionStepsWithoutMeetsQuota.FormatCurrentCulture("idle", "work"),
		}));
	}

	/// <summary>
	/// A miller's steps: grinding in another household's home, for the time chosen.
	/// </summary>
	private static IReadOnlyList<Step> MillerSteps() =>
	[
		GoTo(new SchedulePlace { Kind = NeedPlace.OtherHome }),
		new Step
		{
			Kind = StepKind.Do, MeetsQuota = true, AtWork = true, Activity = "Grinding for a neighbor",
			Player = new StepPlayer { Title = "Grind", Result = "You ground" },
		},
	];

	/// <summary>
	/// Creates a world with the test schedules and the given professions' steps, from the first seed whose town has someone
	/// matching a condition.
	/// </summary>
	private static (GameWorld World, Need Work) CreateWorld(IReadOnlyDictionary<string, IReadOnlyList<Step>> professionSteps, Func<Person, bool> isWanted)
	{
		for (var seed = 0; seed < 50; seed++)
		{
			var gameData = CreateGameData(withSchedules: true);
			gameData.Schedules.ProfessionSteps = professionSteps;
			var random = new Random(seed);
			var world = GameWorld.CreateFromTown(gameData, TownGenerator.Generate(gameData, CreatePlayerTraits(gameData), random), random);
			if (world.People.Any(isWanted))
			{
				world.IsHeadless = !world.People.Where(isWanted).All(x => x.IsPlayer);
				world.DeclineTalk();
				return (world, gameData.Schedules.Needs.Single(x => x.Id == "work"));
			}
		}

		throw new InvalidOperationException("No town had the person wanted.");
	}
}
