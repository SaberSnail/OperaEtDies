using OperaEtDies.Data;
using OperaEtDies.GameState;
using static OperaEtDies.Test.TestData;

namespace OperaEtDies.Test.GameState;

/// <summary>
/// The player's sequences of steps: starting them, taking each step themselves or having it done for them, choosing,
/// keeping going, being in the middle of more than one, and failing.
/// </summary>
[TestFixture]
public sealed class PlayerStepsTests
{
	[Test]
	public async Task StartingPassesStepsAlreadyDoneAndStopsAtOnesThePlayerTakesAsync()
	{
		var (world, _) = CreateWorld();
		var player = world.Player;
		var laundry = GetNeed(world, "laundry");
		player.Location = player.Home;

		// Starting laundry at home, the player is already where its first step goes, so gathering the washing is next.
		var choice = world.GetPlayerStartChoices().Single(x => x.Need == laundry);
		Assert.That((choice.CanStart, choice.IsInProgress), Is.EqualTo((true, false)));
		await world.StartPlayerStepsAsync(laundry, isAutomatic: false);
		Assert.That(player.Current?.Step, Is.EqualTo(1));
		Assert.That(world.GetPlayerStartChoices().Single(x => x.Need == laundry).IsInProgress, Is.True);

		// The steps shown start there, with going to the river leading to the nearest place of its kinds.
		var steps = world.GetPlayerSteps(player.Current!);
		Assert.That(steps.Select(x => x.StepIndex), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
		Assert.That(steps[0].IsCurrent, Is.True);
		Assert.That(steps[1].Place?.QuarterKind, Is.AnyOf("square", "landmark"));
	}

	[Test]
	public async Task ThePlayerTakesStepsThemselvesAndWithButtonsAsync()
	{
		var (world, square) = CreateWorld();
		var player = world.Player;
		var water = GetNeed(world, "water");
		player.Location = square;
		await world.StartPlayerStepsAsync(water, isAutomatic: false);
		var sequence = player.Current!;

		// Fetching the water is a button, after which they carry a bucket of water and have to take it home.
		Assert.That(sequence.Step, Is.EqualTo(1));
		var start = world.Time;
		await world.DoPlayerStepAsync(sequence);
		Assert.That(world.GameData.Calendar.GetTotalMinutes(world.Time - start), Is.EqualTo(5));
		Assert.That((sequence.Step, player.GetBundles().Keys.Single()), Is.EqualTo((3, "Bucket of water")));

		// Walking home themselves gets them to putting it away, which finishes it.
		await world.MovePlayerAsync(player.Home);
		Assert.That(sequence.Step, Is.EqualTo(4));
		await world.DoPlayerStepAsync(sequence);
		Assert.That((player.Sequences.Count, player.GetProgress(water), player.GetBundles().Count), Is.EqualTo((0, 1L, 0)));
	}

	[Test]
	public async Task StepsCanAllBeDoneForThePlayerAsync()
	{
		var (world, _) = CreateWorld();
		var player = world.Player;
		var laundry = GetNeed(world, "laundry");
		player.Location = player.Home;
		var start = world.Time;
		await world.StartPlayerStepsAsync(laundry, isAutomatic: true);

		// Washing and hanging out the washing take their time, and the washing is put away.
		var members = player.Family.Members.Count;
		Assert.That(world.GameData.Calendar.GetTotalMinutes(world.Time - start), Is.GreaterThanOrEqualTo(30 * members));
		Assert.That((player.Location, player.Sequences.Count, player.GetProgress(laundry), player.GetBundles().Count), Is.EqualTo((player.Home, 0, 1L, 0)));
	}

	[Test]
	public async Task LeavingWhereAStepIsDoneFailsItAsync()
	{
		var (world, square) = CreateWorld();
		var player = world.Player;
		var water = GetNeed(world, "water");
		player.Location = square;
		await world.StartPlayerStepsAsync(water, isAutomatic: false);
		var failures = new List<FailCause>();
		world.StepFailed += (_, e) => failures.Add(e.Cause);

		// Walking off before fetching the water gives it up, as the need says nothing else.
		await world.MovePlayerAsync(player.Home);
		Assert.That((failures, player.Sequences.Count), Is.EqualTo((new[] { FailCause.Left }, 0)));
	}

	[Test]
	public async Task ThePlayerCanBeInTheMiddleOfMoreThanOneThingAsync()
	{
		var (world, square) = CreateWorld();
		var player = world.Player;
		var water = GetNeed(world, "water");
		var upkeep = GetNeed(world, "upkeep");
		player.Location = square;
		await world.StartPlayerStepsAsync(water, isAutomatic: false);
		var fetching = player.Current!;
		await world.DoPlayerStepAsync(fetching);

		// Starting upkeep puts the water on hold, still on its way home.
		await world.StartPlayerStepsAsync(upkeep, isAutomatic: false);
		var tidying = player.Current!;
		Assert.That((player.Sequences, fetching.Step), Is.EqualTo((new[] { fetching, tidying }, 3)));

		// Getting home does both of their walks home, and the water can be put away once it's switched to.
		await world.MovePlayerAsync(player.Home);
		Assert.That((fetching.Step, tidying.Step), Is.EqualTo((4, 1)));
		world.FocusPlayerSteps(fetching);
		await world.DoPlayerStepAsync(fetching);
		Assert.That((player.Sequences, player.GetProgress(water)), Is.EqualTo((new[] { tidying }, 1L)));
	}

	[Test]
	public async Task ThePlayerChoosesAmongTheOptionsAsync()
	{
		var (world, _) = CreateWorld();
		var player = world.Player;
		player.Location = player.Home;
		var need = AddNeed(new Need
		{
			Id = "choosing", Title = "Choosing", Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 },
			StepSettings =
			[
				new Step
				{
					Kind = StepKind.Choose,
					Options =
					[
						new StepOption { Player = new StepOptionPlayer { Title = "Rest" }, Steps = [Do(Amount(10), new StepPlayer { Title = "Rest", Result = "You rested" })] },
						new StepOption { Player = new StepOptionPlayer { Title = "Stretch" }, Steps = [Do(Amount(5), new StepPlayer { Title = "Stretch", Result = "You stretched" })] },
					],
				},
				Do(Amount(1)),
			],
		});
		await world.StartPlayerStepsAsync(need, isAutomatic: false);
		var sequence = player.Current!;

		// The options are shown, followed by the steps of whichever is chosen and the step after them.
		Assert.That(world.GetPlayerSteps(sequence).Select(x => x.Kind),
			Is.EqualTo(new[] { PlayerStepKind.Option, PlayerStepKind.Option, PlayerStepKind.OptionSteps, PlayerStepKind.Step }));
		var options = world.GetPlayerOptions(sequence);
		Assert.That(options.Select(x => x.Player?.Title), Is.EqualTo(new[] { "Rest", "Stretch" }));
		Assert.That(world.CanDoPlayerStep(sequence), Is.False);

		await world.ChoosePlayerOptionAsync(sequence, options[1]);
		Assert.That(sequence.CurrentStep?.Player?.Title, Is.EqualTo("Stretch"));
	}

