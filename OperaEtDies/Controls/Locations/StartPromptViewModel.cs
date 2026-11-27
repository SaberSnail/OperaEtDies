using GoldenAnvil.Utility;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// Asks the player whether to start an appointment now: the worker asking whether they're ready, or the client telling
/// them they're ready now, after someone said no. It's shown on top of everything else until they answer.
/// </summary>
public sealed class StartPromptViewModel : ViewModelBase, INumberedChoices
{
	public StartPromptViewModel(Appointment appointment, GameWorld world)
	{
		ArgumentNullException.ThrowIfNull(appointment);
		ArgumentNullException.ThrowIfNull(world);
		var gameData = world.GameData;
		var asker = appointment.GetOther(world.Player);
		Asker = new LocationPersonViewModel(asker, gameData.PortraitRenderer, gameData.Schedules, gameData.Relationships);
		Text = AppointmentText.GetStartAsked(world, appointment);
	}

	/// <summary>
	/// Raised when the player answers, with whether they said yes.
	/// </summary>
	public event EventHandler<GenericEventArgs<bool>>? Answered;

	/// <summary>
	/// Who's asking, with their name and portrait.
	/// </summary>
	public LocationPersonViewModel Asker { get; }

	/// <summary>
	/// What they said, such as "Nicholas Miller asks whether you're ready to start."
	/// </summary>
	public string Text { get; }

	public void Yes()
	{
		VerifyAccess();
		Answered.Raise(this, new GenericEventArgs<bool>(true));
	}

	public void No()
	{
		VerifyAccess();
		Answered.Raise(this, new GenericEventArgs<bool>(false));
	}

	/// <summary>
	/// Presses the numbered button: 1 for yes, 2 for no.
	/// </summary>
	public bool ChooseNumber(int number)
	{
		VerifyAccess();
		var choice = number switch
		{
			1 => (Action) Yes,
			2 => No,
			_ => null,
		};
		choice?.Invoke();
		return choice is not null;
	}
}
