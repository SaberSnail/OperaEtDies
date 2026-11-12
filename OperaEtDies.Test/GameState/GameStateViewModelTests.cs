using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class GameStateViewModelTests
{
	[Test]
	public void PlayerStartsAtHome()
	{
		var gameState = CreateGameState();

		Assert.That(gameState.Player.IsPlayer, Is.True);
		Assert.That(gameState.Player.FullName, Is.EqualTo("John Smith"));
		Assert.That(gameState.Player.Location, Is.SameAs(gameState.Player.Home));
		Assert.That(gameState.Player.Home.Id, Is.EqualTo(gameState.Town.PlayerHousehold.House!.Id));
		Assert.That(gameState.Player.Location.People, Does.Contain(gameState.Player));
	}

	[Test]
	public void LocationsAreCreatedFromTown()
	{
		var gameState = CreateGameState();

		Assert.That(gameState.Locations.Select(x => x.Id), Is.EqualTo(gameState.Town.Locations.Select(x => x.Id)));
		foreach (var (location, townLocation) in gameState.Locations.Zip(gameState.Town.Locations))
		{
			// Houses are named after the head of the household that lives there.
			var household = gameState.Town.Households.FirstOrDefault(x => x.House == townLocation);
			Assert.That(location.Title, Is.EqualTo(household is not null ? $"The {household.Head.Traits.GetString("lastName")} Home" : townLocation.Template.Title));
			Assert.That(location.ShortDescription, Is.EqualTo(townLocation.Template.ShortDescription));
			Assert.That(location.MediumDescription, Is.EqualTo(townLocation.Template.MediumDescription));
			Assert.That(location.LongDescription, Is.EqualTo(townLocation.Template.LongDescription));
			Assert.That(location.Connections.Select(x => x.Id), Is.EqualTo(townLocation.Connections.Select(x => x.Id)));
			Assert.That(gameState.FindLocation(location.Id), Is.SameAs(location));
		}

		Assert.That(gameState.FindLocation("nowhere"), Is.Null);
	}

	[Test]
	public void EveryoneStartsAtHomeAtWorkOrInAQuarter()
	{
		var places = new HashSet<string>();
		var homeWork = new HashSet<bool>();
		for (var seed = 0; seed < 20; seed++)
		{
			var gameState = CreateGameState(seed);
			var quarters = gameState.Locations.Where(x => x.Bounds is not null).ToList();
			var people = gameState.Town.Households.SelectMany(x => x.Members).ToList();

			Assert.That(gameState.Npcs, Has.Count.EqualTo(people.Count - 1));
			foreach (var (npc, person) in gameState.Npcs.Zip(people.Where(x => !x.IsPlayer)))
			{
				Assert.That(npc.IsPlayer, Is.False);
				Assert.That(npc.Traits.GetValueId("profession"), Is.EqualTo(person.Traits.GetValueId("profession")));
				Assert.That(npc.Location == npc.Home || npc.Location == npc.Workplace || quarters.Contains(npc.Location), Is.True);
				// People who work from home are working half the time they're there.
				if (npc.Location == npc.Workplace && npc.Workplace == npc.Home && npc.Profession is not null)
					homeWork.Add(npc.IsAtWork);
				else
					Assert.That(npc.IsAtWork, Is.EqualTo(npc.Location == npc.Workplace && npc.Workplace != npc.Home));
				Assert.That(npc.Home.Id, Is.EqualTo(person.Household.House!.Id));
				Assert.That(npc.Workplace.Id, Is.EqualTo(person.Business?.Location?.Id ?? npc.Home.Id));
				Assert.That(npc.Location.People, Does.Contain(npc));
				places.Add(npc.Location == npc.Home ? "home" : npc.Location == npc.Workplace ? "work" : "quarter");
			}
		}

		Assert.That(places, Is.EquivalentTo(new[] { "home", "work", "quarter" }));
		Assert.That(homeWork, Is.EquivalentTo(new[] { true, false }));
	}

	[Test]
	public void PeopleAreGroupedByFamily()
	{
		var gameState = CreateGameState();

		Assert.That(gameState.Families.Select(x => x.LastName), Is.EqualTo(gameState.Town.Households.Select(x => x.LastName)));
		Assert.That(gameState.Families[0].Members[0], Is.SameAs(gameState.Player));
		Assert.That(gameState.Families.SelectMany(x => x.Members).Where(x => !x.IsPlayer), Is.EqualTo(gameState.Npcs));
		foreach (var (family, household) in gameState.Families.Zip(gameState.Town.Households))
		{
			// The game sets the at work property from where people are.
			Assert.That(family.Members.Select(x => x.Traits.Values.Where(value => value.Key != "atWork")),
				Is.EqualTo(household.Members.Select(x => x.Traits.Values.Where(value => value.Key != "atWork"))));
			Assert.That(family.Members.Select(x => x.Home).Distinct(), Has.Exactly(1).Items);
		}
	}

	[Test]
	public void MovingPersonUpdatesLocations()
	{
		var gameState = CreateGameState();
		var player = gameState.Player;
		var home = player.Home;
		var quarter = home.Connections[0];
		player.Location = home;
		var changed = new List<string?>();
		player.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		player.Location = quarter;

		Assert.That(player.Location, Is.SameAs(quarter));
		Assert.That(home.People, Does.Not.Contain(player));
		Assert.That(quarter.People, Does.Contain(player));
		Assert.That(changed, Is.EquivalentTo(new[] { nameof(PersonViewModel.Location), nameof(PersonViewModel.PortraitPlace), nameof(PersonViewModel.Portrait) }));
		Assert.That(player.PortraitPlace, Is.EqualTo(PortraitPlace.InQuarter(quarter.QuarterKind!)));
	}

	[Test]
	public void PersonDescribesThemselves()
	{
		var gameData = TestData.CreateGameData();
		var traits = TestData.CreateTraits(gameData, "female", "Alice", "Miller", "miller", "tall", "heavy", 34);

		var person = new PersonViewModel(gameData, traits, new LocationViewModel("home", gameData.HouseTemplates[0]), isPlayer: false);

		Assert.That(person.FullName, Is.EqualTo("Alice Miller"));
		Assert.That(person.Traits.GetOption("profession").Id, Is.EqualTo("miller"));
		Assert.That(person.Traits.GetOption("height").Id, Is.EqualTo("tall"));
		Assert.That(person.Description, Is.EqualTo("Alice Miller is a woman of 34. She is tall and heavy, and works as a miller."));
		Assert.That(person.Workplace, Is.SameAs(person.Home));
	}

	[Test]
	public void WorkAndPortraitPlaceFollowTheLocation()
	{
		var gameData = TestData.CreateGameData();
		var traits = TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40);
		var home = new LocationViewModel("home", gameData.HouseTemplates[0]);
		var otherHome = new LocationViewModel("otherHome", gameData.HouseTemplates[0]);
		var forge = new LocationViewModel("forge", gameData.BusinessTemplates.Get("forge"));
		var smithy = new LocationViewModel("smithy", gameData.BusinessTemplates.Get("smithy"));
		var lane = new LocationViewModel("lane", gameData.QuarterTemplates[0], quarterKind: "lane");
		var person = new PersonViewModel(gameData, traits, home, isPlayer: false, forge);

		Assert.That(person.IsAtWork, Is.False);
		Assert.That(person.Traits.GetValueId("atWork"), Is.EqualTo("notAtWork"));
		Assert.That(person.PortraitPlace, Is.EqualTo(PortraitPlace.Home));

		person.Location = forge;
		Assert.That(person.IsAtWork, Is.True);
		Assert.That(person.Traits.GetValueId("atWork"), Is.EqualTo("atWork"));
		Assert.That(person.PortraitPlace, Is.EqualTo(PortraitPlace.InWorkplace("smith")));

		// Someone else's business of the same profession looks the same, but the person isn't working there.
		person.Location = smithy;
		Assert.That(person.IsAtWork, Is.False);
		Assert.That(person.Traits.GetValueId("atWork"), Is.EqualTo("notAtWork"));
		Assert.That(person.PortraitPlace, Is.EqualTo(PortraitPlace.InWorkplace("smith")));

		person.Location = otherHome;
		Assert.That(person.PortraitPlace, Is.EqualTo(PortraitPlace.Home));

		person.Location = lane;
		Assert.That(person.PortraitPlace, Is.EqualTo(PortraitPlace.InQuarter("lane")));
	}

	[Test]
	public void WorkingFromHomeMakesHomeTheWorkplace()
	{
		var gameData = TestData.CreateGameData();
		var traits = TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40);
		var person = new PersonViewModel(gameData, traits, new LocationViewModel("home", gameData.HouseTemplates[0]), isPlayer: false);
		person.IsAtWork = false;
		var changed = new List<string?>();
		person.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		person.IsAtWork = true;

		Assert.That(person.Traits.GetValueId("atWork"), Is.EqualTo("atWork"));
		Assert.That(person.PortraitPlace, Is.EqualTo(PortraitPlace.InWorkplace("smith")));
		Assert.That(changed, Is.EquivalentTo(new[] { nameof(PersonViewModel.IsAtWork), nameof(PersonViewModel.Traits), nameof(PersonViewModel.Description), nameof(PersonViewModel.PortraitPlace), nameof(PersonViewModel.Portrait) }));
	}

	[Test]
	public void WorkingFromHomeIsDecidedOnArrivingHome()
	{
		var gameData = TestData.CreateGameData();
		var smith = TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40);
		var child = new CharacterTraits(smith.Values.Where(x => x.Key != "profession").ToDictionary(x => x.Key, x => x.Value));
		var home = new LocationViewModel("home", gameData.HouseTemplates[0]);
		var lane = new LocationViewModel("lane", gameData.QuarterTemplates[0], quarterKind: "lane");
		var random = new Random(0);
		var person = new PersonViewModel(gameData, smith, home, isPlayer: false, random: random);
		var results = new HashSet<bool> { person.IsAtWork };

		for (var visit = 0; visit < 40; visit++)
		{
			person.Location = lane;
			Assert.That(person.IsAtWork, Is.False);

			person.Location = home;
			results.Add(person.IsAtWork);
			Assert.That(person.PortraitPlace, Is.EqualTo(person.IsAtWork ? PortraitPlace.InWorkplace("smith") : PortraitPlace.Home));
		}

		// Someone without a profession is never working.
		var idle = new PersonViewModel(gameData, child, home, isPlayer: false, random: random);

		Assert.That(results, Is.EquivalentTo(new[] { true, false }));
		Assert.That(idle.IsAtWork, Is.False);
	}

	[Test]
	public void ShippedDataDescribesPeople()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;

		for (var seed = 0; seed < 10; seed++)
		{
			var player = new CharacterRandomizer(gameData, new Random(seed), CharacterRole.Player).GetTraits();
			var gameState = GameStateViewModel.CreateNewGame(gameData, player, new Random(seed));

			Assert.That(gameState.Npcs, Is.Not.Empty);
			foreach (var npc in gameState.Npcs)
			{
				Assert.That(npc.FullName, Is.Not.Empty);
				Assert.That(npc.Description, Is.Not.Empty.And.Not.Contain("  ").And.Not.Contain(" .").And.Not.Contain(" ,"));

				// A character has a value for exactly the properties whose conditions its other values meet.
				foreach (var property in gameData.CharacterProperties)
					Assert.That(npc.Traits.HasValue(property.Id), Is.EqualTo(property.HasValue(CharacterRole.Npc, npc.Traits.GetValueId)), $"{property.Id}: {npc.Description}");
			}
		}
	}

	private static GameStateViewModel CreateGameState(int seed = 0)
	{
		var gameData = TestData.CreateGameData();
		return GameStateViewModel.CreateNewGame(gameData, TestData.CreatePlayerTraits(gameData), new Random(seed));
	}
}
