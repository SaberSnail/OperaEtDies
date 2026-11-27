using GoldenAnvil.Utility;
using OperaEtDies.Controls.Game;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.Controls.Game;

[TestFixture]
public sealed class CharacterPanelViewModelTests
{
	[Test]
	public void NeedsAreIconsOfHowWellTheyreMet()
	{
		var world = CreateWorld();
		var fatigue = world.GameData.Schedules.Needs.Single(x => x.Id == "fatigue");
		world.Player.UpdateNeeds(world.Time);
		world.Player.SetLevel(fatigue, 20);
		var panel = new CharacterPanelViewModel(world);

		var meter = panel.Needs.Single(x => x.Title == "Fatigue");
		Assert.That((meter.Fraction, meter.IsActive, meter.IsWarning, meter.IsComplete, meter.IsStartable), Is.EqualTo((0.8, true, false, false, false)));
		Assert.That(meter.ToolTip, Is.EqualTo(OurResources.NeedToolTip.FormatCurrentCulture("Fatigue", fatigue.GetLevelTitle(20))));

		// Quotas aren't needs.
		Assert.That(panel.Needs.Select(x => x.Title), Has.None.EqualTo("Upkeep"));
	}

	[Test]
	public void ANeedAtItsWorstLevelIsAWarning()
	{
		var world = CreateWorld();
		var fatigue = world.GameData.Schedules.Needs.Single(x => x.Id == "fatigue");
		world.Player.UpdateNeeds(world.Time);
		var panel = new CharacterPanelViewModel(world);
		var meter = panel.Needs.Single(x => x.Title == "Fatigue");
		Assume.That(fatigue.Levels![^1].Minimum, Is.EqualTo(50));

		world.Player.SetLevel(fatigue, 49);
		panel.OnTimeChanged();
		Assert.That(meter.IsWarning, Is.False);

		world.Player.SetLevel(fatigue, 50);
		panel.OnTimeChanged();
		Assert.That(meter.IsWarning, Is.True);
	}

	[Test]
	public void ShowsTheProfession()
	{
		var world = CreateWorld();
		var propertyId = world.GameData.Families.Roles.Properties.Profession;
		var expected = (world.Player.Traits.Values.GetValueOrDefault(propertyId) as CharacterPropertyOption)?.Title;

		Assert.That(new CharacterPanelViewModel(world).Profession, Is.EqualTo(expected));
	}

	private static GameWorld CreateWorld()
	{
		var gameData = TestData.CreateGameData(withSchedules: true);
		return GameWorld.CreateNewGame(gameData, TestData.CreatePlayerTraits(gameData), new Random(0));
	}
}
