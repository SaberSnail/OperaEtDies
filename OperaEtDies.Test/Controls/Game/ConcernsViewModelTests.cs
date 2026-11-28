using GoldenAnvil.Utility;
using OperaEtDies.Controls;
using OperaEtDies.Controls.Game;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.Controls.Game;

[TestFixture]
public sealed class ConcernsViewModelTests
{
	[Test]
	public void ListsTodaysAppointmentsSoonestFirst()
	{
		var gameData = TestData.CreateGameData(withSchedules: true);
		var world = GameWorld.CreateNewGame(gameData, TestData.CreatePlayerTraits(gameData), new Random(0));
		var calendar = world.GameData.Calendar;
		var concerns = new ConcernsViewModel(world);
		Assert.That(concerns.Appointments, Is.Empty);

		var others = world.Npcs.Where(x => x.Family != world.Player.Family).Take(3).ToList();
		var need = new Need
		{
			Id = "errand", Title = "Errand", Icon = "icon", Category = NeedCategory.Personal, Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 },
			StepSettings =
			[
				new Step
				{
					Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "agreed", Minutes = TestData.Amount(5), Chance = new StepChance { Base = 100 },
					Player = new StepPlayer { Title = "Ask" },
					Appointment = TestData.AtPartnerHome([TestData.Do(TestData.Amount(30))]),
				},
			],
		};
		need.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		var later = calendar.AddMinutes(world.Time, 60);
		var sooner = calendar.AddMinutes(world.Time, 30);
		var tomorrow = calendar.GetStartOfDay(world.Time) + calendar.CreateTimeOffset(1, 10, 0);
		Assume.That(calendar.GetStartOfDay(later), Is.EqualTo(calendar.GetStartOfDay(world.Time)));
		var laterAppointment = world.AddAppointment(world.Player, others[0], need, 0, others[0].Home, later);
		var soonerAppointment = world.AddAppointment(others[1], world.Player, need, 0, others[1].Home, sooner);
		world.AddAppointment(world.Player, others[2], need, 0, others[2].Home, tomorrow);

		// It's only brought up to date when told to.
		var changes = new List<string?>();
		concerns.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
		concerns.Update();

		Assert.That(changes, Is.EqualTo(new[] { nameof(ConcernsViewModel.Appointments) }));
		Assert.That(concerns.Appointments, Is.EqualTo(new[]
		{
			new ConcernsAppointmentViewModel(calendar.FormatClock(sooner),
				OurResources.ConcernsAppointment.FormatCurrentCulture(others[1].FullName, AppointmentText.FormatPlace(world, soonerAppointment)), others[1].Home),
			new ConcernsAppointmentViewModel(calendar.FormatClock(later),
				OurResources.ConcernsAppointment.FormatCurrentCulture(others[0].FullName, AppointmentText.FormatPlace(world, laterAppointment)), others[0].Home),
		}));

