using System.Globalization;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// Asks the player whether to keep going with what they're doing, such as working, after each pass of steps that repeat,
/// saying if something else has become more pressing, shown on top of everything else until they answer.
/// </summary>
public sealed class RepeatPromptViewModel : ViewModelBase
{
	public RepeatPromptViewModel(GameWorld world, StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(world);
		ArgumentNullException.ThrowIfNull(sequence);
		var player = world.Player;
		var activity = LowerFirst(sequence.Need.GetActivity(null, player.Business?.Profession.Id));
		Text = world.GetPlayerMoreUrgentNeed(sequence) is { } pressing
			? OurResources.RepeatPromptText.FormatCurrentCulture(pressing.Title, activity)
			: OurResources.RepeatPromptQuestion.FormatCurrentCulture(activity);
		var calendar = world.GameData.Calendar;
		ContinueText = OurResources.RepeatPromptContinue.FormatCurrentCulture(calendar.FormatOffsetFrom(world.Time, calendar.CreateTimeOffset(0, 0, sequence.MinutesLeft ?? 0), TimeFormat.Long));
	}

	/// <summary>
	/// Raised when the player keeps going.
	/// </summary>
	public event EventHandler? Continued;

	/// <summary>
	/// Raised when the player stops.
	/// </summary>
	public event EventHandler? Stopped;

	/// <summary>
	/// The question, such as "Hunger is getting pressing. Keep working?"
	/// </summary>
	public string Text { get; }

	/// <summary>
	/// The text of the button that keeps going, with how much of the time chosen is left.
	/// </summary>
	public string ContinueText { get; }

	public void Continue()
	{
		VerifyAccess();
		Continued.Raise(this);
	}

	public void Stop()
	{
		VerifyAccess();
		Stopped.Raise(this);
	}

	private static string LowerFirst(string text) => text.Length == 0 ? text : char.ToLower(text[0], CultureInfo.CurrentCulture) + text[1..];
}
