using System.Windows.Input;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using OperaEtDies.Controls.LoadGame;
using OperaEtDies.Controls.ManageMods;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Mods;

namespace OperaEtDies.Windows.StartUp;

public sealed class StartUpWindowViewModel : ViewModelBase, IDisposable
{
	/// <summary>
	/// Creates the start up window. The mod list can only be managed if a store for it is given, which needs the game
	/// info. Mods that are turned off are still listed, but aren't loaded.
	/// </summary>
	public StartUpWindowViewModel(GameInfo? gameInfo, GameData? gameData, Random random, IReadOnlyList<DataLoadError> loadErrors, IModListStore? modListStore = null, IFolderPicker? folderPicker = null, bool areModsEnabled = true, bool isDeveloper = false, SaveSettings? saveSettings = null)
	{
		IsDeveloper = isDeveloper;
		m_taskGroup = new TaskGroup();
		m_random = random;
		m_folderPicker = folderPicker ?? new FolderPicker();
		m_areModsEnabled = areModsEnabled;
		m_title = "";
		m_description = "";
		m_loadErrors = [];
		ApplyData(gameInfo, gameData, loadErrors, modListStore, saveSettings);
	}

	/// <summary>
	/// Whether the game was started in developer mode, which shows extra information and controls.
	/// </summary>
	public bool IsDeveloper { get; }

	public event EventHandler<GenericEventArgs<NewGameSettings>>? StartNewGameRequested;

	/// <summary>
	/// Raised when a saved game has been loaded, with its world.
	/// </summary>
	public event EventHandler<GenericEventArgs<GameWorld>>? GameLoaded;

	public event EventHandler? ExitGameRequested;

