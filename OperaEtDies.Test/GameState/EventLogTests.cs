using OperaEtDies.Controls.Log;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class EventLogTests
{
	[Test]
	public void PlayerSeesPeopleComeAndGoWhereTheyAre()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var player = world.Player;
		var npc = world.Npcs.First(x => x.Location != player.Location);
		var from = npc.Location;
		var elsewhere = world.Locations.First(x => x != from && x != player.Location);
		var count = world.EventLog.Entries.Count;

		world.MovePerson(npc, player.Location, world.Time);
		Assert.That(world.EventLog.Entries[^1], Is.EqualTo(new LogEntry(world.Time, LogEntryKind.Arrived, npc, player.Location, Place: from)));

		world.MovePerson(npc, elsewhere, world.Time);
		Assert.That(world.EventLog.Entries[^1], Is.EqualTo(new LogEntry(world.Time, LogEntryKind.Left, npc, player.Location, Place: elsewhere)));

		// Coming and going anywhere else isn't seen.
		world.MovePerson(npc, from, world.Time);
		Assert.That(world.EventLog.Entries, Has.Count.EqualTo(count + 2));
	}

	[Test]
	public void PlayerSeesThemselvesArrive()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var player = world.Player;
		var from = player.Location;
		var to = from.Exits[0].Location;

		world.MovePerson(player, to, world.Time);

		Assert.That(world.EventLog.Entries[^1], Is.EqualTo(new LogEntry(world.Time, LogEntryKind.Arrived, player, to, Place: from)));
	}

	[Test]
	public void CarryingOnWithTheSameThingIsLoggedOnce()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var npc = world.Npcs[0];
		world.MovePerson(npc, world.Player.Location, world.Time);
		world.OnTaskStarted(npc, new WaitTask(world.Player, 5), world.Time);
		var count = world.EventLog.Entries.Count;

		world.OnTaskStarted(npc, new BusyTask(30), world.Time);
		world.OnTaskStarted(npc, new BusyTask(30), world.Time);

		Assert.That(world.EventLog.Entries, Has.Count.EqualTo(count + 1));
		Assert.That(world.EventLog.Entries[^1].Kind, Is.AnyOf(LogEntryKind.Busy, LogEntryKind.Idle));
	}

	[Test]
	public void TalkingIsLoggedOnceForBothPeople()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var player = world.Player;
		var npc = world.Npcs[0];
		world.MovePerson(npc, player.Location, world.Time);
		var count = world.EventLog.Entries.Count;

		world.StartTalking(npc, player, 15, world.Time);

		Assert.That(world.EventLog.Entries, Has.Count.EqualTo(count + 1));
		Assert.That(world.EventLog.Entries[^1], Is.EqualTo(new LogEntry(world.Time, LogEntryKind.Talking, npc, player.Location, Other: player)));
	}

	[Test]
	public void OnlyTheMostRecentEntriesAreKept()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var player = world.Player;
		var npc = world.Npcs.First(x => x.Location != player.Location);
		var away = npc.Location;
		for (var index = 0; index < EventLog.MaxEntries; index++)
		{
			world.MovePerson(npc, player.Location, world.Time);
			world.MovePerson(npc, away, world.Time);
		}

		Assert.That(world.EventLog.Entries, Has.Count.EqualTo(EventLog.MaxEntries));
		Assert.That(world.EventLog.Entries[^1].Kind, Is.EqualTo(LogEntryKind.Left));
	}

	[Test]
	public void EntriesAreWrittenWithTheirPeopleAndPlaces()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var player = world.Player;
		var npc = world.Npcs.First(x => x.Location != player.Location);
		var from = npc.Location;

		var arrived = LogEntryText.Format(new LogEntry(world.Time, LogEntryKind.Arrived, npc, player.Location, Place: from));
		Assert.That(arrived.Select(x => (x.Text, x.Person, x.Place)), Is.EqualTo(new[] { (npc.FullName, npc, (Location?) null), (" arrived from ", null, null), (from.Title, null, from), (".", null, null) }));

		var talking = LogEntryText.Format(new LogEntry(world.Time, LogEntryKind.Talking, player, player.Location, Other: npc));
		Assert.That(LogEntryText.ToPlainText(talking), Is.EqualTo($"You started talking with {npc.FullName}."));
	}
}
