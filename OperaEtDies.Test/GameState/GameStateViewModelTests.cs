using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class GameStateViewModelTests
{
	[Test]
	public void PlayerStartsInStartingLocation()
	{
		var gameState = CreateGameState();

		Assert.That(gameState.Player.IsPlayer, Is.True);
		Assert.That(gameState.Player.FullName, Is.EqualTo("John Smith"));
		Assert.That(gameState.Player.Location.Id, Is.EqualTo("home"));
		Assert.That(gameState.Player.Location.People, Does.Contain(gameState.Player));
	}

	[Test]
	public void LocationsAreCreatedFromData()
	{
		var gameState = CreateGameState();

		Assert.That(gameState.Locations.Select(x => x.Id), Is.EqualTo(new[] { "home", "lane", "cottage" }));

		var lane = gameState.FindLocation("lane")!;
		Assert.That(lane.Title, Is.EqualTo("lane title"));
		Assert.That(lane.ShortDescription, Is.EqualTo("lane short"));
		Assert.That(lane.MediumDescription, Is.EqualTo("lane medium"));
		Assert.That(lane.LongDescription, Is.EqualTo(new[] { "lane long 1", "lane long 2", "lane long 3" }));
		Assert.That(lane.Connections, Is.EqualTo(new[] { gameState.FindLocation("home"), gameState.FindLocation("cottage") }));
		Assert.That(gameState.FindLocation("nowhere"), Is.Null);
	}

	[Test]
	public void GeneratesThreeNpcs()
	{
		var locations = new HashSet<string>();
		for (var seed = 0; seed < 50; seed++)
		{
			var gameState = CreateGameState(seed);

			Assert.That(gameState.Npcs, Has.Count.EqualTo(3));
			foreach (var npc in gameState.Npcs)
			{
				Assert.That(npc.IsPlayer, Is.False);
				Assert.That(npc.Traits.GetInteger("age"), Is.InRange(16, 70));
				Assert.That(npc.Location.People, Does.Contain(npc));
				locations.Add(npc.Location.Id);
			}
		}

		Assert.That(locations, Is.EquivalentTo(new[] { "home", "lane", "cottage" }));
	}

	[Test]
	public void MovingPersonUpdatesLocations()
	{
		var gameState = CreateGameState();
		var player = gameState.Player;
		var home = gameState.FindLocation("home")!;
		var lane = gameState.FindLocation("lane")!;
		var changed = new List<string?>();
		player.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		player.Location = lane;

		Assert.That(player.Location, Is.SameAs(lane));
		Assert.That(home.People, Does.Not.Contain(player));
		Assert.That(lane.People, Does.Contain(player));
		Assert.That(changed, Is.EqualTo(new[] { nameof(PersonViewModel.Location) }));
	}

	[Test]
	public void PersonDescribesThemselves()
	{
		var gameData = TestData.CreateGameData();
		var traits = TestData.CreateTraits(gameData, "female", "Alice", "Miller", "miller", "tall", "heavy", 34);

		var person = new PersonViewModel(gameData, traits, new LocationViewModel(gameData.StartingLocation), isPlayer: false);

		Assert.That(person.FullName, Is.EqualTo("Alice Miller"));
		Assert.That(person.Traits.GetOption("profession").Id, Is.EqualTo("miller"));
		Assert.That(person.Traits.GetOption("height").Id, Is.EqualTo("tall"));
		Assert.That(person.Description, Is.EqualTo("Alice Miller is a woman of 34. She is tall and heavy, and works as a miller."));
	}

	[Test]
	public void ShippedDataDescribesPeople()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;

		var gameState = GameStateViewModel.CreateNewGame(gameData, TestData.CreatePlayerTraits(gameData), new Random(0));

		foreach (var npc in gameState.Npcs)
		{
			Assert.That(npc.Description, Does.StartWith(npc.FullName));
			Assert.That(npc.Description, Does.Contain(npc.Traits.GetOption("profession").Text["description"]));
		}
	}

	private static GameStateViewModel CreateGameState(int seed = 0)
	{
		var gameData = TestData.CreateGameData();
		return GameStateViewModel.CreateNewGame(gameData, TestData.CreatePlayerTraits(gameData), new Random(seed));
	}
}
