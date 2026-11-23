using System.Diagnostics;
using GoldenAnvil.Utility.Calendar;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using OperaEtDies.GameState;
using OperaEtDies.Time;

namespace OperaEtDies.Controls;

/// <summary>
/// Paces time passing for something the player does, so they can watch it go by: the more time passes, the longer it
/// takes, but each minute takes less than the one before, from about half a second for a minute up to five seconds for
/// twelve hours, after which the rest passes at once. Time that's skipped doesn't count toward it. Time passes in slices
/// about a frame apart. Stopping the task running it asks time to stop.
/// </summary>
public sealed class TimePacer : ITimePacer
{
	public TimePacer(GameCalendar calendar, TimePoint start, TaskStateController state)
	{
		m_calendar = calendar;
		m_start = start;
		m_state = state;
		m_stopwatch = Stopwatch.StartNew();
	}

	public bool IsStopRequested => m_state.CancellationToken.IsCancellationRequested;

	public TimePoint GetSliceEnd(TimePoint time, TimePoint end)
	{
		var minutes = GetMinutes(time);
		var seconds = GetSeconds(minutes) + FrameSeconds;
		if (seconds >= MaxSeconds)
			return end;

		var next = m_calendar.AddMinutes(m_start, Math.Max(minutes + 1, (long) Math.Ceiling(GetMinutesFor(seconds))));
		return next < end ? next : end;
	}

	public async Task WaitUntilAsync(TimePoint time)
	{
		// Always pausing a little lets the window show how things are now, even when time is passing slower than it should.
		var wait = TimeSpan.FromSeconds(GetSeconds(GetMinutes(time))) - m_stopwatch.Elapsed;
		if (!IsStopRequested)
		{
			try
			{
				await Task.Delay(wait > MinWait ? wait : MinWait, m_state.CancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Stopping only ends the pause early.
			}

			await m_state.ToSyncContext();
		}
	}

	public void Skip(TimeOffset offset) => m_start += offset;

	/// <summary>
	/// Gets how many seconds of real time it takes for so many minutes of game time to pass.
	/// </summary>
	internal static double GetSeconds(long minutes) =>
		minutes <= 0 ? 0 : MinSeconds + ((MaxSeconds - MinSeconds) * Math.Sqrt(Math.Min(minutes, LongestMinutes) / (double) LongestMinutes));

	/// <summary>
	/// Gets how many minutes of game time pass in so many seconds of real time, the reverse of <see cref="GetSeconds"/>.
	/// </summary>
	internal static double GetMinutesFor(double seconds)
	{
		var fraction = Math.Clamp((seconds - MinSeconds) / (MaxSeconds - MinSeconds), 0, 1);
		return LongestMinutes * fraction * fraction;
	}

	private long GetMinutes(TimePoint time) => m_calendar.GetTotalMinutes(time - m_start);

	/// <summary>
	/// The real time it takes for the shortest time to pass.
	/// </summary>
	private const double MinSeconds = 0.5;

	/// <summary>
	/// The real time it takes for <see cref="LongestMinutes"/> or more to pass.
	/// </summary>
	private const double MaxSeconds = 5;

	/// <summary>
	/// The game time that takes the longest, twelve hours. Any more passes at once.
	/// </summary>
	private const long LongestMinutes = 12 * 60;

	/// <summary>
	/// The real time between slices.
	/// </summary>
	private const double FrameSeconds = 1.0 / 30;

	private static readonly TimeSpan MinWait = TimeSpan.FromMilliseconds(1);

	private readonly GameCalendar m_calendar;
	// When the time being paced started, moved later by any time that passed at once.
	private TimePoint m_start;
	private readonly TaskStateController m_state;
	private readonly Stopwatch m_stopwatch;
}
