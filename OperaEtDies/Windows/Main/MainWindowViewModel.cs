using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using OperaEtDies.Data;

namespace OperaEtDies.Windows.Main;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
	public MainWindowViewModel(GameInfo gameInfo)
	{
		m_taskGroup = new TaskGroup();
		Title = gameInfo.Title;
	}

	public string Title { get; }

	public void Dispose()
	{
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(MainWindowViewModel));

	private TaskGroup m_taskGroup;
}
