using OperaEtDies.Controls.Locations;
using OperaEtDies.GameState;
using OperaEtDies.Test.GameState;
using OperaEtDies.Windows.Main;

namespace OperaEtDies.Test.Windows.Main;

[TestFixture]
public sealed class MainWindowViewModelTests
{
	[Test]
	public void StartsInStartingLocation()
	{
		using var viewModel = CreateViewModel();

		Assert.That(viewModel.Title, Is.EqualTo("Title"));
		Assert.That(viewModel.CurrentLocation.Location, Is.SameAs(viewModel.GameState.Player.Location));
		Assert.That(viewModel.CurrentLocation.Title, Is.EqualTo("home title"));
		Assert.That(viewModel.CurrentLocation.Description, Is.EqualTo("home medium"));
		Assert.That(viewModel.CurrentLocation.Connections.Select(x => x.Title), Is.EqualTo(new[] { "lane title" }));
		Assert.That(viewModel.CurrentLocation.Connections.Select(x => x.Description), Is.EqualTo(new[] { "lane short" }));
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
		Assert.That(location.LongDescription, Is.EqualTo(new[] { "home long 1", "home long 2", "home long 3" }));
	}

	[Test]
	public void TravelMovesPlayer()
	{
		using var viewModel = CreateViewModel();
		var changed = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
		viewModel.CurrentLocation.Examine();

		viewModel.CurrentLocation.Connections[0].Travel();

		Assert.That(viewModel.GameState.Player.Location.Id, Is.EqualTo("lane"));
		Assert.That(viewModel.CurrentLocation.Title, Is.EqualTo("lane title"));
		Assert.That(viewModel.CurrentLocation.IsExamined, Is.False);
		Assert.That(viewModel.CurrentLocation.Connections.Select(x => x.Title), Is.EqualTo(new[] { "home title", "cottage title" }));
		Assert.That(changed, Does.Contain(nameof(MainWindowViewModel.CurrentLocation)));

		viewModel.CurrentLocation.Connections[1].Travel();

		Assert.That(viewModel.GameState.Player.Location.Id, Is.EqualTo("cottage"));
	}

	[Test]
	public void LocationShowsPeopleInIt()
	{
		using var viewModel = CreateViewModel();
		var gameState = viewModel.GameState;

		foreach (var id in new[] { "lane", "cottage", "lane", "home" })
		{
			viewModel.TravelTo(gameState.FindLocation(id)!);

			var expected = gameState.Npcs.Where(x => x.Location.Id == id).Select(x => x.FullName);
			Assert.That(viewModel.CurrentLocation.People.Select(x => x.Name), Is.EquivalentTo(expected));
			Assert.That(viewModel.CurrentLocation.HasPeople, Is.EqualTo(expected.Any()));
		}
	}

	[Test]
	public void ExaminePersonShowsDescription()
	{
		var gameData = TestData.CreateGameData();
		var person = new PersonViewModel(GameStateViewModelTests.CreatePlayerTraits(gameData), new LocationViewModel(gameData.StartingLocation), isPlayer: false);
		var viewModel = new LocationPersonViewModel(person);

		Assert.That(viewModel.Name, Is.EqualTo("John Smith"));
		Assert.That(viewModel.Description, Is.EqualTo(person.Description));
		Assert.That(viewModel.IsExamined, Is.False);
		Assert.That(viewModel.CanExamine(), Is.True);

		viewModel.Examine();

		Assert.That(viewModel.IsExamined, Is.True);
		Assert.That(viewModel.CanExamine(), Is.False);
	}

	private static MainWindowViewModel CreateViewModel(int seed = 0)
	{
		var gameData = TestData.CreateGameData();
		var gameState = GameStateViewModel.CreateNewGame(gameData, GameStateViewModelTests.CreatePlayerTraits(gameData), new Random(seed));
		return new MainWindowViewModel(TestData.CreateGameInfo().Title, gameState);
	}
}
