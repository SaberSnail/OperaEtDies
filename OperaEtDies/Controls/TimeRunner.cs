using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using System.ComponentModel;
using OperaEtDies.GameState;
using OperaEtDies.Settings;

namespace OperaEtDies.Controls;

/// <summary>
/// Runs what the player does that passes time, one thing at a time. Time takes a little while to go by, at the speed
/// chosen in the settings (see <see cref="TimePacer"/>), and while it does, the player can't do anything else, only stop
/// it. At the instant speed, or without settings (as for tests), time passes at once.
/// </summary>
public sealed class TimeRunner : ViewModelBase, IDisposable
{
	public TimeRunner(GameWorld world, AppSettings? settings)
	{
		m_world = world;
		m_settings = settings;
		m_taskGroup = new TaskGroup();
		if (settings is not null)
			settings.PropertyChanged += OnSettingsPropertyChanged;
	}

	/// <summary>
	/// Whether time passes at once, so there's never anything to stop.
	/// </summary>
	public bool IsInstant => VerifyAccess(GetPaceScale() is null);

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
	/// Whether time is held still, such as while a sheet is open, so nothing new can run.
	/// </summary>
	public bool IsHeld
	{
		get => VerifyAccess(m_isHeld);
		set => SetPropertyField(value, ref m_isHeld);
	}

	/// <summary>
	/// Runs something the player did that passes time, unless something else is already running, or time is held.
	/// </summary>
	public void Run(Func<Task> action)
	{
		VerifyAccess();
		ArgumentNullException.ThrowIfNull(action);
		if (m_isHeld)
		{
			Log.Warn("Time is held, so nothing can run");
		}
		else if (m_watcher is null)
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

	public void Dispose()
	{
		if (m_settings is not null)
			m_settings.PropertyChanged -= OnSettingsPropertyChanged;
		m_taskGroup.Dispose();
	}

	private async Task RunAsync(Func<Task> action, TaskStateController state)
	{
		// The speed is read as each thing starts. It can only change from the settings, and opening them stops time.
		m_world.Pacer = GetPaceScale() is { } scale ? new TimePacer(m_world.GameData.Calendar, m_world.Time, state, scale) : null;
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

	private double? GetPaceScale() => m_settings?.TimeSpeed.GetPaceScale();

	private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AppSettings.TimeSpeed))
			RaisePropertyChanged(nameof(IsInstant));
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(TimeRunner));

	private readonly GameWorld m_world;
	private readonly AppSettings? m_settings;
	private readonly TaskGroup m_taskGroup;
	private TaskWatcher? m_watcher;
	private bool m_isStopping;
	private bool m_isHeld;
}
