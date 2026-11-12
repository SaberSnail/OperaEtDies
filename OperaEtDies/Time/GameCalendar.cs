using System.Globalization;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;

namespace OperaEtDies.Time;

/// <summary>
/// The game's calendar. A year is made of seasons, each season of weeks, and each week of days, as the data defines
/// them. A day has 24 hours of 60 minutes, and minutes are the smallest part of the game's time. Years count from 1.
/// </summary>
public sealed class GameCalendar : CalendarBase
{
	public const int HoursInDay = 24;
	public const int MinutesInHour = 60;

	public GameCalendar(CalendarSettings settings)
	{
		// The data is checked when it's loaded, so this only guards against settings created in code.
		if (settings.Days.Count == 0 || settings.Weeks.Count == 0 || settings.Seasons.Count == 0 || settings.DayParts.Count == 0)
			throw new ArgumentException("The calendar needs at least one day, week, season, and part of the day.", nameof(settings));

		m_settings = settings;
		DaysInYear = DaysInWeek * WeeksInSeason * SeasonsInYear;
	}

	public int DaysInWeek => m_settings.Days.Count;

	public int WeeksInSeason => m_settings.Weeks.Count;

	public int SeasonsInYear => m_settings.Seasons.Count;

	public int DaysInYear { get; }

	/// <summary>
	/// The year a new game starts in.
	/// </summary>
	public int StartYear => m_settings.StartYear;

	/// <summary>
	/// The parts of the day, in order of the hour they start at.
	/// </summary>
	public IReadOnlyList<CalendarDayPart> DayParts => m_settings.DayParts;

	/// <summary>
	/// Creates a time. The season, the week within the season, and the day within the week count from 1.
	/// </summary>
	public TimePoint CreateTimePoint(int year, int season, int week, int day, int hour = 0, int minute = 0)
	{
		ThrowIfOutOfRange(year, 1, int.MaxValue, nameof(year));
		ThrowIfOutOfRange(season, 1, SeasonsInYear, nameof(season));
		ThrowIfOutOfRange(week, 1, WeeksInSeason, nameof(week));
		ThrowIfOutOfRange(day, 1, DaysInWeek, nameof(day));
		ThrowIfOutOfRange(hour, 0, HoursInDay - 1, nameof(hour));
		ThrowIfOutOfRange(minute, 0, MinutesInHour - 1, nameof(minute));

		var totalDays = ((year - 1L) * DaysInYear) + ((season - 1L) * WeeksInSeason * DaysInWeek) + ((week - 1L) * DaysInWeek) + (day - 1L);
		return CreateTimePointFromTotalSeconds((totalDays * c_secondsInDay) + (hour * c_secondsInHour) + (minute * c_secondsInMinute));
	}

	/// <summary>
	/// Creates a length of time. The parts can be negative, and can be larger than the next larger part.
	/// </summary>
	public TimeOffset CreateTimeOffset(long days, long hours, long minutes) =>
		CreateTimeOffsetFromTotalSeconds((days * c_secondsInDay) + (hours * c_secondsInHour) + (minutes * c_secondsInMinute));

	/// <summary>
	/// Gets midnight at the start of the time's day.
	/// </summary>
	public TimePoint GetStartOfDay(TimePoint point) =>
		CreateTimePointFromTotalSeconds(GetTotalSeconds(point) / c_secondsInDay * c_secondsInDay);

	public GameTimePointParts GetParts(TimePoint point)
	{
		var totalSeconds = GetTotalSeconds(point);
		var totalDays = totalSeconds / c_secondsInDay;
		var dayOfYear = (int) (totalDays % DaysInYear);
		var daysInSeason = WeeksInSeason * DaysInWeek;
		return new GameTimePointParts(
			Year: (int) (totalDays / DaysInYear) + 1,
			Season: (dayOfYear / daysInSeason) + 1,
			Week: (dayOfYear % daysInSeason / DaysInWeek) + 1,
			Day: (dayOfYear % DaysInWeek) + 1,
			Hour: (int) (totalSeconds % c_secondsInDay / c_secondsInHour),
			Minute: (int) (totalSeconds % c_secondsInHour / c_secondsInMinute));
	}

	public GameTimeOffsetParts GetParts(TimeOffset offset)
	{
		var totalSeconds = GetTotalSeconds(offset);
		return new GameTimeOffsetParts(
			Days: totalSeconds / c_secondsInDay,
			Hours: (int) (totalSeconds % c_secondsInDay / c_secondsInHour),
			Minutes: (int) (totalSeconds % c_secondsInHour / c_secondsInMinute));
	}

	public CalendarSeason GetSeason(TimePoint point) => m_settings.Seasons[GetParts(point).Season - 1];

	public CalendarWeek GetWeek(TimePoint point) => m_settings.Weeks[GetParts(point).Week - 1];

