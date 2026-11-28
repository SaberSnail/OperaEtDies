using System.IO;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using OperaEtDies.Controls.Game;
using OperaEtDies.Controls.Loading;
using OperaEtDies.Controls.LoadGame;
using OperaEtDies.Controls.MainMenu;
using OperaEtDies.Controls.ManageMods;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Controls.Settings;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Mods;
using OperaEtDies.Settings;

namespace OperaEtDies.Windows.Main;

/// <summary>
/// The game's one window. It shows the game being played, if there is one, with a screen over it that takes over the
/// window: the loading screen while the game data loads, the main menu, or a screen opened from it (setting up a new game, loading a game, settings, or managing mods). The game
/// stays underneath while a screen is over it, so resuming returns to it exactly as it was. Leaving a game with changes
/// since it was last saved asks whether to save it first.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
	/// <summary>
	/// Creates the window, showing the loading screen until the data is shown (see <see cref="SetData"/>), when the main
	/// menu takes its place. Mods that are turned off, or replaced by mod folders chosen from the command line, are still
	/// listed, but aren't loaded. Without settings, the defaults are used, and they aren't kept. The title is the game's,
	/// if it's known before the rest of the data loads, or the engine's name.
	/// </summary>
	public MainWindowViewModel(Random random, IFolderPicker? folderPicker = null, bool areModsEnabled = true, bool isDeveloper = false, bool areModsOverridden = false, AppSettings? settings = null, string? title = null)
	{
		m_random = random;
		m_folderPicker = folderPicker ?? new FolderPicker();
		m_areModsEnabled = areModsEnabled;
		m_areModsOverridden = areModsOverridden;
		Settings = settings ?? new AppSettings();
		IsDeveloper = isDeveloper;
		m_title = title ?? OurResources.EngineName;
		MainMenu = new MainMenuViewModel(isDeveloper);
		MainMenu.Chosen += OnMainMenuChosen;
		m_loadingScreen = new LoadingScreenViewModel();
		m_screen = m_loadingScreen;
	}

	/// <summary>
	/// Raised when the application should exit, once any game has been saved or the player chose not to.
	/// </summary>
	public event EventHandler? ExitRequested;

	/// <summary>
	/// Raised when the game data should be loaded again, such as after the mod list changes. The data is shown again
	/// with <see cref="SetData"/>.
	/// </summary>
	public event EventHandler? ReloadDataRequested;

	/// <summary>
	/// Whether to show extra information and controls for developing the game.
	/// </summary>
	public bool IsDeveloper { get; }

	public string Title
	{
		get => VerifyAccess(m_title);
		private set => SetPropertyField(value, ref m_title);
	}

	public MainMenuViewModel MainMenu { get; }

	/// <summary>
	/// The player's settings, such as the text size and full screen.
	/// </summary>
	public AppSettings Settings { get; }

	/// <summary>
	/// Whether the game data is loading, so the loading screen is showing.
	/// </summary>
	public bool IsLoading => VerifyAccess(m_screen == m_loadingScreen);

	/// <summary>
	/// The game being played, or null if there isn't one.
	/// </summary>
	public GameScreenViewModel? Game
	{
		get => VerifyAccess(m_game);
		private set => SetPropertyField([nameof(Game), nameof(IsPlaying)], value, ref m_game);
	}

	/// <summary>
	/// The screen taking over the window, over any game: the loading screen, the main menu, setting up a new game, loading
	/// a game, or managing mods. Null while the game is showing.
	/// </summary>
	public ViewModelBase? Screen
	{
		get => VerifyAccess(m_screen);
		private set => SetPropertyField([nameof(Screen), nameof(IsPlaying), nameof(IsLoading)], value, ref m_screen);
	}

	/// <summary>
	/// Whether the game is showing, with no screen or question over it, so the toolbar can be used.
	/// </summary>
	public bool IsPlaying => VerifyAccess(m_game is not null && m_screen is null && m_savePrompt is null);

	/// <summary>
	/// The new game being set up, kept while the player is back at the main menu so they can return to it, or null.
	/// </summary>
	public NewGameViewModel? NewGame => VerifyAccess(m_newGame);

	/// <summary>
	/// The question of whether to save the game before leaving it, shown over everything, or null.
	/// </summary>
	public SavePromptViewModel? SavePrompt
	{
		get => VerifyAccess(m_savePrompt);
		private set => SetPropertyField([nameof(SavePrompt), nameof(IsPlaying)], value, ref m_savePrompt);
	}

	/// <summary>
	/// Shows the loaded game data, at start up or after <see cref="ReloadDataRequested"/>, replacing the loading screen
	/// with the main menu.
	/// </summary>
	public void SetData(GameInfo? gameInfo, GameData? gameData, IReadOnlyList<DataLoadError> loadErrors, IModListStore? modListStore, SaveSettings? saveSettings = null)
	{
		VerifyAccess();
		m_gameData = gameData;
		m_modListStore = modListStore;
		m_saveSettings = saveSettings;
		Title = gameInfo?.Title ?? OurResources.EngineName;
		MainMenu.SetData(gameInfo, gameData, loadErrors, modListStore is not null, saveSettings);
		if (m_screen == m_loadingScreen)
			Screen = MainMenu;

		// Commands only check whether they can run after input, so make sure they see the new data.
		System.Windows.Input.CommandManager.InvalidateRequerySuggested();
	}

	/// <summary>
	/// Shows the main menu over the game.
	/// </summary>
	public void ShowMainMenu()
	{
		VerifyAccess();
		if (SavePrompt is null)
		{
			if (m_game is { } game)
				game.TimeRunner.Stop();
			MainMenu.SaveStatus = null;
			Screen = MainMenu;
		}
	}

	/// <summary>
	/// Switches between full screen and a window, as F11 and Alt+Enter do anywhere.
	/// </summary>
	public void ToggleFullScreen()
	{
		VerifyAccess();
		Settings.IsFullScreen = !Settings.IsFullScreen;
	}

	/// <summary>
	/// Handles a hotkey. Full screen and Esc work everywhere. The rest only work during play, with no screen or question
	/// over the game: quick save and quick load here, and the others in the game. Returns whether the key did anything, or
	/// was used up where it can't.
	/// </summary>
	public bool HandleHotkey(Hotkey hotkey)
	{
		VerifyAccess();
		var isHandled = true;
		if (hotkey.Command == HotkeyCommand.FullScreen)
			ToggleFullScreen();
		else if (hotkey.Command == HotkeyCommand.Escape)
			Escape();
		else if (!IsPlaying)
			isHandled = false;
		else if (hotkey.Command == HotkeyCommand.QuickLoad)
			QuickLoad();
		else
			isHandled = m_game!.HandleHotkey(hotkey);
		return isHandled;
	}

	/// <summary>
	/// Loads the quick save in place of the game, without asking whether to save the game first, since going back is the
	/// point. A quick save made with different data or mods is only loaded once the player confirms it in the list of
	/// saved games. Time stops first if it's passing. If there's no quick save, or it can't be loaded, the center says so.
	/// </summary>
	public void QuickLoad()
	{
		VerifyAccess();
		if (m_game is { } game && m_saveSettings is { } settings && m_gameData is { } gameData)
		{
			game.WhenStopped(() =>
			{
				var filePath = SaveFile.GetQuickSavePath(settings.Directory);
				if (!File.Exists(filePath))
					game.ShowSaveStatus(OurResources.QuickLoadNone);
				else
					QuickLoad(game, filePath, settings, gameData);
			});
		}
	}

	private void QuickLoad(GameScreenViewModel game, string filePath, SaveSettings settings, GameData gameData)
	{
		try
		{
			var save = SaveFile.Read(filePath);
			if (save.IsDataChanged(settings))
			{
				ShowLoadGame(isQuickLoad: true);
				m_loadGame!.Load(filePath);
			}
			else
			{
				var world = save.Load(gameData, settings.GameTitle, m_random);
				Log.Info($"Quick loaded {filePath}");
				OpenLoadedGame(world);
			}
		}
		catch (SaveLoadException e)
		{
			Log.Error($"Couldn't quick load {filePath}: {e.Message}");
			game.ShowSaveStatus(OurResources.QuickLoadFailed.FormatCurrentCulture(e.Message));
		}
	}

	/// <summary>
	/// Handles Esc. In a game, it stops time, closes the sheet on top (or every sheet, pressed twice quickly), or closes the
	/// view in the center, or if there's nothing to do that for, opens the main menu. On the main menu, it resumes the
	/// game. On the loading screen, it does nothing. On another screen, it goes back. With the save prompt open, it
	/// cancels it.
	/// </summary>
	public void Escape()
	{
		VerifyAccess();
		if (m_savePrompt is { } savePrompt)
			savePrompt.Cancel();
		else if (m_screen is null)
		{
			if (m_game is { } game && !game.HandleEscape())
				ShowMainMenu();
		}
		else if (m_screen == MainMenu)
			MainMenu.Resume();
		else if (m_screen is NewGameViewModel newGame)
			newGame.Back();
		else if (m_screen is LoadGameViewModel loadGame)
		{
			if (loadGame.IsConfirming)
				loadGame.Cancel();
			else
				loadGame.Back();
		}
		else if (m_screen is ManageModsViewModel manageMods)
			manageMods.Back();
		else if (m_screen is SettingsViewModel settings)
			settings.Back();
	}

	/// <summary>
	/// Asks to close the window, which exits the game. Returns whether it can close now. If there's a game with changes
	/// since it was last saved, it can't, and asks whether to save first, exiting once it's answered.
	/// </summary>
	public bool RequestClose()
	{
		VerifyAccess();
		var canClose = m_isExiting;
		if (!canClose && SavePrompt is null)
		{
			if (m_game is { } game && game.HasUnsavedChanges())
			{
				game.TimeRunner.Stop();
				AskToSave(Exit);
			}
			else
			{
				m_isExiting = true;
				canClose = true;
			}
		}
		return canClose;
	}

	public void Dispose()
	{
		MainMenu.Chosen -= OnMainMenuChosen;
		CloseSavePrompt();
		CloseNewGame();
		CloseLoadGame();
		CloseManageMods();
		CloseSettings();
		CloseGame();
	}

	private void OnMainMenuChosen(object? sender, GenericEventArgs<MainMenuChoice> e)
	{
		switch (e.Value)
		{
		case MainMenuChoice.Resume:
			Screen = null;
			break;
		case MainMenuChoice.ContinueNewGame:
			if (m_newGame is not null)
				Screen = m_newGame;
			break;
		case MainMenuChoice.NewGame:
			ShowNewGame();
			break;
		case MainMenuChoice.SaveGame:
			if (m_game is { } game)
			{
				game.SaveGame();
				MainMenu.SaveStatus = game.SaveStatus;
			}
			break;
		case MainMenuChoice.LoadGame:
			ShowLoadGame();
			break;
		case MainMenuChoice.Settings:
			ShowSettings();
			break;
		case MainMenuChoice.ManageMods:
			ShowManageMods();
			break;
		case MainMenuChoice.QuitToMainMenu:
			LeaveGame(() => { });
			break;
		case MainMenuChoice.ExitGame:
			LeaveGame(Exit);
			break;
		}
	}

	/// <summary>
	/// Starts setting up a new game, replacing any that was being set up.
	/// </summary>
	private void ShowNewGame()
	{
		CloseNewGame();
		var newGame = new NewGameViewModel(m_gameData!, m_random, IsDeveloper);
		newGame.StartGameRequested += OnStartGameRequested;
		newGame.BackRequested += OnNewGameBackRequested;
		m_newGame = newGame;
		RaisePropertyChanged(nameof(NewGame));
		MainMenu.IsNewGameInProgress = true;
		Screen = newGame;
	}

	/// <summary>
	/// Shows the list of saved games. Opened for a quick load, the game it loads replaces this one without asking whether
	/// to save it first.
	/// </summary>
	private void ShowLoadGame(bool isQuickLoad = false)
	{
		CloseLoadGame();
		var loadGame = new LoadGameViewModel(m_gameData!, m_saveSettings!, m_random);
		loadGame.GameLoaded += OnGameLoaded;
		loadGame.BackRequested += OnLoadGameBackRequested;
		m_loadGame = loadGame;
		m_isQuickLoad = isQuickLoad;
		Screen = loadGame;
	}

	private void ShowManageMods()
	{
		var manageMods = new ManageModsViewModel(m_modListStore!, m_folderPicker, m_areModsEnabled, m_areModsOverridden);
		manageMods.BackRequested += OnManageModsBackRequested;
		m_manageMods = manageMods;
		Screen = manageMods;
	}

	private void ShowSettings()
	{
		var settings = new SettingsViewModel(Settings);
		settings.BackRequested += OnSettingsBackRequested;
		m_settings = settings;
		Screen = settings;
	}

	/// <summary>
	/// Shows a game, replacing any other, and closes the screens that led to it.
	/// </summary>
	private void OpenGame(GameWorld world)
	{
		CloseGame();
		CloseNewGame();
		CloseLoadGame();
		Game = new GameScreenViewModel(world, IsDeveloper, m_saveSettings, Settings);
		MainMenu.IsInGame = true;
		Screen = null;
	}

	/// <summary>
	/// Leaves the game, if there is one, asking whether to save it first if it has changed since it was last saved, then
	/// shows the main menu and does what comes next, unless the player chose to stay.
	/// </summary>
	private void LeaveGame(Action next)
	{
		if (m_game is { } game && game.HasUnsavedChanges())
		{
			AskToSave(() =>
			{
				QuitGame();
				next();
			});
		}
		else
		{
			QuitGame();
			next();
		}
	}

	private void QuitGame()
	{
		CloseGame();
		CloseLoadGame();
		Screen = MainMenu;
	}

	private void AskToSave(Action next)
	{
		var savePrompt = new SavePromptViewModel(m_game!);
		savePrompt.Answered += (_, e) =>
		{
			CloseSavePrompt();
			if (e.Value)
				next();
		};
		SavePrompt = savePrompt;
	}

	private void Exit()
	{
		m_isExiting = true;
		ExitRequested.Raise(this);
	}

	private void CloseGame()
	{
		var game = m_game;
		if (game is null)
			return;

		Game = null;
		MainMenu.IsInGame = false;
		game.Dispose();
	}

	private void CloseSavePrompt()
	{
		var savePrompt = m_savePrompt;
		if (savePrompt is null)
			return;

		SavePrompt = null;
		savePrompt.Dispose();
	}

	private void CloseNewGame()
	{
		var newGame = m_newGame;
		if (newGame is null)
			return;

		m_newGame = null;
		RaisePropertyChanged(nameof(NewGame));
		MainMenu.IsNewGameInProgress = false;
		newGame.StartGameRequested -= OnStartGameRequested;
		newGame.BackRequested -= OnNewGameBackRequested;
		if (m_screen == newGame)
			Screen = MainMenu;
	}

	private void CloseLoadGame()
	{
		var loadGame = m_loadGame;
		if (loadGame is null)
			return;

		m_loadGame = null;
		m_isQuickLoad = false;
		loadGame.GameLoaded -= OnGameLoaded;
		loadGame.BackRequested -= OnLoadGameBackRequested;
		if (m_screen == loadGame)
			Screen = MainMenu;
	}

	private void CloseManageMods()
	{
		var manageMods = m_manageMods;
		if (manageMods is null)
			return;

		m_manageMods = null;
		manageMods.BackRequested -= OnManageModsBackRequested;
		if (m_screen == manageMods)
			Screen = MainMenu;
	}

	private void CloseSettings()
	{
		var settings = m_settings;
		if (settings is null)
			return;

		m_settings = null;
		settings.BackRequested -= OnSettingsBackRequested;
		settings.Dispose();
		if (m_screen == settings)
			Screen = MainMenu;
	}

	private void OnSettingsBackRequested(object? sender, EventArgs e) => CloseSettings();

	private void OnStartGameRequested(object? sender, GenericEventArgs<NewGameSettings> e)
	{
		var gameData = m_gameData!;
		Log.Info($"Starting new game as {e.Value.Player.GetFullName(gameData.CharacterTemplates)}");
		OpenGame(GameWorld.CreateNewGame(gameData, e.Value.Player, m_random));
	}

	// Going back keeps the new game being set up, so the player can return to it.
	private void OnNewGameBackRequested(object? sender, EventArgs e) => Screen = MainMenu;

	// A game loaded during another replaces it, once it's saved or the player chose not to, unless it's a quick load.
	private void OnGameLoaded(object? sender, GenericEventArgs<GameWorld> e)
	{
		var world = e.Value;
		if (!m_isQuickLoad && m_game is { } game && game.HasUnsavedChanges())
			AskToSave(() => OpenLoadedGame(world));
		else
			OpenLoadedGame(world);
	}

	private void OpenLoadedGame(GameWorld world)
	{
		Log.Info($"Opening loaded game as {world.Player.FullName}");
		OpenGame(world);
	}

	private void OnLoadGameBackRequested(object? sender, EventArgs e) => CloseLoadGame();

	// Returning from the mod list loads the game data again, with the mods as they are now, showing the loading screen
	// until it's done.
	private void OnManageModsBackRequested(object? sender, EventArgs e)
	{
		CloseManageMods();
		CloseNewGame();
		Screen = m_loadingScreen;
		ReloadDataRequested.Raise(this);
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(MainWindowViewModel));

	private readonly Random m_random;
	private readonly IFolderPicker m_folderPicker;
	private readonly bool m_areModsEnabled;
	private readonly bool m_areModsOverridden;
	private readonly LoadingScreenViewModel m_loadingScreen;
	private GameData? m_gameData;
	private IModListStore? m_modListStore;
	private SaveSettings? m_saveSettings;
	private string m_title;
	private GameScreenViewModel? m_game;
	private ViewModelBase? m_screen;
	private NewGameViewModel? m_newGame;
	private LoadGameViewModel? m_loadGame;
	private ManageModsViewModel? m_manageMods;
	private SettingsViewModel? m_settings;
	private SavePromptViewModel? m_savePrompt;
	private bool m_isExiting;
	private bool m_isQuickLoad;
}
