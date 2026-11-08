using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;

namespace OperaEtDies.Windows.Main;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
	public MainWindowViewModel()
	{
		m_taskGroup = new TaskGroup();
	}

	public void Dispose()
	{
		DisposableUtility.Dispose(ref m_taskGroup);
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(MainWindowViewModel));

	private TaskGroup m_taskGroup;
}
