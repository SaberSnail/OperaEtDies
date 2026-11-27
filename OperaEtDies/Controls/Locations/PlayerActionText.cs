using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Time;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// Describes things the player can do: how long they take, and when a need is open.
/// </summary>
internal static class PlayerActionText
{
	/// <summary>
	/// Describes how long something takes from now, such as "1 hour, 30 minutes".
	/// </summary>
	public static string FormatMinutes(GameWorld world, int minutes)
	{
		var calendar = world.GameData.Calendar;
		return calendar.FormatOffsetFrom(world.Time, calendar.CreateTimeOffset(0, 0, minutes), TimeFormat.Long);
	}

	/// <summary>
	/// Describes something that takes time with when it would end and how long it takes, such as "Work until 15:00 (1
	/// hour)".
	/// </summary>
	public static string FormatUntil(GameWorld world, string title, int minutes)
	{
		var calendar = world.GameData.Calendar;
		return OurResources.PlayerActionUntilText.FormatCurrentCulture(title, calendar.FormatClock(world.Time + calendar.CreateTimeOffset(0, 0, minutes)), FormatMinutes(world, minutes));
	}

	/// <summary>
	/// Describes the days and hours a quota need can be met, in the calendar's order of the days, such as "Open on Moonday
	/// from 7:00 to 14:00".
	/// </summary>
	public static string FormatOpeningHours(Need need, GameCalendar calendar)
	{
		var days = need.Days is { } dayIds ? calendar.Days.Where(x => dayIds.Contains(x.Id, StringComparer.Ordinal)).Select(x => x.Title).ToList() : null;
		var dayText = days is null ? OurResources.OpeningHoursEveryDay :
			days.Count == 1 ? days[0] :
			days.Count == 2 ? OurResources.OpeningHoursTwoDays.FormatCurrentCulture(days[0], days[1]) :
			OurResources.OpeningHoursManyDays.FormatCurrentCulture(string.Join(OurResources.OpeningHoursDaySeparator, days.Take(days.Count - 1)), days[^1]);
		var start = OurResources.GameClock.FormatCurrentCulture(need.Start!.Value, 0);
		var end = OurResources.GameClock.FormatCurrentCulture(need.End!.Value, 0);
		return (need.OnSpecialDays ? OurResources.OpeningHours : OurResources.OpeningHoursNotSpecialDays).FormatCurrentCulture(dayText, start, end);
	}
}
