using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class SightingTests
{
	[Test]
	public void PeopleRememberTheirFamilyFromWhenTheyWoke()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var family = world.Families.First(x => x.Members.Count > 1);
		var (first, second) = (family.Members[0], family.Members[1]);

		Assert.That(first.Sightings[second].Location, Is.SameAs(family.Home));
		Assert.That(first.Sightings[second].IsSecondhand, Is.False);
		Assert.That(first.Sightings.Keys, Has.None.SameAs(first));
	}

	[Test]
	public void StartingSomethingIsSeenByEveryoneAwake()
	{
		var (_, world, quarter, people) = CreatePeopleTogether(3);
		var (watcher, sleeper, worker) = (people[0], people[1], people[2]);
		var (sleep, laundry) = (Need(world, world.GameData.Schedules.SleepNeed!), Need(world, "laundry"));

		// Falling asleep, the sleeper sees everyone there.
		Start(world, sleeper, sleep, new BusyTask(600));
		Assert.That(sleeper.Sightings[watcher], Is.EqualTo(new Sighting(quarter, world.Time, SightingActivity.None)));
		Assert.That(watcher.Sightings[sleeper], Is.EqualTo(new Sighting(quarter, world.Time, SightingActivity.Busy, sleep, Doing: sleep.GetActivity(null))));

		// Once asleep, they see no one new.
		var later = world.GameData.Calendar.AddMinutes(world.Time, 5);
		Start(world, worker, laundry, new BusyTask(30), later);
		Assert.That(watcher.Sightings[worker], Is.EqualTo(new Sighting(quarter, later, SightingActivity.Busy, laundry, Doing: laundry.GetActivity(null))));
		Assert.That(worker.Sightings[sleeper], Is.EqualTo(new Sighting(quarter, later, SightingActivity.Busy, sleep, Doing: sleep.GetActivity(null))));
		Assert.That(sleeper.Sightings[worker], Is.EqualTo(new Sighting(quarter, world.Time, SightingActivity.None)));

		// Setting off along a path shows only the next place, and idling has no need.
		Start(world, worker, laundry, new TravelTask(world.Locations[0]), later);
		Assert.That(watcher.Sightings[worker], Is.EqualTo(new Sighting(quarter, later, SightingActivity.Walking, Destination: world.Locations[0])));
		Start(world, worker, null, new BusyTask(10), later);
		Assert.That(watcher.Sightings[worker], Is.EqualTo(new Sighting(quarter, later, SightingActivity.Idle)));
	}

	[Test]
	public void LeavingIsSeenEvenByThoseWhoLeaveAtTheSameTime()
	{
		var (_, world, quarter, people) = CreatePeopleTogether(3);
		var (first, second, third) = (people[0], people[1], people[2]);
		var (east, west) = (world.Locations.First(x => x != quarter), world.Locations.Last(x => x != quarter));
		var calendar = world.GameData.Calendar;
		var time = calendar.AddMinutes(world.Time, 10);

		world.MovePerson(first, east, time);
		world.MovePerson(second, west, time);

		Assert.That(third.Sightings[first], Is.EqualTo(new Sighting(quarter, time, SightingActivity.Left, Destination: east)));
		Assert.That(third.Sightings[second], Is.EqualTo(new Sighting(quarter, time, SightingActivity.Left, Destination: west)));
		Assert.That(second.Sightings[first], Is.EqualTo(new Sighting(quarter, time, SightingActivity.Left, Destination: east)));
		Assert.That(first.Sightings[second], Is.EqualTo(new Sighting(quarter, time, SightingActivity.Left, Destination: west)));
		Assert.That(first.Sightings[third], Is.EqualTo(new Sighting(quarter, time, SightingActivity.None)));

		// Someone who left earlier doesn't see a later departure.
		var later = calendar.AddMinutes(time, 1);
		world.MovePerson(third, east, later);
		Assert.That(first.Sightings[third].Time, Is.EqualTo(time));
	}

	[Test]
	public void SightingsAreForgottenAfterADay()
	{
		var (_, world, quarter, people) = CreatePeopleTogether(2);
		var calendar = world.GameData.Calendar;

		// They're kept apart, so they don't see each other again as time passes.
		people[1].Location = world.Locations.First(x => x != quarter && x.People.Count == 0);
		var old = world.Time - calendar.CreateTimeOffset(0, world.GameData.Schedules.SightingHours, 1);
		people[0].See(people[1], new Sighting(quarter, old, SightingActivity.None));
		people[1].See(people[0], new Sighting(quarter, calendar.AddMinutes(old, 2), SightingActivity.None));

		world.AdvanceTime(calendar.CreateTimeOffset(0, 0, 0));

		Assert.That(people[0].Sightings.ContainsKey(people[1]), Is.False);
		Assert.That(people[1].Sightings.ContainsKey(people[0]), Is.True);
	}

	[Test]
	public void LearnedSightingsAreSecondhandAndOnlyReplaceOlderOnes()
	{
		var (_, world, quarter, people) = CreatePeopleTogether(3);
		var (listener, teller, subject) = (people[0], people[1], people[2]);
		var calendar = world.GameData.Calendar;
		var seen = new Sighting(quarter, world.Time, SightingActivity.Idle);
		listener.See(subject, seen);

		Assert.That(listener.LearnSighting(subject, seen with { Time = calendar.AddMinutes(world.Time, -5) }, teller), Is.False);
		Assert.That(listener.Sightings[subject], Is.EqualTo(seen));

		var newer = seen with { Time = calendar.AddMinutes(world.Time, 5) };
		Assert.That(listener.LearnSighting(subject, newer, teller), Is.True);
		Assert.That(listener.Sightings[subject], Is.EqualTo(newer with { Source = teller }));
		Assert.That(listener.Sightings[subject].IsSecondhand, Is.True);

		// No one learns about themselves, or about the person telling them.
		Assert.That(listener.LearnSighting(listener, newer, teller), Is.False);
		Assert.That(listener.LearnSighting(teller, newer, teller), Is.False);
	}

	/// <summary>
	/// Creates a world and gathers some of its people together in one quarter, doing nothing.
	/// </summary>
	private static (OperaEtDies.Town.GeneratedTown Town, GameWorld World, Location Quarter, IReadOnlyList<Person> People) CreatePeopleTogether(int count)
	{
		var (town, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var quarter = world.Locations.First(x => x.Template is QuarterTemplate);
		var people = world.Npcs.Take(count).ToList();
		foreach (var person in people)
		{
			person.SetPlan(null, []);
			person.Location = quarter;
		}

		return (town, world, quarter, people);
	}

	private static void Start(GameWorld world, Person person, Need? goal, PersonTask task, GoldenAnvil.Utility.Calendar.TimePoint? time = null)
	{
		var start = time ?? world.Time;
		person.SetPlan(goal, [task]);
		person.StartTask(world.GameData.Calendar.AddMinutes(start, 30));
		world.OnTaskStarted(person, task, start);
	}

	private static Need Need(GameWorld world, string id) => world.GameData.Schedules.Needs.Single(x => x.Id == id);
}
