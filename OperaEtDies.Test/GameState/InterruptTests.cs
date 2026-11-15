using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class InterruptTests
{
	[Test]
	public void TalkingPausesWhatSomeoneWasDoing()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true, isHeadless: false);
		var calendar = world.GameData.Calendar;
		var person = world.Npcs.First(x => x.Family.Members.All(member => !member.IsPlayer));
		person.Location = world.Player.Location;
		var upkeep = Need(world, "upkeep");
		person.SetPlan(upkeep, [new BusyTask(60)]);
		person.StartTask(calendar.AddMinutes(world.Time, 60));
		var (end, progress) = (person.TaskEnd!, person.GetProgress(upkeep));
		var start = world.Time;

		Assert.That(world.IsTooBusyToTalk(person), Is.False);
		var passed = world.TalkTo(person, 5);

		// The player spent the time talking, and the person carries on afterward, with no progress while they talked.
		Assert.That(passed, Is.EqualTo(calendar.CreateTimeOffset(0, 0, 5)));
		Assert.That(world.Player.Tasks, Is.Empty);
		Assert.That(person.Goal, Is.SameAs(upkeep));
		Assert.That(person.CurrentTask, Is.EqualTo(new BusyTask(60) { ResumeMinutes = 60 }));
		Assert.That(person.TaskEnd, Is.EqualTo(calendar.AddMinutes(end, 5)));
		Assert.That(person.GetProgress(upkeep), Is.EqualTo(progress));
		Assert.That(person.GetRelationship(world.Player).LastTalked, Is.EqualTo(start));
		Assert.That(person.Sightings[world.Player], Is.EqualTo(new Sighting(world.Player.Location, world.Time, SightingActivity.None)));
	}

	[Test]
	public void SomeoneWalkingCarriesOnFromWhereTheyAre()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var calendar = world.GameData.Calendar;
		var person = world.Npcs[0];
		var (here, next) = (world.Player.Location, world.Player.Location.Connections[0]);
		person.SetPlan(null, [new TravelTask(next)]);
		person.StartTask(calendar.AddMinutes(world.Time, 10));

		person.Location = world.Player.Location;
		world.StartTalking(world.Player, person, 5, calendar.AddMinutes(world.Time, 4));

		Assert.That(person.CurrentTask, Is.EqualTo(new TalkTask(world.Player, 5)));
		Assert.That(person.Tasks[1], Is.EqualTo(new TravelTask(next) { ResumeMinutes = 6 }));
		Assert.That(person.Location, Is.SameAs(here));
		Assert.That(world.Player.Sightings[person].Activity, Is.EqualTo(SightingActivity.Talking));
	}

	[Test]
	public void SomeoneChoosesAgainIfTheyCantFinishAfterTalking()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var calendar = world.GameData.Calendar;
		var person = world.Npcs.First(x => x.Family.Members.All(member => !member.IsPlayer));
		var upkeep = Need(world, "upkeep");
		person.SetPlan(upkeep, [new BusyTask(60)]);
		person.StartTask(calendar.AddMinutes(world.Time, 60));

		// While they talk, the rest of their household finishes the chore.
		world.TalkTo(person, 1);
		world.StartTalking(world.Player, person, 5, world.Time);
		person.SetProgress(upkeep, upkeep.GetQuotaMinutes(person.Family.Members.Count));
		world.AdvanceTime(calendar.CreateTimeOffset(0, 0, 5));

		Assert.That(person.GoalChoice!.Time, Is.EqualTo(world.Time));
		Assert.That(person.Goal, Is.Not.SameAs(upkeep));
	}

	[Test]
	public void WakingSomeoneTiresThemAndTheyRefuseWhenDesperate()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var calendar = world.GameData.Calendar;
		var schedules = world.GameData.Schedules;
		var fatigue = Need(world, schedules.SleepNeed!);
		world.AdvanceTime(calendar.GetStartOfDay(world.Time) + calendar.CreateTimeOffset(0, 23, 0) - world.Time);
		var person = world.Npcs[0];
		person.SetLevel(fatigue, 50);
		person.SetPlan(fatigue, [new BusyTask(null)]);
		person.StartTask(calendar.AddMinutes(world.Time, 240));

		Assert.That(person.IsAsleep, Is.True);
		Assert.That(world.IsTooBusyToTalk(person), Is.False);
		world.StartTalking(world.Player, person, 5, world.Time);
		Assert.That(person.GetLevel(fatigue), Is.EqualTo(50 + schedules.Talking.WakeLevel));
		Assert.That(person.IsAsleep, Is.False);

		// Someone desperate for sleep won't be woken.
		var other = world.Npcs[1];
		other.SetLevel(fatigue, OperaEtDies.Data.Need.MaximumLevel);
		other.SetPlan(fatigue, [new BusyTask(null)]);
		other.StartTask(calendar.AddMinutes(world.Time, 240));
		var time = world.Time;
		Assert.That(world.IsTooBusyToTalk(other), Is.True);
		Assert.That(world.TalkTo(other, 5), Is.Null);
		Assert.That(world.Time, Is.EqualTo(time));
	}

	private static Need Need(GameWorld world, string id) => world.GameData.Schedules.Needs.Single(x => x.Id == id);
}
