using System.IO;
using GoldenAnvil.Utility;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Test.Controls.ManageMods;
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
	public void DeveloperModeIsShownOnlyWhenTurnedOn()
	{
		using var player = new StartUpWindowViewModel(TestData.CreateGameInfo(), TestData.CreateGameData(), new Random(0), []);
		using var developer = new StartUpWindowViewModel(TestData.CreateGameInfo(), TestData.CreateGameData(), new Random(0), [], isDeveloper: true);

		Assert.That(player.IsDeveloper, Is.False);
		Assert.That(developer.IsDeveloper, Is.True);
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
	public void LoadGameNeedsDataAndSaveSettings()
	{
		var settings = new SaveSettings("Title", "hash", "", [], Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
		using var withoutSettings = CreateViewModel();
		using var withoutData = new StartUpWindowViewModel(TestData.CreateGameInfo(), null, new Random(0), [], saveSettings: settings);
		using var viewModel = new StartUpWindowViewModel(TestData.CreateGameInfo(), TestData.CreateGameData(), new Random(0), [], saveSettings: settings);

		Assert.That(withoutSettings.HasHashes, Is.False);
		Assert.That(viewModel.HasHashes, Is.True);
		Assert.That(viewModel.DataHash, Is.EqualTo("hash"));
		Assert.That(viewModel.DataHashText, Does.Contain("hash"));
		Assert.That(viewModel.HasModsHash, Is.False);

		using var withMods = new StartUpWindowViewModel(TestData.CreateGameInfo(), TestData.CreateGameData(), new Random(0), [], saveSettings: settings with { DataHash = "0123456789ABCDEF", ModsHash = "FEDCBA9876543210" });
		Assert.That(withMods.DataHashText, Does.Contain("0123456789AB").And.Not.Contain("CDEF"));
		Assert.That(withMods.ModsHashText, Does.Contain("FEDCBA987654").And.Not.Contain("3210"));
		Assert.That(withMods.ModsHash, Is.EqualTo("FEDCBA9876543210"));
		Assert.That(withMods.HasModsHash, Is.True);

		Assert.That(withoutSettings.CanShowLoadGame(), Is.False);
		Assert.That(withoutData.CanShowLoadGame(), Is.False);
		Assert.That(viewModel.CanShowLoadGame(), Is.True);

		viewModel.ShowLoadGame();
		Assert.That(viewModel.LoadGame, Is.Not.Null);
		Assert.That(viewModel.IsMenuVisible, Is.False);

		viewModel.LoadGame!.Back();
		Assert.That(viewModel.LoadGame, Is.Null);
		Assert.That(viewModel.IsMenuVisible, Is.True);
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

	[Test]
	public void ManageModsNeedsModListStore()
	{
		using var viewModel = CreateViewModel();

		Assert.That(viewModel.CanShowManageMods(), Is.False);
		viewModel.ShowManageMods();
		Assert.That(viewModel.ManageMods, Is.Null);
	}

	[Test]
	public void ReturningFromManageModsReloadsData()
	{
		var store = new ManageModsViewModelTests.FakeModListStore();
		using var viewModel = new StartUpWindowViewModel(TestData.CreateGameInfo(), TestData.CreateGameData(), new Random(0), [], store, new ManageModsViewModelTests.FakeFolderPicker());
		var reloadCount = 0;
		viewModel.ReloadDataRequested += (_, _) => reloadCount++;

		viewModel.ShowManageMods();
		Assert.That(viewModel.ManageMods, Is.Not.Null);
		Assert.That(viewModel.IsMenuVisible, Is.False);

		viewModel.ManageMods!.Back();
		Assert.That(viewModel.ManageMods, Is.Null);
		Assert.That(reloadCount, Is.EqualTo(1));
		Assert.That(viewModel.IsLoading, Is.True);
		Assert.That(viewModel.CanShowNewGame(), Is.False);

		viewModel.SetData(TestData.CreateGameInfo(), null, [new DataLoadError(@"C:\Data\Characters.json", "Broken", "with mods")], store);
		Assert.That(viewModel.IsLoading, Is.False);
		Assert.That(viewModel.LoadErrors, Is.EqualTo(new[] { "Characters.json (with mods): Broken" }));
		Assert.That(viewModel.CanShowNewGame(), Is.False);
		Assert.That(viewModel.CanShowManageMods(), Is.True);
	}

	private static StartUpWindowViewModel CreateViewModel() =>
		new(TestData.CreateGameInfo(), TestData.CreateGameData(), new Random(0), []);
}
