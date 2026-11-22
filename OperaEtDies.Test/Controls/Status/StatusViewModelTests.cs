using GoldenAnvil.Utility;
using OperaEtDies.Controls.Status;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Test.GameState;

namespace OperaEtDies.Test.Controls.Status;

[TestFixture]
public sealed class StatusViewModelTests
{
	[Test]
	public void DevelopersCanChangeNeeds()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var person = world.Families.First(x => x.Members.Count > 1 && !x.Members.Any(member => member.IsPlayer)).Members[0];
		var viewModel = new StatusViewModel(world, person, isDeveloper: true);
		var (fatigue, upkeep, laundry) = (Need(viewModel, "Fatigue"), Need(viewModel, "Upkeep"), Need(viewModel, "Laundry"));
		var (upkeepNeed, laundryNeed) = (GetNeed(world, "upkeep"), GetNeed(world, "laundry"));
		var members = person.Family.Members;

		Assert.That(viewModel.Title, Is.EqualTo(OurResources.StatusHeaderPerson.FormatCurrentCulture(person.FullName)));
		Assert.That(viewModel.Needs.Select(x => x.IsDeveloper), Is.All.True);

		// A level need's level.
		fatigue.Amount = 70;
		Assert.That(person.GetLevel(GetNeed(world, "fatigue")), Is.EqualTo(70));
		Assert.That((fatigue.Amount, fatigue.Maximum), Is.EqualTo((70, OperaEtDies.Data.Need.MaximumLevel)));

		// The household's minutes toward a shared quota, taken from the person's own share.
		var quota = upkeepNeed.GetQuotaMinutes(members.Count);
		Assert.That(upkeep.Maximum, Is.EqualTo(quota));
		upkeep.Amount = quota;
		Assert.That(members.Sum(x => x.GetProgress(upkeepNeed)), Is.EqualTo(quota));
		Assert.That(upkeep.IsDone, Is.True);
		upkeep.Amount = 0;
		Assert.That(person.GetProgress(upkeepNeed), Is.Zero);

		// Finished runs toward a quota of runs.
		Assert.That(laundry.Maximum, Is.EqualTo(laundryNeed.GetQuotaRuns(members.Count)));
		laundry.Amount = 1;
		Assert.That(members.Sum(x => x.GetProgress(laundryNeed)), Is.EqualTo(1));
		Assert.That(laundry.Amount, Is.EqualTo(1));
	}

	[Test]
	public void OnlyDevelopersCanChangeNeeds()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var viewModel = new StatusViewModel(world);
		var fatigue = Need(viewModel, "Fatigue");
		var level = world.Player.GetLevel(GetNeed(world, "fatigue"));

		fatigue.Amount = level > 50 ? 0 : 100;

		Assert.That(viewModel.Title, Is.EqualTo(OurResources.StatusHeader));
		Assert.That(fatigue.IsDeveloper, Is.False);
		Assert.That(world.Player.GetLevel(GetNeed(world, "fatigue")), Is.EqualTo(level));
	}

	[Test]
	public void ThePlayersUnfinishedJobsAreShown()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var work = GetNeed(world, "work");
		var client = world.Npcs.First(x => x.Family != world.Player.Family);
		Assert.That(new StatusViewModel(world).HasJobs, Is.False);

		var job = world.AddJob(world.Player, client, work, client.Home, 600);
		job.MinutesDone = 240;
		var viewModel = new StatusViewModel(world);

		Assert.That(viewModel.HasJobs, Is.True);
		Assert.That(viewModel.Jobs, Has.Count.EqualTo(1));
		Assert.That(viewModel.Jobs[0], Does.StartWith($"{work.Title} for {client.FullName}, at "));
		Assert.That(viewModel.Jobs[0], Does.Contain("6 hours").And.Contain("10 hours"));

		// Someone else's jobs aren't shown, and neither is a job that's done.
		Assert.That(new StatusViewModel(world, client, isDeveloper: true).HasJobs, Is.False);
		job.MinutesDone = 600;
		Assert.That(new StatusViewModel(world).HasJobs, Is.False);
	}

	[Test]
	public void CancellingAGoalDropsAnUnfinishedRun()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var person = world.Npcs.First(x => x.Family.Members.All(member => !member.IsPlayer));
		var laundry = GetNeed(world, "laundry");
		foreach (var member in person.Family.Members)
			member.SetProgress(laundry, 0);
		person.SetPlan(laundry, [new BusyTask(30, 3)], startStep: 4);
		person.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 30));
		person.IsAtWork = true;

		world.CancelGoal(person);

		// A run only counts once it's finished, and time spent on it doesn't count by itself.
		Assert.That(person.GetProgress(laundry), Is.Zero);
		Assert.That(person.IsAtWork, Is.False);
		Assert.That(person.GoalChoice!.Time, Is.EqualTo(world.Time));
		Assert.That(person.CurrentTask, Is.Not.Null);
	}

	private static NeedStatusViewModel Need(StatusViewModel viewModel, string title) => viewModel.Needs.Single(x => x.Title == title);

	private static Need GetNeed(GameWorld world, string id) => world.GameData.Schedules.Needs.Single(x => x.Id == id);
}
