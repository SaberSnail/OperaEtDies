using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Windows.Async;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Data;

namespace OperaEtDies.Windows.Main;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
	public MainWindowViewModel(GameInfo gameInfo, GameData gameData, NewGameSettings newGameSettings)
	{
		m_taskGroup = new TaskGroup();
		Title = gameInfo.Title;
		GameData = gameData;
		NewGameSettings = newGameSettings;
	}

	public string Title { get; }

	public GameData GameData { get; }

	public NewGameSettings NewGameSettings { get; }

	public void Dispose()
	{
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	private TaskGroup m_taskGroup;
}
