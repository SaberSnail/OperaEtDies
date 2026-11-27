using GoldenAnvil.Utility;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// Asks the player, once it's time to set off for an appointment their sequence is for, whether to set off or call it
/// off, shown on top of everything else until they answer.
/// </summary>
public sealed class AppointmentPromptViewModel : ViewModelBase, INumberedChoices
{
	public AppointmentPromptViewModel(GameWorld world, StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(world);
		ArgumentNullException.ThrowIfNull(sequence);
		var appointment = sequence.Appointment ?? throw new ArgumentException("The sequence isn't for an appointment.", nameof(sequence));
		var gameData = world.GameData;
		Sequence = sequence;
		Client = new LocationPersonViewModel(appointment.GetOther(world.Player), gameData.PortraitRenderer, gameData.Schedules, gameData.Relationships);
		Text = OurResources.AppointmentReminder.FormatCurrentCulture(appointment.GetOther(world.Player).FullName, AppointmentText.FormatPlace(world, appointment), gameData.Calendar.FormatClock(appointment.Time));
	}

	/// <summary>
	/// Raised when the player sets off.
	/// </summary>
	public event EventHandler? SetOff;

	/// <summary>
	/// Raised when the player calls it off.
	/// </summary>
	public event EventHandler? CalledOff;

	/// <summary>
	/// The player's sequence for the appointment.
	/// </summary>
	public StepSequence Sequence { get; }

	/// <summary>
	/// Who the appointment is with, with their name and portrait.
	/// </summary>
	public LocationPersonViewModel Client { get; }

	/// <summary>
	/// The reminder, such as "It's time to set off for your appointment with Mary Smith at her home (14:30)."
	/// </summary>
	public string Text { get; }

	public void SetOffNow()
	{
		VerifyAccess();
		SetOff.Raise(this);
	}

	public void CallOff()
	{
		VerifyAccess();
		CalledOff.Raise(this);
	}

	/// <summary>
	/// Presses the numbered button: 1 to set off, 2 to call it off.
	/// </summary>
	public bool ChooseNumber(int number)
	{
		VerifyAccess();
		var choice = number switch
		{
			1 => (Action) SetOffNow,
			2 => CallOff,
			_ => null,
		};
		choice?.Invoke();
		return choice is not null;
	}
}
