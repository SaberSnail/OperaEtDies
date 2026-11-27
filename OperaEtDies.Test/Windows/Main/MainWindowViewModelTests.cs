using System.IO;
using OperaEtDies.Controls.LoadGame;
using OperaEtDies.Controls.ManageMods;
using OperaEtDies.Controls.Settings;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Settings;
using OperaEtDies.Test.Controls.ManageMods;
using OperaEtDies.Windows.Main;

namespace OperaEtDies.Test.Windows.Main;

[TestFixture]
public sealed class MainWindowViewModelTests
{
	[SetUp]
	public void SetUp()
	{
		m_folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		m_settings = new SaveSettings(TestData.CreateGameInfo().Title, "hash", "", [], m_folder);
	}

	[TearDown]
	public void TearDown()
	{
		if (Directory.Exists(m_folder))
			Directory.Delete(m_folder, recursive: true);
	}

	[Test]
	public void ShowsTheMainMenuWhileLoading()
	{
		using var viewModel = new MainWindowViewModel(new Random(0), settings: CreateAppSettings());

		Assert.That(viewModel.Screen, Is.SameAs(viewModel.MainMenu));
		Assert.That(viewModel.Game, Is.Null);
		Assert.That(viewModel.MainMenu.IsLoading, Is.True);
		Assert.That(viewModel.MainMenu.CanNewGame(), Is.False);

		viewModel.SetData(TestData.CreateGameInfo(), TestData.CreateGameData(), [], null);
		Assert.That(viewModel.MainMenu.IsLoading, Is.False);
		Assert.That(viewModel.Title, Is.EqualTo("Title"));
		Assert.That(viewModel.MainMenu.Title, Is.EqualTo("Title"));
		Assert.That(viewModel.MainMenu.CanNewGame(), Is.True);
	}

	[Test]
	public void MissingGameInfoShowsErrorsAndBlocksNewGame()
	{
		using var viewModel = new MainWindowViewModel(new Random(0), settings: CreateAppSettings());
		viewModel.SetData(null, TestData.CreateGameData(), [new DataLoadError(@"C:\Data\GameInfo.json", "Broken")], null);

		Assert.That(viewModel.Title, Is.EqualTo(OurResources.EngineName));
		Assert.That(viewModel.MainMenu.LoadErrors, Is.EqualTo(new[] { "GameInfo.json: Broken" }));
		Assert.That(viewModel.MainMenu.CanNewGame(), Is.False);

		viewModel.MainMenu.NewGame();
		Assert.That(viewModel.NewGame, Is.Null);
		Assert.That(viewModel.Screen, Is.SameAs(viewModel.MainMenu));
	}

	[Test]
	public void MissingGameDataBlocksNewGame()
	{
		using var viewModel = new MainWindowViewModel(new Random(0), settings: CreateAppSettings());
		viewModel.SetData(TestData.CreateGameInfo(), null, [new DataLoadError(@"C:\Data\Professions.json", "Broken")], null);

		Assert.That(viewModel.MainMenu.Title, Is.EqualTo("Title"));
		Assert.That(viewModel.MainMenu.CanNewGame(), Is.False);
	}

	[Test]
	public void LoadGameNeedsDataAndSaveSettings()
	{
		using var withoutSettings = CreateViewModel(saveSettings: null);
		using var withoutData = new MainWindowViewModel(new Random(0), settings: CreateAppSettings());
		withoutData.SetData(TestData.CreateGameInfo(), null, [], null, m_settings);
		using var viewModel = CreateViewModel();

		Assert.That(withoutSettings.MainMenu.HasHashes, Is.False);
		Assert.That(viewModel.MainMenu.HasHashes, Is.True);
		Assert.That(viewModel.MainMenu.DataHash, Is.EqualTo("hash"));
		Assert.That(viewModel.MainMenu.DataHashText, Does.Contain("hash"));
		Assert.That(viewModel.MainMenu.HasModsHash, Is.False);

		using var withMods = new MainWindowViewModel(new Random(0), settings: CreateAppSettings());
		withMods.SetData(TestData.CreateGameInfo(), TestData.CreateGameData(), [], null, m_settings with { DataHash = "0123456789ABCDEF", ModsHash = "FEDCBA9876543210" });
		Assert.That(withMods.MainMenu.DataHashText, Does.Contain("0123456789AB").And.Not.Contain("CDEF"));
		Assert.That(withMods.MainMenu.ModsHashText, Does.Contain("FEDCBA987654").And.Not.Contain("3210"));
		Assert.That(withMods.MainMenu.ModsHash, Is.EqualTo("FEDCBA9876543210"));
		Assert.That(withMods.MainMenu.HasModsHash, Is.True);

		Assert.That(withoutSettings.MainMenu.CanLoadGame(), Is.False);
		Assert.That(withoutData.MainMenu.CanLoadGame(), Is.False);
		Assert.That(viewModel.MainMenu.CanLoadGame(), Is.True);

		viewModel.MainMenu.LoadGame();
		Assert.That(viewModel.Screen, Is.InstanceOf<LoadGameViewModel>());

		((LoadGameViewModel) viewModel.Screen!).Back();
		Assert.That(viewModel.Screen, Is.SameAs(viewModel.MainMenu));
	}

