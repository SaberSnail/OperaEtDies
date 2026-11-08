using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using OperaEtDies.Data;

namespace OperaEtDies.Windows.StartUp;

public sealed class StartUpWindowViewModel : ViewModelBase, IDisposable
{
	public StartUpWindowViewModel(GameInfo? gameInfo, IReadOnlyList<DataLoadError> loadErrors)
	{
		m_taskGroup = new TaskGroup();
		m_gameInfo = gameInfo;
		LoadErrors = loadErrors.Select(x => x.ToString()).ToList();
	}

	public event EventHandler? StartNewGameRequested;

	public event EventHandler? ExitGameRequested;

	public string Title => m_gameInfo?.Title ?? OurResources.EngineName;

	public string Description => m_gameInfo?.Description ?? "";

	public IReadOnlyList<string> LoadErrors { get; }

	public bool HasLoadErrors => LoadErrors.Count != 0;

	// A new game can't be started without the game data.
	public bool CanStartNewGame() => m_gameInfo is not null;

	public void StartNewGame()
	{
		VerifyAccess();
		if (!CanStartNewGame())
			return;

		Log.Info("Starting new game");
		StartNewGameRequested.Raise(this);
	}

	// Saving and loading aren't implemented yet.
	public bool CanLoadGame() => false;

	public void LoadGame()
	{
		VerifyAccess();
	}

	public void ExitGame()
	{
		VerifyAccess();
		ExitGameRequested.Raise(this);
	}

	public void Dispose()
	{
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(StartUpWindowViewModel));

	private readonly GameInfo? m_gameInfo;
	private TaskGroup m_taskGroup;
}
