using OperaEtDies.Controls.Locations;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Test.GameState;
using OperaEtDies.Town;
using OperaEtDies.Windows.Main;

namespace OperaEtDies.Test.Windows.Main;

[TestFixture]
public sealed class MainWindowViewModelTests
{
	[Test]
	public void StartsAtHome()
	{
		using var viewModel = CreateViewModel();
		var home = viewModel.World.Player.Home;

		Assert.That(viewModel.Title, Is.EqualTo("Title"));
		Assert.That(viewModel.CurrentLocation.Location, Is.SameAs(viewModel.World.Player.Location));
		Assert.That(viewModel.CurrentLocation.Location, Is.SameAs(home));
		Assert.That(viewModel.CurrentLocation.Title, Is.EqualTo(home.Title));
		Assert.That(viewModel.CurrentLocation.Description, Is.EqualTo(home.MediumDescription));
		Assert.That(viewModel.CurrentLocation.Connections.Select(x => x.Title), Is.EqualTo(home.Connections.Select(x => x.Title)));
		Assert.That(viewModel.CurrentLocation.Connections.Select(x => x.Description), Is.EqualTo(home.Connections.Select(x => x.ShortDescription)));
	}

	[Test]
	public void ExamineShowsLongDescription()
	{
		using var viewModel = CreateViewModel();
		var location = viewModel.CurrentLocation;

		Assert.That(location.IsExamined, Is.False);
		Assert.That(location.CanExamine(), Is.True);

		location.Examine();

		Assert.That(location.IsExamined, Is.True);
		Assert.That(location.CanExamine(), Is.False);
		Assert.That(location.LongDescription, Is.EqualTo(viewModel.World.Player.Home.LongDescription));
	}

	[Test]
	public void TravelMovesPlayer()
	{
		using var viewModel = CreateViewModel();
		var home = viewModel.World.Player.Home;
		var changed = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
		viewModel.CurrentLocation.Examine();

		viewModel.CurrentLocation.Connections[0].Travel();

		var quarter = home.Connections[0];
		Assert.That(viewModel.World.Player.Location, Is.SameAs(quarter));
		Assert.That(viewModel.CurrentLocation.Title, Is.EqualTo(quarter.Title));
		Assert.That(viewModel.CurrentLocation.IsExamined, Is.False);
		Assert.That(viewModel.CurrentLocation.Connections.Select(x => x.Location), Does.Contain(home));
		Assert.That(changed, Does.Contain(nameof(MainWindowViewModel.CurrentLocation)));
	}

	[Test]
	public void WalkingBetweenQuartersTakesTime()
	{
		using var viewModel = CreateViewModel();
		var world = viewModel.World;
		var calendar = world.GameData.Calendar;
		var start = world.Time;

		// Stepping out of the house into its quarter takes a minute.
		viewModel.TravelTo(world.Player.Home.Quarter!);
		Assert.That(world.Time, Is.EqualTo(start + calendar.CreateTimeOffset(0, 0, 1)));
		start = world.Time;

		var quarter = world.Player.Location;
		var next = quarter.Exits.First(x => x.Direction is not null).Location;
		viewModel.TravelTo(next);

		Assert.That(world.Time, Is.EqualTo(start + world.GetTravelTime(quarter, next)));
		Assert.That(world.Time, Is.GreaterThan(start));
		Assert.That(calendar.GetParts(world.Time - start).Minutes, Is.EqualTo(2).Or.EqualTo(5));
	}

	[Test]
	public void ConnectionsShowHowLongTheyTake()
	{
		using var viewModel = CreateViewModel();
		var world = viewModel.World;

		// From the house, the only way out is into its quarter.
		Assert.That(viewModel.CurrentLocation.Places.Select(x => x.TravelTime), Is.EqualTo(new[] { "1 minute" }));

		viewModel.TravelTo(world.Player.Home.Quarter!);
		var quarter = world.Player.Location;
		Assert.Multiple(() =>
		{
			foreach (var path in viewModel.CurrentLocation.Paths)
			{
				var minutes = world.GameData.Calendar.GetParts(world.GetTravelTime(quarter, path.Location)).Minutes;
				Assert.That(path.TravelTime, Is.EqualTo($"{minutes} minutes"));
				Assert.That(path.TravelTimeText, Is.EqualTo($"({minutes} minutes)"));
			}
			Assert.That(viewModel.CurrentLocation.Places.Select(x => x.TravelTime), Is.All.EqualTo("1 minute"));
		});
	}

