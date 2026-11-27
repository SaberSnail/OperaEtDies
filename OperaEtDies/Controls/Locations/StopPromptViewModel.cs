using GoldenAnvil.Utility;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// Asks the player how to stop steps that have something left to take care of: finish up first, keep the bundle they
/// carry to finish later, or just stop, or else keep going, shown on top of everything else until they answer.
/// </summary>
public sealed class StopPromptViewModel : ViewModelBase, INumberedChoices
{
	public StopPromptViewModel(GameWorld world, StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(world);
		ArgumentNullException.ThrowIfNull(sequence);
		Sequence = sequence;
		var options = world.GetPlayerStopOptions(sequence);
		CanFinishUp = options.CanFinishUp;
		CanKeep = options.Kept is not null;
		var activity = PlayerStepsViewModel.LowerFirst(ActivityText.Fill(sequence.Need.GetActivity(sequence.IsDone ? null : sequence.Step, world.Player.Business?.Profession.Id), sequence));
		var notes = new List<string> { OurResources.StopPromptQuestion.FormatCurrentCulture(activity) };
		if (options.CanFinishUp)
			notes.Add(OurResources.StopPromptFinishUpNote);
		if (options.Kept is { } kept)
			notes.Add(OurResources.StopPromptKeepNote.FormatCurrentCulture(PlayerStepsViewModel.LowerFirst(kept.Title)));
		Text = string.Join(" ", notes);
	}

	/// <summary>
	/// Raised when the player chooses how to stop, or to keep going.
	/// </summary>
	public event EventHandler<GenericEventArgs<StopChoice>>? Chosen;

	/// <summary>
	/// The steps being stopped.
	/// </summary>
	public StepSequence Sequence { get; }

	/// <summary>
	/// The question, with what's left to take care of, such as "Stop drawing water? You could keep the bucket of water and
	/// finish later."
	/// </summary>
	public string Text { get; }

	/// <summary>
	/// Whether there's something left to take care of, so the player can finish up first.
	/// </summary>
	public bool CanFinishUp { get; }

	/// <summary>
	/// Whether the player carries a bundle they could keep, to finish later.
	/// </summary>
	public bool CanKeep { get; }

	public void FinishUp() => Choose(StopChoice.FinishUp);

	public void Keep() => Choose(StopChoice.Keep);

	public void JustStop() => Choose(StopChoice.JustStop);

	public void Cancel() => Choose(StopChoice.Cancel);

	/// <summary>
	/// Presses the numbered button: 1 to finish up, 2 to keep what they carry, 3 to just stop, 4 to keep going.
	/// </summary>
	public bool ChooseNumber(int number)
	{
		VerifyAccess();
		var choice = number switch
		{
			1 when CanFinishUp => (Action) FinishUp,
			2 when CanKeep => Keep,
			3 => JustStop,
			4 => Cancel,
			_ => null,
		};
		choice?.Invoke();
		return choice is not null;
	}

	private void Choose(StopChoice choice)
	{
		VerifyAccess();
		Chosen.Raise(this, new GenericEventArgs<StopChoice>(choice));
	}
}

/// <summary>
/// How the player chose to stop their steps.
/// </summary>
public enum StopChoice
{
	/// <summary>
	/// Take care of what's left (the "finally" steps), then stop.
	/// </summary>
	FinishUp,

	/// <summary>
	/// Keep the bundle they carry, to finish the steps later.
	/// </summary>
	Keep,

	/// <summary>
	/// Stop at once.
	/// </summary>
	JustStop,

	/// <summary>
	/// Don't stop after all.
	/// </summary>
	Cancel,
}
