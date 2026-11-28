using GoldenAnvil.Utility;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Controls.Map;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Time;

namespace OperaEtDies.Controls.Game;

/// <summary>
/// The left column during play, showing the state of the player's character: who they are, the date and time, their next
/// appointment, their own needs, which they can start from here, and the mini-map. The game screen tells it when the time,
/// the player's location, their appointments, or their steps change.
/// </summary>
public sealed class CharacterPanelViewModel : ViewModelBase
{
	/// <summary>
	/// Creates the panel. Starting a need calls the given action.
	/// </summary>
	public CharacterPanelViewModel(GameWorld world, Action<Need>? start = null)
	{
		m_world = world;
		Player = new LocationPersonViewModel(world.Player, world.GameData.PortraitRenderer);
		m_miniMap = CreateMiniMap(world.Player.Location);

		// The player has the same personal needs for the whole game.
		Needs = world.GameData.Schedules.Needs
			.Where(x => x.Category == NeedCategory.Personal && Scheduler.HasNeed(world.Player, x))
			.Select(x => new MeterViewModel(x, world.GameData.GetIcon(x.Icon), start))
			.ToList();
		UpdateNeeds();
	}

	/// <summary>
	/// The player's character, with their portrait and name.
	/// </summary>
	public LocationPersonViewModel Player { get; }

	/// <summary>
	/// The player's profession, such as "Blacksmith", or null if they have none.
	/// </summary>
	public string? Profession
	{
		get
		{
			VerifyAccess();
			var propertyId = m_world.GameData.Families.Roles.Properties.Profession;
			return (m_world.Player.Traits.Values.GetValueOrDefault(propertyId) as CharacterPropertyOption)?.Title;
		}
	}

	/// <summary>
	/// Today's day of the week, such as "Aftday".
	/// </summary>
	public string DayOfWeek => VerifyAccess(m_world.GameData.Calendar.GetDayOfWeek(m_world.Time).Title);

	/// <summary>
	/// The rest of today's date, under the day of the week, such as "Mid Winter, 227 CR".
	/// </summary>
	public string SeasonAndYear => VerifyAccess(m_world.GameData.Calendar.FormatSeasonAndYear(m_world.Time));

	/// <summary>
	/// The special days that fall today, such as a festival, or null if today is an ordinary day.
	/// </summary>
	public string? SpecialDay
	{
		get
		{
			VerifyAccess();
			var specialDays = m_world.GameData.Calendar.GetSpecialDays(m_world.Time);
			return specialDays.Count == 0 ? null : string.Join(OurResources.SpecialDaySeparator, specialDays.Select(x => x.Title));
		}
	}

	/// <summary>
	/// The part of the day and the time on the clock, such as "Morning, 6:00".
	/// </summary>
	public string TimeOfDay
	{
		get
		{
			VerifyAccess();
			var calendar = m_world.GameData.Calendar;
			return OurResources.TimeOfDay.FormatCurrentCulture(calendar.GetDayPart(m_world.Time).Title, calendar.FormatClock(m_world.Time));
		}
	}

	/// <summary>
	/// Where the time is on the day dial: how far it is from sunrise to sunset, or while <see cref="IsNight"/>, from
	/// sunset to sunrise, from 0 to 1.
	/// </summary>
	public double DayDialPosition => VerifyAccess(GetDayDial().Position);

	/// <summary>
	/// Whether it's night: from sunset until sunrise. The day dial shows the moon rather than the sun.
	/// </summary>
	public bool IsNight => VerifyAccess(GetDayDial().IsNight);

	/// <summary>
	/// Where noon (or, at night, midnight) is on the day dial, from 0 to 1, or nothing if the sun (or moon) doesn't cross
	/// the dial then.
	/// </summary>
	public IReadOnlyList<double> DayDialMarks
	{
		get
		{
			VerifyAccess();
			var isNight = GetDayDial().IsNight;
			return new[] { c_noon, 0 }
				.Select(GetDayDialPosition)
				.Where(x => x.IsNight == isNight && x.Position > 0 && x.Position < 1)
				.Select(x => x.Position)
				.ToList();
		}
	}

	/// <summary>
	/// The player's next appointment, such as "Next: Mary Smith, 14:30 today, at her home", or null if they have none.
	/// </summary>
	public string? NextAppointment
	{
		get
		{
			VerifyAccess();
			return m_world.GetPlayerAppointments() is [var next, ..] ?
				OurResources.AppointmentNext.FormatCurrentCulture(next.GetOther(m_world.Player).FullName, AppointmentText.FormatWhen(m_world, next.Time), AppointmentText.FormatPlace(m_world, next)) :
				null;
		}
	}

	/// <summary>
	/// How well each of the player's personal needs is met, such as fatigue, as icons that start them: a full bar is a need
	/// that's fully met.
	/// </summary>
	public IReadOnlyList<MeterViewModel> Needs { get; }

