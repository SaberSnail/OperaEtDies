using OperaEtDies.Controls.Locations;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Test.GameState;

namespace OperaEtDies.Test.Controls.Locations;

[TestFixture]
public sealed class LocationPersonViewModelTests
{
	[Test]
	public void ActivityShowsWhatThePersonIsDoing()
	{
		var gameData = TestData.CreateGameData(withSchedules: true);
		var schedules = gameData.Schedules;
		var world = new TestWorld(gameData);
		var home = world.AddLocation(gameData.HouseTemplates[0]);
		var square = world.AddLocation(gameData.QuarterTemplates.Get("square"));
		var lane = world.AddLocation(gameData.QuarterTemplates[0]);
		var person = world.AddPerson(TestData.CreatePlayerTraits(gameData), home);
		var viewModel = new LocationPersonViewModel(person, null, schedules);
		var changed = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
		var (fatigue, hunger, laundry) = (Need("fatigue"), Need("hunger"), Need("laundry"));

		// Nothing to do yet.
		Assert.That(viewModel.Activity, Is.Null);

		// A need's activity, or its title without one, or resting while idle.
		Start(fatigue, new BusyTask(60));
		Assert.That(viewModel.Activity, Is.EqualTo("Sleeping"));
		Assert.That(changed, Does.Contain(nameof(LocationPersonViewModel.Activity)));
		Start(hunger, new BusyTask(10));
		Assert.That(viewModel.Activity, Is.EqualTo("Hunger"));
		Start(null, new BusyTask(10));
		Assert.That(viewModel.Activity, Is.EqualTo("Resting"));

		// Walking only shows the next step, not where the walk ends, and developer information isn't shown.
		Start(laundry, new TravelTask(lane), new TravelTask(square), new BusyTask(20), new TravelTask(lane), new TravelTask(home), new BusyTask(10));
		Assert.That(viewModel.Activity, Is.EqualTo($"Walking to {lane.Title}"));
		Assert.That((viewModel.DeveloperGoal, viewModel.DeveloperGoalReason), Is.EqualTo(((string?) null, (string?) null)));

		// Time at the place has the need's activity, and time at home afterward has its home activity.
		Start(laundry, new BusyTask(20));
		person.Location = square;
		Assert.That(viewModel.Activity, Is.EqualTo("Washing"));
		person.Location = home;
		Assert.That(viewModel.Activity, Is.EqualTo("Hanging out the washing"));

		Need Need(string id) => schedules.Needs.Single(x => x.Id == id);

		void Start(Need? goal, params PersonTask[] tasks)
		{
			person.SetPlan(goal, tasks);
			person.StartTask(gameData.Calendar.AddMinutes(world.Time, 1));
		}
	}

	[Test]
	public void DevelopersSeeWhereSomeoneIsGoingAndWhy()
	{
		var gameData = TestData.CreateGameData(withSchedules: true);
		var schedules = gameData.Schedules;
		var calendar = gameData.Calendar;
		var world = new TestWorld(gameData);
		var home = world.AddLocation(gameData.HouseTemplates[0]);
		var square = world.AddLocation(gameData.QuarterTemplates.Get("square"));
		var lane = world.AddLocation(gameData.QuarterTemplates[0]);
		var person = world.AddPerson(TestData.CreatePlayerTraits(gameData), home);
		var viewModel = new LocationPersonViewModel(person, null, schedules, developerCalendar: calendar);
		var changed = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
		var (fatigue, hunger, laundry) = (Need("fatigue"), Need("hunger"), Need("laundry"));
		var clock = calendar.FormatClock(world.Time);

		// Nothing planned yet.
		Assert.That(viewModel.DeveloperGoal, Is.Null);
		Assert.That(viewModel.DeveloperGoalReason, Is.EqualTo(OurResources.DeveloperNoGoalChoice));

		// While walking, the goal, where it's met, and where the walk ends, with the chance each need had.
		var choice = new GoalChoice(world.Time, [new NeedUrgency(fatigue, 30), new NeedUrgency(hunger, 60), new NeedUrgency(laundry, 80)], laundry, GoalChoiceResult.Chosen);
		Start(laundry, choice, new TravelTask(lane), new TravelTask(square), new BusyTask(20), new TravelTask(lane), new TravelTask(home), new BusyTask(10));
		Assert.That(viewModel.Activity, Is.EqualTo($"Walking to {lane.Title}"));
		Assert.That(viewModel.DeveloperGoal, Is.EqualTo($"Laundry at {square.Title}, then home (walking to {square.Title})"));
		var hungerChance = 60.0 * 60 / ((60 * 60) + (80 * 80));
		Assert.That(viewModel.DeveloperGoalReason, Is.EqualTo(
			$"At {clock}, chose Laundry. Urgencies: Fatigue 30 (below {schedules.MinimumUrgency:0}), Hunger 60 ({hungerChance:0%}), Laundry 80 ({1 - hungerChance:0%})."));
		Assert.That(changed, Does.Contain(nameof(LocationPersonViewModel.DeveloperGoal)).And.Contain(nameof(LocationPersonViewModel.DeveloperGoalReason)));

		// Idle, and why.
		Start(null, new GoalChoice(world.Time, [], null, GoalChoiceResult.NothingUrgent), new BusyTask(10));
		Assert.That(viewModel.DeveloperGoal, Is.EqualTo($"Idle at {home.Title}"));
		Assert.That(viewModel.DeveloperGoalReason, Is.EqualTo($"At {clock}, nothing was urgent enough. Urgencies: none."));
		Start(null, new GoalChoice(world.Time, [new NeedUrgency(hunger, 60)], hunger, GoalChoiceResult.NoPlace), new BusyTask(10));
		Assert.That(viewModel.DeveloperGoalReason, Does.StartWith($"At {clock}, chose Hunger, but there was nowhere to do it."));

		Need Need(string id) => schedules.Needs.Single(x => x.Id == id);

		void Start(Need? goal, GoalChoice goalChoice, params PersonTask[] tasks)
		{
			person.SetPlan(goal, tasks, goalChoice);
			person.StartTask(calendar.AddMinutes(world.Time, 1));
		}
	}

	[Test]
	public void FollowingCanBeToggled()
	{
		var gameData = TestData.CreateGameData();
		var world = new TestWorld(gameData);
		var person = world.AddPerson(TestData.CreatePlayerTraits(gameData), world.AddLocation(gameData.HouseTemplates[0]));
		var followed = new List<Person>();

		var viewModel = new LocationPersonViewModel(person, null, toggleFollow: followed.Add);
		viewModel.ToggleFollow();

		Assert.That((viewModel.CanFollow, viewModel.FollowText), Is.EqualTo((true, OurResources.FollowPerson)));
		Assert.That(followed, Is.EqualTo(new[] { person }));
		viewModel.IsFollowed = true;
		Assert.That(viewModel.FollowText, Is.EqualTo(OurResources.StopFollowing));
	}
}
