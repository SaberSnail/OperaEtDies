using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class SchedulerTests
{
	[Test]
	public void NewGameStartsWhenThePlayerWakesAroundTheWakeHour()
	{
		for (var seed = 0; seed < 20; seed++)
		{
			var (_, world) = GameWorldTests.CreateWorld(seed, withSchedules: true);
			var calendar = world.GameData.Calendar;
			var parts = calendar.GetParts(world.Time);
			var fatigue = GetNeed(world, "fatigue");

			Assert.That(parts.Day, Is.EqualTo(1));
			Assert.That((parts.Hour * 60) + parts.Minute, Is.InRange((6 * 60) - 10, (6 * 60) + 10));
			Assert.That(world.Player.Tasks, Is.Empty);
			Assert.That(world.Player.TaskEnd, Is.Null);
			Assert.That(world.Player.Location, Is.SameAs(world.Player.Home));
			Assert.That(world.Player.GetLevel(fatigue), Is.LessThan(1));
			Assert.That(world.Npcs.All(x => x.TaskEnd is not null && x.TaskEnd >= world.Time), Is.True);
		}
	}

	[Test]
	public void PeopleWalkPathByPathAndWorkAtTheirWorkplace()
	{
		var (_, world) = GameWorldTests.CreateWorld(3, withSchedules: true);
		var starts = new List<(Person Person, string? Goal, PersonTask Task, string? QuarterKind)>();
		world.TaskStarted += (_, e) =>
		{
			starts.Add((e.Person, e.Person.Goal?.Id, e.Task, e.Person.Location.QuarterKind));
			if (e.Task is TravelTask travel)
				Assert.That(e.Person.Location.Connections, Does.Contain(travel.Destination));
			else if (e.Task is BusyTask && e.Person.Goal?.Id == "work")
				Assert.That((e.Person.Location, e.Person.IsAtWork), Is.EqualTo((e.Person.Workplace, true)));
			else if (e.Task is BusyTask)
				Assert.That(e.Person.IsAtWork, Is.False);
		};

		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(2, 0, 0));

		Assert.That(starts.Select(x => x.Task.GetType()).Distinct(), Is.SubsetOf(new[] { typeof(TravelTask), typeof(BusyTask), typeof(TalkTask) }).And.Contains(typeof(TravelTask)).And.Contains(typeof(BusyTask)));
		Assert.That(starts.Select(x => x.Goal).Distinct(), Is.SupersetOf(new[] { "fatigue", "hunger", "restlessness", "work", "upkeep" }));
		Assert.That(starts.Where(x => x.Task is BusyTask && x.Goal == "restlessness").Select(x => x.QuarterKind).Distinct(), Is.SubsetOf(new[] { "square", "landmark" }));
	}

	[Test]
	public void WorkersPutInAFullDayAndRestOnTheSeventhDay()
	{
		var (_, world) = GameWorldTests.CreateWorld(1, withSchedules: true);
		var calendar = world.GameData.Calendar;
		var work = GetNeed(world, "work");
		var workers = world.Npcs.Where(x => x.Business is not null).ToList();

		// The game starts on the first day, so by 19:00 the working day is over.
		world.AdvanceTime(calendar.GetStartOfDay(world.Time) + calendar.CreateTimeOffset(0, 19, 0) - world.Time);

		Assert.That(workers, Is.Not.Empty);
		Assert.That(workers.All(x => x.GetProgress(work) <= work.GetQuotaMinutes(1)), Is.True);
		Assert.That(workers.Count(x => x.GetProgress(work) == work.GetQuotaMinutes(1)), Is.GreaterThanOrEqualTo(workers.Count * 2 / 3));

		var workOnSeventhDay = 0;
		world.AdvanceTime(calendar.CreateTimeOffset(5, 6, 0));
		world.TaskStarted += (_, e) => workOnSeventhDay += e.Person.Goal == work ? 1 : 0;
		world.AdvanceTime(calendar.CreateTimeOffset(1, 0, 0));

		Assert.That(calendar.GetParts(world.Time).Day, Is.EqualTo(1));
		Assert.That(workOnSeventhDay, Is.Zero);
	}

	[Test]
	public void SharedNeedsAreMetByTheFamily()
	{
		var (_, world) = GameWorldTests.CreateWorld(2, withSchedules: true);
		var calendar = world.GameData.Calendar;
		var (upkeep, water) = (GetNeed(world, "upkeep"), GetNeed(world, "water"));

		world.AdvanceTime(calendar.GetStartOfDay(world.Time) + calendar.CreateTimeOffset(0, 21, 0) - world.Time);

		// The quota grows with the family, and the family doesn't do more than it needs, since everyone counts what the
		// others are already doing.
		var families = world.Families.Where(x => !x.Members.Any(member => member.IsPlayer)).ToList();
		foreach (var need in new[] { upkeep, water })
		{
			Assert.That(families.All(x => x.Members.Sum(member => member.GetProgressMinutes(need)) <= need.GetQuotaMinutes(x.Members.Count)), Is.True, need.Id);
			Assert.That(families.Count(x => x.Members.Sum(member => member.GetProgressMinutes(need)) == need.GetQuotaMinutes(x.Members.Count)), Is.GreaterThanOrEqualTo(families.Count * 3 / 4), need.Id);
		}
	}

	[Test]
	public void RunsEndBackAtHome()
	{
		var (_, world) = GameWorldTests.CreateWorld(4, withSchedules: true);
		var water = GetNeed(world, "water");
		var runs = 0;
		var progress = world.People.ToDictionary(x => x, x => x.GetProgress(water));
		world.TaskStarted += (_, e) =>
		{
			// Fetching water is done at a quarter, and each run is counted once the person is back home.
			if (e.Task is BusyTask && e.Person.Goal == water)
				Assert.That(e.Person.Location.Template, Is.InstanceOf<QuarterTemplate>());
			if (e.Person.GetProgress(water) > progress[e.Person])
			{
				runs++;
				Assert.That(e.Person.Location, Is.SameAs(e.Person.Home));
			}

			progress[e.Person] = e.Person.GetProgress(water);
		};

		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 12, 0));

		Assert.That(runs, Is.GreaterThan(world.Npcs.Count / 2));
	}

	[Test]
	public void LaundryIsOneRunWithTimeAtHomeAfterward()
	{
		var (_, world) = GameWorldTests.CreateWorld(5, withSchedules: true);
		var calendar = world.GameData.Calendar;
		var laundry = GetNeed(world, "laundry");
		var busy = new List<(Person Person, Location Location, int Minutes)>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Task is BusyTask { Minutes: { } minutes } && e.Person.Goal == laundry)
				busy.Add((e.Person, e.Person.Location, minutes));
		};

		// The game starts on the first day, wash day, which is over by 18:00.
		world.AdvanceTime(calendar.GetStartOfDay(world.Time) + calendar.CreateTimeOffset(0, 18, 0) - world.Time);

		var families = world.Families.Where(x => !x.Members.Any(member => member.IsPlayer)).ToList();
		Assert.That(families.Count(x => x.Members.Sum(member => member.GetProgress(laundry)) == 1), Is.GreaterThanOrEqualTo(families.Count * 3 / 4));
		foreach (var family in families)
		{
			var size = family.Members.Count;
			var spent = busy.Where(x => x.Person.Family == family).ToList();
			// One person does the whole run, and nobody else in the family starts another.
			Assert.That(spent.Sum(x => x.Minutes), Is.LessThanOrEqualTo(laundry.GetRunMinutes(size)));
			Assert.That(spent.Where(x => x.Location == family.Home).Sum(x => x.Minutes), Is.LessThanOrEqualTo(10 * size));
			Assert.That(family.Members.Select(x => x.GetBundles().Count), Is.All.Zero);
		}
	}

	[Test]
	public void MissedWashDayIsOverdueForTheRestOfTheWeek()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var calendar = world.GameData.Calendar;
		var scheduler = new Scheduler(world);
		var person = world.Npcs[0];
		var laundry = GetNeed(world, "laundry");
		var monday = calendar.GetStartOfDay(world.Time);

		// Early on wash day there's plenty of time, so it isn't urgent yet. Once wash day has passed without it, it's
		// overdue during its hours on the other days, until it's done.
		Assert.That(scheduler.GetUrgency(person, laundry, At(0, 7)), Is.Zero);
		Assert.That(scheduler.GetUrgency(person, laundry, At(1, 9)), Is.EqualTo(80));
		Assert.That(scheduler.GetUrgency(person, laundry, At(1, 18)), Is.Zero);
		person.SetProgress(laundry, laundry.GetQuotaRuns(person.Family.Members.Count));
		Assert.That(scheduler.GetUrgency(person, laundry, At(2, 9)), Is.Zero);

		TimePoint At(int day, int hour) => monday + calendar.CreateTimeOffset(day, hour, 0);
	}

	[Test]
	public void QuotasStartAgainEachDayOrWeek()
	{
		var gameData = TestData.CreateGameData(withSchedules: true);
		var calendar = gameData.Calendar;
		var world = new TestWorld(gameData);
		var person = world.AddPerson(TestData.CreatePlayerTraits(gameData), world.AddLocation(gameData.HouseTemplates[0]));
		var (upkeep, mending) = (gameData.Schedules.Needs.Single(x => x.Id == "upkeep"), gameData.Schedules.Needs.Single(x => x.Id == "mending"));
		var monday = world.Time;

		// Half an hour of each on the first day of the week.
		Spend(upkeep, monday, 30);
		Spend(mending, calendar.AddMinutes(monday, 30), 30);
		Assert.That((person.GetProgress(upkeep), person.GetProgress(mending)), Is.EqualTo((30L, 30L)));

		// The next day, the daily quota starts again, but the weekly one doesn't.
		Spend(mending, calendar.AddMinutes(monday, 24 * 60), 30);
		Assert.That((person.GetProgress(upkeep), person.GetProgress(mending)), Is.EqualTo((0L, 60L)));

		// The next week, the weekly one starts again too.
		person.UpdateNeeds(calendar.AddMinutes(monday, 7 * 24 * 60));
		Assert.That((person.GetProgress(upkeep), person.GetProgress(mending)), Is.EqualTo((0L, 0L)));

		void Spend(Need need, TimePoint start, int minutes)
		{
			person.UpdateNeeds(start);
			person.SetPlan(need, [new BusyTask(minutes)]);
			person.StartTask(calendar.AddMinutes(start, minutes));
			person.UpdateNeeds(calendar.AddMinutes(start, minutes));
			person.FinishTask();
		}
	}

	[Test]
	public void WeeklyQuotasGrowUrgentAsTheWeekRunsOut()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var calendar = world.GameData.Calendar;
		var scheduler = new Scheduler(world);
		var person = world.Npcs[0];
		var mending = GetNeed(world, "mending");
		var week = calendar.GetStartOfWeek(world.Time);
		var quota = mending.GetQuotaMinutes(person.Family.Members.Count);

		// Fifteen hours a day for seven days is 6300 minutes. With a lead of 3000, mending only becomes urgent later in the
		// week, and is fully urgent on the last evening.
		var monday = scheduler.GetUrgency(person, mending, week + calendar.CreateTimeOffset(0, 9, 0));
		var friday = scheduler.GetUrgency(person, mending, week + calendar.CreateTimeOffset(4, 9, 0));
		var sunday = scheduler.GetUrgency(person, mending, week + calendar.CreateTimeOffset(6, 21, -quota));

		Assert.That(monday, Is.Zero);
		Assert.That(friday, Is.GreaterThan(0).And.LessThan(100));
		Assert.That(sunday, Is.EqualTo(100));
	}

	[Test]
	public void LevelNeedsGrowDropAndPause()
	{
		var gameData = TestData.CreateGameData(withSchedules: true);
		var world = new TestWorld(gameData);
		var person = world.AddPerson(TestData.CreatePlayerTraits(gameData), world.AddLocation(gameData.HouseTemplates[0]));
		var (fatigue, hunger, restlessness) = (gameData.Schedules.Needs[0], gameData.Schedules.Needs[1], gameData.Schedules.Needs[2]);
		person.SetLevel(fatigue, 50);
		person.SetLevel(hunger, 10);
		person.SetLevel(restlessness, 20);

		// Sleeping meets fatigue and pauses restlessness, but hunger still grows.
		person.SetPlan(fatigue, [new BusyTask(60)]);
		person.StartTask(After(world.Time, 60));
		person.UpdateNeeds(After(world.Time, 60));
		Assert.That((person.GetLevel(fatigue), person.GetLevel(hunger), person.GetLevel(restlessness)), Is.EqualTo((38.0, 20.0, 20.0)));

		person.FinishTask();
		person.UpdateNeeds(After(world.Time, 180));
		Assert.That((person.GetLevel(fatigue), person.GetLevel(hunger), person.GetLevel(restlessness)), Is.EqualTo((50.0, 40.0, 30.0)));

		// Levels stay within their range.
		person.UpdateNeeds(After(world.Time, 60 * 24));
		Assert.That(person.GetLevel(fatigue), Is.EqualTo(OperaEtDies.Data.Need.MaximumLevel));

		TimePoint After(TimePoint start, int minutes) => gameData.Calendar.AddMinutes(start, minutes);
	}

	[Test]
	public void UrgencyRisesAsTimeRunsOutOrLevelsRise()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var calendar = world.GameData.Calendar;
		var scheduler = new Scheduler(world);
		var worker = world.Npcs.First(x => x.Business is not null);
		var (fatigue, work) = (GetNeed(world, "fatigue"), GetNeed(world, "work"));

		// The second day is a working day. Eight hours of work between 6:00 and 19:00 leaves five spare, which is the lead.
		var nextDay = calendar.GetStartOfDay(world.Time) + calendar.CreateTimeOffset(1, 0, 0);
		Assert.That(scheduler.GetUrgency(worker, work, At(5, 0)), Is.Zero);
		Assert.That(scheduler.GetUrgency(worker, work, At(6, 0)), Is.Zero);
		Assert.That(scheduler.GetUrgency(worker, work, At(9, 0)), Is.EqualTo(60).Within(0.001));
		Assert.That(scheduler.GetUrgency(worker, work, At(12, 0)), Is.EqualTo(100));
		Assert.That(scheduler.GetUrgency(worker, work, At(19, 0)), Is.Zero);

		// Fatigue is urgent from 85, and only at night, for someone with no work to go to bed for.
		var testWorld = new TestWorld(world.GameData);
		var idle = testWorld.AddPerson(TestData.CreateTraits(world.GameData, "female", "Jane", "Smith", "homemaker", "tall", "slim", 40), testWorld.AddLocation(world.GameData.HouseTemplates[0]));
		idle.SetLevel(fatigue, 92.5);
		Assert.That(scheduler.GetUrgency(idle, fatigue, At(22, 0)), Is.EqualTo(50).Within(0.001));
		Assert.That(scheduler.GetUrgency(idle, fatigue, At(12, 0)), Is.Zero);

		// Someone who works also needs to be rested for it, like a deadline: at 22:00, work at 6:00 is 8 hours away, less
		// 92.5 / 12 hours of sleep, which leaves 17.5 minutes to spare out of the hour's bedtime lead.
		worker.SetLevel(fatigue, 92.5);
		Assert.That(scheduler.GetUrgency(worker, fatigue, At(22, 0)), Is.EqualTo(100 * (1 - (17.5 / 60))).Within(0.001));

		TimePoint At(int hour, int minute) => nextDay + calendar.CreateTimeOffset(0, hour, minute);
	}

	[Test]
	public void HomemakersFindChoresMoreUrgent()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var gameData = world.GameData;
		var scheduler = new Scheduler(world);
		var testWorld = new TestWorld(gameData);
		var smith = testWorld.AddPerson(TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40), testWorld.AddLocation(gameData.HouseTemplates[0]));
		var homemaker = testWorld.AddPerson(TestData.CreateTraits(gameData, "female", "Jane", "Smith", "homemaker", "tall", "slim", 40), testWorld.AddLocation(gameData.HouseTemplates[0]));
		var (upkeep, fatigue) = (GetNeed(world, "upkeep"), GetNeed(world, "fatigue"));
		var afternoon = gameData.Calendar.AddMinutes(testWorld.Time, 15 * 60);

		var usual = scheduler.GetUrgency(smith, upkeep, afternoon);
		Assert.That(usual, Is.GreaterThan(0));
		Assert.That(scheduler.GetUrgency(homemaker, upkeep, afternoon), Is.EqualTo(usual * 3).Within(0.001));

		// Needs the factor doesn't list are unchanged.
		smith.SetLevel(fatigue, 95);
		homemaker.SetLevel(fatigue, 95);
		var night = gameData.Calendar.AddMinutes(testWorld.Time, 22 * 60);
		Assert.That(scheduler.GetUrgency(homemaker, fatigue, night), Is.EqualTo(scheduler.GetUrgency(smith, fatigue, night)));
	}

	[Test]
	public void AProfessionsWorkHoursReplaceTheWorkNeedsHours()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var gameData = world.GameData;
		var scheduler = new Scheduler(world);
		var testWorld = new TestWorld(gameData);
		var home = testWorld.AddLocation(gameData.HouseTemplates[0]);
		var workplace = testWorld.AddLocation(gameData.BusinessTemplates[0]);
		var dayWorker = testWorld.AddPerson(TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40), home, workplace);
		var nightWorker = testWorld.AddPerson(TestData.CreateTraits(gameData, "male", "Tom", "Smith", "smith", "tall", "slim", 40), home);
		var smith = gameData.Professions.Get("smith");
		var lateSmith = new Profession { Id = smith.Id, Title = smith.Title, Business = new ProfessionBusiness { Wealth = smith.Wealth!.Value, Hours = new Hours { Start = 14, End = 24 } } };
		testWorld.Objects.Create(id => new Business(id, lateSmith, nightWorker, [], workplace));
		var work = GetNeed(world, "work");
		var day = gameData.Calendar.GetStartOfDay(testWorld.Time);

		// Late in the evening, the work need's own hours are over, but the late shift still has time to work.
		var evening = day + gameData.Calendar.CreateTimeOffset(0, 20, 0);
		Assert.That(scheduler.GetUrgency(dayWorker, work, evening), Is.Zero);
		Assert.That(scheduler.GetUrgency(nightWorker, work, evening), Is.GreaterThan(0));

		// And in the morning, before the late shift starts, they can't work.
		var morning = day + gameData.Calendar.CreateTimeOffset(0, 9, 0);
		Assert.That(scheduler.GetUrgency(nightWorker, work, morning), Is.Zero);
	}

	[Test]
	public void SomeNeedsAreMetAtTheNearestPlace()
	{
		var (_, world) = GameWorldTests.CreateWorld(6, withSchedules: true);
		var scheduler = new Scheduler(world);
		var (water, restlessness) = (GetNeed(world, "water"), GetNeed(world, "restlessness"));
		var outings = new HashSet<Location?>();
		foreach (var person in world.Npcs)
		{
			// The nearest place is always the same, and is as near as any other place it could be.
			var places = Enumerable.Range(0, 10).Select(_ => scheduler.FindPlace(person, water)).Distinct().ToList();
			Assert.That(places, Has.Count.EqualTo(1));
			var nearest = scheduler.GetTravelMinutesTo(person, places[0]!);
			var others = world.Locations.Where(x => x.Bounds is not null && x.QuarterKind is "square" or "landmark");
			Assert.That(others.All(x => scheduler.GetTravelMinutesTo(person, x) >= nearest), Is.True);
			for (var index = 0; index < 10; index++)
				outings.Add(scheduler.FindPlace(person, restlessness));
		}

		// Other needs vary where they're met.
		Assert.That(outings, Has.Count.GreaterThan(1));
	}

	[Test]
	public void SomePeopleWalkMoreSlowly()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var gameData = world.GameData;
		var scheduler = new Scheduler(world);
		var testWorld = new TestWorld(gameData);
		var home = testWorld.AddLocation(gameData.HouseTemplates[0]);
		var smith = testWorld.AddPerson(TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40), home);
		var infirm = testWorld.AddPerson(TestData.CreateTraits(gameData, "male", "Old", "Smith", "infirm", "tall", "slim", 70), home);
		var quarter = world.Locations.First(x => x.Bounds is not null);
		var next = quarter.Connections.First(x => x.Bounds is not null);

		Assert.That(scheduler.GetTravelMinutes(smith, quarter, next), Is.EqualTo(world.GetTravelMinutes(quarter, next)));
		Assert.That(scheduler.GetTravelMinutes(infirm, quarter, next), Is.EqualTo(world.GetTravelMinutes(quarter, next) * 2));
	}
	[Test]
	public void WorkRepeatsAnHourAtATimeUntilTheShiftIsUsedUp()
	{
		var (world, person, work) = CreateWorkerAtWork();
		var passes = new List<int>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is BusyTask { Minutes: { } minutes, ResumeMinutes: null } && person.Goal == work && person.GoalChoice is null)
				passes.Add(minutes);
		};

		// A shift of two and a half hours is two hours and then half an hour, with nothing more urgent to stop for.
		person.SetPlan(work, [], startStep: GetDoStep(work), goalMinutes: 150);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 4, 0));

		Assert.That(passes, Is.EqualTo(new[] { 60, 60, 30 }));
	}

	[Test]
	public void RepeatingStopsWhenSomethingIsMoreUrgent()
	{
		var (world, person, work) = CreateWorkerAtWork();
		var lastPassEnd = (TimePoint?) null;
		var choiceTime = (TimePoint?) null;
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is BusyTask && person.Goal == work && person.GoalChoice is null)
				lastPassEnd = person.TaskEnd;
			if (e.Person == person && person.GoalChoice is { } choice)
				choiceTime ??= choice.Time;
		};

		// Starving, they stop after the first hour to choose again, though their shift isn't over.
		person.SetLevel(GetNeed(world, "hunger"), OperaEtDies.Data.Need.MaximumLevel);
		person.SetPlan(work, [], startStep: GetDoStep(work), goalMinutes: 240);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 3, 0));

		Assert.That(choiceTime, Is.Not.Null);
		Assert.That(choiceTime, Is.EqualTo(lastPassEnd));
		Assert.That(person.GetProgress(work), Is.LessThan(240));
	}
	[Test]
	public void WalkingCountsTowardAQuotasUrgency()
	{
		var (_, world) = GameWorldTests.CreateWorld(3, withSchedules: true);
		var calendar = world.GameData.Calendar;
		var scheduler = new Scheduler(world);
		var water = GetNeed(world, "water");
		var person = world.Npcs.First(x => x.Profession != "homemaker");
		foreach (var member in person.Family.Members)
		{
			member.SetPlan(null, []);
			member.SetProgress(water, 0);
		}

		// At 19:00 there's an hour left to fetch the water. Without walking, the spare time would be that hour less the
		// fetching, but each trip to the square and back takes walking too, which makes it more urgent.
		var at = calendar.GetStartOfDay(world.Time) + calendar.CreateTimeOffset(0, 19, 0);
		var remaining = water.GetQuotaMinutes(person.Family.Members.Count);
		var withoutWalking = OperaEtDies.Data.Need.MaximumLevel * Math.Clamp(1 - ((60.0 - remaining) / water.Lead!.Value), 0, 1);
		Assert.That(scheduler.GetUrgency(person, water, at), Is.GreaterThan(withoutWalking));
	}

	/// <summary>
	/// Creates a world with a worker at their workplace in the morning of a working day, with nothing done of their work
	/// and nothing else urgent: their level needs at 0, and the rest of their family's chores done.
	/// </summary>
	private static (GameWorld World, Person Person, Need Work) CreateWorkerAtWork()
	{
		var world = Enumerable.Range(0, 50).Select(x => GameWorldTests.CreateWorld(x, withSchedules: true).World).First(x => x.Npcs.Any(y => y.Business is not null));
		var calendar = world.GameData.Calendar;
		while (calendar.GetParts(world.Time).Hour < 9)
			world.AdvanceTime(calendar.CreateTimeOffset(0, 1, 0));

		var person = world.Npcs.First(x => x.Business is not null);
		var work = GetNeed(world, "work");
		person.Location = person.Workplace;
		foreach (var need in world.GameData.Schedules.Needs)
		{
			if (need.Kind == NeedKind.Level)
				person.SetLevel(need, 0);
			else
				person.SetProgress(need, need == work ? 0 : need.IsRuns ? need.GetQuotaRuns(person.Family.Members.Count) : need.GetQuotaMinutes(person.Family.Members.Count));
		}

		return (world, person, work);
	}

	private static int GetDoStep(Need need) => need.Steps.ToList().FindIndex(x => x.Kind == StepKind.Do);

	private static Need GetNeed(GameWorld world, string id) => world.GameData.Schedules.Needs.Single(x => x.Id == id);
}
