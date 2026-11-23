using GoldenAnvil.Utility.Calendar;

namespace OperaEtDies.GameState;

/// <summary>
/// Paces time as it passes in the world, so the player can watch it go by: time passes a slice at a time, pausing after
/// each until it's time for the next. It also says when the player has asked time to stop.
/// </summary>
public interface ITimePacer
{
	/// <summary>
	/// Whether the player has asked time to stop passing.
	/// </summary>
	bool IsStopRequested { get; }

	/// <summary>
	/// Gets when the next slice of time ends, from the current time, no later than the end of the time passing.
	/// </summary>
	TimePoint GetSliceEnd(TimePoint time, TimePoint end);

	/// <summary>
	/// Waits until it's time to show the world as it is at the given time. Returns early, without failing, if the player
	/// asks time to stop.
	/// </summary>
	Task WaitUntilAsync(TimePoint time);

	/// <summary>
	/// Lets so much time pass at once, without pausing, and without it counting toward how long the rest takes.
	/// </summary>
	void Skip(TimeOffset offset);
}
