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
	public void AppointmentLinesNeedTitlesAndText()
	{
		var empty = new LongText { Text = " ", Ai = true };
		var appointment = new StepAppointment
		{
			Place = Home,
			Gap = 0,
			Patience = 0,
			Steps = [Meet(), Do(Amount(5))],
			Start = new AppointmentLine { Text = empty, Asked = empty },
			Ready = new AppointmentLine { Title = "Say you're ready" },
		};

		Assert.That(appointment.Validate("appointment"), Is.EqualTo(new[]
		{
			OurResources.DataValueMissing.FormatCurrentCulture("appointment.start.title"),
			OurResources.DataValueMissing.FormatCurrentCulture("appointment.start.text.text"),
			OurResources.DataValueMissing.FormatCurrentCulture("appointment.start.asked.text"),
		}));
	}

	[Test]
	public void AppointmentsAreMadeKeptAndSaved()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, worker, client) = CreateWorld(need);
		var calendar = world.GameData.Calendar;
		var attended = new List<(Location Place, Person? Leader)>();
		TimePoint? started = null;
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == worker && e.Task is BusyTask { Minutes: 30, ResumeMinutes: null } && worker.Goal == need)
				attended.Add((worker.Location, client.CurrentTask is FollowTask follow ? follow.Leader : null));
			if (e.Person == client && e.Task is FollowTask)
				started ??= e.Time;
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

		// Waiting for it, or having started it, is saved too.
		world.AdvanceTime(calendar.CreateTimeOffset(1, 0, 0), () => appointment.IsStarted || world.People.Any(x => x.CurrentTask is MeetTask));
		Assert.That(appointment.IsStarted || world.People.Any(x => x.CurrentTask is MeetTask), Is.True);
		AssertSavesAndLoads(world);

		// At the client's home, the worker carries out the steps while the client stays with them, and then it's over. It
		// started as soon as both were there and ready, which was no later than its time.
		world.AdvanceTime(calendar.CreateTimeOffset(1, 0, 0), () => world.Appointments.Count == 0);
		Assert.That(attended, Is.EqualTo(new[] { (client.Home, (Person?) worker) }));
		Assert.That(client.CurrentTask, Is.Not.TypeOf<FollowTask>());
		Assert.That(started, Is.Not.Null);
		Assert.That(started! <= appointment.Time, Is.True);
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
	public async Task ThePlayerAsksInConversationAndKeepsAppointmentsAsync()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);
		var calendar = world.GameData.Calendar;

		// The player asks the client in conversation, who agrees to meet at their home.
		Scheduler.StartSequence(player, need, 0, null, [], null);
		Assert.That(world.GetPlayerAsk(client)?.StepIndex, Is.Zero);
		var answer = await world.AskPlayerQuestionAsync(client);
		var appointment = answer.Appointment!;
		Assert.That((answer.IsYes, appointment.Worker, appointment.Client, appointment.Place), Is.EqualTo((true, player, client, client.Home)));
		Assert.That(world.GetPlayerAppointments(), Is.EqualTo(new[] { appointment }));
		var keeping = appointment.GetSequence(player)!;
		Assert.That((player.Sequences.Single(), keeping.HasSetOff, player.Current), Is.EqualTo((keeping, false, (StepSequence?) null)));

		// Once it's time to set off (here, as soon as they're done talking), time stops to ask them, and stays stopped until they answer.
		await world.AdvanceTimeAsync(calendar.CreateTimeOffset(1, 0, 0));
		Assert.That(world.GetPlayerSetOffSequence(), Is.SameAs(keeping));
		Assert.That(await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 1, 0)), Is.EqualTo(calendar.CreateTimeOffset(0, 0, 0)));
		AssertSavesAndLoads(world);

		// Setting off, they first go there and meet the client.
		await world.AnswerSetOffAsync(isSettingOff: true);
		Assert.That((world.GetPlayerSetOffSequence(), player.Current, keeping.SetOff), Is.EqualTo(((StepSequence?) null, keeping, SetOffState.Gone)));
		Assert.That(world.GetPlayerSteps(keeping).Select(x => x.Kind), Is.EqualTo(new[] { PlayerStepKind.GoToAppointment, PlayerStepKind.Meet, PlayerStepKind.Step }));
		AssertSavesAndLoads(world);
		await world.DoPlayerStepAsync(keeping);
		Assert.That(player.Location, Is.EqualTo(appointment.Place));
		for (var count = 0; count < 3 && !appointment.IsStarted; count++)
			await world.DoPlayerStepAsync(keeping);

		// Once both are there, the client follows them while they take its steps, and is let go at the end.
		Assert.That(appointment.IsStarted, Is.True);
		Assert.That(client.CurrentTask, Is.TypeOf<FollowTask>());
		Assert.That(world.GetPlayerSteps(keeping).Select(x => (x.Kind, x.Step.Kind)), Is.EqualTo(new[] { (PlayerStepKind.Step, StepKind.Do) }));
		AssertSavesAndLoads(world);
		await world.DoPlayerStepAsync(keeping);
		Assert.That((player.Sequences.Count, world.Appointments.Count), Is.EqualTo((0, 0)));
		Assert.That(client.CurrentTask, Is.Not.TypeOf<FollowTask>());
	}

	[Test]
	public async Task ThePlayerIsShownChangingClothesForAnAppointmentAsync()
	{
		var need = Errand([Asking(
			new Step { Kind = StepKind.Dress, Outfit = OutfitKind.Nothing, WithPartner = true },
			Do(Amount(30)),
			new Step { Kind = StepKind.Undress, Outfit = OutfitKind.Nothing, WithPartner = true })]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);
		Scheduler.StartSequence(player, need, 0, null, [], null);
		var appointment = (await world.AskPlayerQuestionAsync(client)).Appointment!;
		var keeping = appointment.GetSequence(player)!;
		await world.AdvanceTimeAsync(world.GameData.Calendar.CreateTimeOffset(1, 0, 0));

		// Setting off, they're shown going there, meeting the client, and every step after that, changes of clothes included.
		await world.AnswerSetOffAsync(isSettingOff: true);
		Assert.That(world.GetPlayerSteps(keeping).Select(x => (x.Kind, x.Step.Kind)), Is.EqualTo(new[]
		{
			(PlayerStepKind.GoToAppointment, StepKind.Meet), (PlayerStepKind.Meet, StepKind.Meet), (PlayerStepKind.Step, StepKind.Dress), (PlayerStepKind.Step, StepKind.Do), (PlayerStepKind.Step, StepKind.Undress),
		}));
	}

	[Test]
	public async Task ThePlayerCanCallOffAppointmentsAsync()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);
		Scheduler.StartSequence(player, need, 0, null, [], null);
		await world.AskPlayerQuestionAsync(client);
		await world.AdvanceTimeAsync(world.GameData.Calendar.CreateTimeOffset(1, 0, 0));
		Assume.That(world.GetPlayerSetOffSequence(), Is.Not.Null);

		await world.AnswerSetOffAsync(isSettingOff: false);

		Assert.That((world.GetPlayerSetOffSequence(), world.Appointments.Count, player.Sequences.Count, client.Sequences.Count), Is.EqualTo(((StepSequence?) null, 0, 0, 0)));
		Assert.That(client.Tasks.OfType<MeetTask>(), Is.Empty);
	}

	[Test]
	public async Task ClientsGiveUpOnThePlayerAfterAWhileAsync()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);
		var calendar = world.GameData.Calendar;
		Scheduler.StartSequence(player, need, 0, null, [], null);
		var appointment = (await world.AskPlayerQuestionAsync(client)).Appointment!;
		var missed = new List<(Appointment Appointment, bool IsClientWaiting)>();
		world.AppointmentMissed += (_, e) => missed.Add((e.Appointment, e.IsOtherWaiting));
		await world.AdvanceTimeAsync(calendar.CreateTimeOffset(1, 0, 0));
		await world.AnswerSetOffAsync(isSettingOff: true);

		// The player never goes, so the client waits there until its "meet" step's 30 minutes after it was to start, and gives
		// up then, which is when the failure says it happened, though time went on passing after it.
		var failures = new List<(Person Person, FailCause Cause, TimePoint Time)>();
		world.StepFailed += (_, e) => failures.Add((e.Person, e.Cause, e.Time));
		await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 3, 0));

		Assert.That(missed, Is.EqualTo(new[] { (appointment, true) }));
		Assert.That((player.Sequences.Count, world.Appointments.Count), Is.EqualTo((0, 0)));
		Assert.That(failures.Where(x => x.Person == client).Select(x => (x.Cause, x.Time)), Is.EqualTo(new[] { (FailCause.NoShow, calendar.AddMinutes(appointment.Time, 30)) }));
		Assert.That(world.Time > calendar.AddMinutes(appointment.Time, 30), Is.True);
	}

	[Test]
	public void PeopleOnTheirWayToAnAppointmentDontStopToTalk()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, worker, client) = CreateWorld(need);
		var calendar = world.GameData.Calendar;
		worker.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(calendar.CreateTimeOffset(0, 0, 5), () => world.Appointments.Count != 0);
		var appointment = world.Appointments.Single();

		// Walking to the client's home for it, the worker is too busy to stop and talk, even with the player.
		world.AdvanceTime(calendar.CreateTimeOffset(1, 0, 0), () => appointment.IsOnTheWay(worker) && worker.CurrentTask is TravelTask);
		Assume.That((appointment.IsOnTheWay(worker), appointment.IsStarted, worker.CurrentTask), Is.EqualTo((true, false, worker.CurrentTask as TravelTask)));
		Assert.That(world.IsTooBusyToTalk(worker), Is.True);
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
	public async Task AnswersAndPartnersAreKeptWhileStepsArePendingAsync()
	{
		var need = Errand([
			new Step { Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "agreed", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(5), Chance = new StepChance { Base = 100 } },
			Do(Amount(5)),
			new Step { Kind = StepKind.Do, Minutes = Amount(5), When = new StepCondition { AnsweredYes = "agreed" } },
		]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);
		var sequence = Scheduler.StartSequence(player, need, 0, null, [], null);

		var answer = await world.AskPlayerQuestionAsync(client);

		// They stopped at the next step they take themselves, still knowing the answer, and who they asked.
		Assert.That(answer.IsYes, Is.True);
		Assert.That(sequence.Step, Is.EqualTo(1));
		Assert.That((sequence.Answers.GetValueOrDefault("agreed"), player.Partner), Is.EqualTo((true, client)));
		Assert.That(world.GetPlayerSteps(sequence).Select(x => x.StepIndex), Is.EqualTo(new[] { 1, 2 }));

		// Giving them up forgets them.
		world.StopPlayerSteps(sequence);
		Assert.That((player.Sequences.Count, player.Partner), Is.EqualTo((0, (Person?) null)));
	}

	[Test]
	public async Task StepsOnHoldForAnAppointmentCarryOnFromThereAfterItAsync()
	{
		var need = Errand([Asking(Do(Amount(30))), Do(Amount(5))]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);
		var failures = new List<FailCause>();
		world.StepFailed += (_, e) => failures.Add(e.Cause);
		var asking = Scheduler.StartSequence(player, need, 0, null, [], null);
		var appointment = (await world.AskPlayerQuestionAsync(client)).Appointment!;
		await world.AdvanceTimeAsync(world.GameData.Calendar.CreateTimeOffset(1, 0, 0));
		await world.AnswerSetOffAsync(isSettingOff: true);
		var keeping = appointment.GetSequence(player)!;

		// Going to the appointment isn't leaving the steps it put on hold, which are saved that way.
		await world.DoPlayerStepAsync(keeping);
		Assert.That((player.Location, player.Sequences, failures), Is.EqualTo((appointment.Place, new[] { asking, keeping }, Array.Empty<FailCause>())));
		AssertSavesAndLoads(world);
		for (var count = 0; count < 3 && !appointment.IsStarted; count++)
			await world.DoPlayerStepAsync(keeping);
		await world.DoPlayerStepAsync(keeping);

		// Once it's done, they carry on from where the appointment was.
		Assert.That((failures, player.Sequences.Single(), asking.Place, asking.Step), Is.EqualTo((Array.Empty<FailCause>(), asking, (Location?) appointment.Place, 3)));
	}

	[Test]
	public async Task WaitingForAnAppointmentStopsWhenItStartsAsync()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);
		client.SetLevel(world.GameData.Schedules.Needs.Single(x => x.Id == "hunger"), OperaEtDies.Data.Need.MaximumLevel);
		Scheduler.StartSequence(player, need, 0, null, [], null);
		var appointment = (await world.AskPlayerQuestionAsync(client)).Appointment!;
		await world.AdvanceTimeAsync(world.GameData.Calendar.CreateTimeOffset(3, 0, 0));
		await world.AnswerSetOffAsync(isSettingOff: true);

		// Walking there themselves, the player waits there until the client is waiting for its time too. The player can talk
		// with them while they wait.
		await world.MovePlayerAsync(appointment.Place);
		for (var count = 0; count < 10 && client.CurrentTask is not MeetTask && world.Appointments.Contains(appointment); count++)
			await world.WaitForPeopleAsync(player.Location);
		Assume.That(client.CurrentTask, Is.TypeOf<MeetTask>());
		Assume.That(world.Time < appointment.Time, Is.True);
		Assert.That(world.CanTalkTo(client), Is.True);

		// It never starts by itself: waiting again, with no one coming or going, goes on past its time.
		await world.WaitForPeopleAsync(player.Location);
		Assert.That((appointment.IsStarted, world.Time > appointment.Time), Is.EqualTo((false, true)));
	}

	[Test]
	public async Task ThePlayerStartsAnAppointmentByAskingAsync()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);
		client.SetLevel(world.GameData.Schedules.Needs.Single(x => x.Id == "hunger"), OperaEtDies.Data.Need.MaximumLevel);
		Scheduler.StartSequence(player, need, 0, null, [], null);
		var appointment = (await world.AskPlayerQuestionAsync(client)).Appointment!;
		await world.AdvanceTimeAsync(world.GameData.Calendar.CreateTimeOffset(3, 0, 0));
		await world.AnswerSetOffAsync(isSettingOff: true);
		await world.MovePlayerAsync(appointment.Place);
		for (var count = 0; count < 10 && client.CurrentTask is not MeetTask && world.Appointments.Contains(appointment); count++)
			await world.WaitForPeopleAsync(player.Location);
		Assume.That(client.CurrentTask, Is.TypeOf<MeetTask>());
		Assume.That(world.Time < appointment.Time, Is.True);

		// Asking in conversation, the client is ready, so it starts at once, before its time, without any time passing.
		var time = world.Time;
		Assert.That(world.GetPlayerStartAppointment(client), Is.SameAs(appointment));
		Assert.That(await world.AskToStartAsync(client), Is.True);
		Assert.That((appointment.IsStarted, world.Time), Is.EqualTo((true, time)));
		Assert.That(client.CurrentTask, Is.TypeOf<FollowTask>());
		Assert.That(world.GetPlayerStartAppointment(client), Is.Null);
	}

	[Test]
	public async Task TheWorkerAsksThePlayerToStartAsync()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, worker, player) = CreateWorld(need, isPlayerClient: true);
		var calendar = world.GameData.Calendar;
		var appointment = await MeetAsPlayerClientAsync(world, worker, need);

		// Once both are there, the worker asks the player whether they're ready, which stops time until they answer.
		Assert.That(player.StartRequest, Is.SameAs(appointment));
		Assert.That(await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 1, 0)), Is.EqualTo(calendar.CreateTimeOffset(0, 0, 0)));
		AssertSavesAndLoads(world);

		// Saying no, the player isn't asked again for a while, but can say they're ready.
		await world.AnswerStartAsync(isYes: false);
		var declined = world.Time;
		Assert.That((player.StartRequest, appointment.IsStarted, appointment.DeclinedBy, appointment.DeclinedAt), Is.EqualTo(((Appointment?) null, false, player, (TimePoint?) declined)));
		Assert.That(world.GetPlayerStartAppointment(worker), Is.SameAs(appointment));
		AssertSavesAndLoads(world);

		// Waiting there, they're asked again once it's been long enough, or at its time, if that comes first.
		for (var count = 0; count < 10 && player.StartRequest is null && world.Appointments.Contains(appointment); count++)
			await world.WaitForPeopleAsync(player.Location);
		var again = calendar.AddMinutes(declined, world.GameData.Schedules.AskToStartAgain.Minutes);
		Assert.That((player.StartRequest, world.Time), Is.EqualTo((appointment, declined < appointment.Time && appointment.Time < again ? appointment.Time : again)));

		// Saying yes starts it.
		await world.AnswerStartAsync(isYes: true);
		Assert.That((appointment.IsStarted, player.Current?.Leader), Is.EqualTo((true, worker)));
	}

	[Test]
	public async Task ThePlayerSaysTheyAreReadyAfterSayingNoAsync()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, worker, player) = CreateWorld(need, isPlayerClient: true);
		var appointment = await MeetAsPlayerClientAsync(world, worker, need);
		Assume.That(player.StartRequest, Is.SameAs(appointment));
		await world.AnswerStartAsync(isYes: false);

		// Telling the worker they're ready, the worker, still waiting, says yes, and it starts.
		Assert.That(await world.AskToStartAsync(worker), Is.True);
		Assert.That(appointment.IsStarted, Is.True);
	}

	[Test]
	public async Task LeavingAndComingBackMeansBeingAskedAgainAsync()
	{
		var need = Errand([Asking(Do(Amount(30)))]);
		var (world, worker, player) = CreateWorld(need, isPlayerClient: true);
		var away = player.Location;
		var appointment = await MeetAsPlayerClientAsync(world, worker, need);
		Assume.That(player.StartRequest, Is.SameAs(appointment));
		await world.AnswerStartAsync(isYes: false);

		// Leaving forgets the no, so coming back, they're asked again at once.
		var time = world.Time;
		await world.MovePlayerAsync(away);
		Assert.That(appointment.DeclinedBy, Is.Null);
		await world.MovePlayerAsync(appointment.Place);
		Assert.That((player.StartRequest, world.Time), Is.EqualTo((appointment, time)));
	}

	/// <summary>
	/// Has the worker ask the player (as the client) to meet, which they agree to, set off for, and walk to, and waits
	/// there until the worker asks them whether they're ready to start it. Returns the appointment.
	/// </summary>
	private static async Task<Appointment> MeetAsPlayerClientAsync(GameWorld world, Person worker, Need need)
	{
		var calendar = world.GameData.Calendar;
		var player = world.Player;
		worker.SetPlan(need, [], startStep: 0);
		await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 5, 0));
		Assume.That(player.AskRequest, Is.SameAs(worker));
		var appointment = (await world.AnswerAskAsync(isYes: true))!;
		Assume.That(appointment, Is.Not.Null);
		for (var count = 0; count < 5 && world.GetPlayerSetOffSequence() is null; count++)
			await world.AdvanceTimeAsync(calendar.CreateTimeOffset(1, 0, 0));
		await world.AnswerSetOffAsync(isSettingOff: true);
		await world.MovePlayerAsync(appointment.Place);
		for (var count = 0; count < 10 && player.StartRequest is null && world.Appointments.Contains(appointment); count++)
			await world.WaitForPeopleAsync(player.Location);
		return appointment;
	}

	[Test]
	public async Task LeavingBeforeAskingSomeoneFoundGoesBackToLookingAsync()
	{
		var need = Errand([
			new Step { Kind = StepKind.Seek, Who = NotFamily, Minutes = Amount(15) },
			new Step { Kind = StepKind.Ask, With = StepTarget.Sought, Name = "agreed", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(5), Chance = new StepChance { Base = 100 } },
		]);
		var (world, player, client) = CreateWorld(need, isPlayer: true);
		var place = player.Location;
		var failures = new List<FailCause>();
		world.StepFailed += (_, e) => failures.Add(e.Cause);

		// The client is there, so looking for someone is done, and asking them is next.
		await world.StartPlayerStepsAsync(need, isAutomatic: false);
		var sequence = player.Current!;
		Assert.That(sequence.Step, Is.EqualTo(1));

		// Leaving before asking goes back to looking, and coming back finds them again.
		await world.MovePlayerAsync(player.Home);
		Assert.That((failures, player.Sequences.Single(), sequence.Step), Is.EqualTo((Array.Empty<FailCause>(), sequence, 0)));
		await world.MovePlayerAsync(place);
		Assert.That((failures, sequence.Step, world.GetPlayerAsk(client)?.StepIndex), Is.EqualTo((Array.Empty<FailCause>(), 1, (int?) 1)));
	}

	[Test]
	public void TheNextPlaceToGoIsOnlyFromStepsOnTheWay()
	{
		static Step GoingTo(string text) => new() { Kind = StepKind.GoTo, Place = Home, Player = new StepPlayer { Text = new LongText { Text = text, Ai = true } } };

		// The steps are: 0 choose, 1 first, 2 and 3 do, 4 third, 5 after.
		var choosing = Errand([
			new Step { Kind = StepKind.Choose, Options = [new StepOption { Steps = [GoingTo("first")] }, new StepOption { Steps = [Do(Amount(5)), Do(Amount(5))] }, new StepOption { Steps = [GoingTo("third")] }] },
			GoingTo("after"),
		]);
		Assert.That(new[] { 0, 2, 3, 5 }.Select(x => new StepSequence(choosing, x, null).NextText), Is.EqualTo(new[] { "first", "after", "after", "after" }));

		// The steps are: 0 ask, 1 meet, 2 do, 3 after. The appointment's steps end its steps.
		var asking = Errand([AskingExactly(Meet(), Do(Amount(5))), GoingTo("after")]);
		Assert.That(new[] { 0, 2 }.Select(x => new StepSequence(asking, x, null).NextText), Is.EqualTo(new[] { "after", null }));
	}

	/// <summary>
	/// Who a step looks for or asks when it leaves out the household of the one taking it.
	/// </summary>
	private static StepWho NotFamily => new() { NotValues = new Dictionary<string, IReadOnlyList<string>> { [StepWho.FamilyKey] = [StepWho.FamilyMember] } };

	/// <summary>
	/// Checks the world saves and loads as it is, with its appointments.
	/// </summary>
	private static void AssertSavesAndLoads(GameWorld world)
	{
		var settings = new SaveSettings("Title", "", "", [], "");
		var json = SaveFile.Create(world, settings, DateTimeOffset.UnixEpoch).Serialize();
		var loaded = SaveFile.Deserialize(json).Load(world.GameData, settings.GameTitle, new Random(0));

		Assert.That(SaveFile.Create(loaded, settings, DateTimeOffset.UnixEpoch).Serialize(), Is.EqualTo(json));
		Assert.That(loaded.Appointments.Select(x => (x.Id, x.Worker.Id, x.Client.Id, x.Need.Id, x.Step, x.Place.Id, x.Time, x.IsOnTheWay(x.Worker), x.IsOnTheWay(x.Client), x.IsStarted, x.DeclinedBy?.Id, x.DeclinedAt)),
			Is.EqualTo(world.Appointments.Select(x => (x.Id, x.Worker.Id, x.Client.Id, x.Need.Id, x.Step, x.Place.Id, x.Time, x.IsOnTheWay(x.Worker), x.IsOnTheWay(x.Client), x.IsStarted, x.DeclinedBy?.Id, x.DeclinedAt))));
		Assert.That(loaded.Player.StartRequest?.Id, Is.EqualTo(world.Player.StartRequest?.Id));
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
		var need = new Need { Id = "errand", Title = "Errand", Icon = "icon", StepSettings = steps, Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 } };
		need.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		return need;
	}

	/// <summary>
	/// Creates a world with a need, and a worker (the player, or someone else) in a quarter with a client from another
	/// family (the player, or someone else), who has every need met and nothing to do, and no one else. Without the player
	/// as either, the game decides for everyone.
	/// </summary>
	private static (GameWorld World, Person Worker, Person Client) CreateWorld(Need need, bool isPlayer = false, bool isPlayerClient = false)
	{
		var gameData = CreateGameData(withSchedules: true);
		gameData.Schedules.StepLists = [];
		gameData.Schedules.Needs = [.. gameData.Schedules.Needs, need];
		var random = new Random(0);
		var world = GameWorld.CreateFromTown(gameData, TownGenerator.Generate(gameData, CreatePlayerTraits(gameData), random), random);
		world.IsHeadless = !isPlayer && !isPlayerClient;

		var worker = isPlayer ? world.Player : isPlayerClient ? world.Npcs.First(x => x.Family != world.Player.Family && x.Profession != "infirm") : world.Npcs[0];
		var client = isPlayerClient ? world.Player : world.Npcs.First(x => x.Family != worker.Family && x.Profession != "infirm");
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

		if (!client.IsPlayer)
		{
			client.SetPlan(null, [new BusyTask(600)]);
			client.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 600));
		}

		return (world, worker, client);
	}
}