	[Test]
	public void BackFromNewGameKeepsItToContinue()
	{
		using var viewModel = CreateViewModel();
		Assert.That(viewModel.MainMenu.CanContinueNewGame(), Is.False);

		viewModel.MainMenu.NewGame();
		var newGame = viewModel.NewGame;
		Assert.That(viewModel.Screen, Is.SameAs(newGame));

		newGame!.Back();
		Assert.That(viewModel.Screen, Is.SameAs(viewModel.MainMenu));
		Assert.That(viewModel.NewGame, Is.SameAs(newGame));
		Assert.That(viewModel.MainMenu.CanContinueNewGame(), Is.True);

		viewModel.MainMenu.ContinueNewGame();
		Assert.That(viewModel.Screen, Is.SameAs(newGame));

		// Starting a new game again starts over.
		viewModel.Escape();
		viewModel.MainMenu.NewGame();
		Assert.That(viewModel.NewGame, Is.Not.SameAs(newGame));
		Assert.That(viewModel.Screen, Is.SameAs(viewModel.NewGame));
	}

	[Test]
	public void StartingAGameShowsIt()
	{
		using var viewModel = CreateViewModel();
		Assert.That(viewModel.IsPlaying, Is.False);

		StartGame(viewModel);

		Assert.That(viewModel.IsPlaying, Is.True);
		Assert.That(viewModel.Game, Is.Not.Null);
		Assert.That(viewModel.Screen, Is.Null);
		Assert.That(viewModel.NewGame, Is.Null);
		Assert.That(viewModel.MainMenu.IsInGame, Is.True);
		Assert.That(viewModel.MainMenu.IsNewGameInProgress, Is.False);
		Assert.That(viewModel.MainMenu.CanNewGame(), Is.False);
		Assert.That(viewModel.MainMenu.CanResume(), Is.True);
	}

	[Test]
	public void EscapeOpensTheMenuAndResumeReturnsToTheSameGame()
	{
		using var viewModel = CreateViewModel();
		StartGame(viewModel);
		var game = viewModel.Game;
		game!.ShowVillagers();
		game.EscapeRepeatTime = TimeSpan.FromMinutes(1);

		// The first Esc closes the sheet on top, and a second one straight after it doesn't go on to the main menu.
		viewModel.Escape();
		Assert.That(game.HasSheets, Is.False);
		Assert.That(viewModel.Screen, Is.Null);
		viewModel.Escape();
		Assert.That(viewModel.Screen, Is.Null);

		viewModel.Escape();
		Assert.That(viewModel.Screen, Is.SameAs(viewModel.MainMenu));
		Assert.That(viewModel.Game, Is.SameAs(game));
		Assert.That(viewModel.IsPlaying, Is.False);

		viewModel.Escape();
		Assert.That(viewModel.Screen, Is.Null);

		viewModel.Escape();
		viewModel.MainMenu.Resume();
		Assert.That(viewModel.Screen, Is.Null);
		Assert.That(viewModel.Game, Is.SameAs(game));
	}

	[Test]
	public void QuittingAnUnchangedGameDoesNotAsk()
	{
		using var viewModel = CreateViewModel();
		StartGame(viewModel);

		Assert.That(viewModel.Game!.HasUnsavedChanges(), Is.False);
		OpenMenu(viewModel);
		viewModel.MainMenu.QuitToMainMenu();

		Assert.That(viewModel.SavePrompt, Is.Null);
		Assert.That(viewModel.Game, Is.Null);
		Assert.That(viewModel.Screen, Is.SameAs(viewModel.MainMenu));
		Assert.That(viewModel.MainMenu.IsInGame, Is.False);
	}

