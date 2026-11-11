using System.Windows.Input;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using OperaEtDies.Controls.ManageMods;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Data;
using OperaEtDies.Mods;

namespace OperaEtDies.Windows.StartUp;

public sealed class StartUpWindowViewModel : ViewModelBase, IDisposable
{
	/// <summary>
	/// Creates the start up window. The mod list can only be managed if a store for it is given, which needs the game
	/// info. Mods that are turned off are still listed, but aren't loaded.
	/// </summary>
	public StartUpWindowViewModel(GameInfo? gameInfo, GameData? gameData, Random random, IReadOnlyList<DataLoadError> loadErrors, IModListStore? modListStore = null, IFolderPicker? folderPicker = null, bool areModsEnabled = true, bool isDeveloper = false)
	{
		IsDeveloper = isDeveloper;
		m_taskGroup = new TaskGroup();
		m_random = random;
		m_folderPicker = folderPicker ?? new FolderPicker();
		m_areModsEnabled = areModsEnabled;
		m_title = "";
		m_description = "";
		m_loadErrors = [];
		ApplyData(gameInfo, gameData, loadErrors, modListStore);
	}

	/// <summary>
	/// Whether the game was started in developer mode, which shows extra information and controls.
	/// </summary>
	public bool IsDeveloper { get; }

	public event EventHandler<GenericEventArgs<NewGameSettings>>? StartNewGameRequested;

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
	/// Whether the start up menu is showing, rather than another screen.
	/// </summary>
	public bool IsMenuVisible => NewGame is null && ManageMods is null;

	/// <summary>
	/// Shows the newly loaded game data, after <see cref="ReloadDataRequested"/>.
	/// </summary>
	public void SetData(GameInfo? gameInfo, GameData? gameData, IReadOnlyList<DataLoadError> loadErrors, IModListStore? modListStore)
	{
		VerifyAccess();
		ApplyData(gameInfo, gameData, loadErrors, modListStore);
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

		var newGame = new NewGameViewModel(m_gameData!, m_random);
		newGame.StartGameRequested += OnStartGameRequested;
		newGame.BackRequested += OnNewGameBackRequested;
		NewGame = newGame;
	}

	// Saving and loading aren't implemented yet.
	// MethodToCommandConverter only finds instance methods, so this can't be static.
#pragma warning disable CA1822 // Mark members as static
	public bool CanLoadGame() => false;
#pragma warning restore CA1822 // Mark members as static

	public void LoadGame()
	{
		VerifyAccess();
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
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	private void ApplyData(GameInfo? gameInfo, GameData? gameData, IReadOnlyList<DataLoadError> loadErrors, IModListStore? modListStore)
	{
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

	// Returning from the mod list loads the game data again, with the mods as they are now.
	private void OnManageModsBackRequested(object? sender, EventArgs e)
	{
		CloseManageMods();
		IsLoading = true;
		LoadErrors = [];
		ReloadDataRequested.Raise(this);
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(StartUpWindowViewModel));

	private readonly Random m_random;
	private readonly IFolderPicker m_folderPicker;
	private readonly bool m_areModsEnabled;
	private GameInfo? m_gameInfo;
	private GameData? m_gameData;
	private IModListStore? m_modListStore;
	private string m_title;
	private string m_description;
	private IReadOnlyList<string> m_loadErrors;
	private bool m_isLoading;
	private NewGameViewModel? m_newGame;
	private ManageModsViewModel? m_manageMods;
	private TaskGroup m_taskGroup;
}
