using System.Windows.Media;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Controls.Start;
using OperaEtDies.Controls.Status;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Time;

namespace OperaEtDies.Controls.Game;

/// <summary>
/// The Concerns panel at the top of the right column during play: what the player should keep in mind. For now that's
/// their own work and their household's chores, which they can start from here, where they're heading, and their
/// appointments for the rest of today. The game screen tells it when the time, the player's destination, or their
/// appointments or steps change.
/// </summary>
public sealed class ConcernsViewModel : ViewModelBase
{
	/// <summary>
	/// Creates the panel. Choosing an appointment's place as the destination calls the given action, which may ask the
	/// player to confirm it here first (see <see cref="AskToReplaceDestination"/>), and confirming it calls the other.
	/// Starting work or a chore calls the last.
	/// </summary>
	public ConcernsViewModel(GameWorld world, Action<Location>? chooseDestination = null, Action<Location>? replaceDestination = null, Action<Need>? start = null)
	{
		m_world = world;
		m_chooseDestination = chooseDestination;
		m_replaceDestination = replaceDestination;
		m_appointments = GetAppointments();
		m_destination = GetDestination();

		// The player has the same work and chores for the whole game.
		var quotas = world.GetPlayerQuotaProgress();
		Work = quotas.Where(x => !x.Need.SharedByFamily).Select(x => new ConcernsWorkViewModel(x.Need, world.GameData.GetIcon(x.Need.Icon), start)).ToList();
		Household = quotas.Where(x => x.Need.SharedByFamily).Select(x => new MeterViewModel(x.Need, world.GameData.GetIcon(x.Need.Icon), start)).ToList();
		UpdateQuotas();
	}

	/// <summary>
	/// The player's own work, such as their trade, with how much is done and a button that starts it.
	/// </summary>
	public IReadOnlyList<ConcernsWorkViewModel> Work { get; }

	/// <summary>
	/// The chores the player's household shares, such as cooking, as icons with how much is done, which start them.
	/// </summary>
	public IReadOnlyList<MeterViewModel> Household { get; }

	/// <summary>
	/// Where the player is heading and how long it takes to walk there, such as "The Mill (12 minutes)", or that they
	/// aren't heading anywhere.
	/// </summary>
	public string Destination
	{
		get => VerifyAccess(m_destination);
		private set => SetPropertyField([nameof(Destination), nameof(HasDestination)], value, ref m_destination);
	}

	/// <summary>
	/// Whether the player has a destination.
	/// </summary>
	public bool HasDestination => VerifyAccess(m_world.Player.Destination is not null);

	/// <summary>
	/// The destination can be cleared while there is one.
	/// </summary>
	public bool CanClearDestination() => HasDestination;

	/// <summary>
	/// Clears the player's destination.
	/// </summary>
	public void ClearDestination()
	{
		VerifyAccess();
		CancelDestination();
		m_world.SetPlayerDestination(null);
	}

	/// <summary>
	/// The question whether to choose a new destination, giving up the steps that chose the one the player has now, such
	/// as "Heading for The Mill instead gives up fetching water.", or null if the player isn't being asked.
	/// </summary>
	public string? DestinationQuestion
	{
		get => VerifyAccess(m_destinationQuestion);
		private set => SetPropertyField([nameof(DestinationQuestion), nameof(IsAskingDestination)], value, ref m_destinationQuestion);
	}

	/// <summary>
	/// Whether the player is being asked whether to choose a new destination.
	/// </summary>
	public bool IsAskingDestination => DestinationQuestion is not null;

	/// <summary>
	/// Asks the player whether to choose a new destination, since it gives up what they're doing, replacing any question
	/// before it.
	/// </summary>
	public void AskToReplaceDestination(Location place, string activity)
	{
		VerifyAccess();
		ArgumentNullException.ThrowIfNull(place);
		m_pendingDestination = place;
		DestinationQuestion = OurResources.ConcernsDestinationQuestion.FormatCurrentCulture(place.Title, activity);
	}

	/// <summary>
	/// Chooses the new destination the player was asked about, giving up what they're doing.
	/// </summary>
	public void ConfirmDestination()
	{
		VerifyAccess();
		if (m_pendingDestination is { } place)
		{
			CancelDestination();
			m_replaceDestination?.Invoke(place);
		}
	}

