using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class TalkingTests
{
	[Test]
	public void AskingWhatSomeoneIsDoingTellsTheirGoalAndWhereTheyreGoing()
	{
		var (world, person) = CreateWorldWithSomeoneHere();
		var restlessness = Need(world, "restlessness");
		var (next, end) = (person.Location.Connections[0], person.Location.Connections[0].Connections[0]);
		person.SetPlan(restlessness, [new TravelTask(next), new TravelTask(end), new BusyTask(30)]);
		person.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 10));
		var calendar = world.GameData.Calendar;
		var start = world.Time;

		var answer = world.AskWhatDoing(person);

		Assert.That(answer.Time, Is.EqualTo(calendar.CreateTimeOffset(0, 0, world.GameData.Schedules.Talking.AskDoingMinutes)));
		Assert.That(answer.Sighting, Is.EqualTo(new Sighting(person.Location, start, SightingActivity.Walking, Destination: next, Plan: new SightingPlan(restlessness, end))));

		// The player still remembers what they were told once the talk is over, since the person's goal hasn't changed.
		Assert.That(world.Player.Sightings[person].Plan, Is.EqualTo(new SightingPlan(restlessness, end)));
		Assert.That(world.Player.Sightings[person].Time, Is.EqualTo(world.Time));
	}

	[Test]
	public void SomeoneWithNothingLeftToDoDoesntMentionTheirGoal()
	{
		var (world, person) = CreateWorldWithSomeoneHere();
		var water = Need(world, "water");
		person.SetPlan(water, [new TravelTask(person.Home)]);
		person.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 10));

		Assert.That(world.AskWhatDoing(person).Sighting!.Plan, Is.EqualTo(new SightingPlan(null, person.Home)));
	}

	[Test]
	public void BeingAskedToChatStopsWhatThePlayerIsDoing()
	{
		var (world, person) = CreateWorldWithSomeoneHere();
		var fatigue = Need(world, world.GameData.Schedules.SleepNeed!);
		var start = world.Time;
		world.Player.TalkRequest = person;

		var passed = world.DoPlayerAction(fatigue, 120);

		Assert.That(passed, Is.LessThan(world.GameData.Calendar.CreateTimeOffset(0, 2, 0)));
		Assert.That(world.Player.Tasks, Is.Empty);
		Assert.That(world.Time, Is.GreaterThanOrEqualTo(start));
	}

	[Test]
	public void LonelyFriendsAskThePlayerToChat()
	{
		var (world, person) = CreateWorldWithSomeoneHere();
		var socializing = Need(world, world.GameData.Schedules.SocializeNeed!);
		person.GetRelationship(world.Player).Score = RelationshipSettings.MaximumScore;
		person.SetLevel(socializing, 90);

		StartIdling(world, person);
		Assert.That(world.Player.TalkRequest, Is.SameAs(person));
		Assert.That(person.CurrentTask, Is.EqualTo(new BusyTask(30)));

		// Once turned down, they don't ask again for a while.
		world.DeclineTalk();
		StartIdling(world, person);
		Assert.That(world.Player.TalkRequest, Is.Null);
	}

	[Test]
	public void InAHeadlessWorldLonelyFriendsJustChatWithThePlayer()
	{
		var (world, person) = CreateWorldWithSomeoneHere();
		var socializing = Need(world, world.GameData.Schedules.SocializeNeed!);
		person.GetRelationship(world.Player).Score = RelationshipSettings.MaximumScore;
		person.SetLevel(socializing, 90);
		world.IsHeadless = true;
		world.Player.SetPlan(null, [new BusyTask(60)]);
		world.Player.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 60));

		StartIdling(world, person);

		Assert.That(world.Player.TalkRequest, Is.Null);
		Assert.That(person.CurrentTask, Is.InstanceOf<TalkTask>());
		Assert.That(world.Player.CurrentTask, Is.InstanceOf<TalkTask>());
	}

	private static void StartIdling(GameWorld world, Person person)
	{
		person.SetPlan(null, [new BusyTask(30)]);
		person.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 30));
		world.OnTaskStarted(person, person.CurrentTask!, world.Time);
	}

	[Test]
	public void AskingAboutSomeoneTellsWhatTheyKnowAsSecondhand()
	{
		var (world, person) = CreateWorldWithSomeoneHere();
		// They're asked about people who aren't here, so the player doesn't see them for themselves.
		var others = world.Npcs.Where(x => x != person && x.Location != world.Player.Location).ToList();
		var (seen, unseen) = (others[0], others[^1]);
		// The player hasn't seen them for themselves lately.
		world.Player.ForgetSightingsBefore(world.GameData.Calendar.AddMinutes(world.Time, 1));

		// The person has only seen one of them.
		var sighting = new Sighting(world.Locations[0], world.Time, SightingActivity.Idle);
		person.ForgetSightingsBefore(world.GameData.Calendar.AddMinutes(world.Time, 1));
		person.See(seen, sighting);

		var answer = world.AskAbout(person, seen);
		Assert.That(answer.Sighting, Is.EqualTo(sighting));
		Assert.That(world.Player.Sightings[seen], Is.EqualTo(sighting with { Source = person }));

		Assert.That(world.AskAbout(person, unseen).Sighting, Is.Null);
		Assert.That(world.Player.Sightings.ContainsKey(unseen), Is.False);
	}

	[Test]
	public void SocializingMeetsTheNeedOfFriends()
	{
		var (world, person) = CreateWorldWithSomeoneHere();
		var socializing = Need(world, world.GameData.Schedules.SocializeNeed!);
		var relationship = person.GetRelationship(world.Player);
		person.SetLevel(socializing, 80);
		world.Player.SetLevel(socializing, 80);

		relationship.Score = 0;
		world.Socialize(person);
		Assert.That(person.GetLevel(socializing), Is.GreaterThan(80));

		relationship.Score = RelationshipSettings.MaximumScore;
		person.SetLevel(socializing, 80);
		world.Player.SetLevel(socializing, 80);
		world.Socialize(person);
		var met = socializing.SatisfyPerHour!.Value * world.GameData.Schedules.Socializing.ChatMinutes / 60;
		Assert.That(person.GetLevel(socializing), Is.EqualTo(80 - met));
		Assert.That(world.Player.GetLevel(socializing), Is.EqualTo(80 - met));
	}

	[Test]
	public void LonelyPeopleSeekOutAFriendSomewhereElse()
	{
		var (world, person) = CreateWorldWithSomeoneHere();
		var socializing = Need(world, world.GameData.Schedules.SocializeNeed!);
		foreach (var need in world.GameData.Schedules.Needs.Where(x => x.Kind == NeedKind.Level))
			person.SetLevel(need, 0);
		person.SetLevel(socializing, OperaEtDies.Data.Need.MaximumLevel);
		var friend = world.Npcs.First(x => x != person && x.Location != person.Location);
		foreach (var relationship in person.Relationships.Values)
			relationship.Score = relationship.GetOther(person) == friend ? RelationshipSettings.MaximumScore : 0;

		world.CancelGoal(person);

		// They look for their only friend somewhere other than where they are, without being satisfied by looking.
		var seeking = person.GoalChoice!.Seeking!;
		Assert.That(person.Goal, Is.SameAs(socializing));
		Assert.That(seeking.Friend, Is.SameAs(friend));
		Assert.That(seeking.Place, Is.Not.SameAs(person.Location));
		Assert.That(person.Tasks[^1], Is.EqualTo(new BusyTask(world.GameData.Schedules.Socializing.SeekMinutes)));
	}

	[Test]
	public void SomeoneSeekingAFriendCallsOutToThemAsTheyLeave()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var calendar = world.GameData.Calendar;
		var socializing = Need(world, world.GameData.Schedules.SocializeNeed!);
		var (seeker, friend) = (world.Npcs[0], world.Npcs.Last(x => x.Family != world.Npcs[0].Family));
		var here = world.Locations.First(x => x.Template is QuarterTemplate);
		var there = here.Connections.First(x => x.Template is QuarterTemplate && x.Connections.Any(other => other != here));
		var beyond = there.Connections.First(x => x != here);
		seeker.Location = here;
		friend.Location = there;
		friend.SetPlan(null, [new TravelTask(beyond)]);
		friend.StartTask(calendar.AddMinutes(world.Time, 3));
		var choice = new GoalChoice(world.Time, [], socializing, GoalChoiceResult.Chosen, new SeekChoice(friend, there, SeekClue.Sighting));
		seeker.SetPlan(socializing, [new TravelTask(there), new BusyTask(15)], choice);

		new Scheduler(world).Continue(seeker, world.Time);

		// The friend waits where they are until the seeker gets there, then they chat.
		var wait = (WaitTask) friend.CurrentTask!;
		Assert.That(wait.For, Is.SameAs(seeker));
		Assert.That(friend.Location, Is.SameAs(there));
		Assert.That(friend.Tasks[1], Is.InstanceOf<TravelTask>());
		world.AdvanceTime(calendar.CreateTimeOffset(0, 0, wait.Minutes - 1));
		Assert.That(seeker.Location, Is.SameAs(there));
		Assert.That(seeker.CurrentTask, Is.InstanceOf<TalkTask>().With.Property(nameof(TalkTask.With)).SameAs(friend));
		Assert.That(friend.CurrentTask, Is.InstanceOf<TalkTask>());
	}

	[Test]
	public void SomeoneWithoutFriendsDoesntSeekAnyone()
	{
		var (world, person) = CreateWorldWithSomeoneHere();
		var socializing = Need(world, world.GameData.Schedules.SocializeNeed!);
		person.SetLevel(socializing, OperaEtDies.Data.Need.MaximumLevel);
		foreach (var relationship in person.Relationships.Values)
			relationship.Score = 0;

		world.CancelGoal(person);

		Assert.That(person.Goal, Is.Not.SameAs(socializing));
		Assert.That(person.GoalChoice!.Seeking, Is.Null);
	}

	[Test]
	public void LonelyFriendsWhoMeetStopToChat()
	{
		var (world, person) = CreateWorldWithSomeoneHere();
		var socializing = Need(world, world.GameData.Schedules.SocializeNeed!);
		var friend = world.Npcs.First(x => x != person && x.Family != world.Player.Family);
		person.GetRelationship(friend).Score = RelationshipSettings.MaximumScore;
		person.SetLevel(socializing, 90);
		friend.Location = person.Location;
		friend.SetPlan(null, [new BusyTask(60)]);
		friend.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 60));
		var talking = world.GameData.Schedules.Talking;

		// When the lonely person starts something new, they stop to chat, asking what their friend is doing first.
		person.SetPlan(null, [new BusyTask(30)]);
		person.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 30));
		world.OnTaskStarted(person, person.CurrentTask!, world.Time);

		Assert.That(person.CurrentTask, Is.EqualTo(new TalkTask(friend, world.GameData.Schedules.Socializing.ChatMinutes + talking.AskDoingMinutes, socializing)));
		Assert.That(friend.CurrentTask, Is.InstanceOf<TalkTask>());
		Assert.That(person.Sightings[friend].Plan, Is.Not.Null);
	}

	[Test]
	public void FriendsWhoArentLonelyDontStopToChat()
	{
		var (world, person) = CreateWorldWithSomeoneHere();
		var socializing = Need(world, world.GameData.Schedules.SocializeNeed!);
		var friend = world.Npcs.First(x => x != person && x.Family != world.Player.Family);
		person.GetRelationship(friend).Score = RelationshipSettings.MaximumScore;
		person.SetLevel(socializing, 0);
		friend.SetLevel(socializing, 0);
		friend.Location = person.Location;
		friend.SetPlan(null, [new BusyTask(60)]);
		friend.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 60));

		person.SetPlan(null, [new BusyTask(30)]);
		person.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 30));
		world.OnTaskStarted(person, person.CurrentTask!, world.Time);

		Assert.That(person.CurrentTask, Is.EqualTo(new BusyTask(30)));
	}

	[Test]
	public void ThePlayerCanOnlyTalkWithSomeoneHere()
	{
		var (world, person) = CreateWorldWithSomeoneHere();
		var away = world.Npcs.First(x => x.Location != world.Player.Location);

		Assert.That(world.CanTalkTo(person), Is.True);
		Assert.That((world.CanTalkTo(away), world.CanTalkTo(world.Player)), Is.EqualTo((false, false)));
		Assert.That(() => world.AskWhatDoing(away), Throws.InvalidOperationException);
		Assert.That(() => world.Socialize(away), Throws.InvalidOperationException);
	}

	/// <summary>
	/// Creates a world in the evening (when the socializing need is within its hours), with someone idle in the player's
	/// location.
	/// </summary>
	internal static (GameWorld World, Person Person) CreateWorldWithSomeoneHere()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var calendar = world.GameData.Calendar;
		world.AdvanceTime(calendar.GetStartOfDay(world.Time) + calendar.CreateTimeOffset(0, 19, 0) - world.Time);

		// The player is played from here on.
		world.IsHeadless = false;
		var person = world.Npcs.First(x => x.Family != world.Player.Family);
		person.Location = world.Player.Location;
		person.SetPlan(null, [new BusyTask(60)]);
		person.StartTask(calendar.AddMinutes(world.Time, 60));
		return (world, person);
	}

	private static Need Need(GameWorld world, string id) => world.GameData.Schedules.Needs.Single(x => x.Id == id);
}
