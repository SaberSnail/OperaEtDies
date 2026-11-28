using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Town;
using static OperaEtDies.Test.TestData;

namespace OperaEtDies.Test.GameState;

/// <summary>
/// Steps carried out whenever steps are left ("finally"), finishing up steps that are given up, and the player keeping a
/// bundle to finish later.
/// </summary>
[TestFixture]
public sealed class FinallyTests
{
	[Test]
	public void FinallyStepsAreCarriedOutHoweverTheStepsAreLeft()
	{
		// Finishing the steps, or giving them up, carries out the need's "finally" steps.
		Assert.That(Run(Errand([Spend(3)], [Spend(4)])), Is.EqualTo(new[] { 3, 4 }));
		Assert.That(Run(Errand([Nowhere(), Spend(3)], [Spend(4)])), Is.EqualTo(new[] { 4 }));

		// Skipping out of a group carries out its "finally" steps first, and the need's come at the end.
		var group = new Step { Kind = StepKind.Group, Steps = [Nowhere(new StepFailure { Any = new StepFailureResult(StepFailureKind.SkipTo, "after") }), Spend(3)], Finally = [Spend(4)] };
		Assert.That(Run(Errand([group, Spend(7, "after")], [Spend(11)])), Is.EqualTo(new[] { 4, 7, 11 }));

		// Giving up inside a group carries out its "finally" steps, then the need's.
		var stopping = new Step { Kind = StepKind.Group, Steps = [Nowhere()], Finally = [Spend(4)] };
		Assert.That(Run(Errand([stopping, Spend(7)], [Spend(11)])), Is.EqualTo(new[] { 4, 11 }));

		// A "finally" step that fails goes on to the next one.
		Assert.That(Run(Errand([Spend(3)], [Nowhere(), Spend(4)])), Is.EqualTo(new[] { 3, 4 }));
	}