	[Test]
	public void LocationShowsPeopleInIt()
	{
		using var viewModel = CreateViewModel();
		var gameState = viewModel.World;

		foreach (var location in gameState.Locations)
		{
			viewModel.TravelTo(location);

			var expected = gameState.Npcs.Where(x => x.Location == location).Select(x => x.FullName);
			Assert.That(viewModel.CurrentLocation.People.Select(x => x.Name), Is.EquivalentTo(expected));
			Assert.That(viewModel.CurrentLocation.HasPeople, Is.EqualTo(expected.Any()));
		}
	}

	[Test]
	public void ExaminePersonShowsDescription()
	{
		var gameData = TestData.CreateGameData();
		var world = new TestWorld(gameData);
		var person = world.AddPerson(TestData.CreatePlayerTraits(gameData), world.AddLocation(gameData.HouseTemplates[0]));
		var viewModel = new LocationPersonViewModel(person, gameData.PortraitRenderer);

		Assert.That(viewModel.Name, Is.EqualTo("John Smith"));
		Assert.That(viewModel.Person, Is.SameAs(person));
		Assert.That(viewModel.IsExamined, Is.False);
		Assert.That(viewModel.CanExamine(), Is.True);

		viewModel.Examine();

		Assert.That(viewModel.IsExamined, Is.True);
		Assert.That(viewModel.CanExamine(), Is.False);
	}