		// Nothing changes when nothing did.
		changes.Clear();
		concerns.Update();
		Assert.That(changes, Is.Empty);
	}

	[Test]
	public void QuotasAreSplitIntoWorkAndHousehold()
	{
		var world = CreateWorld();
		var upkeep = world.GameData.Schedules.Needs.Single(x => x.Id == "upkeep");
		foreach (var member in world.Player.Family.Members)
		{
			member.UpdateNeeds(world.Time);
			member.SetProgress(upkeep, 0);
		}

		// The household's chores count what the whole family has done.
		var quota = 30 + (15 * world.Player.Family.Members.Count);
		world.Player.SetProgress(upkeep, 15);
		world.Player.Family.Members.First(x => !x.IsPlayer).SetProgress(upkeep, 15);
		var concerns = new ConcernsViewModel(world);

		var meter = concerns.Household.Single(x => x.Title == "Upkeep");
		Assert.That((meter.Fraction, meter.IsComplete), Is.EqualTo((30.0 / quota, false)));
		if (meter.IsActive)
			Assert.That(meter.ToolTip, Does.StartWith(OurResources.QuotaProgressToolTip.FormatCurrentCulture("Upkeep", "30m", ConcernsViewModel.FormatQuotaAmount(upkeep, quota), OurResources.QuotaToday)));
		// The work is named by the player's profession.
		var profession = (world.Player.Traits.Values.GetValueOrDefault(world.GameData.Families.Roles.Properties.Profession) as CharacterPropertyOption)?.Title ?? "Work";
		Assert.That(concerns.Work.Select(x => x.Title), Is.EqualTo(world.Player.Business is null ? Array.Empty<string>() : new[] { profession }));
		Assert.That(concerns.Household.Select(x => x.Title), Has.None.EqualTo("Work"));

		// Once it's done, it's shown as done, and can't be started.
		world.Player.SetProgress(upkeep, quota);
		concerns.Update();
		Assert.That((meter.Fraction, meter.IsComplete, meter.IsStartable), Is.EqualTo((1.0, true, false)));
	}

	[Test]
	public void ChoresAndWorkStartTheirSteps()
	{
		var world = CreateWorld();
		var started = new List<Need>();
		var concerns = new ConcernsViewModel(world, start: started.Add);
		var startable = concerns.Household.Where(x => x.IsStartable).ToList();
		Assume.That(startable, Is.Not.Empty);

		startable[0].Start();
		Assert.That(started, Is.EqualTo(new[] { startable[0].Need }));

		// One that can't be started now does nothing.
		if (concerns.Household.FirstOrDefault(x => !x.IsStartable) is { } blocked)
		{
			blocked.Start();
			Assert.That(started, Has.Count.EqualTo(1));
		}

		// Without a way to start them, nothing can be started.
		Assert.That(new ConcernsViewModel(world).Household.Any(x => x.IsStartable), Is.False);
	}

	[Test]
	public async Task GoToOffersHomeWorkAndMarkedQuartersAlphabeticallyAsync()
	{
		var world = CreateWorld();
		var player = world.Player;
		await world.MovePlayerAsync(player.Home);
		var chosen = new List<Location>();
		var concerns = new ConcernsViewModel(world, chosen.Add);

		// The square is the only kind marked for the menu in the test data.
		var square = world.Locations.Single(x => x.Quarter == x && x.QuarterKind == "square");
		var expected = new List<Location> { player.Home, square };
		if (player.Business is not null && player.Workplace != player.Home)
			expected.Add(player.Workplace);
		expected = expected.OrderBy(x => x.Title, StringComparer.CurrentCulture).ToList();

		var choices = concerns.GoTo.Alternatives;
		Assert.That(concerns.GoTo.Text, Is.EqualTo(OurResources.ConcernsGoTo));
		Assert.That(choices.Select(x => x.Text.Split(" (")[0]), Is.EqualTo(expected.Select(x => x.Title)));

		// The place the player is in can't be chosen, and shows no walking time.
		var home = choices[expected.IndexOf(player.Home)];
		Assert.That((home.Text, home.CanDo(), home.ToolTip), Is.EqualTo((player.Home.Title, false, OurResources.ConcernsGoToHereToolTip)));

		// Pressing the button opens the menu, and choosing a place chooses it as the destination.
		concerns.GoTo.Do();
		Assert.That(concerns.GoTo.IsMenuOpen, Is.True);
		Assert.That(concerns.GoTo.ChooseAlternative(expected.IndexOf(square) + 1), Is.True);
		Assert.That(chosen, Is.EqualTo(new[] { square }));

		// Once the player moves, the menu follows them.
		await world.MovePlayerAsync(square);
		concerns.Update();
		choices = concerns.GoTo.Alternatives;
		Assert.That(choices[expected.IndexOf(square)].CanDo(), Is.False);
		Assert.That(choices[expected.IndexOf(player.Home)].CanDo(), Is.True);
		Assert.That(choices[expected.IndexOf(player.Home)].Text, Does.StartWith(player.Home.Title + " ("));
	}

	[Test]
	public void QuotaAmountsAreHoursAndMinutesOrRuns()
	{
		var world = CreateWorld();
		var upkeep = world.GameData.Schedules.Needs.Single(x => x.Id == "upkeep");
		var water = world.GameData.Schedules.Needs.Single(x => x.Id == "water");
		Assume.That(water.IsRuns, Is.True);

		Assert.That(ConcernsViewModel.FormatQuotaAmount(upkeep, 45), Is.EqualTo("45m"));
		Assert.That(ConcernsViewModel.FormatQuotaAmount(upkeep, 120), Is.EqualTo("2h"));
		Assert.That(ConcernsViewModel.FormatQuotaAmount(upkeep, 90), Is.EqualTo("1h 30m"));
		Assert.That(ConcernsViewModel.FormatQuotaAmount(water, 2), Is.EqualTo("2"));
	}

	private static GameWorld CreateWorld()
	{
		var gameData = TestData.CreateGameData(withSchedules: true);
		return GameWorld.CreateNewGame(gameData, TestData.CreatePlayerTraits(gameData), new Random(0));
	}
}
