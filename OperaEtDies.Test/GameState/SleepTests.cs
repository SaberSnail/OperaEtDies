using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class SleepTests
{
	[Test]
	public void SomeoneOnlySomewhatTiredNapsOnceBetweenFullSleeps()
	{
		var gameData = TestData.CreateGameData(withSchedules: true, napBelow: 99);
		var world = GameWorld.CreateFromTown(gameData, OperaEtDies.Town.TownGenerator.Generate(gameData, TestData.CreatePlayerTraits(gameData), new Random(0)), new Random(0));
		world.IsHeadless = true;
		var scheduler = new Scheduler(world);
		var testWorld = new TestWorld(gameData);
		var person = testWorld.AddPerson(TestData.CreateTraits(gameData, "female", "Jane", "Smith", "homemaker", "tall", "slim", 40), testWorld.AddLocation(gameData.HouseTemplates[0]));
		var fatigue = Need(gameData, "fatigue");
		var night = At(gameData.Calendar, testWorld.Time, 22);

		person.UpdateNeeds(night);

		// Tired, but not tired enough for a full sleep: a short nap.
		person.SetLevel(fatigue, 95);
		scheduler.ChooseGoal(person, night);
		Assert.That((person.Goal, person.HasNapped), Is.EqualTo((fatigue, true)));
		Assert.That((person.Current?.Step, person.Current?.MinutesLeft), Is.EqualTo(((int?) 0, (int?) 30)));

		// Having napped, they don't nap again.
		person.SetPlan(null, []);
		scheduler.ChooseGoal(person, night);
		Assert.That(person.Goal, Is.Not.SameAs(fatigue));

		// Exhausted, they sleep until rested.
		person.SetLevel(fatigue, 99.5);
		person.SetPlan(null, []);
		scheduler.ChooseGoal(person, night);
		Assert.That((person.Goal, person.HasNapped), Is.EqualTo((fatigue, false)));
		Assert.That((person.Current?.Step, person.Current?.MinutesLeft), Is.EqualTo(((int?) 0, (int?) null)));
	}

	[Test]
	public void HungerCanWakeASleeper()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var calendar = world.GameData.Calendar;
		var scheduler = new Scheduler(world);
		var person = world.Npcs.First(x => x.Business is null || x.Location == x.Home);
		var (fatigue, hunger) = (Need(world.GameData, "fatigue"), Need(world.GameData, "hunger"));
		var night = At(calendar, calendar.GetStartOfDay(world.Time), 22);
		person.Location = person.Home;
		person.SetLevel(fatigue, 95);
		person.SetLevel(hunger, 50);

		// Rested would take nearly eight hours, but by 5:00, when hunger can be met, it's urgent enough to wake them.
		person.SetPlan(fatigue, [new BusyTask(null)]);
		scheduler.Continue(person, night);

		Assert.That(person.TaskEnd, Is.EqualTo(At(calendar, calendar.GetStartOfDay(night), 29)));
	}

	[Test]
	public void WorkHoursPastMidnightAreTwoStretches()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var gameData = world.GameData;
		var scheduler = new Scheduler(world);
		var testWorld = new TestWorld(gameData);
		var home = testWorld.AddLocation(gameData.HouseTemplates[0]);
		var workplace = testWorld.AddLocation(gameData.BusinessTemplates[0]);
		var worker = testWorld.AddPerson(TestData.CreateTraits(gameData, "male", "Tom", "Smith", "smith", "tall", "slim", 40), home);
		var smith = gameData.Professions.Get("smith");
		var nightSmith = new Profession { Id = smith.Id, Title = smith.Title, Business = new ProfessionBusiness { Wealth = smith.Wealth!.Value, Hours = new Hours { Start = 20, End = 4 } } };
		testWorld.Objects.Create(id => new Business(id, nightSmith, worker, [], workplace));
		var work = Need(gameData, "work");
		var day = gameData.Calendar.GetStartOfDay(testWorld.Time);

		Assert.That(Scheduler.GetStretches(worker, work), Is.EqualTo(new[] { (0, 4), (20, 24) }));
		Assert.That(scheduler.IsInWorkHours(worker, At(gameData.Calendar, day, 2)), Is.True);
		Assert.That(scheduler.IsInWorkHours(worker, At(gameData.Calendar, day, 12)), Is.False);
		Assert.That(scheduler.IsInWorkHours(worker, At(gameData.Calendar, day, 22)), Is.True);

		// Late in the evening, the day's work is still due before midnight, with the morning's stretch already over.
		Assert.That(scheduler.GetUrgency(worker, work, At(gameData.Calendar, day, 22)), Is.EqualTo(100));
	}

	private static TimePoint At(OperaEtDies.Time.GameCalendar calendar, TimePoint day, int hour) => calendar.GetStartOfDay(day) + calendar.CreateTimeOffset(0, hour, 0);

	private static Need Need(GameData gameData, string id) => gameData.Schedules.Needs.Single(x => x.Id == id);
}