	public CalendarDay GetDayOfWeek(TimePoint point) => m_settings.Days[GetParts(point).Day - 1];

	/// <summary>
	/// Gets the part of the day the time is in: the last one that starts at or before its hour, or else the last one of
	/// the day before, which lasts past midnight.
	/// </summary>
	public CalendarDayPart GetDayPart(TimePoint point)
	{
		var hour = GetParts(point).Hour;
		return m_settings.DayParts.LastOrDefault(x => x.Start <= hour) ?? m_settings.DayParts[^1];
	}

	/// <summary>
	/// Gets the special days that fall on the time's day.
	/// </summary>
	public IReadOnlyList<CalendarSpecialDay> GetSpecialDays(TimePoint point)
	{
		var season = GetSeason(point).Id;
		var week = GetWeek(point).Id;
		var day = GetDayOfWeek(point).Id;
		return m_settings.SpecialDays.Where(x => x.Season == season && x.Week == week && x.Day == day).ToList();
	}

	/// <summary>
	/// Shows a year the way the data says to, such as "227 CR".
	/// </summary>
	public string FormatYear(int year) =>
		m_settings.YearFormat.Replace(CalendarSettings.YearPlaceholder, year.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal);

	/// <summary>
	/// Shows the date of a time, with its year, such as "Thursday, Mid Winter, 227 CR".
	/// </summary>
	public string FormatDate(TimePoint point)
	{
		var parts = GetParts(point);
		return OurResources.GameDate.FormatCurrentCulture(m_settings.Days[parts.Day - 1].Title, m_settings.Weeks[parts.Week - 1].Title,
			m_settings.Seasons[parts.Season - 1].Title, FormatYear(parts.Year));
	}

	/// <summary>
	/// Shows the time of day as a clock would, such as "6:00".
	/// </summary>
	public string FormatClock(TimePoint point)
	{
		var parts = GetParts(point);
		return OurResources.GameClock.FormatCurrentCulture(parts.Hour, parts.Minute);
	}

	/// <summary>
	/// Formats a time. The long format includes the year.
	/// </summary>
	public override string FormatTime(TimePoint point, TimeFormat format)
	{
		var parts = GetParts(point);
		var day = m_settings.Days[parts.Day - 1].Title;
		var week = m_settings.Weeks[parts.Week - 1].Title;
		var season = m_settings.Seasons[parts.Season - 1].Title;
		return format == TimeFormat.Long
			? OurResources.GameTimeLong.FormatCurrentCulture(day, week, season, FormatYear(parts.Year), parts.Hour, parts.Minute)
			: OurResources.GameTimeShort.FormatCurrentCulture(day, week, season, parts.Hour, parts.Minute);
	}

	/// <summary>
	/// Formats a length of time in days, hours, and minutes, leaving out the parts that are zero. The time it starts
	/// from doesn't change how it's formatted.
	/// </summary>
	/// <exception cref="ArgumentException">The offset is negative.</exception>
	public override string FormatOffsetFrom(TimePoint point, TimeOffset offset, TimeFormat format)
	{
		if (GetTotalSeconds(offset) < 0)
			throw new ArgumentException("The offset must not be negative.", nameof(offset));

		var parts = GetParts(offset);
		var isLong = format == TimeFormat.Long;
		var terms = new List<string>();
		if (parts.Days != 0)
			terms.Add(FormatOffsetTerm(isLong ? "GameOffsetDayLong" : "GameOffsetDayShort", parts.Days));
		if (parts.Hours != 0)
			terms.Add(FormatOffsetTerm(isLong ? "GameOffsetHourLong" : "GameOffsetHourShort", parts.Hours));
		if (parts.Minutes != 0 || terms.Count == 0)
			terms.Add(FormatOffsetTerm(isLong ? "GameOffsetMinuteLong" : "GameOffsetMinuteShort", parts.Minutes));

		var join = isLong ? OurResources.GameOffsetJoinLong : OurResources.GameOffsetJoinShort;
		return terms.Aggregate((output, term) => join.FormatCurrentCulture(output, term));
	}

	protected override bool IsSupported(TimePoint point) => GetTotalSeconds(point) >= 0;

	private static string FormatOffsetTerm(string baseKey, long value) =>
		OurResources.ResourceManager.Pluralize(baseKey, value).FormatCurrentCulture(value);

	private static void ThrowIfOutOfRange(int value, int minimum, int maximum, string name)
	{
		if (value < minimum || value > maximum)
			throw new ArgumentOutOfRangeException(name, value, $"{name} must be between {minimum} and {maximum}.");
	}

	private const long c_secondsInMinute = 60;
	private const long c_secondsInHour = c_secondsInMinute * MinutesInHour;
	private const long c_secondsInDay = c_secondsInHour * HoursInDay;

	private readonly CalendarSettings m_settings;
}
