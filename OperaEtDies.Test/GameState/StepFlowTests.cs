using GoldenAnvil.Utility;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Town;
using static OperaEtDies.Test.TestData;

namespace OperaEtDies.Test.GameState;

/// <summary>
/// Steps that choose, stop, call step lists, remember places, and wait.
/// </summary>
[TestFixture]
public sealed class StepFlowTests
{
	[Test]
	public void AFailedStepGoesOnSkipsOrStopsAsItSays()
	{
		Step Nowhere(StepFailure? onFail) => new() { Kind = StepKind.GoTo, Place = new SchedulePlace { Kind = NeedPlace.Remembered, Remembered = "nowhere" }, OnFail = onFail };
		Need Lost(StepFailure? onFail) => Errand([Nowhere(onFail), Do(Amount(3)), new Step { Kind = StepKind.Do, Minutes = Amount(7), Id = "after" }]);

		// Somewhere they can't go fails, which gives up the steps unless it says otherwise.
		Assert.That(Run(Lost(null)), Is.Empty);
		Assert.That(Run(Lost(GoOn)), Is.EqualTo(new[] { 3, 7 }));
		Assert.That(Run(Lost(new StepFailure { Any = new StepFailureResult(StepFailureKind.SkipTo, "after") })), Is.EqualTo(new[] { 7 }));
		Assert.That(Run(Lost(new StepFailure { ByCause = new Dictionary<FailCause, StepFailureResult> { [FailCause.No] = new(StepFailureKind.Next) } })), Is.Empty);
	}

	[Test]
	public void ChoosingTakesOneOptionAndSkipsTheOthers()
	{
		var need = Errand([
			new Step { Kind = StepKind.Choose, Options = [Option([Do(Amount(5))]), Option([Do(Amount(7))])] },
			Do(Amount(11)),
		]);

		// The options come after the "choose" step, each going on past the others once it's done.
		Assert.That(need.Steps.Select(x => x.Kind), Is.EqualTo(new[] { StepKind.Choose, StepKind.Do, StepKind.Do, StepKind.Do }));
		Assert.That((need.GetNextStep(0), need.GetNextStep(1), need.GetNextStep(2), need.GetNextStep(3)), Is.EqualTo((3, 3, 3, 4)));
		Assert.That(need.GetOptions(0).Select(x => (x.Start, x.End)), Is.EqualTo(new[] { (1, 2), (2, 3) }));
		Assert.That((need.IsOptional(1), need.IsOptional(3)), Is.EqualTo((true, false)));

		var busy = Run(need);
		Assert.That(busy, Has.Count.EqualTo(2));
		Assert.That(busy[0], Is.EqualTo(5).Or.EqualTo(7));
		Assert.That(busy[1], Is.EqualTo(11));
	}

	[Test]
	public void ChoosingTheFirstTakesTheFirstOptionWhoseConditionIsMet()
	{
		var need = Errand([
			new Step
			{
				Kind = StepKind.Choose,
				First = true,
				Options = [Option([Do(Amount(5))], new StepCondition { At = Quarters(false, "square") }), Option([Do(Amount(7))])],
			},
		]);

		// The person starts at home, not in a square.
		Assert.That(Run(need), Is.EqualTo(new[] { 7 }));
	}

	[Test]
	public void OptionsThatCantBeReachedArentChosen()
	{
		var need = Errand([
			new Step { Kind = StepKind.Choose, Options = [Option([GoTo(Quarters(false, "nowhere")), Do(Amount(5))]), Option([Do(Amount(7))])] },
		]);

		for (var seed = 0; seed < 5; seed++)
			Assert.That(Run(need, seed), Is.EqualTo(new[] { 7 }));
	}

	[Test]
	public void OptionsThatWontFitInWhatsLeftOfTheHoursArentChosen()
	{
		// A day is never long enough for the long option, so only the short one fits in what's left of it.
		var need = new Need
		{
			Id = "chore",
			Title = "Chore",
			StepSettings = [new Step { Kind = StepKind.Choose, Options = [Option([Do(Amount(5))]), Option([Do(Amount(2000))])] }, new Step { Kind = StepKind.CountRun }],
			Hours = new Hours { Start = 0, End = 24 },
			Quota = new QuotaNeedSettings { Lead = 30, Runs = new FamilyAmount { Base = 1 } },
		};
		need.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);