	/// <summary>
	/// Raised when the game data should be loaded again, such as after the mod list changes. The data is shown again
	/// with <see cref="SetData"/>.
	/// </summary>
	public event EventHandler? ReloadDataRequested;

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
		private set
		{
			if (SetPropertyField(value, ref m_loadErrors))
				RaisePropertyChanged(nameof(HasLoadErrors));
		}
	}

	public bool HasLoadErrors => LoadErrors.Count != 0;

	/// <summary>
	/// True while the game data is being loaded again.
	/// </summary>
	public bool IsLoading
	{
		get => VerifyAccess(m_isLoading);
		private set => SetPropertyField(value, ref m_isLoading);
	}

	/// <summary>
	/// The new game configuration being shown in place of the start up menu, or null if it isn't showing.
	/// </summary>
	public NewGameViewModel? NewGame
	{
		get => VerifyAccess(m_newGame);
		private set
		{
			if (SetPropertyField(value, ref m_newGame))
				RaisePropertyChanged(nameof(IsMenuVisible));
		}
	}

	/// <summary>
	/// The mod list being shown in place of the start up menu, or null if it isn't showing.
	/// </summary>
	public ManageModsViewModel? ManageMods
	{
		get => VerifyAccess(m_manageMods);
		private set
		{
			if (SetPropertyField(value, ref m_manageMods))
				RaisePropertyChanged(nameof(IsMenuVisible));
		}
	}

	/// <summary>
	/// The list of saved games being shown in place of the start up menu, or null if it isn't showing.
	/// </summary>
	public LoadGameViewModel? LoadGame
	{
		get => VerifyAccess(m_loadGame);
		private set
		{
			if (SetPropertyField(value, ref m_loadGame))
				RaisePropertyChanged(nameof(IsMenuVisible));
		}
	}

	/// <summary>
	/// Whether the start up menu is showing, rather than another screen.
	/// </summary>
	public bool IsMenuVisible => NewGame is null && ManageMods is null && LoadGame is null;

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
	/// Shows the newly loaded game data, after <see cref="ReloadDataRequested"/>.
	/// </summary>
	public void SetData(GameInfo? gameInfo, GameData? gameData, IReadOnlyList<DataLoadError> loadErrors, IModListStore? modListStore, SaveSettings? saveSettings = null)
	{
		VerifyAccess();
		ApplyData(gameInfo, gameData, loadErrors, modListStore, saveSettings);
		IsLoading = false;

		// Commands only check whether they can run after input, so make sure they see the new data.
		CommandManager.InvalidateRequerySuggested();
	}

	// A new game can't be started without the game data.
	public bool CanShowNewGame() => m_gameInfo is not null && m_gameData is not null && !m_isLoading;

	public void ShowNewGame()
	{
		VerifyAccess();
		if (!CanShowNewGame() || !IsMenuVisible)
			return;

		var newGame = new NewGameViewModel(m_gameData!, m_random, IsDeveloper);
		newGame.StartGameRequested += OnStartGameRequested;
		newGame.BackRequested += OnNewGameBackRequested;
		NewGame = newGame;
	}

	// Games can't be loaded without the game data, or without knowing where saves are kept.
	public bool CanShowLoadGame() => m_gameData is not null && m_saveSettings is not null && !m_isLoading;

	public void ShowLoadGame()
	{
		VerifyAccess();
		if (!CanShowLoadGame() || !IsMenuVisible)
			return;

		var loadGame = new LoadGameViewModel(m_gameData!, m_saveSettings!, m_random);
		loadGame.GameLoaded += OnGameLoaded;
		loadGame.BackRequested += OnLoadGameBackRequested;
		LoadGame = loadGame;
	}

	// The mod list is kept under the game's title, so it can't be managed without the game info.
	public bool CanShowManageMods() => m_modListStore is not null && !m_isLoading;

	public void ShowManageMods()
	{
		VerifyAccess();
		if (!CanShowManageMods() || !IsMenuVisible)
			return;

		var manageMods = new ManageModsViewModel(m_modListStore!, m_folderPicker, m_areModsEnabled);
		manageMods.BackRequested += OnManageModsBackRequested;
		ManageMods = manageMods;
	}

	public void ExitGame()
	{
		VerifyAccess();
		ExitGameRequested.Raise(this);
	}

	public void Dispose()
	{
		CloseNewGame();
		CloseManageMods();
		CloseLoadGame();
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	private void ApplyData(GameInfo? gameInfo, GameData? gameData, IReadOnlyList<DataLoadError> loadErrors, IModListStore? modListStore, SaveSettings? saveSettings)
	{
		using (ScopedPropertyChange(nameof(DataHash), nameof(DataHashText), nameof(ModsHash), nameof(ModsHashText), nameof(HasModsHash), nameof(HasHashes)))
			m_saveSettings = saveSettings;
		m_gameInfo = gameInfo;
		m_gameData = gameData;
		m_modListStore = modListStore;
		Title = gameInfo?.Title ?? OurResources.EngineName;
		Description = gameInfo?.Description ?? "";
		LoadErrors = loadErrors.Select(x => x.ToString()).ToList();
	}

	private void CloseNewGame()
	{
		var newGame = m_newGame;
		if (newGame is null)
			return;

		NewGame = null;
		newGame.StartGameRequested -= OnStartGameRequested;
		newGame.BackRequested -= OnNewGameBackRequested;
	}

	private void CloseLoadGame()
	{
		var loadGame = m_loadGame;
		if (loadGame is null)
			return;

		LoadGame = null;
		loadGame.GameLoaded -= OnGameLoaded;
		loadGame.BackRequested -= OnLoadGameBackRequested;
	}

	private void CloseManageMods()
	{
		var manageMods = m_manageMods;
		if (manageMods is null)
			return;

		ManageMods = null;
		manageMods.BackRequested -= OnManageModsBackRequested;
	}

	private void OnStartGameRequested(object? sender, GenericEventArgs<NewGameSettings> e)
	{
		Log.Info("Starting new game");
		StartNewGameRequested.Raise(this, e);
	}

	private void OnNewGameBackRequested(object? sender, EventArgs e) => CloseNewGame();

	private void OnGameLoaded(object? sender, GenericEventArgs<GameWorld> e) => GameLoaded.Raise(this, e);

	private void OnLoadGameBackRequested(object? sender, EventArgs e) => CloseLoadGame();

	// Returning from the mod list loads the game data again, with the mods as they are now.
	private void OnManageModsBackRequested(object? sender, EventArgs e)
	{
		CloseManageMods();
		IsLoading = true;
		LoadErrors = [];
		ReloadDataRequested.Raise(this);
	}

	private static string ShortenHash(string hash) => hash.Length > ShortHashLength ? hash[..ShortHashLength] : hash;

	private const int ShortHashLength = 12;

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(StartUpWindowViewModel));

	private readonly Random m_random;
	private readonly IFolderPicker m_folderPicker;
	private readonly bool m_areModsEnabled;
	private GameInfo? m_gameInfo;
	private GameData? m_gameData;
	private IModListStore? m_modListStore;
	private SaveSettings? m_saveSettings;
	private string m_title;
	private string m_description;
	private IReadOnlyList<string> m_loadErrors;
	private bool m_isLoading;
	private NewGameViewModel? m_newGame;
	private ManageModsViewModel? m_manageMods;
	private LoadGameViewModel? m_loadGame;
	private TaskGroup m_taskGroup;
}