	[Test]
	public void QuittingAChangedGameAsksToSave()
	{
		using var viewModel = CreateViewModel();
		StartGame(viewModel);
		var game = viewModel.Game!;
		ChangeGame(viewModel);
		Assert.That(game.HasUnsavedChanges(), Is.True);

		// Cancelling stays in the game.
		OpenMenu(viewModel);
		viewModel.MainMenu.QuitToMainMenu();
		Assert.That(viewModel.SavePrompt, Is.Not.Null);
		Assert.That(viewModel.IsPlaying, Is.False);
		viewModel.Escape();
		Assert.That(viewModel.SavePrompt, Is.Null);
		Assert.That(viewModel.Game, Is.SameAs(game));

		// Saving saves, then quits.
		viewModel.MainMenu.QuitToMainMenu();
		viewModel.SavePrompt!.Save();
		Assert.That(viewModel.SavePrompt, Is.Null);
		Assert.That(viewModel.Game, Is.Null);
		Assert.That(Directory.GetFiles(m_folder), Has.Length.EqualTo(1));
	}

	[Test]
	public void SavingFromTheMenuStopsTheQuestion()
	{
		using var viewModel = CreateViewModel();
		StartGame(viewModel);
		ChangeGame(viewModel);

		OpenMenu(viewModel);
		viewModel.MainMenu.SaveGame();
		Assert.That(viewModel.MainMenu.SaveStatus, Is.EqualTo(OurResources.GameSaved));
		Assert.That(viewModel.Game!.HasUnsavedChanges(), Is.False);

		viewModel.MainMenu.QuitToMainMenu();
		Assert.That(viewModel.SavePrompt, Is.Null);
		Assert.That(viewModel.Game, Is.Null);
	}

	[Test]
	public void ClosingWithAChangedGameAsksToSaveThenExits()
	{
		using var viewModel = CreateViewModel();
		var exitCount = 0;
		viewModel.ExitRequested += (_, _) => exitCount++;
		StartGame(viewModel);
		ChangeGame(viewModel);

		Assert.That(viewModel.RequestClose(), Is.False);
		Assert.That(viewModel.SavePrompt, Is.Not.Null);

		viewModel.SavePrompt!.DontSave();
		Assert.That(exitCount, Is.EqualTo(1));
		Assert.That(viewModel.RequestClose(), Is.True);
		Assert.That(Directory.Exists(m_folder), Is.False);
	}

	[Test]
	public void ClosingWithoutAGameDoesNotAsk()
	{
		using var viewModel = CreateViewModel();

		Assert.That(viewModel.RequestClose(), Is.True);
		Assert.That(viewModel.SavePrompt, Is.Null);
	}

	[Test]
	public void ExitingFromTheMenuRaisesExit()
	{
		using var viewModel = CreateViewModel();
		var exitCount = 0;
		viewModel.ExitRequested += (_, _) => exitCount++;

		viewModel.MainMenu.ExitGame();

		Assert.That(exitCount, Is.EqualTo(1));
	}

	[Test]
	public void LoadingAGameDuringAnotherReplacesIt()
	{
		using var viewModel = CreateViewModel();
		StartGame(viewModel);
		var game = viewModel.Game!;
		var save = SaveFile.Create(game.World, m_settings, DateTimeOffset.Now);
		save.Write(Path.Combine(m_folder, save.GetFileName()));
		ChangeGame(viewModel);

		OpenMenu(viewModel);
		viewModel.MainMenu.LoadGame();
		var loadGame = (LoadGameViewModel) viewModel.Screen!;
		loadGame.Saves.Single().Load();

		// The changed game is kept until the player answers.
		Assert.That(viewModel.SavePrompt, Is.Not.Null);
		Assert.That(viewModel.Game, Is.SameAs(game));

		viewModel.SavePrompt!.DontSave();
		Assert.That(viewModel.Game, Is.Not.Null.And.Not.SameAs(game));
		Assert.That(viewModel.Screen, Is.Null);
	}

	[Test]
	public void ManageModsNeedsModListStore()
	{
		using var viewModel = CreateViewModel();

		Assert.That(viewModel.MainMenu.CanManageMods(), Is.False);
		viewModel.MainMenu.ManageMods();
		Assert.That(viewModel.Screen, Is.SameAs(viewModel.MainMenu));
	}

