using OperaEtDies.Controls.NewGame;
using OperaEtDies.Windows.Main;

namespace OperaEtDies.Test.Windows.Main;

[TestFixture]
public sealed class MainWindowViewModelTests
{
	[Test]
	public void StartsInStartingLocation()
	{
		using var viewModel = CreateViewModel();

		Assert.That(viewModel.PlayerLocation.Id, Is.EqualTo("home"));
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

		Assert.That(viewModel.PlayerLocation.Id, Is.EqualTo("lane"));
		Assert.That(viewModel.CurrentLocation.Title, Is.EqualTo("lane title"));
		Assert.That(viewModel.CurrentLocation.IsExamined, Is.False);
		Assert.That(viewModel.CurrentLocation.Connections.Select(x => x.Title), Is.EqualTo(new[] { "home title", "cottage title" }));
		Assert.That(changed, Does.Contain(nameof(MainWindowViewModel.CurrentLocation)));

		viewModel.CurrentLocation.Connections[1].Travel();

		Assert.That(viewModel.PlayerLocation.Id, Is.EqualTo("cottage"));
	}

	private static MainWindowViewModel CreateViewModel()
	{
		var gameData = TestData.CreateGameData();
		var settings = new NewGameSettings(gameData.Professions.Professions[0], "John", "Smith", gameData.CharacterOptions.Heights[0], gameData.CharacterOptions.Weights[0], 40);
		return new MainWindowViewModel(TestData.CreateGameInfo(), gameData, settings);
	}
}
