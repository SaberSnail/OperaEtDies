using OperaEtDies.Controls;

namespace OperaEtDies.Test.Controls;

[TestFixture]
public sealed class SplitActionViewModelTests
{
	[Test]
	public void TheMainPartDoesTheDefault()
	{
		var count = 0;
		var canDo = true;
		var action = new SplitActionViewModel("Wait", () => count++, () => canDo);
		action.Do();
		canDo = false;
		action.Do();
		Assert.That((count, action.CanDo(), action.HasMenu), Is.EqualTo((1, false, false)));

		// Without other ways, there's no menu to open.
		Assert.That((action.OpenMenu(), action.IsMenuOpen), Is.EqualTo((false, false)));
	}

	[Test]
	public void TheMenuListsTheOtherWaysNumbered()
	{
		var chosen = new List<string>();
		var action = new SplitActionViewModel("Wait", () => chosen.Add("wait"),
			alternatives: [new MenuActionViewModel("Wait an hour", () => chosen.Add("hour")), new MenuActionViewModel("Wait until noon", () => chosen.Add("noon"), () => false, "Not today")]);
		Assert.That(action.Alternatives.Select(x => x.Badge), Is.EqualTo(new[] { "1", "2" }));

		// Choosing one by number closes the menu, and one that can't be chosen now isn't.
		Assert.That(action.OpenMenu(), Is.True);
		Assert.That(action.ChooseAlternative(2), Is.False);
		Assert.That((action.ChooseAlternative(1), action.IsMenuOpen), Is.EqualTo((true, false)));
		Assert.That(action.ChooseAlternative(3), Is.False);
		Assert.That(chosen, Is.EqualTo(new[] { "hour" }));
	}

	[Test]
	public void TheMenuIsOnlyShownWhenItHelps()
	{
		MenuActionViewModel Disabled() => new("Later", () => { }, () => false);

		// A button whose text stays the same keeps its menu even when nothing in it can be chosen now, so it doesn't come
		// and go, but one whose text changes only shows it while something in it can be chosen.
		Assert.That(new SplitActionViewModel("Walk there", () => { }, alternatives: [Disabled()]).HasMenu, Is.True);
		Assert.That(new SplitActionViewModel("Work (1 hour)", () => { }, alternatives: [Disabled()], isTextStable: false).HasMenu, Is.False);
		Assert.That(new SplitActionViewModel("Work (1 hour)", () => { }, alternatives: [Disabled(), new MenuActionViewModel("Now", () => { })], isTextStable: false).HasMenu, Is.True);
	}

	[Test]
	public void TheMenuCantBeUsedWhileTheDefaultCantBeDone()
	{
		var chosen = 0;
		var canDo = false;
		var action = new SplitActionViewModel("Walk there", () => { }, () => canDo, [new MenuActionViewModel("Walk home", () => chosen++)]);

		// The menu is still shown, since the text doesn't change, but neither opens nor chooses.
		Assert.That((action.HasMenu, action.OpenMenu(), action.ChooseAlternative(1), chosen), Is.EqualTo((true, false, false, 0)));
		canDo = true;
		Assert.That((action.OpenMenu(), action.ChooseAlternative(1), chosen), Is.EqualTo((true, true, 1)));
	}

	[Test]
	public void TheChevronsTooltipShowsTheKeysThatOpenIt()
	{
		var action = new SplitActionViewModel("Wait", () => { }, alternatives: [new MenuActionViewModel("Wait an hour", () => { })]);
		Assert.That(action.MenuToolTip, Is.EqualTo("Other ways"));
		action.Badge = "3";
		Assert.That(action.MenuToolTip, Is.EqualTo("Other ways (Shift+3)"));
	}
}
