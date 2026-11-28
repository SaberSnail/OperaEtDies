using GoldenAnvil.Utility;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.MainMenu;

/// <summary>
/// The main menu, which takes over the window: the game's title and description, any errors loading the data, and the
/// choices. Before a game it offers a new game (or going back to the one being set up), loading a game, settings,
/// managing mods, and exiting. During a game it offers resuming, saving, loading another game, settings, quitting to
/// manage mods, quitting to the main menu, and exiting. The window carries out the choices.
/// </summary>
public sealed class MainMenuViewModel : ViewModelBase
{
	public MainMenuViewModel(bool isDeveloper)
	{
		IsDeveloper = isDeveloper;
		m_title = OurResources.EngineName;
		m_description = "";
		m_loadErrors = [];
	}

	/// <summary>
	/// Raised when the player chooses something from the menu.
	/// </summary>
	public event EventHandler<GenericEventArgs<MainMenuChoice>>? Chosen;

	/// <summary>
	/// Whether the game was started in developer mode, which shows extra information and controls.
	/// </summary>
	public bool IsDeveloper { get; }

	public string Title
	{
		get => VerifyAccess(m_title);
		private set => SetPropertyField(value, ref m_title);
	}

	public string Description
	{
		get => VerifyAccess(m_description);
		private set => SetPropertyField(value, ref m_description);
	}

	public IReadOnlyList<string> LoadErrors
	{
		get => VerifyAccess(m_loadErrors);
		private set => SetPropertyField([nameof(LoadErrors), nameof(HasLoadErrors)], value, ref m_loadErrors);
	}

	public bool HasLoadErrors => LoadErrors.Count != 0;

	/// <summary>
	/// Whether a game is being played, which changes the choices.
	/// </summary>
	public bool IsInGame
	{
		get => VerifyAccess(m_isInGame);
		set
		{
			if (SetPropertyField(value, ref m_isInGame))
				SaveStatus = null;
		}
	}

	/// <summary>
	/// Whether a new game is being set up, which the player can go back to.
	/// </summary>
	public bool IsNewGameInProgress
	{
		get => VerifyAccess(m_isNewGameInProgress);
		set => SetPropertyField(value, ref m_isNewGameInProgress);
	}

	/// <summary>
	/// Whether the last save from the menu worked, or null if the game hasn't been saved from it.
	/// </summary>
	public string? SaveStatus
	{
		get => VerifyAccess(m_saveStatus);
		set => SetPropertyField(value, ref m_saveStatus);
	}

	/// <summary>
	/// Whether the hashes of the data and mods are known, which they are once the game info has loaded.
	/// </summary>
	public bool HasHashes => m_saveSettings is not null;

	/// <summary>
	/// The hash of the data files, saved with games to tell whether the data has changed.
	/// </summary>
	public string DataHash => m_saveSettings?.DataHash ?? "";

	/// <summary>
	/// The start of the data hash, for showing in small text.
	/// </summary>
	public string DataHashText => OurResources.DataHashLabel.FormatCurrentCulture(ShortenHash(DataHash));

	/// <summary>
	/// The hash of the mod files, saved with games to tell whether the mods have changed. Empty without mods.
	/// </summary>
	public string ModsHash => m_saveSettings?.ModsHash ?? "";

	/// <summary>
	/// The start of the mods hash, for showing in small text.
	/// </summary>
	public string ModsHashText => OurResources.ModsHashLabel.FormatCurrentCulture(ShortenHash(ModsHash));

	/// <summary>
	/// Whether any mods are loaded, so there's a mods hash to show.
	/// </summary>
	public bool HasModsHash => ModsHash.Length != 0;

	/// <summary>
	/// Shows the loaded game data.
	/// </summary>
	public void SetData(GameInfo? gameInfo, GameData? gameData, IReadOnlyList<DataLoadError> loadErrors, bool canManageMods, SaveSettings? saveSettings)
	{
		VerifyAccess();
		using (ScopedPropertyChange(nameof(DataHash), nameof(DataHashText), nameof(ModsHash), nameof(ModsHashText), nameof(HasModsHash), nameof(HasHashes)))
			m_saveSettings = saveSettings;
		m_hasGameInfo = gameInfo is not null;
		m_hasGameData = gameData is not null;
		m_canManageMods = canManageMods;
		Title = gameInfo?.Title ?? OurResources.EngineName;
		Description = gameInfo?.Description.Text ?? "";
		LoadErrors = loadErrors.Select(x => x.ToString()).ToList();
	}

	public bool CanResume() => IsInGame;

	public void Resume() => Choose(MainMenuChoice.Resume, CanResume());

	public bool CanContinueNewGame() => !IsInGame && IsNewGameInProgress;

	public void ContinueNewGame() => Choose(MainMenuChoice.ContinueNewGame, CanContinueNewGame());

	// A new game can't be started without the game data.
	public bool CanNewGame() => !IsInGame && m_hasGameInfo && m_hasGameData;

	public void NewGame() => Choose(MainMenuChoice.NewGame, CanNewGame());

	// Games can't be saved without knowing where saves are kept.
	public bool CanSaveGame() => IsInGame && m_saveSettings is not null;

	public void SaveGame() => Choose(MainMenuChoice.SaveGame, CanSaveGame());

	// Games can't be loaded without the game data, or without knowing where saves are kept.
	public bool CanLoadGame() => m_hasGameData && m_saveSettings is not null;

	public void LoadGame() => Choose(MainMenuChoice.LoadGame, CanLoadGame());

	// The settings don't depend on the data, so they're always there.
	public void OpenSettings() => Choose(MainMenuChoice.Settings, true);

	// The mod list is kept under the game's title, so it can't be managed without the game info. Mods are only managed
	// outside a game, since changing them loads the data again, which a game can't survive.
	public bool CanManageMods() => !IsInGame && m_canManageMods;

	public void ManageMods() => Choose(MainMenuChoice.ManageMods, CanManageMods());

	public bool CanQuitToMainMenu() => IsInGame;

	public void QuitToMainMenu() => Choose(MainMenuChoice.QuitToMainMenu, CanQuitToMainMenu());

	public void ExitGame() => Choose(MainMenuChoice.ExitGame, true);

	private void Choose(MainMenuChoice choice, bool canChoose)
	{
		VerifyAccess();
		if (canChoose)
			Chosen.Raise(this, new GenericEventArgs<MainMenuChoice>(choice));
	}

	private static string ShortenHash(string hash) => hash.Length > ShortHashLength ? hash[..ShortHashLength] : hash;

	private const int ShortHashLength = 12;

	private SaveSettings? m_saveSettings;
	private bool m_hasGameInfo;
	private bool m_hasGameData;
	private bool m_canManageMods;
	private string m_title;
	private string m_description;
	private IReadOnlyList<string> m_loadErrors;
	private bool m_isInGame;
	private bool m_isNewGameInProgress;
	private string? m_saveStatus;
}

/// <summary>
/// Something the player can choose from the main menu.
/// </summary>
public enum MainMenuChoice
{
	Resume,
	ContinueNewGame,
	NewGame,
	SaveGame,
	LoadGame,
	Settings,
	ManageMods,
	QuitToMainMenu,
	ExitGame,
}