	/// <summary>
	/// Keeps the destination the player has, rather than the one they were asked about.
	/// </summary>
	public void CancelDestination()
	{
		VerifyAccess();
		m_pendingDestination = null;
		DestinationQuestion = null;
	}

	/// <summary>
	/// The player's appointments that are still to come today, soonest first.
	/// </summary>
	public IReadOnlyList<ConcernsAppointmentViewModel> Appointments
	{
		get => VerifyAccess(m_appointments);
		private set => SetPropertyField(value, ref m_appointments);
	}

	/// <summary>
	/// Brings the destination and the appointments up to date, once time has passed or they may have changed. A question
	/// about the destination is dropped once the destination it would replace has changed some other way.
	/// </summary>
	public void Update()
	{
		VerifyAccess();
		var appointments = GetAppointments();
		if (!appointments.SequenceEqual(m_appointments))
			Appointments = appointments;
		Destination = GetDestination();
		UpdateQuotas();
		if (m_pendingDestination is not null && !m_world.WouldDestinationStopPlayerSteps(m_pendingDestination))
			CancelDestination();
	}

	/// <summary>
	/// Formats an amount of a quota: minutes as hours and minutes, or a number of runs.
	/// </summary>
	internal static string FormatQuotaAmount(Need need, long amount)
	{
		if (need.IsRuns)
			return OurResources.QuotaRuns.FormatCurrentCulture(amount);

		var hours = amount / GameCalendar.MinutesInHour;
		var minutes = amount % GameCalendar.MinutesInHour;
		return hours == 0 ? OurResources.QuotaMinutes.FormatCurrentCulture(minutes) :
			minutes == 0 ? OurResources.QuotaHours.FormatCurrentCulture(hours) :
			OurResources.QuotaHoursMinutes.FormatCurrentCulture(hours, minutes);
	}

	/// <summary>
	/// Shows how far along the work and chores are, and whether each can be started now.
	/// </summary>
	private void UpdateQuotas()
	{
		if (Work.Count != 0 || Household.Count != 0)
		{
			var progress = m_world.GetPlayerQuotaProgress().ToDictionary(x => x.Need);
			var choices = m_world.GetPlayerStartChoices().ToDictionary(x => x.Need);
			foreach (var work in Work)
			{
				if (progress.GetValueOrDefault(work.Need) is { } quota)
					work.Update(quota, choices.GetValueOrDefault(work.Need), m_world);
			}

			foreach (var meter in Household)
			{
				if (progress.GetValueOrDefault(meter.Need) is { } quota)
					UpdateChore(meter, quota, choices.GetValueOrDefault(meter.Need));
			}
		}
	}

	/// <summary>
	/// Shows how far along a chore is, and whether it can be started now, with both in its tooltip.
	/// </summary>
	private void UpdateChore(MeterViewModel meter, PlayerQuotaProgress quota, PlayerStartChoice? choice)
	{
		var need = quota.Need;
		var status = quota.IsOpenToday ?
			OurResources.QuotaProgressToolTip.FormatCurrentCulture(need.Title, FormatQuotaAmount(need, quota.Done), FormatQuotaAmount(need, quota.Total), need.Period == NeedPeriod.Week ? OurResources.QuotaThisWeek : OurResources.QuotaToday) :
			OurResources.QuotaClosedToolTip.FormatCurrentCulture(need.Title);
		var action = choice is null ? null :
			choice.CanStart ? (choice.IsInProgress ? OurResources.ConcernsCarryOnToolTip : OurResources.ConcernsStartToolTip) :
			StartChoiceViewModel.GetNote(choice, m_world);
		var toolTip = action is null ? status : OurResources.ConcernsChoreToolTip.FormatCurrentCulture(status, action);
		meter.Update(quota.Fraction, toolTip, isActive: quota.IsOpenToday, isComplete: quota.Done >= quota.Total, isStartable: choice?.CanStart ?? false);
	}

	private string GetDestination()
	{
		if (m_world.Player.Destination is not { } destination)
			return OurResources.ConcernsNoDestination;

		var time = m_world.GetPlayerTravelTimeTo(destination);
		return time is { } offset
			? OurResources.ConcernsDestination.FormatCurrentCulture(destination.Title, m_world.GameData.Calendar.FormatOffsetFrom(m_world.Time, offset, TimeFormat.Long))
			: destination.Title;
	}