	[Test]
	public async Task ThePlayerIsAskedWhetherToKeepGoingAsync()
	{
		var (world, _) = CreateWorld();
		var player = world.Player;
		player.Location = player.Home;
		var need = AddNeed(new Need
		{
			Id = "chores", Title = "Chores", Hours = Hours(0, 24),
			Quota = new QuotaNeedSettings { Lead = 600, Time = new QuotaTime { Minutes = Amount(120), Shift = 120 } },
			StepSettings = [new Step { Kind = StepKind.Repeat, Reconsider = true, Steps = [new Step { Kind = StepKind.Do, Minutes = Amount(30), MeetsQuota = true, Player = new StepPlayer { Title = "Work", Result = "You worked" } }] }],
		});
		await world.StartPlayerStepsAsync(need, isAutomatic: false);
		var sequence = player.Current!;

		// After each pass, they're asked, with what's left of the shift.
		await world.DoPlayerStepAsync(sequence);
		Assert.That((world.IsAskingPlayerToKeepGoing(sequence), sequence.MinutesLeft), Is.EqualTo((true, 90)));
		await world.AnswerKeepGoingAsync(sequence, keepGoing: true);
		Assert.That((world.IsAskingPlayerToKeepGoing(sequence), sequence.Step), Is.EqualTo((false, 0)));
		await world.DoPlayerStepAsync(sequence);
		await world.AnswerKeepGoingAsync(sequence, keepGoing: false);
		Assert.That((player.Sequences.Count, player.GetProgress(need)), Is.EqualTo((0, 60L)));
	}

	[Test]
	public async Task StoppingGivesUpTheStepsAndKeepsWhatWasCarriedAsync()
	{
		var (world, _) = CreateWorld();
		var player = world.Player;
		var laundry = GetNeed(world, "laundry");
		player.Location = player.Home;
		await world.StartPlayerStepsAsync(laundry, isAutomatic: false);
		var sequence = player.Current!;
		await world.DoPlayerStepAsync(sequence);
		Assert.That(player.GetBundles().Keys, Is.EqualTo(new[] { "Washing" }));

		// Anything in the washing stays carried, without its bundle, and is put away when they're next at home.
		world.StopPlayerSteps(sequence);
		Assert.That((player.Sequences.Count, player.GetBundles().Count), Is.EqualTo((0, 0)));
	}

	[Test]
	public async Task StepsInProgressAreSavedAsync()
	{
		var (world, square) = CreateWorld();
		var player = world.Player;
		player.Location = square;
		await world.StartPlayerStepsAsync(GetNeed(world, "water"), isAutomatic: false);
		await world.DoPlayerStepAsync(player.Current!);

		var settings = new SaveSettings("Title", "", "", [], "");
		var json = SaveFile.Create(world, settings, DateTimeOffset.UnixEpoch).Serialize();
		var loaded = SaveFile.Deserialize(json).Load(world.GameData, settings.GameTitle, new Random(0));
		Assert.That(loaded.Player.Current?.Step, Is.EqualTo(player.Current!.Step));
		Assert.That(loaded.Player.Current!.Bundles["water"].Title, Is.EqualTo("Bucket of water"));
		Assert.That(SaveFile.Create(loaded, settings, DateTimeOffset.UnixEpoch).Serialize(), Is.EqualTo(json));
	}

	/// <summary>
	/// Creates a world where the game doesn't decide for the player and their family has no chores done, with its square.
	/// </summary>
	internal static (GameWorld World, Location Square) CreateWorld()
	{
		var (_, world) = GameWorldTests.CreateWorld(0, withSchedules: true, isHeadless: false);
		foreach (var member in world.Player.Family.Members)
		{
			member.UpdateNeeds(world.Time);
			if (!member.IsPlayer)
				member.SetPlan(null, []);
			foreach (var need in world.GameData.Schedules.Needs.Where(x => x.Kind == NeedKind.Quota))
				member.SetProgress(need, 0);
		}

		return (world, world.Locations.First(x => x.Template is QuarterTemplate && x.QuarterKind == "square"));
	}

	/// <summary>
	/// Adds a need for the player only, for a test.
	/// </summary>
	internal static Need AddNeed(Need need)
	{
		need.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		return need;
	}

	private static Need GetNeed(GameWorld world, string id) => world.GameData.Schedules.Needs.Single(x => x.Id == id);
}
