using System.Globalization;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// Asks the player whether to keep going with what they're doing, such as working, after each pass of steps that repeat,
/// saying why the pass ended, if a step failed, and if something else has become more pressing, shown on top of
/// everything else until they answer.
/// </summary>
public sealed class RepeatPromptViewModel : ViewModelBase, INumberedChoices
{
	public RepeatPromptViewModel(GameWorld world, StepSequence sequence, string? reason = null)
	{
		ArgumentNullException.ThrowIfNull(world);
		ArgumentNullException.ThrowIfNull(sequence);
		var player = world.Player;
		var activity = LowerFirst(ActivityText.Fill(sequence.Need.GetActivity(null, player.Business?.Profession.Id), sequence));
		var question = world.GetPlayerMoreUrgentNeed(sequence) is { } pressing
			? OurResources.RepeatPromptText.FormatCurrentCulture(pressing.Title, activity)
			: OurResources.RepeatPromptQuestion.FormatCurrentCulture(activity);
		Text = reason is null ? question : OurResources.RepeatPromptWithReason.FormatCurrentCulture(reason, question);
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
	/// The question, such as "Hunger is getting pressing. Keep working?", after why the pass ended, if a step failed, such
	/// as "Nicholas Miller said no."
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

	/// <summary>
	/// Presses the numbered button: 1 to keep going, 2 to stop.
	/// </summary>
	public bool ChooseNumber(int number)
	{
		VerifyAccess();
		var choice = number switch
		{
			1 => (Action) Continue,
			2 => Stop,
			_ => null,
		};
		choice?.Invoke();
		return choice is not null;
	}

	private static string LowerFirst(string text) => text.Length == 0 ? text : char.ToLower(text[0], CultureInfo.CurrentCulture) + text[1..];
}
