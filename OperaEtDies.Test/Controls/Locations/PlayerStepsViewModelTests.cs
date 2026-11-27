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
			Id = "choosing", Title = "Choosing", Icon = "icon", Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 },
			StepSettings = [new Step { Kind = StepKind.Choose, Options = [new StepOption { Player = new StepOptionPlayer { Title = "Rest" }, Steps = [TestData.Do(TestData.Amount(10))] }] }],
		};
		var sequence = Scheduler.StartSequence(player, need, 0, null, [], null);
		var steps = new PlayerStepsViewModel(sequence,
			[new PlayerStep(sequence, PlayerStepKind.Option, 0, Option: need.GetOptions(0)[0].Option, IsCurrent: true), new PlayerStep(sequence, PlayerStepKind.OptionSteps, 0)],
			player, isExpanded: true, action: null, choices: [], others: [], () => { }, () => { }, () => { }, canDoStep: false);

		Assert.That(steps.Steps.Select(x => x.Text), Is.EqualTo(new[] { "Or: Rest", "…" }));
		Assert.That((steps.Next, steps.CanDoStep()), Is.EqualTo(("Next: Choose what to do", false)));
	}

	[Test]
	public void TheSplitButtonPressesTheStepsButtonOrHasItDone()
	{
		var (_, world) = GameWorldTests.CreateWorld(0, withSchedules: true, isHeadless: false);
		var player = world.Player;
		var water = world.GameData.Schedules.Needs.Single(x => x.Id == "water");
		var sequence = Scheduler.StartSequence(player, water, 1, null, [], null);
		var (actionCount, stepCount, allCount) = (0, 0, 0);
		var action = new PlayerStepActionViewModel("Fetch water", isEnabled: true, note: null, () => actionCount++);
		var steps = new PlayerStepsViewModel(sequence, [new PlayerStep(sequence, PlayerStepKind.Step, 1, IsCurrent: true)], player, isExpanded: false, action, choices: [], others: [],
			() => stepCount++, () => allCount++, () => { }, canDoStep: true, firstNumber: 3);

		// With a button, the step's button is the default, numbered first, and having it or all of it done is in the menu.
		Assert.That((steps.Primary.Text, steps.Primary.Badge), Is.EqualTo(("Fetch water", "3")));
		Assert.That(steps.Primary.Alternatives.Select(x => x.Text), Is.EqualTo(new[] { "Do it for me", "Do it all for me" }));
		Assert.That(steps.ChooseNumber(3), Is.True);
		Assert.That((steps.OpenChoiceMenu(4), steps.OpenChoiceMenu(3), steps.Primary.IsMenuOpen), Is.EqualTo((false, true, true)));
		Assert.That(steps.Primary.ChooseAlternative(1), Is.True);
		steps.Primary.Alternatives[1].Do();
		Assert.That((actionCount, stepCount, allCount, steps.Primary.IsMenuOpen), Is.EqualTo((1, 1, 1, false)));

		// Without one, having the step done is the default, and only having all of it done is in the menu, which isn't
		// shown while it can't be chosen, since the button's text changes from step to step.
		var none = new PlayerStepsViewModel(sequence, [new PlayerStep(sequence, PlayerStepKind.Step, 1, IsCurrent: true)], player, isExpanded: false, action: null, choices: [], others: [],
			() => stepCount++, () => allCount++, () => { }, canDoStep: false);
		Assert.That((none.Primary.Text, none.Primary.CanDo()), Is.EqualTo(("Do it for me", false)));
		Assert.That((none.Primary.Alternatives.Single().Text, none.Primary.HasMenu), Is.EqualTo(("Do it all for me", true)));
	}
}
