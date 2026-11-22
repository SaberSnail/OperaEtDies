using OperaEtDies.Controls.Locations;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Test.GameState;

namespace OperaEtDies.Test.Controls.Locations;

[TestFixture]
public sealed class PlayerStepsViewModelTests
{
	[Test]
	public void TheStepThePlayerIsOnStandsOut()
	{
		var (_, world) = GameWorldTests.CreateWorld(0, withSchedules: true, isHeadless: false);
		var player = world.Player;
		var water = world.GameData.Schedules.Needs.Single(x => x.Id == "water");
		var square = world.Locations.First(x => x.Template is QuarterTemplate && x.QuarterKind == "square");
		var sequence = Scheduler.StartSequence(player, water, 3, null, [], null);
		var (stepCount, allCount, stopCount) = (0, 0, 0);
		var steps = new PlayerStepsViewModel(sequence,
			[new PlayerStep(sequence, PlayerStepKind.Step, 0, square), new PlayerStep(sequence, PlayerStepKind.Step, 3, player.Home, IsCurrent: true), new PlayerStep(sequence, PlayerStepKind.Step, 4)],
			player, isExpanded: false, action: null, choices: [], others: [], () => stepCount++, () => allCount++, () => stopCount++, canDoStep: true);

		// Going somewhere is described by the step's text, if it has one, or where it goes, and a step with a button by its title.
		Assert.That(steps.Steps.Select(x => x.Text), Is.EqualTo(new[] { $"Go to {square.Title}", "Take it home to finish.", "Put away the water" }));
		Assert.That(steps.Steps.Select(x => x.IsNext), Is.EqualTo(new[] { false, true, false }));
		Assert.That(steps.Next, Is.EqualTo("Next: Take it home to finish."));
		Assert.That(steps.Title, Is.EqualTo("In progress: Water"));

		// Expanding is reported, so it's kept when the steps are shown again.
		var expandedChanges = 0;
		steps.ExpandedChanged += (_, _) => expandedChanges++;
		steps.ToggleExpanded();
		Assert.That((steps.IsExpanded, expandedChanges), Is.EqualTo((true, 1)));
		steps.DoStep();
		steps.DoAll();
		steps.Stop();
		Assert.That((stepCount, allCount, stopCount, steps.Action, steps.HasChoices, steps.HasOthers), Is.EqualTo((1, 1, 1, (PlayerStepActionViewModel?) null, false, false)));
	}

	[Test]
	public void OptionsAreShownAsChoices()
	{
		var (_, world) = GameWorldTests.CreateWorld(0, withSchedules: true, isHeadless: false);
		var player = world.Player;
		var need = new Need
		{
			Id = "choosing", Title = "Choosing", Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 },
			StepSettings = [new Step { Kind = StepKind.Choose, Options = [new StepOption { Player = new StepOptionPlayer { Title = "Rest" }, Steps = [TestData.Do(TestData.Amount(10))] }] }],
		};
		var sequence = Scheduler.StartSequence(player, need, 0, null, [], null);
		var steps = new PlayerStepsViewModel(sequence,
			[new PlayerStep(sequence, PlayerStepKind.Option, 0, Option: need.GetOptions(0)[0].Option, IsCurrent: true), new PlayerStep(sequence, PlayerStepKind.OptionSteps, 0)],
			player, isExpanded: true, action: null, choices: [], others: [], () => { }, () => { }, () => { }, canDoStep: false);

		Assert.That(steps.Steps.Select(x => x.Text), Is.EqualTo(new[] { "Or: Rest", "…" }));
		Assert.That((steps.Next, steps.CanDoStep()), Is.EqualTo(("Next: Choose what to do", false)));
	}
}