	[Test]
	public void ReturningFromManageModsReloadsData()
	{
		var store = new ManageModsViewModelTests.FakeModListStore();
		using var viewModel = new MainWindowViewModel(new Random(0), new ManageModsViewModelTests.FakeFolderPicker(), settings: CreateAppSettings());
		viewModel.SetData(TestData.CreateGameInfo(), TestData.CreateGameData(), [], store);
		var reloadCount = 0;
		viewModel.ReloadDataRequested += (_, _) => reloadCount++;
		viewModel.MainMenu.NewGame();
		viewModel.NewGame!.Back();

		viewModel.MainMenu.ManageMods();
		Assert.That(viewModel.Screen, Is.InstanceOf<ManageModsViewModel>());

		viewModel.Escape();
		Assert.That(viewModel.Screen, Is.SameAs(viewModel.MainMenu));
		Assert.That(reloadCount, Is.EqualTo(1));
		Assert.That(viewModel.MainMenu.IsLoading, Is.True);
		Assert.That(viewModel.MainMenu.CanNewGame(), Is.False);

		// The new game being set up used the old data, so it's gone.
		Assert.That(viewModel.NewGame, Is.Null);

		viewModel.SetData(TestData.CreateGameInfo(), null, [new DataLoadError(@"C:\Data\Characters.json", "Broken", "with mods")], store);
		Assert.That(viewModel.MainMenu.IsLoading, Is.False);
		Assert.That(viewModel.MainMenu.LoadErrors, Is.EqualTo(new[] { "Characters.json (with mods): Broken" }));
		Assert.That(viewModel.MainMenu.CanNewGame(), Is.False);
		Assert.That(viewModel.MainMenu.CanManageMods(), Is.True);
	}

	[Test]
	public void ManagingModsDuringAGameQuitsIt()
	{
		var store = new ManageModsViewModelTests.FakeModListStore();
		using var viewModel = new MainWindowViewModel(new Random(0), new ManageModsViewModelTests.FakeFolderPicker(), settings: CreateAppSettings());
		viewModel.SetData(TestData.CreateGameInfo(), TestData.CreateGameData(), [], store, m_settings);
		StartGame(viewModel);

		OpenMenu(viewModel);
		viewModel.MainMenu.ManageMods();

		Assert.That(viewModel.Game, Is.Null);
		Assert.That(viewModel.Screen, Is.InstanceOf<ManageModsViewModel>());
	}

	[Test]
	public void SettingsOpenFromTheMainMenuBeforeAGame()
	{
		using var viewModel = CreateViewModel();

		viewModel.MainMenu.OpenSettings();
		Assert.That(viewModel.Screen, Is.InstanceOf<SettingsViewModel>());

		viewModel.Escape();
		Assert.That(viewModel.Screen, Is.SameAs(viewModel.MainMenu));

		viewModel.MainMenu.OpenSettings();
		((SettingsViewModel) viewModel.Screen!).Back();
		Assert.That(viewModel.Screen, Is.SameAs(viewModel.MainMenu));
	}

	[Test]
	public void SettingsOpenFromTheMainMenuDuringAGameAndKeepIt()
	{
		using var viewModel = CreateViewModel();
		StartGame(viewModel);
		var game = viewModel.Game;
		OpenMenu(viewModel);

		viewModel.MainMenu.OpenSettings();
		var settings = (SettingsViewModel) viewModel.Screen!;
		settings.TimeSpeeds[0].IsSelected = true;
		Assert.That(viewModel.Settings.TimeSpeed, Is.EqualTo(TimeSpeed.Slow));
		Assert.That(viewModel.Game, Is.SameAs(game));

		settings.Back();
		Assert.That(viewModel.Screen, Is.SameAs(viewModel.MainMenu));
		viewModel.MainMenu.Resume();
		Assert.That(viewModel.Screen, Is.Null);
		Assert.That(viewModel.Game, Is.SameAs(game));
	}

	[Test]
	public void ToggleFullScreenSwitchesTheSetting()
	{
		using var viewModel = CreateViewModel();

		viewModel.ToggleFullScreen();
		Assert.That(viewModel.Settings.IsFullScreen, Is.True);

		viewModel.ToggleFullScreen();
		Assert.That(viewModel.Settings.IsFullScreen, Is.False);
	}

	[Test]
	public void WithoutSettingsTheDefaultsAreUsed()
	{
		using var viewModel = new MainWindowViewModel(new Random(0));

		Assert.That(viewModel.Settings.TextSize, Is.EqualTo(AppSettings.DefaultTextSize));
		Assert.That(viewModel.Settings.TimeSpeed, Is.EqualTo(TimeSpeed.Normal));
	}

