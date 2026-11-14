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
			else if (e.Person.Goal?.Id == "work")
				Assert.That((e.Person.Location, e.Person.IsAtWork), Is.EqualTo((e.Person.Workplace, true)));
			else
				Assert.That(e.Person.IsAtWork, Is.False);
		};

		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(2, 0, 0));

		Assert.That(starts.Select(x => x.Task.GetType()).Distinct(), Is.EquivalentTo(new[] { typeof(TravelTask), typeof(BusyTask) }));
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
		Assert.That(workers.All(x => x.GetProgress(work) <= work.Minutes), Is.True);
		Assert.That(workers.Count(x => x.GetProgress(work) == work.Minutes), Is.GreaterThanOrEqualTo(workers.Count * 2 / 3));

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
			Assert.That(families.All(x => x.Members.Sum(member => member.GetProgress(need)) <= need.GetQuotaMinutes(x.Members.Count)), Is.True, need.Id);
			Assert.That(families.Count(x => x.Members.Sum(member => member.GetProgress(need)) == need.GetQuotaMinutes(x.Members.Count)), Is.GreaterThanOrEqualTo(families.Count * 3 / 4), need.Id);
		}
	}

	[Test]
	public void TripsEndBackAtHome()
	{
		var (_, world) = GameWorldTests.CreateWorld(4, withSchedules: true);
		var trips = 0;
		world.TaskStarted += (_, e) =>
		{
			// Time at home after a trip, such as hanging up washing, is the last thing done.
			if (e.Task is BusyTask && e.Person.Goal is { ReturnHome: true } && e.Person.Location == e.Person.Home)
				Assert.That(e.Person.Tasks, Has.Count.EqualTo(1));
			else if (e.Task is BusyTask && e.Person.Goal is { ReturnHome: true })
			{
				trips++;
				Assert.That(e.Person.Location.Template, Is.InstanceOf<QuarterTemplate>());
				Assert.That(e.Person.Tasks.OfType<TravelTask>().Last().Destination, Is.SameAs(e.Person.Home));
			}
		};

		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(1, 0, 0));

		Assert.That(trips, Is.GreaterThan(world.Npcs.Count / 2));
	}

	[Test]
	public void LaundryIsOneTripWithTimeAtHomeAfterward()
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
		Assert.That(families.Count(x => x.Members.Sum(member => member.GetProgress(laundry)) == laundry.GetQuotaMinutes(x.Members.Count)), Is.GreaterThanOrEqualTo(families.Count * 3 / 4));
		foreach (var family in families)
		{
			var size = family.Members.Count;
			var spent = busy.Where(x => x.Person.Family == family).ToList();
			Assert.That(spent.Where(x => x.Location != family.Home).Sum(x => x.Minutes), Is.LessThanOrEqualTo(laundry.GetVisitMinutes(size)));
			Assert.That(spent.Where(x => x.Location == family.Home).Sum(x => x.Minutes), Is.LessThanOrEqualTo(laundry.GetHomeMinutes(size)));
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
		person.SetProgress(laundry, laundry.GetQuotaMinutes(person.Family.Members.Count));
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

		// Fatigue is urgent from 85, and only at night.
		worker.SetLevel(fatigue, 92.5);
		Assert.That(scheduler.GetUrgency(worker, fatigue, At(22, 0)), Is.EqualTo(50).Within(0.001));
		Assert.That(scheduler.GetUrgency(worker, fatigue, At(12, 0)), Is.Zero);

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
	public void PlayerSleepsAtHomeUntilRestedOrForAWhile()
	{
		var (_, world) = GameWorldTests.CreateWorld(0, withSchedules: true);
		var calendar = world.GameData.Calendar;
		var player = world.Player;
		var fatigue = GetNeed(world, "fatigue");
		var options = fatigue.PlayerAction!.Options;
		Assert.That(world.GetPlayerActions().Select(x => x.Need), Does.Contain(fatigue));

		// Fatigue builds while the player does nothing, and sleeping until rested takes it back to 0.
		world.AdvanceTime(calendar.CreateTimeOffset(0, 10, 0));
		var level = player.GetLevel(fatigue);
		var minutes = world.GetPlayerActionMinutes(fatigue, options[0]);
		Assert.That(minutes, Is.EqualTo((int) Math.Ceiling(level / 12 * 60)));
		var start = world.Time;
		Assert.That(world.DoPlayerAction(fatigue, minutes), Is.EqualTo(calendar.CreateTimeOffset(0, 0, minutes)));
		Assert.That(world.Time, Is.EqualTo(calendar.AddMinutes(start, minutes)));
		Assert.That(player.GetLevel(fatigue), Is.EqualTo(0).Within(0.000001));
		Assert.That((player.Goal, player.Tasks.Count, player.TaskEnd), Is.EqualTo(((Need?) null, 0, (TimePoint?) null)));
		Assert.That(world.GetPlayerActionMinutes(fatigue, options[0]), Is.Zero);

		// Sleeping until dawn ends at the next 6:00, and sleeping for an hour takes an hour.
		world.DoPlayerAction(fatigue, world.GetPlayerActionMinutes(fatigue, options[1]));
		Assert.That((calendar.GetParts(world.Time).Hour, calendar.GetParts(world.Time).Minute), Is.EqualTo((6, 0)));
		Assert.That(world.GetPlayerActionMinutes(fatigue, options[1]), Is.EqualTo(24 * 60));
		Assert.That(world.GetPlayerActionMinutes(fatigue, options[2]), Is.EqualTo(60));

		// Sleep is only at home.
		player.Location = player.Home.Connections[0];
		Assert.That(world.GetPlayerActions().Select(x => x.Need), Does.Not.Contain(fatigue));
	}

	[Test]
	public void PlayerWorksAtTheirWorkplaceTowardTheirWorkQuota()
	{
		var world = Enumerable.Range(0, 50).Select(x => GameWorldTests.CreateWorld(x, withSchedules: true).World).First(x => x.Player.Business is not null);
		var player = world.Player;
		var work = GetNeed(world, "work");
		var options = work.PlayerAction!.Options;
		player.Location = player.Workplace;
		Assert.That(world.GetPlayerActions().Select(x => x.Need), Does.Contain(work));
		Assert.That(world.GetPlayerActionMinutes(work, options[0]), Is.EqualTo(480));

		var atWork = new List<bool>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == player)
				atWork.Add(player.IsAtWork);
		};
		world.DoPlayerAction(work, world.GetPlayerActionMinutes(work, options[1]));

		Assert.That(atWork, Is.EqualTo(new[] { true }));
		Assert.That(player.IsAtWork, Is.False);
		Assert.That(player.GetProgress(work), Is.EqualTo(120));
		Assert.That(world.GetPlayerActionMinutes(work, options[0]), Is.EqualTo(360));
	}

	[Test]
	public void PlayerDoesHouseholdChoresAtHome()
	{
		var (_, world) = GameWorldTests.CreateWorld(0, withSchedules: true);
		var player = world.Player;
		var upkeep = GetNeed(world, "upkeep");
		ClearFamilyChores(world);
		var quota = 30 + (15 * player.Family.Members.Count);

		Assert.That(world.GetPlayerActions(), Does.Contain(new PlayerActionChoice(upkeep, IsHome: false)));
		Assert.That(world.GetPlayerActionMinutes(upkeep, upkeep.PlayerAction!.Options[0]), Is.EqualTo(quota));
		world.DoPlayerAction(upkeep, quota);
		Assert.That(player.GetProgress(upkeep), Is.EqualTo(quota));
		Assert.That(world.GetPlayerActionMinutes(upkeep, upkeep.PlayerAction.Options[0]), Is.Zero);
	}

	[Test]
	public void PlayerFetchesWaterAtTheSquareAndPutsItAwayAtHome()
	{
		var (_, world) = GameWorldTests.CreateWorld(0, withSchedules: true);
		var player = world.Player;
		var water = GetNeed(world, "water");
		ClearFamilyChores(world);
		var square = world.Locations.First(x => x.Template is OperaEtDies.Data.QuarterTemplate && x.QuarterKind == "square");

		// The water is only fetched at the place, and only put away at home once it's been fetched.
		Assert.That(world.GetPlayerActions().Any(x => x.Need == water), Is.False);
		player.Location = square;
		Assert.That(world.GetPlayerActions(), Does.Contain(new PlayerActionChoice(water, IsHome: false)));
		Assert.That(world.GetPlayerTripMinutes(water, isHome: true), Is.Null);
		Assert.That(world.GetPlayerTripMinutes(water, isHome: false), Is.EqualTo(5));

		var start = world.Time;
		world.DoPlayerTrip(water, isHome: false);
		Assert.That(world.Time, Is.EqualTo(world.GameData.Calendar.AddMinutes(start, 5)));
		Assert.That((player.PendingTrip, player.GetProgress(water)), Is.EqualTo((water, 5L)));
		Assert.That(world.GetPlayerTripMinutes(water, isHome: false), Is.Null);

		// Putting it away takes no time, and the trip is done.
		player.Location = player.Home;
		Assert.That(world.GetPlayerActions(), Does.Contain(new PlayerActionChoice(water, IsHome: true)));
		Assert.That(world.GetPlayerTripMinutes(water, isHome: true), Is.Zero);
		var end = world.Time;
		world.DoPlayerTrip(water, isHome: true);
		Assert.That((world.Time, player.PendingTrip), Is.EqualTo((end, (Need?) null)));
	}

	[Test]
	public void PlayersLaundryIsLeftToThemUntilTheyHangItOutAtHome()
	{
		var (_, world) = GameWorldTests.CreateWorld(0, withSchedules: true);
		var player = world.Player;
		var laundry = GetNeed(world, "laundry");
		var members = player.Family.Members.Count;
		ClearFamilyChores(world);
		player.Location = world.Locations.First(x => x.Template is OperaEtDies.Data.QuarterTemplate && x.QuarterKind == "square");

		Assert.That(world.GetPlayerTripMinutes(laundry, isHome: false), Is.EqualTo(20 * members));
		world.DoPlayerTrip(laundry, isHome: false);

		// While the washing waits to be hung out, the rest of the family leaves it to the player.
		Assert.That(player.PendingTrip, Is.SameAs(laundry));
		Assert.That(player.GetPlannedMinutes(laundry, world.Time), Is.EqualTo(10 * members));
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 2, 0));
		Assert.That(player.Family.Members.Where(x => !x.IsPlayer).Sum(x => x.GetProgress(laundry)), Is.Zero);
		Assert.That(player.Family.Members.Where(x => !x.IsPlayer).Any(x => x.Goal == laundry), Is.False);

		player.Location = player.Home;
		Assert.That(world.GetPlayerTripMinutes(laundry, isHome: true), Is.EqualTo(10 * members));
		world.DoPlayerTrip(laundry, isHome: true);
		Assert.That(player.PendingTrip, Is.Null);
		Assert.That(player.GetProgress(laundry), Is.EqualTo(30 * members));
	}

	/// <summary>
	/// Starts the player's family over on its chores, with nothing done or planned, so the player has them to do.
	/// </summary>
	private static void ClearFamilyChores(GameWorld world)
	{
		foreach (var member in world.Player.Family.Members)
		{
			member.UpdateNeeds(world.Time);
			if (!member.IsPlayer)
				member.SetPlan(null, []);
			foreach (var need in world.GameData.Schedules.Needs.Where(x => x.Kind == NeedKind.Quota))
				member.SetProgress(need, 0);
		}
	}

	private static Need GetNeed(GameWorld world, string id) => world.GameData.Schedules.Needs.Single(x => x.Id == id);
}
