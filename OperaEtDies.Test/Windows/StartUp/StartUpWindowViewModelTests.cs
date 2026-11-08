using OperaEtDies.Data;
using OperaEtDies.Windows.StartUp;

namespace OperaEtDies.Test.Windows.StartUp;

[TestFixture]
public sealed class StartUpWindowViewModelTests
{
	[Test]
	public void ShowsGameInfo()
	{
		using var viewModel = new StartUpWindowViewModel(new GameInfo { Title = "Title", Description = "Description" }, []);

		Assert.That(viewModel.Title, Is.EqualTo("Title"));
		Assert.That(viewModel.Description, Is.EqualTo("Description"));
		Assert.That(viewModel.HasLoadErrors, Is.False);
		Assert.That(viewModel.CanStartNewGame(), Is.True);
	}

	[Test]
	public void MissingGameInfoShowsErrorsAndBlocksStart()
	{
		using var viewModel = new StartUpWindowViewModel(null, [new DataLoadError(@"C:\Data\GameInfo.json", "Broken")]);

		Assert.That(viewModel.Title, Is.EqualTo(OurResources.EngineName));
		Assert.That(viewModel.LoadErrors, Is.EqualTo(new[] { "GameInfo.json: Broken" }));
		Assert.That(viewModel.CanStartNewGame(), Is.False);

		var raised = false;
		viewModel.StartNewGameRequested += (_, _) => raised = true;
		viewModel.StartNewGame();
		Assert.That(raised, Is.False);
	}

	[Test]
	public void StartAndExitRaiseRequests()
	{
		using var viewModel = new StartUpWindowViewModel(new GameInfo { Title = "Title", Description = "Description" }, []);
		var startCount = 0;
		var exitCount = 0;
		viewModel.StartNewGameRequested += (_, _) => startCount++;
		viewModel.ExitGameRequested += (_, _) => exitCount++;

		viewModel.StartNewGame();
		viewModel.ExitGame();

		Assert.That(startCount, Is.EqualTo(1));
		Assert.That(exitCount, Is.EqualTo(1));
	}
}