	[Test]
	public void StartsShowingLocation()
	{
		using var viewModel = CreateViewModel();

		Assert.That(viewModel.Villagers, Is.Null);
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.CurrentLocation));
	}

	[Test]
	public void ShowVillagersListsEveryoneByFamily()
	{
		using var viewModel = CreateViewModel();
		var changed = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		viewModel.ShowVillagers();

		var villagers = viewModel.Villagers!;
		var families = viewModel.World.Families.OrderBy(x => x.LastName, StringComparer.CurrentCulture).ToList();
		Assert.That(viewModel.CurrentView, Is.SameAs(villagers));
		Assert.That(changed, Is.EquivalentTo(new[] { nameof(MainWindowViewModel.Villagers), nameof(MainWindowViewModel.CurrentView) }));
		// Families are sorted by last name.
		Assert.That(villagers.Families.Select(x => x.Family), Is.EqualTo(families));
		Assert.That(villagers.Families.Select(x => x.Title), Is.EqualTo(families.Select(x => $"The {x.LastName} Family")));
		foreach (var family in villagers.Families)
			Assert.That(family.People.Select(x => x.Person), Is.EqualTo(family.Family.Members));

		// Showing them again while they're open changes nothing.
		viewModel.ShowVillagers();
		Assert.That(viewModel.Villagers, Is.SameAs(villagers));
	}

	[Test]
	public void ExamineVillagerShowsDescription()
	{
		using var viewModel = CreateViewModel();
		viewModel.ShowVillagers();
		var person = viewModel.Villagers!.Families[1].People[0];

		Assert.That(person.IsExamined, Is.False);

		person.Examine();

		Assert.That(person.IsExamined, Is.True);
		Assert.That(person.Person.Description, Is.Not.Empty);
	}

	[Test]
	public void CloseVillagersReturnsToLocation()
	{
		using var viewModel = CreateViewModel();
		var location = viewModel.CurrentLocation;
		viewModel.ShowVillagers();
		var changed = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		viewModel.Villagers!.Close();

		Assert.That(viewModel.Villagers, Is.Null);
		Assert.That(viewModel.CurrentLocation, Is.SameAs(location));
		Assert.That(viewModel.CurrentView, Is.SameAs(location));
		Assert.That(changed, Is.EquivalentTo(new[] { nameof(MainWindowViewModel.Villagers), nameof(MainWindowViewModel.CurrentView) }));
	}

	[Test]
	public void VillagersShowDeveloperInformationOnlyForDevelopers()
	{
		using var player = CreateViewModel();
		using var developer = CreateViewModel(isDeveloper: true);

		player.ShowVillagers();
		developer.ShowVillagers();

		Assert.That(player.Villagers!.Families.Select(x => x.IsDeveloper), Has.All.False);
		Assert.That(developer.Villagers!.Families.Select(x => x.IsDeveloper), Has.All.True);
		foreach (var family in developer.Villagers.Families)
		{
			Assert.That(family.Wealth, Is.EqualTo($"Wealth: {family.Family.WealthLevel.GetDataName()}"));
			Assert.That(family.HomeTitle, Is.EqualTo(family.Family.Home.Title));
			Assert.That(family.Family.Members.Select(x => x.Home), Has.All.SameAs(family.Family.Home));
		}
	}

	[Test]
	public void VisitHomeClosesVillagersAndGoesToTheHouse()
	{
		using var viewModel = CreateViewModel(isDeveloper: true);
		viewModel.ShowVillagers();
		var family = viewModel.Villagers!.Families[1];

		family.VisitHome();

		Assert.That(viewModel.Villagers, Is.Null);
		Assert.That(viewModel.World.Player.Location, Is.SameAs(family.Family.Home));
		Assert.That(viewModel.CurrentLocation.Location, Is.SameAs(family.Family.Home));
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.CurrentLocation));
	}

	[Test]
	public void QuartersListPathsInCompassOrderAndPlacesSeparately()
	{
		using var viewModel = CreateViewModel();
		var quarter = viewModel.World.Player.Home.Connections.Single();

		viewModel.TravelTo(quarter);
		var screen = viewModel.CurrentLocation;

		// Paths lead to other quarters, in compass order, and say which way they go. Places are the houses and businesses.
		Assert.That(screen.Paths, Is.Not.Empty);
		Assert.That(screen.Paths.Select(x => x.Direction), Is.Ordered);
		Assert.That(screen.Paths.Select(x => x.Location.Bounds), Has.All.Not.Null);
		foreach (var path in screen.Paths)
		{
			var exit = quarter.Exits.Single(x => x.Location == path.Location);
			Assert.That(path.Route, Is.EqualTo($"{exit.Direction!.Value.GetTitle()} along the {exit.Path}"));
		}

		Assert.That(screen.Places.Select(x => x.Location), Does.Contain(viewModel.World.Player.Home));
		Assert.That(screen.Places.Select(x => x.Route), Has.All.Null);
		Assert.That(screen.PlacesHeader, Is.EqualTo(OurResources.PlacesHeader));
		Assert.That(screen.Connections, Is.EqualTo(screen.Paths.Concat(screen.Places)));

		// A house only leads back to its quarter.
		viewModel.TravelTo(viewModel.World.Player.Home);
		Assert.That(viewModel.CurrentLocation.HasPaths, Is.False);
		Assert.That(viewModel.CurrentLocation.PlacesHeader, Is.EqualTo(OurResources.ConnectionsHeader));
	}

	[Test]
	public void OnlyDevelopersCanTravelByTheMap()
	{
		using var player = CreateViewModel();
		using var developer = CreateViewModel(isDeveloper: true);
		var home = player.World.Player.Home;

		player.ShowMap();
		developer.ShowMap();

		// Everyone can open the map.
		Assert.That(player.CurrentView, Is.SameAs(player.Map));
		Assert.That(developer.CurrentView, Is.SameAs(developer.Map));
		Assert.That(player.Map!.CanTravel, Is.False);
		Assert.That(developer.Map!.CanTravel, Is.True);

		// Choosing a quarter does nothing for players.
		var quarter = player.Map.Quarters.First(x => !x.IsCurrent);
		Assert.That(quarter.CanVisit(), Is.False);
		quarter.Visit();
		Assert.That(player.Map, Is.Not.Null);
		Assert.That(player.World.Player.Location, Is.SameAs(home));
	}

	[Test]
	public void MiniMapShowsThePlayersQuarterAndItsNeighbors()
	{
		using var viewModel = CreateViewModel();
		var home = viewModel.World.Player.Home;
		var quarter = home.Connections.Single();
		var neighbors = quarter.Exits.Where(x => x.Direction is not null).Select(x => x.Location).ToList();

		// At home, the mini-map shows the quarter the house is in.
		var miniMap = viewModel.MiniMap;
		Assert.That(miniMap.CanTravel, Is.False);
		Assert.That(miniMap.Quarters.Select(x => x.Quarter), Is.EquivalentTo(neighbors.Prepend(quarter)));
		Assert.That(miniMap.Quarters.Where(x => x.IsCurrent).Select(x => x.Quarter), Is.EqualTo(new[] { quarter }));
		Assert.That(miniMap.Width, Is.LessThanOrEqualTo(196.001));
		Assert.That(miniMap.Height, Is.LessThanOrEqualTo(196.001));

		// Moving updates it.
		var changed = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
		var next = neighbors[0];
		viewModel.TravelTo(next);

		Assert.That(changed, Does.Contain(nameof(MainWindowViewModel.MiniMap)));
		Assert.That(viewModel.MiniMap.Quarters.Where(x => x.IsCurrent).Select(x => x.Quarter), Is.EqualTo(new[] { next }));
		Assert.That(viewModel.MiniMap.Quarters.Select(x => x.Quarter), Does.Contain(quarter));
	}

	[Test]
	public void MapShowsEveryQuarterAndMarksThePlayersQuarter()
	{
		using var viewModel = CreateViewModel(isDeveloper: true);
		var quarters = viewModel.World.Locations.Where(x => x.Bounds is not null).ToList();
		var home = viewModel.World.Player.Home;

		viewModel.ShowMap();
		var map = viewModel.Map!;

		// The player is at home, so their quarter is the one the house is in.
		Assert.That(map.Quarters.Select(x => x.Quarter), Is.EquivalentTo(quarters));
		Assert.That(map.Quarters.Where(x => x.IsCurrent).Select(x => x.Quarter), Is.EqualTo(new[] { home.Connections.Single() }));
		Assert.That(map.Quarters.Select(x => x.Left), Has.All.GreaterThanOrEqualTo(0));
		Assert.That(map.Quarters.Select(x => x.Top), Has.All.GreaterThanOrEqualTo(0));
		Assert.That(map.Quarters.Max(x => x.Left + x.Width), Is.EqualTo(map.Width));
		Assert.That(map.Quarters.Max(x => x.Top + x.Height), Is.EqualTo(map.Height));

		// Each path between quarters is drawn as two segments.
		var pathCount = quarters.Sum(x => x.Exits.Count(exit => exit.Direction is not null)) / 2;
		Assert.That(map.Paths, Has.Count.EqualTo(pathCount * 2));
	}

	[Test]
	public void ChoosingAQuarterOnTheMapGoesThere()
	{
		using var viewModel = CreateViewModel(isDeveloper: true);
		viewModel.ShowMap();
		var quarter = viewModel.Map!.Quarters.First(x => !x.IsCurrent);

		var time = viewModel.World.Time;

		quarter.Visit();

		Assert.That(viewModel.World.Time, Is.EqualTo(time), "Developers' jumps take no time.");
		Assert.That(viewModel.Map, Is.Null);
		Assert.That(viewModel.World.Player.Location, Is.SameAs(quarter.Quarter));
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.CurrentLocation));
	}

	[Test]
	public void MapAndVillagersReplaceEachOther()
	{
		using var viewModel = CreateViewModel(isDeveloper: true);

		viewModel.ShowMap();
		viewModel.ShowVillagers();
		Assert.That(viewModel.Map, Is.Null);
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.Villagers));

		viewModel.ShowMap();
		Assert.That(viewModel.Villagers, Is.Null);
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.Map));

		viewModel.Map!.Close();
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.CurrentLocation));
	}

	[Test]
	public void ShowsTheDateAndTimeAsTimePasses()
	{
		using var viewModel = CreateViewModel();
		var world = viewModel.World;
		var calendar = world.GameData.Calendar;
		var changes = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

		// Move to midsummer afternoon of the next year, which is always later than the start.
		var midsummer = calendar.CreateTimePoint(calendar.StartYear + 1, 2, 2, 4, 13, 5);
		world.AdvanceTime(midsummer - world.Time);

		Assert.Multiple(() =>
		{
			Assert.That(viewModel.Date, Is.EqualTo("Day 4, Mid Summer, year 2"));
			Assert.That(viewModel.TimeOfDay, Is.EqualTo("Afternoon, 13:05"));
			Assert.That(viewModel.SpecialDay, Is.EqualTo("Midsummer"));
			Assert.That(changes, Is.SupersetOf(new[] { nameof(MainWindowViewModel.Date), nameof(MainWindowViewModel.TimeOfDay), nameof(MainWindowViewModel.SpecialDay) }));
		});

		world.AdvanceTime(calendar.CreateTimeOffset(1, 9, 0));

		Assert.Multiple(() =>
		{
			Assert.That(viewModel.Date, Is.EqualTo("Day 5, Mid Summer, year 2"));
			Assert.That(viewModel.TimeOfDay, Is.EqualTo("Night, 22:05"));
			Assert.That(viewModel.SpecialDay, Is.Null);
		});
	}

	private static MainWindowViewModel CreateViewModel(int seed = 0, bool isDeveloper = false)
	{
		var gameData = TestData.CreateGameData();
		var world = GameWorld.CreateNewGame(gameData, TestData.CreatePlayerTraits(gameData), new Random(seed));
		return new MainWindowViewModel(TestData.CreateGameInfo().Title, world, isDeveloper);
	}
}