	[Test]
	public void ABundleMadeInPlaceOfAnotherCarriesItsThings()
	{
		var need = Errand([
			new Step { Kind = StepKind.Take, ItemKind = StepItemKind.Clothing, From = StepItemPlace.Wardrobe, Bundle = Bundle("washing", "Washing"), Accessories = false },
			new Step { Kind = StepKind.Make, Bundle = Bundle("wet", "Wet washing"), Replaces = "washing" },
			Spend(5),
		]);
		var (world, person) = CreateWorld(need, isPlayer: false);
		Item item = world.CreateBundle(person, "Shirt", []);
		var sequence = person.SetPlan(need, [], startStep: 1)!;
		sequence.AddBundle("washing", world.CreateBundle(person, "Washing", [item]));
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 0, 1));

		Assert.That(sequence.Bundles.Keys, Is.EqualTo(new[] { "wet" }));
		Assert.That(sequence.Bundles["wet"].Items, Is.EqualTo(new[] { item }));
	}

	[Test]
	public async Task ThePlayerCanFinishUpTheStepsTheyStopAsync()
	{
		var need = Errand(
			[new Step { Kind = StepKind.Do, Minutes = Amount(30), Player = new StepPlayer { Title = "Work", Result = "You worked" } }],
			[new Step { Kind = StepKind.Do, Minutes = Amount(5), Player = new StepPlayer { Title = "Tidy up", Result = "You tidied up" } }]);
		var (world, player) = CreateWorld(need, isPlayer: true);
		await world.StartPlayerStepsAsync(need, isAutomatic: false);
		var sequence = player.Current!;
		Assert.That(world.GetPlayerStopOptions(sequence), Is.EqualTo(new PlayerStopOptions(true, null)));

		// Finishing up, only the "finally" steps are left, which they take as they would any others.
		world.FinishUpPlayerSteps(sequence);
		Assert.That((sequence.IsFinishingUp, sequence.Step), Is.EqualTo((true, 1)));
		Assert.That(world.GetPlayerSteps(sequence).Select(x => x.StepIndex), Is.EqualTo(new[] { 1 }));
		Assert.That(world.GetPlayerStopOptions(sequence).HasChoice, Is.False);
		var loaded = SaveAndLoad(world).Player.Current!;
		Assert.That((loaded.IsFinishingUp, loaded.UnwindRunning, loaded.UnwindTo), Is.EqualTo((true, sequence.UnwindRunning, sequence.UnwindTo)));

		await world.DoPlayerStepAsync(sequence);
		Assert.That(player.Sequences, Is.Empty);
	}

	[TestCase(true)]
	[TestCase(false)]
	public void ThePlayerKeepsABundleToFinishLaterWhileOthersDropIt(bool isPlayer)
	{
		var need = Errand([
			new Step { Kind = StepKind.Make, Bundle = Bundle("bucket", "Empty bucket") },
			Spend(5),
			new Step { Kind = StepKind.Make, Bundle = Bundle("water", "Bucket of water"), Replaces = "bucket" },
			new Step { Kind = StepKind.GoTo, Place = Home },
			new Step { Kind = StepKind.Put, Bundle = Bundle("water") },
		]);
		var (world, person) = CreateWorld(need, isPlayer);
		var sequence = Scheduler.StartSequence(person, need, 1, null, [], null);
		sequence.AddBundle("water", world.CreateBundle(person, "Bucket of water", []));
		new Scheduler(world).FailStep(person, sequence, FailCause.Hours, world.Time);

		if (isPlayer)
		{
			// The player carries on from taking it home, and is reminded of it once they've carried it for a while.
			Assert.That((person.Sequences.Contains(sequence), sequence.Step, sequence.KeptSince), Is.EqualTo((true, 3, (GoldenAnvil.Utility.Calendar.TimePoint?) world.Time)));
			Assert.That(world.GetPlayerStopOptions(sequence).Kept, Is.SameAs(sequence.Bundles["water"]));
			Assert.That(world.IsPlayerCarryingTooLong(sequence), Is.False);
			Assert.That(SaveAndLoad(world).Player.Sequences.Single(x => x.Need.Id == need.Id).KeptSince, Is.EqualTo(sequence.KeptSince));
		}
		else
		{
			Assert.That(person.Sequences, Does.Not.Contain(sequence));
		}
	}

	[Test]
	public void StepsGivenUpToSetOffForAnAppointmentFinishUpFirst()
	{
		// The client is working at something with steps to finish up when it's time to set off for an appointment.
		var chore = Errand([Spend(600)], [Spend(5)]);
		var lesson = new Need
		{
			Id = "lesson", Title = "Lesson", Icon = "icon", Category = NeedCategory.Personal, Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 },
			StepSettings = [new Step
			{
				Kind = StepKind.Ask, With = StepTarget.Anyone, Player = new StepPlayer { Title = "Ask" }, Name = "agreed", Minutes = Amount(5), Chance = new StepChance { Base = 100 },
				Appointment = new StepAppointment { Place = new SchedulePlace { Kind = NeedPlace.PartnerHome }, Gap = 120, Patience = 60, Steps = [Meet(), Spend(30)] },
			}],
		};
		lesson.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		var (world, client) = CreateWorld(chore, isPlayer: false, lesson);
		var worker = world.Npcs.First(x => x.Family != client.Family);
		var choring = client.SetPlan(chore, [], startStep: 0)!;
		var appointment = world.AddAppointment(worker, client, lesson, 0, client.Home, world.GameData.Calendar.AddMinutes(world.Time, 60));

		// Setting off gives up the chore, which finishes up first, before going to the appointment.
		new Scheduler(world).TakeUpAppointment(appointment.GetSequence(client)!, client, world.Time);
		Assert.That((client.Current, choring.IsFinishingUp, appointment.GetSequence(client)!.HasSetOff), Is.EqualTo((choring, true, true)));
		var busy = new List<int>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == client && e.Task is BusyTask { Minutes: { } minutes } && client.Goal == chore)
				busy.Add(minutes);
		};
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 0, 10));
		Assert.That((busy, client.Sequences.Contains(choring), client.Current), Is.EqualTo((new[] { 5 }, false, appointment.GetSequence(client))));
	}

	private static Step Spend(int minutes, string? id = null) => new() { Kind = StepKind.Do, Minutes = Amount(minutes), Id = id };

	private static Step Nowhere(StepFailure? onFail = null) => new() { Kind = StepKind.GoTo, Place = new SchedulePlace { Kind = NeedPlace.Remembered, Remembered = "nowhere" }, OnFail = onFail };

	/// <summary>
	/// Creates a level need with the given steps and "finally" steps, which never grows, so nothing else interferes with it.
	/// </summary>
	private static Need Errand(IReadOnlyList<Step> steps, IReadOnlyList<Step>? finallySteps = null)
	{
		var need = new Need { Id = "errand", Title = "Errand", Icon = "icon", Category = NeedCategory.Personal, StepSettings = steps, Finally = finallySteps, Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 } };
		need.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		return need;
	}

	/// <summary>
	/// Has someone at home carry out a need's steps, and gets the minutes of each step of it they spent time on, in order.
	/// </summary>
	private static IReadOnlyList<int> Run(Need need)
	{
		var (world, person) = CreateWorld(need, isPlayer: false);
		var busy = new List<int>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is BusyTask { Minutes: { } minutes, ResumeMinutes: null } && person.Goal == need)
				busy.Add(minutes);
		};
		person.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 2, 0));
		return busy;
	}

	private static GameWorld SaveAndLoad(GameWorld world)
	{
		var settings = new SaveSettings("Title", "", "", [], "");
		var json = SaveFile.Create(world, settings, DateTimeOffset.UnixEpoch).Serialize();
		var loaded = SaveFile.Deserialize(json).Load(world.GameData, settings.GameTitle, new Random(0));
		Assert.That(SaveFile.Create(loaded, settings, DateTimeOffset.UnixEpoch).Serialize(), Is.EqualTo(json));
		return loaded;
	}

	/// <summary>
	/// Creates a world with the test schedules and a need, and the player, or someone else with the game deciding for
	/// everyone, at home with nothing to do.
	/// </summary>
	private static (GameWorld World, Person Person) CreateWorld(Need need, bool isPlayer, params Need[] others)
	{
		var gameData = CreateGameData(withSchedules: true);
		gameData.Schedules.StepLists = [];
		gameData.Schedules.Needs = [.. gameData.Schedules.Needs, need, .. others];
		var random = new Random(0);
		var world = GameWorld.CreateFromTown(gameData, TownGenerator.Generate(gameData, CreatePlayerTraits(gameData), random), random);
		world.IsHeadless = !isPlayer;
		var person = isPlayer ? world.Player : world.Npcs[0];
		person.Location = person.Home;
		return (world, person);
	}
}