	/// <summary>
	/// The mini-map, showing the player's quarter and the quarters connected to it.
	/// </summary>
	public TownMapViewModel MiniMap
	{
		get => VerifyAccess(m_miniMap);
		private set => SetPropertyField(value, ref m_miniMap);
	}

	/// <summary>
	/// Brings everything that depends on the time up to date, once time has passed.
	/// </summary>
	public void OnTimeChanged()
	{
		VerifyAccess();
		RaisePropertyChanged(nameof(DayOfWeek), nameof(SeasonAndYear), nameof(SpecialDay), nameof(TimeOfDay), nameof(DayDialPosition), nameof(IsNight), nameof(DayDialMarks));
		UpdateNeeds();
		OnAppointmentsChanged();
	}

	/// <summary>
	/// Shows the player's location on the mini-map, once they're somewhere else.
	/// </summary>
	public void OnLocationChanged()
	{
		VerifyAccess();
		MiniMap = CreateMiniMap(m_world.Player.Location);
		UpdateNeeds();
	}

	/// <summary>
	/// Shows how the player's needs are going, and whether each can be started now, once their steps may have changed.
	/// </summary>
	public void OnStepsChanged()
	{
		VerifyAccess();
		UpdateNeeds();
	}

	/// <summary>
	/// Shows the player's next appointment, and marks its place on the mini-map, once their appointments may have changed.
	/// </summary>
	public void OnAppointmentsChanged()
	{
		VerifyAccess();
		RaisePropertyChanged(nameof(NextAppointment));
		if (GetAppointmentPlace() != m_miniMapAppointment)
			MiniMap = CreateMiniMap(m_world.Player.Location);
	}

	/// <summary>
	/// Marks the player's destination on the mini-map, once it changes.
	/// </summary>
	public void OnDestinationChanged()
	{
		VerifyAccess();
		MiniMap.SetDestination(m_world.Player.Destination);
	}

	/// <summary>
	/// Gets the place of the player's next appointment, which the maps mark, or null if they have none.
	/// </summary>
	internal Location? GetAppointmentPlace() => m_world.GetPlayerAppointments() is [var next, ..] ? next.Place : null;

	private TownMapViewModel CreateMiniMap(Location location)
	{
		m_miniMapAppointment = GetAppointmentPlace();
		var player = m_world.Player;
		return TownMapViewModel.CreateMiniMap(location, m_world.Features, MiniMapSize, player.Home, player.Business is null ? null : player.Workplace, m_miniMapAppointment, player.Destination);
	}

	private void UpdateNeeds()
	{
		if (Needs.Count != 0)
		{
			var choices = m_world.GetPlayerStartChoices().ToDictionary(x => x.Need);
			var quotas = Needs.Any(x => x.Need.Kind == NeedKind.Quota) ? m_world.GetPlayerQuotaProgress().ToDictionary(x => x.Need) : new Dictionary<Need, PlayerQuotaProgress>();
			foreach (var meter in Needs)
			{
				var choice = choices.GetValueOrDefault(meter.Need);
				if (meter.Need.Kind == NeedKind.Level)
					meter.UpdateLevel(m_world, choice);
				else if (quotas.GetValueOrDefault(meter.Need) is { } quota)
					meter.UpdateQuota(m_world, quota, choice);
			}
		}
	}

	private (double Position, bool IsNight) GetDayDial()
	{
		var parts = m_world.GameData.Calendar.GetParts(m_world.Time);
		return GetDayDialPosition((parts.Hour * GameCalendar.MinutesInHour) + parts.Minute);
	}

	/// <summary>
	/// Gets where a minute of the day is on the day dial: how far from sunrise to sunset, or at night, from sunset to
	/// sunrise.
	/// </summary>
	private (double Position, bool IsNight) GetDayDialPosition(int minute)
	{
		var calendar = m_world.GameData.Calendar;
		var sunrise = calendar.Sunrise * GameCalendar.MinutesInHour;
		var sunset = calendar.Sunset * GameCalendar.MinutesInHour;
		const int minutesInDay = GameCalendar.HoursInDay * GameCalendar.MinutesInHour;
		if (minute >= sunrise && minute < sunset)
			return ((double) (minute - sunrise) / (sunset - sunrise), false);

		var nightLength = minutesInDay - (sunset - sunrise);
		var intoNight = (minute - sunset + minutesInDay) % minutesInDay;
		return ((double) intoNight / nightLength, true);
	}

	private const double MiniMapSize = 196;
	private const int c_noon = 12 * GameCalendar.MinutesInHour;

	private readonly GameWorld m_world;
	private TownMapViewModel m_miniMap;
	private Location? m_miniMapAppointment;
}
