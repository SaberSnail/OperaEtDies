using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Town;
using static OperaEtDies.Test.TestData;

namespace OperaEtDies.Test.GameState;

/// <summary>
/// The player taking part in someone else's steps: being asked, keeping an appointment as its client, and being led.
/// </summary>
[TestFixture]
public sealed class TakingPartTests
{
	[Test]
	public async Task ThePlayerIsAskedAndKeepsAnAppointmentAsItsClientAsync()
	{
		var need = Errand([new Step
		{
			Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "agreed", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(5), Chance = new StepChance { Base = 0 },
			Appointment = AtPartnerHome([Do(Amount(30))]),
		}]);
		var (world, worker, player) = CreateWorld(need);
		var calendar = world.GameData.Calendar;

		// The worker asks the player, who's asked to answer, with time stopped until they do.
		worker.SetPlan(need, [], startStep: 0);
		await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 0, 30));
		Assert.That(player.AskRequest, Is.EqualTo(worker));
		Assert.That(await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 1, 0)), Is.EqualTo(calendar.CreateTimeOffset(0, 0, 0)));

		// Saying yes makes an appointment with the player as its client, however unlikely a yes would be from anyone else.
		var appointment = (await world.AnswerAskAsync(isYes: true))!;
		Assert.That((appointment.Worker, appointment.Client, player.AskRequest), Is.EqualTo((worker, player, (Person?) null)));
		Assert.That(world.GetPlayerAppointments(), Is.EqualTo(new[] { appointment }));

		// Their part waits until it's time to set off, without taking over what they're doing.
		var part = appointment.GetSequence(player)!;
		Assert.That((part.HasSetOff, player.Current), Is.EqualTo((false, (StepSequence?) null)));
		AssertSavesAndLoads(world);

		// When it's time, they set off, and their part is going there, meeting the worker, and staying with them.
		await world.AdvanceTimeAsync(calendar.CreateTimeOffset(1, 0, 0));
		Assert.That(world.GetPlayerSetOffSequence(), Is.SameAs(part));
		await world.AnswerSetOffAsync(isSettingOff: true);
		Assert.That(player.Current, Is.SameAs(part));
		Assert.That(part.Role, Is.EqualTo(SequenceRole.Client));
		Assert.That(world.GetPlayerSteps(part).Select(x => x.Kind), Is.EqualTo(new[] { PlayerStepKind.GoToAppointment, PlayerStepKind.Meet, PlayerStepKind.Follow }));
		AssertSavesAndLoads(world);
		for (var count = 0; count < 4 && !appointment.IsStarted; count++)
			await world.DoPlayerStepAsync(part);

		// Once both are there, the player follows the worker, until they're done and let them go.
		Assert.That(appointment.IsStarted, Is.True);
		Assert.That((world.GetPlayerLeader(), world.GetPlayerSteps(part).Select(x => x.Kind)), Is.EqualTo((worker, new[] { PlayerStepKind.Follow })));
		AssertSavesAndLoads(world);
		await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 2, 0));
		Assert.That((player.Sequences.Count, world.Appointments.Count), Is.EqualTo((0, 0)));
	}

	[Test]
	public async Task ThePlayerCanSayNoAsync()
	{
		var need = Errand([new Step { Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "agreed", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(5), Chance = new StepChance { Base = 100 } }, Do(Amount(5))]);
		var (world, worker, player) = CreateWorld(need);
		worker.SetPlan(need, [], startStep: 0);
		await world.AdvanceTimeAsync(world.GameData.Calendar.CreateTimeOffset(0, 0, 30));
		Assume.That(player.AskRequest, Is.EqualTo(worker));

		// However likely a yes would be from anyone else, the player's no fails the step, and the worker doesn't ask again for a while.
		await world.AnswerAskAsync(isYes: false);
		Assert.That((player.AskRequest, worker.Goal, worker.GetRelationship(player).LastDeclined is not null), Is.EqualTo(((Person?) null, (Need?) null, true)));
	}

	[Test]
	public async Task ThePlayerIsAskedToTalkForATalkStepAsync()
	{
		var need = Errand([new Step { Kind = StepKind.Talk, With = StepTarget.Anyone, Minutes = Amount(15) }, Do(Amount(5))]);
		var (world, worker, player) = CreateWorld(need);
		worker.SetPlan(need, [], startStep: 0);
		await world.AdvanceTimeAsync(world.GameData.Calendar.CreateTimeOffset(0, 0, 30));
		Assert.That((player.TalkRequest, world.IsTalkStepRequest(worker), world.GetTalkRequestMinutes(worker)), Is.EqualTo(((Person?) worker, true, 15)));

		// Agreeing, they talk for as long as the step takes.
		await world.AcceptTalkAsync();
		Assert.That((player.TalkRequest, worker.Current?.Partner), Is.EqualTo(((Person?) null, (Person?) player)));
	}

	[Test]
	public void ThePlayerTurningDownATalkStepFailsIt()
	{
		var need = Errand([new Step { Kind = StepKind.Talk, With = StepTarget.Anyone, Minutes = Amount(15) }, Do(Amount(5))]);
		var (world, worker, player) = CreateWorld(need);
		worker.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 0, 30));
		Assume.That(player.TalkRequest, Is.EqualTo(worker));

		world.DeclineTalk();
		Assert.That((player.TalkRequest, worker.Goal, worker.GetRelationship(player).LastDeclined is not null), Is.EqualTo(((Person?) null, (Need?) null, true)));
	}

	[Test]
	public async Task ThePlayerIsLedOnlyForTheLeadStepsMinutesAsync()
	{
		var need = Errand([
			new Step { Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "coming", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(5), ForMinutes = Amount(30), Chance = new StepChance { Base = 0 } },
			new Step { Kind = StepKind.Lead, Minutes = Amount(30) },
			Do(Amount(120)),
		]);
		var (world, worker, player) = CreateWorld(need);
		var calendar = world.GameData.Calendar;
		worker.SetPlan(need, [], startStep: 0);
		await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 0, 30));
		Assume.That(player.AskRequest, Is.EqualTo(worker));
		await world.AnswerAskAsync(isYes: true);
		Assume.That(world.GetPlayerLeader(), Is.EqualTo(worker));
		AssertSavesAndLoads(world);

		await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 0, 45));
		await world.UpdatePlayerStepsAsync();
		Assert.That((world.GetPlayerLeader(), player.Sequences.Count), Is.EqualTo(((Person?) null, 0)));
	}

	[Test]
	public async Task ThePlayerIsWarnedOfNeedsSayingYesWouldKeepThemFromAsync()
	{
		var need = Errand([new Step { Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "coming", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(5), ForMinutes = Amount(60), Chance = new StepChance { Base = 0 } }]);
		var (world, worker, player) = CreateWorld(need);
		worker.SetPlan(need, [], startStep: 0);
		await world.AdvanceTimeAsync(world.GameData.Calendar.CreateTimeOffset(0, 0, 30));
		Assume.That(player.AskRequest, Is.EqualTo(worker));
		Assert.That(world.GetPlayerAskConflicts(worker), Is.Empty);

		// A need that would be urgent by the time they're done is one they're warned of, though they can still say yes.
		var hunger = world.GameData.Schedules.Needs.First(x => x.Kind == NeedKind.Level && x.PerHour > 0 && x != need && x.Id != world.GameData.Schedules.SleepNeed);
		player.SetLevel(hunger, Need.MaximumLevel);
		Assert.That(world.GetPlayerAskConflicts(worker), Does.Contain(hunger));
		await world.AnswerAskAsync(isYes: true);
		Assert.That(player.AskRequest, Is.Null);
	}

	[Test]
	public async Task ThePlayerFollowsSomeoneWhoLeadsThemUntilTheyCallItOffAsync()
	{
		var need = Errand([
			new Step { Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "coming", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(5), ForMinutes = Amount(60), Chance = new StepChance { Base = 0 } },
			new Step { Kind = StepKind.Lead, Minutes = Amount(60) },
			GoTo(Home),
			Do(Amount(30)),
			new Step { Kind = StepKind.Release },
		]);
		var (world, worker, player) = CreateWorld(need);
		var calendar = world.GameData.Calendar;
		worker.SetPlan(need, [], startStep: 0);
		await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 0, 30));
		Assume.That(player.AskRequest, Is.EqualTo(worker));

		// Saying yes, the player follows the worker as part of their steps, going wherever they go while they wait.
		await world.AnswerAskAsync(isYes: true);
		Assert.That((world.GetPlayerLeader(), player.Current?.Role), Is.EqualTo((worker, (SequenceRole?) SequenceRole.Follower)));
		await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 0, 30), () => worker.Location == worker.Home);
		Assert.That(player.Location, Is.EqualTo(worker.Home));
		AssertSavesAndLoads(world);

		// Calling it off, they stop following, and the worker carries on without them.
		Assert.That(world.CanCallOffWith(worker), Is.True);
		world.CallOffWith(worker);
		Assert.That((player.Sequences.Count, worker.Partner, world.CanCallOffWith(worker)), Is.EqualTo((0, (Person?) null, false)));
	}

	/// <summary>
	/// Creates a world with a need, where the player and someone from another household who'll carry out its steps are in
	/// a quarter with no one else, and the player has nothing urgent to do.
	/// </summary>
	private static (GameWorld World, Person Worker, Person Player) CreateWorld(Need need)
	{
		var gameData = CreateGameData(withSchedules: true);
		gameData.Schedules.StepLists = [];
		gameData.Schedules.Needs = [.. gameData.Schedules.Needs, need];
		var random = new Random(0);
		var world = GameWorld.CreateFromTown(gameData, TownGenerator.Generate(gameData, CreatePlayerTraits(gameData), random), random);
		world.IsHeadless = false;
		var player = world.Player;
		var worker = world.Npcs.First(x => x.Family != player.Family && x.Profession != "infirm");
		var place = world.Locations.First(x => x.Template is QuarterTemplate);
		foreach (var someone in world.People.Where(x => x.Location == place))
			someone.Location = someone.Home;
		player.Location = place;
		worker.Location = place;
		foreach (var each in gameData.Schedules.Needs)
		{
			foreach (var person in new[] { player, worker })
			{
				if (each.Kind == NeedKind.Level)
					person.SetLevel(each, 0);
				else
					person.SetProgress(each, 100_000);
			}
		}

		return (world, worker, player);
	}

	private static Need Errand(IReadOnlyList<Step> steps)
	{
		var need = new Need { Id = "errand", Title = "Errand", StepSettings = steps, Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 } };
		need.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		return need;
	}

	/// <summary>
	/// Checks the world saves and loads as it is.
	/// </summary>
	private static void AssertSavesAndLoads(GameWorld world)
	{
		var settings = new SaveSettings("Title", "", "", [], "");
		var json = SaveFile.Create(world, settings, DateTimeOffset.UnixEpoch).Serialize();
		var loaded = SaveFile.Deserialize(json).Load(world.GameData, settings.GameTitle, new Random(0));
		Assert.That(SaveFile.Create(loaded, settings, DateTimeOffset.UnixEpoch).Serialize(), Is.EqualTo(json));
		Assert.That((loaded.Player.Current?.Role, loaded.Player.Current?.Leader?.Id), Is.EqualTo((world.Player.Current?.Role, world.Player.Current?.Leader?.Id)));
		Assert.That(loaded.Player.Sequences.Select(x => (x.Role, x.SetOff, x.Appointment?.Id)), Is.EqualTo(world.Player.Sequences.Select(x => (x.Role, x.SetOff, x.Appointment?.Id))));
	}
}
