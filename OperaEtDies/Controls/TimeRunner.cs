using GoldenAnvil.Utility.Windows.Async;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls;

/// <summary>
/// Runs what the player does that passes time, one thing at a time. When it's paced, time takes a little while to go by
/// (see <see cref="TimePacer"/>), and while it does, the player can't do anything else, only stop it. Unpaced, as for
/// tests, time passes at once.
/// </summary>
public sealed class TimeRunner : ViewModelBase, IDisposable
{
	public TimeRunner(GameWorld world, bool isPaced)
	{
		m_world = world;
		m_isPaced = isPaced;
		m_taskGroup = new TaskGroup();
	}

	/// <summary>
	/// Whether something the player did is running, so they can't do anything else until it's done, other than stop it.
	/// </summary>
	public bool IsRunning => VerifyAccess(m_watcher is not null);

	/// <summary>
	/// Whether the player asked what's running to stop, and it hasn't stopped yet.
	/// </summary>
	public bool IsStopping
	{
		get => VerifyAccess(m_isStopping);
		private set => SetPropertyField([nameof(IsStopping), nameof(CanBeStopped)], value, ref m_isStopping);
	}

	/// <summary>
	/// Whether what's running can be asked to stop: it's running, and hasn't been asked yet.
	/// </summary>
	public bool CanBeStopped => IsRunning && !IsStopping;

	/// <summary>
	/// Runs something the player did that passes time, unless something else is already running.
	/// </summary>
	public void Run(Func<Task> action)
	{
		VerifyAccess();
		ArgumentNullException.ThrowIfNull(action);
		if (m_watcher is null)
		{
			var watcher = TaskWatcher.Create(state => RunAsync(action, state), m_taskGroup);
			m_watcher = watcher;
			RaisePropertyChanged(nameof(IsRunning), nameof(CanBeStopped));
			watcher.Start();
		}
	}

	/// <summary>
	/// Asks what's running to stop, which it does at the end of the slice of time passing, or once the player arrives if
	/// they're walking somewhere.
	/// </summary>
	public void Stop()
	{
		VerifyAccess();
		if (m_watcher is { } watcher)
		{
			IsStopping = true;
			watcher.Cancel();
		}
	}

	public void Dispose() => m_taskGroup.Dispose();

	private async Task RunAsync(Func<Task> action, TaskStateController state)
	{
		m_world.Pacer = m_isPaced ? new TimePacer(m_world.GameData.Calendar, m_world.Time, state) : null;
		try
		{
			await action().ConfigureAwait(true);
		}
		finally
		{
			m_world.Pacer = null;
			m_watcher = null;
			IsStopping = false;
			RaisePropertyChanged(nameof(IsRunning), nameof(CanBeStopped));
		}
	}

	private readonly GameWorld m_world;
	private readonly bool m_isPaced;
	private readonly TaskGroup m_taskGroup;
	private TaskWatcher? m_watcher;
	private bool m_isStopping;
}
