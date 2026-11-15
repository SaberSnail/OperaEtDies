using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Status;

/// <summary>
/// Shows how someone is doing: each of their needs. It's usually the player's, but developers can see anyone's, and change
/// their needs.
/// </summary>
public sealed class StatusViewModel : ViewModelBase
{
	public StatusViewModel(GameWorld world, bool isDeveloper = false)
		: this(world, world.Player, isDeveloper)
	{
	}

	public StatusViewModel(GameWorld world, Person person, bool isDeveloper)
	{
		Person = person;
		Title = person.IsPlayer ? OurResources.StatusHeader : OurResources.StatusHeaderPerson.FormatCurrentCulture(person.FullName);
		Needs = world.GameData.Schedules.Needs
			.Where(x => Scheduler.HasNeed(person, x))
			.Select(x => new NeedStatusViewModel(world, person, x, isDeveloper))
			.ToList();
		foreach (var need in Needs)
			need.Changed += OnNeedChanged;
	}

	public event EventHandler? CloseRequested;

	/// <summary>
	/// The person whose needs are shown.
	/// </summary>
	public Person Person { get; }

	public string Title { get; }

	public IReadOnlyList<NeedStatusViewModel> Needs { get; }

	/// <summary>
	/// Whether a developer changed any of the needs.
	/// </summary>
	public bool IsChanged { get; private set; }

	public void Close()
	{
		VerifyAccess();
		CloseRequested.Raise(this);
	}

	private void OnNeedChanged(object? sender, EventArgs e) => IsChanged = true;
}

/// <summary>
/// How someone is doing with one need, such as "Fatigue: Tired (62 of 100)", "Work: 2 hours of 8 hours today", or
/// "Fetching water: 1 of 3 visits today (5 minutes each)", whether a quota need is done, and notes such as whether the
/// household shares it, and whether the player still has to finish a trip for it at home. Developers can change how far
/// along the need is: a level need's level, or the minutes or trips done toward a quota (for the whole household, if it
/// shares the need).
/// </summary>
public sealed class NeedStatusViewModel : ViewModelBase
{
	public NeedStatusViewModel(GameWorld world, Person person, Need need, bool isDeveloper)
	{
		m_world = world;
		m_person = person;
		m_need = need;
		IsDeveloper = isDeveloper;
		Title = need.Title;
	}

	/// <summary>
	/// Raised when a developer changes the need.
	/// </summary>
	public event EventHandler? Changed;

	public string Title { get; }

	/// <summary>
	/// Whether the amount can be changed.
	/// </summary>
	public bool IsDeveloper { get; }

	/// <summary>
	/// How the person is doing with the need, such as "Tired (62 of 100)".
	/// </summary>
	public string Value
	{
		get
		{
			VerifyAccess();
			if (m_need.Kind == NeedKind.Level)
			{
				var level = m_person.GetLevel(m_need);
				var amount = OurResources.NeedStatusLevel.FormatCurrentCulture(Math.Round(level), Need.MaximumLevel);
				return m_need.GetLevelTitle(level) is { } title ? OurResources.NeedStatusLevelTitled.FormatCurrentCulture(title, amount) : amount;
			}

			var (done, quota) = (GetDone(), GetQuota());
			var isDay = m_need.Period == NeedPeriod.Day;
			if (m_need.IsTrips)
			{
				// Trips are counted once they're finished, with how long each one takes at the place and at home.
				var tripMinutes = GetTripMinutes();
				var trips = quota / tripMinutes;
				return (isDay ? OurResources.NeedStatusTripsDay : OurResources.NeedStatusTripsWeek).FormatCurrentCulture(Math.Min(done / tripMinutes, trips), trips, FormatMinutes(tripMinutes));
			}

			return (isDay ? OurResources.NeedStatusQuotaDay : OurResources.NeedStatusQuotaWeek).FormatCurrentCulture(FormatMinutes(Math.Min(done, quota)), FormatMinutes(quota));
		}
	}

	/// <summary>
	/// Notes about the need, such as whether the household shares it, or null if there are none.
	/// </summary>
	public string? Note
	{
		get
		{
			VerifyAccess();
			var notes = m_need.Kind == NeedKind.Level ? [] :
				new[] { m_need.SharedByFamily ? OurResources.NeedStatusShared : null, m_person.PendingTrip == m_need ? m_need.PlayerAction?.Return?.Text : null }.OfType<string>().ToList();
			return notes.Count == 0 ? null : string.Join(OurResources.NeedStatusNoteSeparator, notes);
		}
	}

	/// <summary>
	/// Whether a quota need's quota is met for this day or week.
	/// </summary>
	public bool IsDone => VerifyAccess(m_need.Kind == NeedKind.Quota && GetDone() >= GetQuota());

	/// <summary>
	/// The most <see cref="Amount"/> can be: the maximum level, or the minutes or trips in the quota.
	/// </summary>
	public double Maximum => VerifyAccess(m_need.Kind == NeedKind.Level ? Need.MaximumLevel : m_need.IsTrips ? GetQuota() / GetTripMinutes() : GetQuota());

	/// <summary>
	/// How far along the need is, which developers can change: the level, or the minutes or finished trips done toward the
	/// quota. A change to a shared quota is made to the person's own share, so the household's total is the amount.
	/// </summary>
	public double Amount
	{
		get
		{
			VerifyAccess();
			return m_need.Kind == NeedKind.Level ? Math.Round(m_person.GetLevel(m_need)) : m_need.IsTrips ? Math.Min(GetDone() / GetTripMinutes(), Maximum) : Math.Min(GetDone(), GetQuota());
		}
		set
		{
			VerifyAccess();
			if (IsDeveloper && value != Amount)
			{
				var amount = Math.Clamp(Math.Round(value), 0, Maximum);
				if (m_need.Kind == NeedKind.Level)
				{
					m_person.SetLevel(m_need, amount);
				}
				else
				{
					var others = GetDone() - m_person.GetProgress(m_need);
					var minutes = m_need.IsTrips ? (long) amount * GetTripMinutes() : (long) amount;
					m_person.SetProgress(m_need, Math.Max(minutes - others, 0));
				}

				RaisePropertyChanged(nameof(Amount), nameof(Value), nameof(IsDone));
				Changed.Raise(this);
			}
		}
	}

	private IReadOnlyList<Person> GetPeople() => m_need.SharedByFamily ? m_person.Family.Members : [m_person];

	private long GetDone() => GetPeople().Sum(x => x.GetProgress(m_need));

	private long GetQuota() => m_need.GetQuotaMinutes(GetPeople().Count);

	private long GetTripMinutes() => Math.Max(m_need.GetShiftMinutes(GetPeople().Count), 1);

	private string FormatMinutes(long minutes)
	{
		var calendar = m_world.GameData.Calendar;
		return calendar.FormatOffsetFrom(m_world.Time, calendar.CreateTimeOffset(0, 0, minutes), TimeFormat.Long);
	}

	private readonly GameWorld m_world;
	private readonly Person m_person;
	private readonly Need m_need;
}
