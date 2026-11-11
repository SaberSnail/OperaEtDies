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
	public void EveryoneStartsAtHome()
	{
		for (var seed = 0; seed < 20; seed++)
		{
			var gameState = CreateGameState(seed);
			var people = gameState.Town.Households.SelectMany(x => x.Members).ToList();

			Assert.That(gameState.Npcs, Has.Count.EqualTo(people.Count - 1));
			foreach (var (npc, person) in gameState.Npcs.Zip(people.Where(x => !x.IsPlayer)))
			{
				Assert.That(npc.IsPlayer, Is.False);
				Assert.That(npc.Traits, Is.SameAs(person.Traits));
				Assert.That(npc.Location, Is.SameAs(npc.Home));
				Assert.That(npc.Home.Id, Is.EqualTo(person.Household.House!.Id));
				Assert.That(npc.Workplace.Id, Is.EqualTo(person.Business?.Location?.Id ?? npc.Home.Id));
				Assert.That(npc.Location.People, Does.Contain(npc));
			}
		}
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
			Assert.That(family.Members.Select(x => x.Traits), Is.EqualTo(household.Members.Select(x => x.Traits)));
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
		var changed = new List<string?>();
		player.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		player.Location = quarter;

		Assert.That(player.Location, Is.SameAs(quarter));
		Assert.That(home.People, Does.Not.Contain(player));
		Assert.That(quarter.People, Does.Contain(player));
		Assert.That(changed, Is.EqualTo(new[] { nameof(PersonViewModel.Location) }));
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
