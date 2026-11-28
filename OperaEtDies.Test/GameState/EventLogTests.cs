using OperaEtDies.Controls.Log;
using OperaEtDies.Data;
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
	public void FollowingAgainAfterChangingClothesIsLoggedOnce()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var player = world.Player;
		var npc = world.Npcs[0];
		world.MovePerson(npc, player.Location, world.Time);
		world.OnTaskStarted(npc, new FollowTask(player, 60), world.Time);
		var count = world.EventLog.Entries.Count;

		world.OnTaskStarted(npc, new ChangeClothesTask(npc.Outfits[0], IsTakingOff: false), world.Time);
		world.OnTaskStarted(npc, new FollowTask(player, 60), world.Time);

		Assert.That(world.EventLog.Entries.Skip(count).Select(x => x.Kind), Is.EqualTo(new[] { LogEntryKind.ChangingClothes }));
	}

	[Test]
	public async Task WalkingAwayThePlayerNoLongerSeesWhatHappensThereAsync()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var player = world.Player;
		// Someone in a neighboring place arrives no later than the player gets where they're going.
		var (from, to, neighbor) = world.Locations.Where(x => x.Exits.Count >= 2 && x != player.Location)
			.SelectMany(x => x.Exits.Select(y => y.Location).SelectMany(y => x.Exits.Select(z => z.Location).Where(z => z != y).Select(z => (From: x, To: y, Neighbor: z))))
			.First(x => world.GetTravelMinutes(x.Neighbor, x.From) <= world.GetTravelMinutes(x.From, x.To));
		player.Location = from;
		var npc = world.Npcs.First(x => x.Location != from);
		npc.Location = neighbor;
		npc.SetPlan(null, [new TravelTask(from), new BusyTask(600)]);
		var count = world.EventLog.Entries.Count;

		// Someone arriving where the player is walking away from isn't seen, and the player sees themselves arrive.
		Assert.That(await world.WalkPlayerToAsync(to), Is.True);
		Assert.That((npc.Location, player.Location), Is.EqualTo((from, to)));
		Assert.That(world.EventLog.Entries.Skip(count).Where(x => x.Person == npc), Is.Empty);
		Assert.That(world.EventLog.Entries.Skip(count).Count(x => x.Person == player && x.Kind == LogEntryKind.Arrived), Is.EqualTo(1));
	}

	[Test]
	public void EntriesNameThePeopleTheyInvolve()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var player = world.Player;
		var npc = world.Npcs[0];
		var need = new Need
		{
			Id = "lesson", Title = "Lesson", Icon = "icon", Category = NeedCategory.Personal, Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 },
			StepSettings = [new Step { Kind = StepKind.Do, Minutes = TestData.Amount(60), Activity = "Teaching {partner} to swim" }],
		};
		need.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		string Text(LogEntry entry) => LogEntryText.ToPlainText(LogEntryText.Format(entry));

		// The partner an activity names is filled in, as "you" for the player.
		Assert.That(Text(new LogEntry(world.Time, LogEntryKind.Busy, player, player.Location, Other: npc, Need: need, Step: 0)), Is.EqualTo($"You started teaching {npc.FullName} to swim."));
		Assert.That(Text(new LogEntry(world.Time, LogEntryKind.Busy, npc, player.Location, Other: player, Need: need, Step: 0)), Is.EqualTo($"{npc.FullName} started teaching you to swim."));
		Assert.That(LogEntryText.Format(new LogEntry(world.Time, LogEntryKind.Busy, player, player.Location, Other: npc, Need: need, Step: 0)).Count(x => x.Person == npc), Is.EqualTo(1));

		// Who a failure was down to is named.
		Assert.That(LogEntryText.GetCauseText(FailCause.Shy, isPlayer: true, npc), Is.EqualTo($"{npc.FullName} wasn't comfortable undressing"));
		Assert.That(LogEntryText.GetCauseText(FailCause.Shy, isPlayer: true, player), Is.EqualTo("you weren't comfortable undressing"));
		Assert.That(LogEntryText.GetCauseText(FailCause.No, isPlayer: false, player), Is.EqualTo("you said no"));
		Assert.That(LogEntryText.GetCauseText(FailCause.NoShow, isPlayer: true, npc), Is.EqualTo($"{npc.FullName} never came"));
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