	[Test]
	public void GameHotkeysOnlyWorkDuringPlay()
	{
		using var viewModel = CreateViewModel();

		// Before a game, and on the screens over one, only Esc and full screen do anything.
		Assert.That(viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Villagers)), Is.False);
		Assert.That(viewModel.HandleHotkey(new Hotkey(HotkeyCommand.QuickLoad)), Is.False);
		Assert.That(viewModel.HandleHotkey(new Hotkey(HotkeyCommand.FullScreen)), Is.True);
		Assert.That(viewModel.Settings.IsFullScreen, Is.True);
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.FullScreen));

		StartGame(viewModel);
		var game = viewModel.Game!;
		Assert.That(viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Villagers)), Is.True);
		Assert.That(game.IsVillagersOpen, Is.True);

		Assert.That(viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Escape)), Is.True);
		Assert.That(game.HasSheets, Is.False);
		viewModel.ShowMainMenu();
		Assert.That(viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Villagers)), Is.False);
		Assert.That(game.HasSheets, Is.False);
	}

	[Test]
	public void QuickSaveKeepsOneFileAndQuickLoadGoesBackWithoutAsking()
	{
		using var viewModel = CreateViewModel();
		StartGame(viewModel);

		// With no quick save, quick load says so.
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.QuickLoad));
		Assert.That(viewModel.Game!.SaveStatus, Is.EqualTo(OurResources.QuickLoadNone));

		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.QuickSave));
		var quickSave = SaveFile.GetQuickSavePath(m_folder);
		Assert.That(viewModel.Game.SaveStatus, Is.EqualTo(OurResources.QuickSaved));
		Assert.That(Directory.GetFiles(m_folder), Is.EqualTo(new[] { quickSave }));
		var location = viewModel.Game.World.Player.Location;

		// A second quick save replaces the first.
		ChangeGame(viewModel);
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.QuickSave));
		Assert.That(Directory.GetFiles(m_folder), Is.EqualTo(new[] { quickSave }));
		var saved = viewModel.Game.World.Player.Location;
		Assert.That(saved, Is.Not.SameAs(location));

		// Quick loading a changed game goes straight back, without asking whether to save it first.
		ChangeGame(viewModel);
		var game = viewModel.Game;
		Assert.That(game.HasUnsavedChanges(), Is.True);
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.QuickLoad));
		Assert.That(viewModel.SavePrompt, Is.Null);
		Assert.That(viewModel.Game, Is.Not.SameAs(game));
		Assert.That(viewModel.Game!.World.Player.Location.Id, Is.EqualTo(saved.Id));
		Assert.That(viewModel.Screen, Is.Null);

		// The quick save is marked in the list of saved games.
		var loadGame = new LoadGameViewModel(TestData.CreateGameData(), m_settings, new Random(0));
		Assert.That(loadGame.Saves.Single().Title, Does.StartWith("Quick save: "));
	}

	[Test]
	public void QuickLoadingWithChangedDataAsksInTheListOfSaves()
	{
		using var viewModel = CreateViewModel();
		StartGame(viewModel);
		viewModel.Game!.QuickSave();

		using var changed = CreateViewModel(m_settings with { DataHash = "other" });
		StartGame(changed);
		changed.QuickLoad();
		var loadGame = (LoadGameViewModel) changed.Screen!;
		Assert.That(loadGame.ConfirmingLoad?.FilePath, Is.EqualTo(SaveFile.GetQuickSavePath(m_folder)));

		// Confirming loads it in place of the game, without asking whether to save it.
		var game = changed.Game;
		ChangeGame(changed);
		loadGame.ConfirmLoad();
		Assert.That(changed.SavePrompt, Is.Null);
		Assert.That(changed.Game, Is.Not.SameAs(game));
		Assert.That(changed.Screen, Is.Null);
	}

	private MainWindowViewModel CreateViewModel() => CreateViewModel(m_settings);

	// Time passes at once, so games in tests don't wait on the clock.
	private static AppSettings CreateAppSettings() => new() { TimeSpeed = TimeSpeed.Instant };

	private static MainWindowViewModel CreateViewModel(SaveSettings? saveSettings)
	{
		var viewModel = new MainWindowViewModel(new Random(0), settings: CreateAppSettings());
		viewModel.SetData(TestData.CreateGameInfo(), TestData.CreateGameData(), [], null, saveSettings);
		return viewModel;
	}

	private static void StartGame(MainWindowViewModel viewModel)
	{
		viewModel.MainMenu.NewGame();
		viewModel.NewGame!.StartGame();
	}

	private static void OpenMenu(MainWindowViewModel viewModel)
	{
		viewModel.Escape();
		Assert.That(viewModel.Screen, Is.SameAs(viewModel.MainMenu));
	}

	// Walking somewhere passes time, which changes the game.
	private static void ChangeGame(MainWindowViewModel viewModel)
	{
		var game = viewModel.Game!;
		game.TravelTo(game.World.Player.Home.Connections[0]);
	}

	private string m_folder = "";
	private SaveSettings m_settings = null!;
}
