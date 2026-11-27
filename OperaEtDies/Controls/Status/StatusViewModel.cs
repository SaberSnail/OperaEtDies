using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Status;

/// <summary>
/// Shows how someone is doing: each of their needs, and for the player, their appointments and jobs. It's usually the player's, but
/// developers can see anyone's, and change their needs.
/// </summary>
public sealed class StatusViewModel : ViewModelBase, ISheet
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
		Appointments = person.IsPlayer ? GetAppointments(world) : null;
		Jobs = person.IsPlayer ? GetJobs(world) : [];
		foreach (var need in Needs)
			need.Changed += OnNeedChanged;
	}

	/// <summary>
	/// The person whose needs are shown.
	/// </summary>
	public Person Person { get; }

	public string Title { get; }

	public bool IsFullWidth => false;

	public IReadOnlyList<NeedStatusViewModel> Needs { get; }

	/// <summary>
	/// For the player, their appointments, soonest first, such as "14:30 today: Attending an expectant mother with Mary Smith,
	/// at her home", or that they have none. For anyone else, null.
	/// </summary>
	public IReadOnlyList<string>? Appointments { get; }

	/// <summary>
	/// For the player, the jobs they've taken on and not yet finished, such as "Work for Mary Smith, at her home, off Mill
	/// Lane: 6 hours left of 16 hours". For anyone else, or a player without jobs, none.
	/// </summary>
	public IReadOnlyList<string> Jobs { get; }

	/// <summary>
	/// Whether there are jobs to show.
	/// </summary>
	public bool HasJobs => Jobs.Count != 0;

	/// <summary>
	/// Whether a developer changed any of the needs.
	/// </summary>
	public bool IsChanged { get; private set; }

	private static IReadOnlyList<string> GetAppointments(GameWorld world) =>
		world.GetPlayerAppointments() is { Count: > 0 } appointments ?
			appointments.Select(x => OurResources.StatusAppointment.FormatCurrentCulture(AppointmentText.FormatWhen(world, x.Time), AppointmentText.FormatActivity(world, x), x.Client.FullName,
				AppointmentText.FormatPlace(world, x))).ToList() :
			[OurResources.StatusAppointmentsNone];

	private static IReadOnlyList<string> GetJobs(GameWorld world)
	{
		var calendar = world.GameData.Calendar;
		string Format(long minutes) => calendar.FormatOffsetFrom(world.Time, calendar.CreateTimeOffset(0, 0, minutes), TimeFormat.Long);
		return world.Player.Jobs.Where(x => !x.IsDone)
			.Select(x => OurResources.StatusJob.FormatCurrentCulture(x.Need.Title, x.Client.FullName, AppointmentText.FormatPlace(world, x.Place, x.Client), Format(x.Minutes - x.MinutesDone), Format(x.Minutes)))
			.ToList();
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
			if (m_need.IsRuns)
			{
				// Runs are counted once they're finished, with how long the "do" steps of each one take.
				return (isDay ? OurResources.NeedStatusTripsDay : OurResources.NeedStatusTripsWeek).FormatCurrentCulture(Math.Min(done, quota), quota, FormatMinutes(m_need.GetRunMinutes(GetPeople().Count)));
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
				new[] { m_need.SharedByFamily ? OurResources.NeedStatusShared : null, m_person.GetSequence(m_need)?.NextText }.OfType<string>().ToList();
			return notes.Count == 0 ? null : string.Join(OurResources.NeedStatusNoteSeparator, notes);
		}
	}

	/// <summary>
	/// Whether a quota need's quota is met for this day or week.
	/// </summary>
	public bool IsDone => VerifyAccess(m_need.Kind == NeedKind.Quota && GetDone() >= GetQuota());

	/// <summary>
	/// The most <see cref="Amount"/> can be: the maximum level, or the minutes or runs in the quota.
	/// </summary>
	public double Maximum => VerifyAccess(m_need.Kind == NeedKind.Level ? Need.MaximumLevel : GetQuota());

	/// <summary>
	/// How far along the need is, which developers can change: the level, or the minutes or finished runs done toward the
	/// quota. A change to a shared quota is made to the person's own share, so the household's total is the amount.
	/// </summary>
	public double Amount
	{
		get
		{
			VerifyAccess();
			return m_need.Kind == NeedKind.Level ? Math.Round(m_person.GetLevel(m_need)) : Math.Min(GetDone(), GetQuota());
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
					m_person.SetProgress(m_need, Math.Max((long) amount - others, 0));
				}

				RaisePropertyChanged(nameof(Amount), nameof(Value), nameof(IsDone));
				Changed.Raise(this);
			}
		}
	}

	private IReadOnlyList<Person> GetPeople() => m_need.SharedByFamily ? m_person.Family.Members : [m_person];

	private long GetDone() => GetPeople().Sum(x => x.GetProgress(m_need));

	/// <summary>
	/// Gets the quota for the person, or their family for a shared need: minutes, or runs.
	/// </summary>
	private long GetQuota() => m_need.IsRuns ? m_need.GetQuotaRuns(GetPeople().Count) : m_need.GetQuotaMinutes(GetPeople().Count);

	private string FormatMinutes(long minutes)
	{
		var calendar = m_world.GameData.Calendar;
		return calendar.FormatOffsetFrom(m_world.Time, calendar.CreateTimeOffset(0, 0, minutes), TimeFormat.Long);
	}

	private readonly GameWorld m_world;
	private readonly Person m_person;
	private readonly Need m_need;
}
