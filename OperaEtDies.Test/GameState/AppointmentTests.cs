using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Town;
using static OperaEtDies.Test.TestData;

namespace OperaEtDies.Test.GameState;

/// <summary>
/// Appointments made by "ask" steps: their steps, making them at a time that fits the one asked, keeping them, and
/// calling them off.
/// </summary>
[TestFixture]
public sealed class AppointmentTests
{
	[Test]
	public void AppointmentStepsAreSkippedUntilTheAppointment()
	{
		var need = Errand([Asking(Do(Amount(30)), Do(Amount(10))), Do(Amount(5))]);

		// The ask step goes on past the appointment's steps (meeting, then its own), which end the steps.
		Assert.That(need.Steps.Count, Is.EqualTo(5));
		Assert.That(need.GetAppointmentSteps(0), Is.EqualTo(((int, int, int)?) (1, 1, 4)));
		Assert.That((need.GetNextStep(0), need.GetNextStep(1), need.GetNextStep(2), need.GetNextStep(3)), Is.EqualTo((4, 2, 3, 5)));
		Assert.That((need.IsOptional(1), need.IsOptional(4)), Is.EqualTo((true, false)));
		Assert.That(need.GetAppointmentSteps(4), Is.Null);
	}

	[Test]
	public void AppointmentsMustFitTheirStep()
	{
		var settings = CreateScheduleSettings();
		settings.Needs =
		[
			.. settings.Needs,
			Errand([
				new Step { Kind = StepKind.Do, Minutes = Amount(5), WithPartner = true, Appointment = new StepAppointment { Place = Home, Gap = 0, Patience = 0, Steps = [Meet(), Do(Amount(5))] } },
				Asking(new Step { Kind = StepKind.Wait, Minutes = Amount(5) }),
				Asking(new Step { Kind = StepKind.Do }),
				Asking(Asking(Do(Amount(5)))),
				new Step
				{
					Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "empty", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(1), Chance = new StepChance { Base = 50 },
					Appointment = new StepAppointment { WithinHours = 0, Steps = [] },
				},
				AskingExactly(Do(Amount(5))),
				AskingExactly(Do(Amount(5)), Meet(), Do(Amount(5))),
				AskingExactly(Meet(), new Step { Kind = StepKind.Meet, Early = 5, OnFail = new StepFailure { Any = new StepFailureResult(StepFailureKind.Next) } }, Do(Amount(5))),
				Meet(),
			]),
		];
		settings.UrgencyFactors = [];

		Assert.That(settings.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[errand].steps[0].appointment", "do"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[errand].steps[0].withPartner", "do"),
			OurResources.StepAppointmentWithoutDo.FormatCurrentCulture("needs[errand].steps[1].appointment"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[errand].steps[2].appointment.steps[1].minutes", "do"),
			OurResources.StepAppointmentNested.FormatCurrentCulture("needs[errand].steps[3].appointment"),
			OurResources.StepAppointmentMeetCount.FormatCurrentCulture("needs[errand].steps[3].appointment"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[errand].steps[4].appointment.place"),
			OurResources.DataNumberTooSmall.FormatCurrentCulture("needs[errand].steps[4].appointment.withinHours", 0, 1),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[errand].steps[4].appointment.gap"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[errand].steps[4].appointment.patience"),
			OurResources.DataListEmpty.FormatCurrentCulture("needs[errand].steps[4].appointment.steps"),
			OurResources.StepAppointmentWithoutDo.FormatCurrentCulture("needs[errand].steps[4].appointment"),
			OurResources.StepAppointmentMeetCount.FormatCurrentCulture("needs[errand].steps[4].appointment"),
			OurResources.StepAppointmentMeetCount.FormatCurrentCulture("needs[errand].steps[5].appointment"),
			OurResources.StepAppointmentBeforeMeet.FormatCurrentCulture("needs[errand].steps[6].appointment"),
			OurResources.StepAppointmentMeetCount.FormatCurrentCulture("needs[errand].steps[7].appointment"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[errand].steps[7].appointment.steps[1].onFail", "meet"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[errand].steps[7].appointment.steps[1].minutes", "meet"),
			OurResources.StepMeetNotAllowed.FormatCurrentCulture("needs[errand].steps[8]"),
		}));
	}

	[Test]
	public void AppointmentsAreMadeKeptAndSaved()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, worker, client) = CreateWorld(need);
		var calendar = world.GameData.Calendar;
		var attended = new List<(Location Place, Person? Leader)>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == worker && e.Task is BusyTask { Minutes: 30, ResumeMinutes: null } && worker.Goal == need)
				attended.Add((worker.Location, client.CurrentTask is FollowTask follow ? follow.Leader : null));
		};

		// Asked, the client has the time to meet at their home right away, once they're done talking and have walked there.
		var start = world.Time;
		worker.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(calendar.CreateTimeOffset(0, 0, 5), () => world.Appointments.Count != 0);
		var appointment = world.Appointments.Single();
		Assert.That((appointment.Worker, appointment.Client, appointment.Place, appointment.Need, appointment.Step), Is.EqualTo((worker, client, client.Home, need, 0)));
		Assert.That(calendar.GetTotalMinutes(appointment.Time - start), Is.LessThan(30));

		// Each has a sequence for it, waiting until it's time to set off.
		Assert.That((appointment.GetSequence(worker)?.SetOff, appointment.GetSequence(client)?.SetOff, appointment.GetSequence(client)?.Role),
			Is.EqualTo(((SetOffState?) SetOffState.Waiting, (SetOffState?) SetOffState.Waiting, (SequenceRole?) SequenceRole.Client)));
		Assert.That(client.Current, Is.Null);
		AssertSavesAndLoads(world);

		// Waiting for it is saved too.
		world.AdvanceTime(calendar.CreateTimeOffset(1, 0, 0), () => worker.CurrentTask is MeetTask || client.CurrentTask is MeetTask);
		Assert.That(world.People.Any(x => x.CurrentTask is MeetTask), Is.True);
		AssertSavesAndLoads(world);

		// At the client's home, the worker carries out the steps while the client stays with them, and then it's over.
		world.AdvanceTime(calendar.CreateTimeOffset(1, 0, 0), () => world.Appointments.Count == 0);
		Assert.That(attended, Is.EqualTo(new[] { (client.Home, (Person?) worker) }));
		Assert.That(client.CurrentTask, Is.Not.TypeOf<FollowTask>());
	}

	[Test]
	public void StepsBeforeMeetingAreDoneOnceTheySetOffInTime()
	{
		var need = Errand([AskingExactly(new Step { Kind = StepKind.GoTo, Place = Home }, Meet(), Do(Amount(30)))]);
		var (world, worker, client) = CreateWorld(need);
		var calendar = world.GameData.Calendar;
		var arrivals = new List<(Location Place, TimePoint Time)>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == worker && worker.Current?.Appointment is not null && e.Task is TravelTask travel)
				arrivals.Add((travel.Destination, worker.TaskEnd!));
		};

		worker.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(calendar.CreateTimeOffset(0, 0, 5), () => world.Appointments.Count != 0);
		var appointment = world.Appointments.Single();
		world.AdvanceTime(calendar.CreateTimeOffset(0, 3, 0), () => appointment.IsStarted);

		// Setting off, they went home first, as the step before meeting says, and still got there before its time.
		Assert.That(appointment.IsStarted, Is.True);
		Assert.That(arrivals.Select(x => x.Place), Does.Contain(worker.Home));
		Assert.That(arrivals.FindIndex(x => x.Place == worker.Home), Is.LessThan(arrivals.FindIndex(x => x.Place == client.Home)));
		Assert.That(arrivals.Single(x => x.Place == client.Home).Time, Is.LessThanOrEqualTo(appointment.Time));
	}

	[Test]
	public void ClientsWhoCantMeetNowSetALaterTime()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, worker, client) = CreateWorld(need);
		var calendar = world.GameData.Calendar;
		var hunger = world.GameData.Schedules.Needs.Single(x => x.Id == "hunger");
		client.SetLevel(hunger, OperaEtDies.Data.Need.MaximumLevel);

		// Too hungry to go along now, the client sets a time on the hour or half hour, once they'd have eaten.
		var start = world.Time;
		worker.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(calendar.CreateTimeOffset(0, 0, 5), () => world.Appointments.Count != 0);
		var appointment = world.Appointments.Single();
		Assert.That(calendar.GetParts(appointment.Time).Minute % 30, Is.Zero);
		Assert.That(calendar.GetTotalMinutes(appointment.Time - start), Is.GreaterThanOrEqualTo(60));
	}

	[Test]
	public void AppointmentsAreCalledOffWhenTheOtherDoesntCome()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, worker, client) = CreateWorld(need);
		var calendar = world.GameData.Calendar;
		var attended = 0;
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == worker && e.Task is BusyTask { Minutes: 30, Step: not null })
				attended++;
		};

		worker.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(calendar.CreateTimeOffset(0, 0, 5), () => world.Appointments.Count != 0);
		var appointment = world.Appointments.Single();

		// Once the client has set off, they're sent off to do something else, which calls it off, so the worker never keeps it.
		world.AdvanceTime(calendar.CreateTimeOffset(1, 0, 0), () => appointment.IsOnTheWay(client));
		world.CancelGoal(client);
		world.AdvanceTime(calendar.CreateTimeOffset(1, 0, 0), () => world.Appointments.Count == 0);

		Assert.That((world.Appointments.Count, attended), Is.EqualTo((0, 0)));
		Assert.That(worker.CurrentTask, Is.Not.TypeOf<MeetTask>());
	}

	[Test]
	public void ThePlayerAsksInConversationAndKeepsAppointments()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);
		var calendar = world.GameData.Calendar;

		// The player asks the client in conversation, who agrees to meet at their home.
		Scheduler.StartSequence(player, need, 0, null, [], null);
		Assert.That(world.GetPlayerAsk(client)?.StepIndex, Is.Zero);
		var answer = world.AskPlayerQuestion(client);
		var appointment = answer.Appointment!;
		Assert.That((answer.IsYes, appointment.Worker, appointment.Client, appointment.Place), Is.EqualTo((true, player, client, client.Home)));
		Assert.That(world.GetPlayerAppointments(), Is.EqualTo(new[] { appointment }));
		var keeping = appointment.GetSequence(player)!;
		Assert.That((player.Sequences.Single(), keeping.HasSetOff, player.Current), Is.EqualTo((keeping, false, (StepSequence?) null)));

		// Once it's time to set off (here, as soon as they're done talking), time stops to ask them, and stays stopped until they answer.
		world.AdvanceTime(calendar.CreateTimeOffset(1, 0, 0));
		Assert.That(world.GetPlayerSetOffSequence(), Is.SameAs(keeping));
		Assert.That(world.AdvanceTime(calendar.CreateTimeOffset(0, 1, 0)), Is.EqualTo(calendar.CreateTimeOffset(0, 0, 0)));
		AssertSavesAndLoads(world);

		// Setting off, they first go there and meet the client.
		world.AnswerSetOff(isSettingOff: true);
		Assert.That((world.GetPlayerSetOffSequence(), player.Current, keeping.SetOff), Is.EqualTo(((StepSequence?) null, keeping, SetOffState.Gone)));
		Assert.That(world.GetPlayerSteps(keeping).Select(x => x.Kind), Is.EqualTo(new[] { PlayerStepKind.GoToAppointment, PlayerStepKind.Meet, PlayerStepKind.Step }));
		AssertSavesAndLoads(world);
		world.DoPlayerStep(keeping);
		Assert.That(player.Location, Is.EqualTo(appointment.Place));
		for (var count = 0; count < 3 && !appointment.IsStarted; count++)
			world.DoPlayerStep(keeping);

		// Once both are there, the client follows them while they take its steps, and is let go at the end.
		Assert.That(appointment.IsStarted, Is.True);
		Assert.That(client.CurrentTask, Is.TypeOf<FollowTask>());
		Assert.That(world.GetPlayerSteps(keeping).Select(x => (x.Kind, x.Step.Kind)), Is.EqualTo(new[] { (PlayerStepKind.Step, StepKind.Do) }));
		AssertSavesAndLoads(world);
		world.DoPlayerStep(keeping);
		Assert.That((player.Sequences.Count, world.Appointments.Count), Is.EqualTo((0, 0)));
		Assert.That(client.CurrentTask, Is.Not.TypeOf<FollowTask>());
	}

	[Test]
	public void ThePlayerCanCallOffAppointments()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);
		Scheduler.StartSequence(player, need, 0, null, [], null);
		world.AskPlayerQuestion(client);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(1, 0, 0));
		Assume.That(world.GetPlayerSetOffSequence(), Is.Not.Null);

		world.AnswerSetOff(isSettingOff: false);

		Assert.That((world.GetPlayerSetOffSequence(), world.Appointments.Count, player.Sequences.Count, client.Sequences.Count), Is.EqualTo(((StepSequence?) null, 0, 0, 0)));
		Assert.That(client.Tasks.OfType<MeetTask>(), Is.Empty);
	}

	[Test]
	public void ClientsGiveUpOnThePlayerAfterAWhile()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);
		var calendar = world.GameData.Calendar;
		Scheduler.StartSequence(player, need, 0, null, [], null);
		var appointment = world.AskPlayerQuestion(client).Appointment!;
		var missed = new List<(Appointment Appointment, bool IsClientWaiting)>();
		world.AppointmentMissed += (_, e) => missed.Add((e.Appointment, e.IsOtherWaiting));
		world.AdvanceTime(calendar.CreateTimeOffset(1, 0, 0));
		world.AnswerSetOff(isSettingOff: true);

		// The player never goes, so the client waits there until a while after it was to start, and gives up.
		world.AdvanceTime(calendar.CreateTimeOffset(0, 3, 0));

		Assert.That(missed, Is.EqualTo(new[] { (appointment, true) }));
		Assert.That((player.Sequences.Count, world.Appointments.Count), Is.EqualTo((0, 0)));
		Assert.That(calendar.GetTotalMinutes(world.Time - appointment.Time), Is.GreaterThanOrEqualTo(30));
	}

	[Test]
	public void ThePlayerOnlyAsksThoseTheStepAsks()
	{
		var need = Errand([
			new Step { Kind = StepKind.Ask, With = StepTarget.Anyone, Who = Who("profession", "infirm"), Name = "agreed", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(5), Chance = new StepChance { Base = 100 } },
			new Step { Kind = StepKind.Ask, With = StepTarget.Partner, Name = "again", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(5), Chance = new StepChance { Base = 100 } },
		]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);

		// The client isn't infirm, and isn't the player's partner.
		var sequence = Scheduler.StartSequence(player, need, 0, null, [], null);
		Assert.That(world.GetPlayerAsk(client), Is.Null);
		sequence.Step = 1;
		Assert.That(world.GetPlayerAsk(client), Is.Null);
		sequence.Partner = client;
		Assert.That(world.GetPlayerAsk(client)?.StepIndex, Is.EqualTo(1));
	}

	[Test]
	public void AnswersAndPartnersAreKeptWhileStepsArePending()
	{
		var need = Errand([
			new Step { Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "agreed", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(5), Chance = new StepChance { Base = 100 } },
			Do(Amount(5)),
			new Step { Kind = StepKind.Do, Minutes = Amount(5), When = new StepCondition { AnsweredYes = "agreed" } },
		]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);
		var sequence = Scheduler.StartSequence(player, need, 0, null, [], null);

		var answer = world.AskPlayerQuestion(client);

		// They stopped at the next step they take themselves, still knowing the answer, and who they asked.
		Assert.That(answer.IsYes, Is.True);
		Assert.That(sequence.Step, Is.EqualTo(1));
		Assert.That((sequence.Answers.GetValueOrDefault("agreed"), player.Partner), Is.EqualTo((true, client)));
		Assert.That(world.GetPlayerSteps(sequence).Select(x => x.StepIndex), Is.EqualTo(new[] { 1, 2 }));

		// Giving them up forgets them.
		world.StopPlayerSteps(sequence);
		Assert.That((player.Sequences.Count, player.Partner), Is.EqualTo((0, (Person?) null)));
	}

	/// <summary>
	/// Checks the world saves and loads as it is, with its appointments.
	/// </summary>
	private static void AssertSavesAndLoads(GameWorld world)
	{
		var settings = new SaveSettings("Title", "", "", [], "");
		var json = SaveFile.Create(world, settings, DateTimeOffset.UnixEpoch).Serialize();
		var loaded = SaveFile.Deserialize(json).Load(world.GameData, settings.GameTitle, new Random(0));

		Assert.That(SaveFile.Create(loaded, settings, DateTimeOffset.UnixEpoch).Serialize(), Is.EqualTo(json));
		Assert.That(loaded.Appointments.Select(x => (x.Id, x.Worker.Id, x.Client.Id, x.Need.Id, x.Step, x.Place.Id, x.Time, x.IsOnTheWay(x.Worker), x.IsOnTheWay(x.Client), x.IsStarted)),
			Is.EqualTo(world.Appointments.Select(x => (x.Id, x.Worker.Id, x.Client.Id, x.Need.Id, x.Step, x.Place.Id, x.Time, x.IsOnTheWay(x.Worker), x.IsOnTheWay(x.Client), x.IsStarted))));
		Assert.That(loaded.People.SelectMany(x => x.Sequences).Select(x => (x.SetOff, x.Appointment?.Id)), Is.EqualTo(world.People.SelectMany(x => x.Sequences).Select(x => (x.SetOff, x.Appointment?.Id))));
		Assert.That(loaded.Player.Current?.Appointment?.Id, Is.EqualTo(world.Player.Current?.Appointment?.Id));
	}

	/// <summary>
	/// An "ask" step that's always answered yes (if the time fits), making an appointment at the home of the one asked,
	/// meeting there and then carrying out the given steps.
	/// </summary>
	private static Step Asking(params Step[] steps) => AskingExactly([Meet(), .. steps]);

	/// <summary>
	/// An "ask" step that's always answered yes (if the time fits), making an appointment at the home of the one asked with
	/// exactly the given steps.
	/// </summary>
	private static Step AskingExactly(params Step[] steps) => new()
	{
		Kind = StepKind.Ask,
		With = StepTarget.Anyone,
		Player = new StepPlayer { Title = "Ask" },
		Name = "agreed",
		Minutes = Amount(5),
		Chance = new StepChance { Base = 100 },
		Appointment = new StepAppointment { Place = new SchedulePlace { Kind = NeedPlace.PartnerHome }, Gap = 120, Patience = 60, Steps = steps },
	};

	/// <summary>
	/// Creates a level need with the given steps, which never grows, so nothing else interferes with it.
	/// </summary>
	private static Need Errand(IReadOnlyList<Step> steps)
	{
		var need = new Need { Id = "errand", Title = "Errand", StepSettings = steps, Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 } };
		need.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		return need;
	}

	/// <summary>
	/// Creates a world with a need, and a worker (the player, or someone else, with the game deciding for everyone) in a
	/// quarter with a client from another family, who has every need met and nothing to do, and no one else.
	/// </summary>
	private static (GameWorld World, Person Worker, Person Client) CreateWorld(Need need, bool isPlayer = false)
	{
		var gameData = CreateGameData(withSchedules: true);
		gameData.Schedules.StepLists = [];
		gameData.Schedules.Needs = [.. gameData.Schedules.Needs, need];
		var random = new Random(0);
		var world = GameWorld.CreateFromTown(gameData, TownGenerator.Generate(gameData, CreatePlayerTraits(gameData), random), random);
		world.IsHeadless = !isPlayer;

		var worker = isPlayer ? world.Player : world.Npcs[0];
		var client = world.Npcs.First(x => x.Family != worker.Family && x.Profession != "infirm");
		var place = world.Locations.First(x => x.Template is QuarterTemplate);
		foreach (var someone in world.People.Where(x => x.Location == place))
			someone.Location = someone.Home;
		worker.Location = place;
		client.Location = place;
		foreach (var each in world.GameData.Schedules.Needs)
		{
			if (each.Kind == NeedKind.Level)
				client.SetLevel(each, 0);
			else
				client.SetProgress(each, 100_000);
		}

		client.SetPlan(null, [new BusyTask(600)]);
		client.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 600));
		return (world, worker, client);
	}
}
