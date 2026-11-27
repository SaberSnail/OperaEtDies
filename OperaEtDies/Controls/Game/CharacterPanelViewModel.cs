using GoldenAnvil.Utility;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Controls.Map;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Time;

namespace OperaEtDies.Controls.Game;

/// <summary>
/// The left column during play, showing the state of the player's character: who they are, the date and time, their next
/// appointment, their needs, their work and their household's chores, and the mini-map. The game screen tells it when the
/// time, the player's location, or their appointments change.
/// </summary>
public sealed class CharacterPanelViewModel : ViewModelBase
{
	public CharacterPanelViewModel(GameWorld world)
	{
		m_world = world;
		Player = new LocationPersonViewModel(world.Player, world.GameData.PortraitRenderer);
		m_miniMap = CreateMiniMap(world.Player.Location);

		// The needs shown to the player are the level needs with words for their levels.
		Needs = world.GameData.Schedules.Needs
			.Where(x => x.Kind == NeedKind.Level && x.Levels is { Count: > 0 })
			.Select(x => new MeterViewModel(x, world.GameData.GetIcon(x.Icon)))
			.ToList();
		UpdateMeters();
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
	/// Today's date, such as "Aftday, Mid Winter, 227 CR".
	/// </summary>
	public string Date => VerifyAccess(m_world.GameData.Calendar.FormatDate(m_world.Time));

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
	/// Where the time is on the day dial: how far through the day it is (from the start of the first part of the day to the
	/// start of the last), or while <see cref="IsNight"/>, how far through the night, from 0 to 1.
	/// </summary>
	public double DayDialPosition => VerifyAccess(GetDayDial().Position);

	/// <summary>
	/// Whether it's night: the last part of the day, or before the first part starts. The day dial shows the moon rather
	/// than the sun.
	/// </summary>
	public bool IsNight => VerifyAccess(GetDayDial().IsNight);

	/// <summary>
	/// Where each part of the day between the first and the last starts on the day dial, from 0 to 1.
	/// </summary>
	public IReadOnlyList<double> DayDialMarks
	{
		get
		{
			VerifyAccess();
			var (dayStart, nightStart) = GetDayHours();
			return nightStart <= dayStart ? [] : m_world.GameData.Calendar.DayParts.Skip(1).SkipLast(1).Select(x => (double) (x.Start - dayStart) / (nightStart - dayStart)).ToList();
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
	/// How well each need shown to the player is met, such as fatigue, as icons: a full bar is a need that's fully met.
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
		RaisePropertyChanged(nameof(Date), nameof(SpecialDay), nameof(TimeOfDay), nameof(DayDialPosition), nameof(IsNight));
		UpdateMeters();
		OnAppointmentsChanged();
	}

	/// <summary>
	/// Shows the player's location on the mini-map, once they're somewhere else.
	/// </summary>
	public void OnLocationChanged()
	{
		VerifyAccess();
		MiniMap = CreateMiniMap(m_world.Player.Location);
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

	private void UpdateMeters()
	{
		foreach (var meter in Needs)
		{
			var need = meter.Need;
			var level = m_world.Player.GetLevel(need);
			meter.Update(1 - (level / Need.MaximumLevel), OurResources.NeedToolTip.FormatCurrentCulture(need.Title, need.GetLevelTitle(level)), isWarning: need.IsWorstLevel(level));
		}
	}

	/// <summary>
	/// Gets the hours the day starts and the night starts: the starts of the first and last parts of the day. With fewer
	/// than two parts of the day, there's no night.
	/// </summary>
	private (int DayStart, int NightStart) GetDayHours()
	{
		var dayParts = m_world.GameData.Calendar.DayParts;
		return dayParts.Count < 2 ? (0, GameCalendar.HoursInDay) : (dayParts[0].Start, dayParts[^1].Start);
	}

	private (double Position, bool IsNight) GetDayDial()
	{
		var calendar = m_world.GameData.Calendar;
		var parts = calendar.GetParts(m_world.Time);
		var minute = (parts.Hour * GameCalendar.MinutesInHour) + parts.Minute;
		var (dayStart, nightStart) = GetDayHours();
		var dayStartMinute = dayStart * GameCalendar.MinutesInHour;
		var nightStartMinute = nightStart * GameCalendar.MinutesInHour;
		const int minutesInDay = GameCalendar.HoursInDay * GameCalendar.MinutesInHour;
		if (minute >= dayStartMinute && minute < nightStartMinute)
			return ((double) (minute - dayStartMinute) / (nightStartMinute - dayStartMinute), false);

		var nightLength = minutesInDay - (nightStartMinute - dayStartMinute);
		var intoNight = (minute - nightStartMinute + minutesInDay) % minutesInDay;
		return ((double) intoNight / nightLength, true);
	}

	private const double MiniMapSize = 196;

	private readonly GameWorld m_world;
	private TownMapViewModel m_miniMap;
	private Location? m_miniMapAppointment;
}