	private IReadOnlyList<ConcernsAppointmentViewModel> GetAppointments()
	{
		var calendar = m_world.GameData.Calendar;
		var today = calendar.GetStartOfDay(m_world.Time);
		return m_world.GetPlayerAppointments()
			.Where(x => calendar.GetStartOfDay(x.Time) == today)
			.Select(x => new ConcernsAppointmentViewModel(calendar.FormatClock(x.Time),
				OurResources.ConcernsAppointment.FormatCurrentCulture(x.GetOther(m_world.Player).FullName, AppointmentText.FormatPlace(m_world, x)), x.Place, m_chooseDestination))
			.ToList();
	}

	private readonly GameWorld m_world;
	private readonly Action<Location>? m_chooseDestination;
	private readonly Action<Location>? m_replaceDestination;
	private IReadOnlyList<ConcernsAppointmentViewModel> m_appointments;
	private string m_destination;
	private string? m_destinationQuestion;
	private Location? m_pendingDestination;
}

/// <summary>
/// One of the player's appointments today in the Concerns panel: when it is on the clock, such as "14:30", and who it's
/// with and where, such as "Mary Smith, her home". Its place can be chosen as the player's destination.
/// </summary>
public sealed record ConcernsAppointmentViewModel(string Clock, string Text, Location Place, Action<Location>? Choose = null)
{
	/// <summary>
	/// The appointment's place can be chosen as the destination unless the player is there.
	/// </summary>
	public bool CanChooseDestination() => Choose is not null && Place.People.All(x => !x.IsPlayer);

	/// <summary>
	/// Makes the appointment's place the player's destination.
	/// </summary>
	public void ChooseDestination()
	{
		if (CanChooseDestination())
			Choose!(Place);
	}
}

/// <summary>
/// The player's own work in the Concerns panel, such as their trade: how much is done, why it can't be started now if it
/// can't, and a button that starts it, or carries on with it.
/// </summary>
public sealed class ConcernsWorkViewModel : ViewModelBase
{
	public ConcernsWorkViewModel(Need need, Geometry icon, Action<Need>? start = null)
	{
		Need = need;
		Icon = icon;
		m_start = start;
	}

	public Need Need { get; }

	public string Title => Need.Title;

	/// <summary>
	/// The work's icon, on a 24 by 24 grid.
	/// </summary>
	public Geometry Icon { get; }

	/// <summary>
	/// How much is done, such as "2 hours of 8 today".
	/// </summary>
	public string Status
	{
		get => VerifyAccess(m_status);
		private set => SetPropertyField(value, ref m_status);
	}

	/// <summary>
	/// Why it can't be started now, such as when it can be done, or null if it can.
	/// </summary>
	public string? Note
	{
		get => VerifyAccess(m_note);
		private set => SetPropertyField(value, ref m_note);
	}

	/// <summary>
	/// The text of the button that starts it, or carries on with it.
	/// </summary>
	public string StartText
	{
		get => VerifyAccess(m_startText);
		private set => SetPropertyField(value, ref m_startText);
	}

	/// <summary>
	/// Whether it's done for the day or week.
	/// </summary>
	public bool IsComplete
	{
		get => VerifyAccess(m_isComplete);
		private set => SetPropertyField(value, ref m_isComplete);
	}

	/// <summary>
	/// Whether the button starts it now.
	/// </summary>
	public bool IsStartable
	{
		get => VerifyAccess(m_isStartable);
		private set => SetPropertyField(value, ref m_isStartable);
	}

	/// <summary>
	/// Starts the work, or carries on with it, if it can be started now.
	/// </summary>
	public void Start()
	{
		VerifyAccess();
		if (IsStartable)
			m_start?.Invoke(Need);
	}

	internal void Update(PlayerQuotaProgress progress, PlayerStartChoice? choice, GameWorld world)
	{
		VerifyAccess();
		Status = new NeedStatusViewModel(world, world.Player, Need, isDeveloper: false).Value;
		Note = choice is null ? null : StartChoiceViewModel.GetNote(choice, world);
		StartText = choice?.IsInProgress ?? false ? OurResources.StartCarryOn : OurResources.StartButton;
		IsComplete = progress.Done >= progress.Total;
		IsStartable = m_start is not null && (choice?.CanStart ?? false);
	}

	private readonly Action<Need>? m_start;
	private string m_status = "";
	private string? m_note;
	private string m_startText = OurResources.StartButton;
	private bool m_isComplete;
	private bool m_isStartable;
}
