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

		// Finished trips toward a quota of trips.
		var tripMinutes = laundryNeed.GetShiftMinutes(members.Count);
		Assert.That(laundry.Maximum, Is.EqualTo(laundryNeed.GetQuotaMinutes(members.Count) / tripMinutes));
		laundry.Amount = 1;
		Assert.That(members.Sum(x => x.GetProgress(laundryNeed)), Is.EqualTo(tripMinutes));
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
	public void CancellingAGoalDropsAnUnfinishedTrip()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var person = world.Npcs.First(x => x.Family.Members.All(member => !member.IsPlayer));
		var laundry = GetNeed(world, "laundry");
		var tripMinutes = laundry.GetShiftMinutes(person.Family.Members.Count);
		foreach (var member in person.Family.Members)
			member.SetProgress(laundry, 0);
		person.SetProgress(laundry, tripMinutes + 7);
		person.SetPlan(laundry, [new BusyTask(30)]);
		person.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 30));
		person.IsAtWork = true;

		world.CancelGoal(person);

		Assert.That(person.GetProgress(laundry), Is.EqualTo(tripMinutes));
		Assert.That(person.IsAtWork, Is.False);
		Assert.That(person.GoalChoice!.Time, Is.EqualTo(world.Time));
		Assert.That(person.CurrentTask, Is.Not.Null);
	}

	private static NeedStatusViewModel Need(StatusViewModel viewModel, string title) => viewModel.Needs.Single(x => x.Title == title);

	private static Need GetNeed(GameWorld world, string id) => world.GameData.Schedules.Needs.Single(x => x.Id == id);
}
