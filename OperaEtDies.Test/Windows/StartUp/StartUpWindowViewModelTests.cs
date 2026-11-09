using GoldenAnvil.Utility;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Data;
using OperaEtDies.Windows.StartUp;

namespace OperaEtDies.Test.Windows.StartUp;

[TestFixture]
public sealed class StartUpWindowViewModelTests
{
	[Test]
	public void ShowsGameInfo()
	{
		using var viewModel = CreateViewModel();

		Assert.That(viewModel.Title, Is.EqualTo("Title"));
		Assert.That(viewModel.Description, Is.EqualTo("Description"));
		Assert.That(viewModel.HasLoadErrors, Is.False);
		Assert.That(viewModel.CanShowNewGame(), Is.True);
		Assert.That(viewModel.NewGame, Is.Null);
	}

	[Test]
	public void MissingGameInfoShowsErrorsAndBlocksNewGame()
	{
		using var viewModel = new StartUpWindowViewModel(null, TestData.CreateGameData(), new Random(0), [new DataLoadError(@"C:\Data\GameInfo.json", "Broken")]);

		Assert.That(viewModel.Title, Is.EqualTo(OurResources.EngineName));
		Assert.That(viewModel.LoadErrors, Is.EqualTo(new[] { "GameInfo.json: Broken" }));
		Assert.That(viewModel.CanShowNewGame(), Is.False);

		viewModel.ShowNewGame();
		Assert.That(viewModel.NewGame, Is.Null);
	}

	[Test]
	public void MissingGameDataBlocksNewGame()
	{
		using var viewModel = new StartUpWindowViewModel(TestData.CreateGameInfo(), null, new Random(0), [new DataLoadError(@"C:\Data\Professions.json", "Broken")]);

		Assert.That(viewModel.Title, Is.EqualTo("Title"));
		Assert.That(viewModel.CanShowNewGame(), Is.False);
	}

	[Test]
	public void BackReturnsToMenu()
	{
		using var viewModel = CreateViewModel();

		viewModel.ShowNewGame();
		Assert.That(viewModel.NewGame, Is.Not.Null);

		viewModel.NewGame!.Back();
		Assert.That(viewModel.NewGame, Is.Null);
	}

	[Test]
	public void StartGameRaisesRequestWithSettings()
	{
		using var viewModel = CreateViewModel();
		NewGameSettings? settings = null;
		viewModel.StartNewGameRequested += (_, e) => settings = e.Value;

		viewModel.ShowNewGame();
		viewModel.NewGame!.StartGame();

		Assert.That(settings, Is.Not.Null);
		Assert.That(settings!.Player.GetString("lastName"), Is.EqualTo(settings.Player.GetOption("profession").Id == "smith" ? "Smith" : "Miller"));
	}

	[Test]
	public void ExitRaisesRequest()
	{
		using var viewModel = CreateViewModel();
		var exitCount = 0;
		viewModel.ExitGameRequested += (_, _) => exitCount++;

		viewModel.ExitGame();

		Assert.That(exitCount, Is.EqualTo(1));
	}

	private static StartUpWindowViewModel CreateViewModel() =>
		new(TestData.CreateGameInfo(), TestData.CreateGameData(), new Random(0), []);
}