		for (var seed = 0; seed < 5; seed++)
			Assert.That(Run(need, seed), Is.EqualTo(new[] { 5 }));

		// When nothing fits, any option can still be chosen.
		var nothingFits = new Need
		{
			Id = "chore",
			Title = "Chore",
			StepSettings = [new Step { Kind = StepKind.Choose, Options = [Option([Do(Amount(1500))]), Option([Do(Amount(2000))])] }, new Step { Kind = StepKind.CountRun }],
			Hours = new Hours { Start = 0, End = 24 },
			Quota = new QuotaNeedSettings { Lead = 30, Runs = new FamilyAmount { Base = 1 } },
		};
		nothingFits.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		Assert.That(Run(nothingFits), Has.Count.EqualTo(1));
	}

	[Test]
	public void StoppingSkipsTheRestOfTheSteps()
	{
		var need = Errand([Do(Amount(5)), new Step { Kind = StepKind.Stop, When = new StepCondition { At = Home } }, Do(Amount(7))]);

		Assert.That(Run(need), Is.EqualTo(new[] { 5 }));
	}

	[Test]
	public void CallingCarriesOutAStepList()
	{
		var list = new StepList { Id = "twice", Steps = [Do(Amount(3)), Do(Amount(4))] };
		var need = Errand([new Step { Kind = StepKind.Call, List = "twice" }, Do(Amount(7))], [list]);

		Assert.That(need.Steps.Select(x => x.Minutes!.Base), Is.EqualTo(new[] { 3.0, 4.0, 7.0 }));
		Assert.That(Run(need, stepLists: [list]), Is.EqualTo(new[] { 3, 4, 7 }));
	}

	[Test]
	public void PeopleGoBackToAPlaceTheyRemembered()
	{
		var need = Errand([
			new Step { Kind = StepKind.Remember, Name = "start" },
			GoTo(Home),
			new Step { Kind = StepKind.Wait, Minutes = Amount(30) },
			GoTo(new SchedulePlace { Kind = NeedPlace.Remembered, Remembered = "start" }),
			Do(Amount(10)),
		]);

		var (world, person) = CreateWorld(need, 0);
		var start = world.Locations.First(x => x.Template is QuarterTemplate && x != person.Home.Quarter);
		person.Location = start;
		var busy = new List<(Location Location, StepKind Kind)>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is BusyTask { Step: { } step, ResumeMinutes: null } && person.Goal == need)
				busy.Add((person.Location, need.Steps[step].Kind));
		};
		person.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 3, 0));

		// They wait at home, which doesn't meet the need, then go back to where they started.
		Assert.That(busy, Is.EqualTo(new[] { (person.Home, StepKind.Wait), (start, StepKind.Do) }));
		Assert.That(person.Sequences.SelectMany(x => x.RememberedPlaces), Is.Empty);
	}

	[Test]
	public void AskingRemembersTheAnswerForLaterSteps()
	{
		Need Asking(double chance) => Errand([
			new Step { Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "agreed", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(5), Chance = new StepChance { Base = chance }, OnFail = GoOn },
			new Step
			{
				Kind = StepKind.Choose,
				First = true,
				Options = [Option([Do(Amount(3))], new StepCondition { AnsweredYes = "agreed" }), Option([Do(Amount(7))], new StepCondition { AnsweredNo = "agreed" })],
			},
		]);

		// Someone who always agrees says yes, and someone who never does says no. Either way, they talked first.
		var (yes, yesTalks, yesPartner) = RunWithSomeoneThere(Asking(100));
		Assert.That((yes, yesTalks), Is.EqualTo((new[] { 3 }, 1)));
		Assert.That(yesPartner, Is.Not.Null);
		var (no, noTalks, _) = RunWithSomeoneThere(Asking(0));
		Assert.That((no, noTalks), Is.EqualTo((new[] { 7 }, 1)));
	}

	[Test]
	public void AskingForTimeOnlyGetsYesWhenItFitsAroundTheirNeeds()
	{
		Need Asking(int forMinutes) => Errand([
			new Step { Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "agreed", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(5), ForMinutes = Amount(forMinutes), Chance = new StepChance { Base = 100 }, OnFail = GoOn },
			new Step
			{
				Kind = StepKind.Choose,
				First = true,
				Options = [Option([Do(Amount(3))], new StepCondition { AnsweredYes = "agreed" }), Option([Do(Amount(7))], new StepCondition { AnsweredNo = "agreed" })],
			},
		]);

		// The one asked has nothing to do, and every need met.
		static void Rested(GameWorld world, Person other)
		{
			foreach (var need in world.GameData.Schedules.Needs)
			{
				if (need.Kind == NeedKind.Level)
					other.SetLevel(need, 0);
				else
					other.SetProgress(need, 100_000);
			}
		}

		// An hour fits, but three days would leave them desperate for sleep and food.
		Assert.That(RunWithSomeoneThere(Asking(60), Rested).Busy, Is.EqualTo(new[] { 3 }));
		Assert.That(RunWithSomeoneThere(Asking(3 * 24 * 60), Rested).Busy, Is.EqualTo(new[] { 7 }));
	}

	[Test]
	public void StepsDoneForThePlayerIncludeStepsWithOtherPeople()
	{
		var need = Errand([new Step { Kind = StepKind.Talk, With = StepTarget.Anyone, Minutes = Amount(10) }, Do(Amount(5))]);
		var (world, _, other) = CreateWorldWithSomeoneThere(need);
		var player = world.Player;
		world.IsHeadless = false;
		player.SetPlan(null, []);
		player.Location = other.Location;
		var sequence = Scheduler.StartSequence(player, need, 0, null, [], null);
		var talks = 0;
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == player && e.Task is TalkTask)
				talks++;
		};

		world.DoPlayerStepsAutomatically(sequence);
		Assert.That((talks, player.Sequences.Count), Is.EqualTo((1, 0)));
	}

	[Test]
	public void ThePlayerTakesTheSameStepsOneAtATime()
	{
		var need = Errand([
			new Step { Kind = StepKind.Seek, Place = Quarters(false, "square"), Minutes = Amount(10) },
			new Step { Kind = StepKind.Ask, With = StepTarget.Sought, Name = "agreed", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(5), Chance = new StepChance { Base = 100 } },
			Do(Amount(5)),
		]);
		var (world, _) = CreateWorld(need, 0);
		world.IsHeadless = false;
		var player = world.Player;
		player.Location = player.Home;
		var sequence = Scheduler.StartSequence(player, need, 0, null, [], null);

		// The player sees every step anyone else would take.
		Assert.That(world.GetPlayerSteps(sequence).Select(x => x.Step.Kind), Is.EqualTo(new[] { StepKind.Seek, StepKind.Ask, StepKind.Do }));

		// Having one done for them does just that one: walking to the square and looking around, and then asking is next.
		world.DoPlayerStep(sequence);
		Assert.That(player.Location.QuarterKind, Is.EqualTo("square"));
		Assert.That(sequence.Step, Is.EqualTo(1));
	}

	[Test]
	public void TalkingTalksWithSomeoneWhoMeetsTheCondition()
	{
		var need = Errand([new Step { Kind = StepKind.Talk, With = StepTarget.Anyone, Minutes = Amount(10) }, Do(Amount(5))]);
		var (busy, talks, partner) = RunWithSomeoneThere(need);
		Assert.That((busy, talks), Is.EqualTo((new[] { 5 }, 1)));
		Assert.That(partner, Is.Not.Null);

		// No one there meets the condition (the one there isn't infirm), so they talk with no one.
		var nobody = Errand([new Step { Kind = StepKind.Talk, With = StepTarget.Anyone, Who = Who("profession", "infirm"), Minutes = Amount(10) }, Do(Amount(5))]);
		Assert.That(RunWithSomeoneThere(nobody).Talks, Is.Zero);
	}

	[Test]
	public void WhoCanSayWhetherTheyreFamily()
	{
		// The one there is from another family, so they fit "other" but not "member", in values or not values.
		Need Talking(StepWho who) => Errand([new Step { Kind = StepKind.Talk, With = StepTarget.Anyone, Who = who, Minutes = Amount(10) }, Do(Amount(5))]);
		Assert.That(RunWithSomeoneThere(Talking(Who(StepWho.FamilyKey, StepWho.FamilyOther))).Talks, Is.EqualTo(1));
		Assert.That(RunWithSomeoneThere(Talking(Who(StepWho.FamilyKey, StepWho.FamilyMember))).Talks, Is.Zero);
		Assert.That(RunWithSomeoneThere(Talking(new StepWho { NotValues = new Dictionary<string, IReadOnlyList<string>> { [StepWho.FamilyKey] = [StepWho.FamilyOther] } })).Talks, Is.Zero);

		// Someone from the same family fits "member".
		var (world, _) = CreateWorld(Talking(Who(StepWho.FamilyKey, StepWho.FamilyMember)), 0);
		var family = world.People.Select(x => x.Family).First(x => x.Members.Count > 1);
		var (person, member) = (family.Members[0], family.Members[1]);
		var stranger = world.Npcs.First(x => x.Family != person.Family);
		var who = Who(StepWho.FamilyKey, StepWho.FamilyMember);
		Assert.That((Scheduler.IsWho(person, member, who), Scheduler.IsWho(person, stranger, who)), Is.EqualTo((true, false)));
	}

	[Test]
	public void ConditionsOnNumbersUseTheirGroups()
	{
		// Everyone's age falls in one of the age groups, so someone of any of them is talked with.
		var need = Errand([new Step { Kind = StepKind.Talk, With = StepTarget.Anyone, Who = Who("age", "young", "adult", "old", "veryOld"), Minutes = Amount(10) }, Do(Amount(5))]);
		Assert.That(RunWithSomeoneThere(need).Talks, Is.EqualTo(1));
	}

	[TestCase(true)]
	[TestCase(false)]
	public void SeekingAnyoneLooksWhereTheStepSays(bool hasWho)
	{
		var need = Errand([new Step { Kind = StepKind.Seek, Who = hasWho ? Who("age", "young", "adult", "old", "veryOld") : null, Place = Quarters(false, "square"), Minutes = Amount(10) }, Do(Amount(5))]);
		var (world, person) = CreateWorld(need, 0);
		person.Location = person.Home;
		var looked = new List<Location>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is BusyTask { Minutes: 10 })
				looked.Add(person.Location);
		};
		person.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 2, 0));

		// They looked in the square, for no friend in particular.
		Assert.That(looked.Select(x => x.QuarterKind), Is.EqualTo(new[] { "square" }));
	}

	[Test]
	public void SeekingFriendsIsAnOptionOnWho()
	{
		var friend = Errand([new Step { Kind = StepKind.Seek, Who = new StepWho { Friends = true }, Minutes = Amount(10) }, Do(Amount(5))]);
		var anyone = Errand([new Step { Kind = StepKind.Seek, Minutes = Amount(10) }, Do(Amount(5))]);
		Assert.That((friend.Seeks, anyone.Seeks), Is.EqualTo((true, false)));

		// Friends are looked for where clues say they might be, not in a place.
		var settings = CreateScheduleSettings();
		settings.Needs = [.. settings.Needs, Errand([new Step { Kind = StepKind.Seek, Who = new StepWho { Friends = true }, Place = Quarters(false, "square"), Minutes = Amount(10) }, Do(Amount(5))])];
		settings.UrgencyFactors = [];
		Assert.That(settings.Validate(), Is.EqualTo(new[] { OurResources.StepSeekFriendsWithPlace.FormatCurrentCulture("needs[errand].steps[0].place") }));
	}

	[Test]
	public void SeekingAnyoneWithoutAPlaceLooksInAnyQuarter()
	{
		var need = Errand([new Step { Kind = StepKind.Seek, Minutes = Amount(10) }, Do(Amount(5))]);
		var (world, person) = CreateWorld(need, 0);
		person.Location = person.Home;
		var looked = new List<Location>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is BusyTask { Minutes: 10 } && person.Goal == need)
				looked.Add(person.Location);
		};
		person.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 2, 0));

		// They looked around in a quarter, for no friend in particular.
		Assert.That(looked.Select(x => x.Template is QuarterTemplate), Is.EqualTo(new[] { true }));
	}

	[Test]
	public void ConditionsCanBeNegatedOrCombinedWithAny()
	{
		var inSquare = new StepCondition { At = Quarters(false, "square") };
		var atHome = new StepCondition { At = Home };
		var need = Errand([
			DoWhen(Amount(3), new StepCondition { Not = inSquare }),
			DoWhen(Amount(5), new StepCondition { Not = atHome }),
			DoWhen(Amount(7), new StepCondition { Any = [inSquare, atHome] }),
			DoWhen(Amount(11), new StepCondition { Any = [inSquare], Not = atHome }),
		]);

		// The person is at home, not in a square.
		Assert.That(Run(need), Is.EqualTo(new[] { 3, 7 }));
	}

	[Test]
	public void AYesTakesOnAJobWhoseSiteTheStepsGoTo()
	{
		var need = Errand([
			new Step
			{
				Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "takesJob", Minutes = Amount(5), Chance = new StepChance { Base = 100 }, Player = new StepPlayer { Title = "Ask" },
				Job = new StepJob { Place = new SchedulePlace { Kind = NeedPlace.PartnerHome }, Minutes = 600 },
			},
			GoTo(new SchedulePlace { Kind = NeedPlace.JobSite }, new StepCondition { HasJob = true }),
			Do(Amount(30)),
		]);
		var (world, person, other) = CreateWorldWithSomeoneThere(need);
		Location? workedAt = null;
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is BusyTask { Minutes: 30, ResumeMinutes: null })
				workedAt = person.Location;
		};
		person.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 2, 0));

		// The job is at the home of the one who said yes, and they went there to work on it.
		var job = world.Jobs.Single();
		Assert.That((job.Worker, job.Client, job.Need, job.Place, job.Minutes), Is.EqualTo((person, other, need, other.Home, 600)));
		Assert.That(person.GetJob(need), Is.SameAs(job));
		Assert.That(workedAt, Is.EqualTo(other.Home));
	}

	[Test]
	public void TalkingCanMeetANeedForThePartner()
	{
		var need = Errand([new Step { Kind = StepKind.Talk, With = StepTarget.Anyone, Minutes = Amount(5), PartnerMeets = "errand" }]);
		var (world, person, other) = CreateWorldWithSomeoneThere(need);
		Need? met = null;
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == other && e.Task is TalkTask talk)
				met = talk.Need;
		};
		person.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 0, 30));

		Assert.That(met, Is.SameAs(need));
	}

	[Test]
	public void RunsCountTheFewestAndMostMinutesAnyWayThroughThem()
	{
		var need = Errand([
			new Step { Kind = StepKind.Choose, Options = [Option([Do(Amount(5))]), Option([Do(Amount(7)), DoWhen(Amount(2), new StepCondition { At = Home })])] },
			Do(Amount(11)),
			new Step { Kind = StepKind.Choose, First = true, Options = [Option([Do(Amount(3))], new StepCondition { At = Home })] },
		]);

		// The fewest skips the step with a condition and the option that takes the first, and the most takes them all.
		Assert.That(need.GetRunMinuteRange(0), Is.EqualTo(new MinuteRange(16, 23)));
		Assert.That(need.GetRunMinutes(0), Is.EqualTo(23));
	}

	[Test]
	public void LeadingTakesThePartnerAlongUntilReleased()
	{
		var need = Errand([
			new Step { Kind = StepKind.Talk, With = StepTarget.Anyone, Minutes = Amount(5) },
			Do(Amount(1)),
			new Step { Kind = StepKind.Lead, Minutes = Amount(120) },
			GoTo(new SchedulePlace { Kind = NeedPlace.PartnerHome }),
			Do(Amount(30)),
			new Step { Kind = StepKind.Release },
			Do(Amount(10)),
		]);
		var (world, person, other) = CreateWorldWithSomeoneThere(need);
		(Location? Place, PersonTask? Task)? atHome = null;
		PersonTask? afterRelease = null;
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is BusyTask { Minutes: 30, ResumeMinutes: null })
				atHome = (other.Location, other.CurrentTask);
			if (e.Person == person && e.Task is BusyTask { Minutes: 10, ResumeMinutes: null })
				afterRelease = other.CurrentTask;
		};
		person.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 2, 0));

		// The one they talked with followed them to their own home, and was let go once they were done there.
		Assert.That(atHome, Is.Not.Null);
		Assert.That(atHome!.Value.Place, Is.EqualTo(other.Home));
		Assert.That(atHome.Value.Task, Is.TypeOf<FollowTask>());
		Assert.That(afterRelease, Is.Not.TypeOf<FollowTask>());
	}

	[Test]
	public void TheChanceOfYesComesFromTheData()
	{
		var (world, asking) = CreateWorld(Errand([Do(Amount(5))]), 0);
		var asked = world.Npcs.First(x => x.Family != asking.Family);
		asked.GetRelationship(asking).Score = 50;
		asked.SetPlan(null, []);
		var scheduler = new Scheduler(world);
		var profession = asked.Profession ?? "";

		// 20, plus half of 50 for the relationship, doubled for their profession.
		var chance = new StepChance { Base = 20, PerScore = 50, Factors = [new ChanceFactor { When = Condition("profession", profession), Factor = 2 }] };
		Assert.That(scheduler.GetChance(asked, asking, chance, world.Time), Is.EqualTo(90).Within(0.001));
		Assert.That(scheduler.GetChance(asked, asking, new StepChance { Base = 80, PerScore = 50 }, world.Time), Is.EqualTo(100));
	}

	[Test]
	public void FlowStepsMustFitTheirKind()
	{
		var settings = CreateScheduleSettings();
		settings.StepLists = [new StepList { Id = "loop", Steps = [new Step { Kind = StepKind.Call, List = "loop" }] }];
		settings.Needs =
		[
			.. settings.Needs,
			Errand([
				new Step { Kind = StepKind.Choose },
				new Step { Kind = StepKind.Choose, Options = [], Player = new StepPlayer { Title = "Pick", Result = "You picked" } },
				new Step { Kind = StepKind.Call, List = "missing" },
				new Step { Kind = StepKind.Call, List = "loop" },
				new Step { Kind = StepKind.Remember },
				new Step { Kind = StepKind.Wait, Minutes = Amount(5), UntilHour = 6 },
				new Step { Kind = StepKind.Wait },
				GoTo(new SchedulePlace { Kind = NeedPlace.Remembered, Remembered = "nowhere" }),
				DoWhen(Amount(5), new StepCondition { ShiftLeft = 0 }),
				new Step { Kind = StepKind.Ask },
				new Step { Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "q", Player = new StepPlayer { Title = "Ask" }, Minutes = Amount(1), Chance = new StepChance { Base = 150 } },
				DoWhen(Amount(5), new StepCondition { AnsweredYes = "unasked" }),
			]),
			new Need { Id = "remembering", Title = "Remembering", PlaceSetting = new SchedulePlace { Kind = NeedPlace.Remembered, Remembered = "spot" }, Level = new LevelNeedSettings { PerHour = 1, SatisfyPerHour = 1 } },
		];
		settings.UrgencyFactors = [];

		Assert.That(settings.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[errand].steps[0].options", "choose"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[errand].steps[1].player", "choose"),
			OurResources.DataListEmpty.FormatCurrentCulture("needs[errand].steps[1].options"),
			OurResources.DataIdUnknown.FormatCurrentCulture("needs[errand].steps[2].list", "missing", "stepLists"),
			OurResources.StepListCalledInItself.FormatCurrentCulture("stepLists[loop].steps[0].list", "loop"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[errand].steps[4].name", "remember"),
			OurResources.StepWaitInvalid.FormatCurrentCulture("needs[errand].steps[5]"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[errand].steps[6].minutes", "wait"),
			OurResources.StepPlaceUnknown.FormatCurrentCulture("needs[errand].steps[7].place.remembered", "nowhere"),
			OurResources.DataNumberNotPositive.FormatCurrentCulture("needs[errand].steps[8].when.shiftLeft", 0),
			OurResources.SchedulePlaceRememberedNotAllowed.FormatCurrentCulture("needs[remembering].place.remembered"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[errand].steps[9].minutes", "ask"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[errand].steps[9].with", "ask"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[errand].steps[9].chance", "ask"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[errand].steps[9].name", "ask"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[errand].steps[9].player", "ask"),
			OurResources.StepChanceOutOfRange.FormatCurrentCulture("needs[errand].steps[10].chance.base"),
			OurResources.StepAnswerUnknown.FormatCurrentCulture("needs[errand].steps[11].when.answeredYes", "unasked"),
		}));
	}

	/// <summary>
	/// Creates a level need with the given steps, which never grows, so nothing else interferes with it.
	/// </summary>
	private static Need Errand(IReadOnlyList<Step> steps, IReadOnlyList<StepList>? stepLists = null)
	{
		var need = new Need { Id = "errand", Title = "Errand", StepSettings = steps, Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 } };
		need.StepLists = (stepLists ?? []).ToDictionary(x => x.Id, StringComparer.Ordinal);
		return need;
	}

	private static StepOption Option(IReadOnlyList<Step> steps, StepCondition? when = null) => new() { Steps = steps, When = when };

	/// <summary>
	/// Carries on with the next step after a step fails, such as an "ask" step answered no, so later steps can tell.
	/// </summary>
	private static StepFailure GoOn { get; } = new() { Any = new StepFailureResult(StepFailureKind.Next) };

	private static Step DoWhen(FamilyDuration minutes, StepCondition when) => new() { Kind = StepKind.Do, Minutes = minutes, When = when };

	/// <summary>
	/// Has a person at home in a world with a need carry out its steps, and gets the minutes of each step of it they spent
	/// time on, in order.
	/// </summary>
	private static IReadOnlyList<int> Run(Need need, int seed = 0, IReadOnlyList<StepList>? stepLists = null)
	{
		var (world, person) = CreateWorld(need, seed, stepLists);
		person.Location = person.Home;
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

	/// <summary>
	/// Has a person carry out a need's steps with someone from another family where they are, who isn't infirm or doing anything:
	/// the minutes of each step of the need they spent time on, how many times they started talking, and their partner.
	/// </summary>
	private static (IReadOnlyList<int> Busy, int Talks, Person? Partner) RunWithSomeoneThere(Need need, Action<GameWorld, Person>? prepareOther = null)
	{
		var (world, person, other) = CreateWorldWithSomeoneThere(need);
		prepareOther?.Invoke(world, other);
		var busy = new List<int>();
		var talks = 0;
		Person? partner = null;
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is BusyTask { Minutes: { } minutes, ResumeMinutes: null } && person.Goal == need)
				busy.Add(minutes);
			if (e.Person == person && e.Task is TalkTask { With: var with } && with == other)
				talks++;
			partner ??= person.Partner;
		};
		person.SetPlan(need, [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 0, 30));
		return (busy, talks, partner);
	}

	/// <summary>
	/// Creates a world with a need and someone in a quarter with someone from another family, who isn't infirm or doing
	/// anything, and no one else.
	/// </summary>
	private static (GameWorld World, Person Person, Person Other) CreateWorldWithSomeoneThere(Need need)
	{
		var (world, person) = CreateWorld(need, 0);
		var other = world.Npcs.First(x => x.Family != person.Family && x.Profession != "infirm");
		var place = world.Locations.First(x => x.Template is QuarterTemplate);
		foreach (var someone in world.People.Where(x => x.Location == place))
			someone.Location = someone.Home;
		person.Location = place;
		other.Location = place;
		other.SetPlan(null, [new BusyTask(600)]);
		other.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 600));
		return (world, person, other);
	}

	/// <summary>
	/// Creates a world with the test schedules and a need, and someone in it who isn't doing anything.
	/// </summary>
	private static (GameWorld World, Person Person) CreateWorld(Need need, int seed, IReadOnlyList<StepList>? stepLists = null)
	{
		var gameData = CreateGameData(withSchedules: true);
		gameData.Schedules.StepLists = stepLists ?? [];
		gameData.Schedules.Needs = [.. gameData.Schedules.Needs, need];
		var random = new Random(seed);
		var world = GameWorld.CreateFromTown(gameData, TownGenerator.Generate(gameData, CreatePlayerTraits(gameData), random), random);
		world.IsHeadless = true;
		return (world, world.Npcs[0]);
	}
}
