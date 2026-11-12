using System.IO;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// The shape of the game's calendar and the names of its parts, loaded from Calendar.json. A year is made of seasons,
/// each season of weeks, and each week of days. A day always has 24 hours of 60 minutes.
/// </summary>
public sealed class CalendarSettings : IValidatedData
{
	public const string FileName = "Calendar.json";

	/// <summary>
	/// Where the year's number goes in <see cref="YearFormat"/>.
	/// </summary>
	public const string YearPlaceholder = "{year}";

	public static DataLoadResult<CalendarSettings> Load(string dataDirectory) => Load(dataDirectory, []);

	/// <summary>
	/// Loads the file from the data folder, with the changes from the mod folders applied in order.
	/// </summary>
	public static DataLoadResult<CalendarSettings> Load(string dataDirectory, IReadOnlyList<string> modDirectories) =>
		JsonDataLoader.Load<CalendarSettings>(Path.Combine(dataDirectory, FileName), modDirectories, []);

	/// <summary>
	/// The days of the week, in order.
	/// </summary>
	public required IReadOnlyList<CalendarDay> Days { get; init; }

	/// <summary>
	/// The weeks of each season, in order, such as early, mid, and late. A week's title is shown with its season's
	/// title, as in "Early Spring".
	/// </summary>
	public required IReadOnlyList<CalendarWeek> Weeks { get; init; }

	/// <summary>
	/// The seasons of the year, in order.
	/// </summary>
	public required IReadOnlyList<CalendarSeason> Seasons { get; init; }

	/// <summary>
	/// The parts of the day, in order of the hour they start at. Each lasts until the next one starts, and the last lasts
	/// until the first one starts the next day.
	/// </summary>
	public required IReadOnlyList<CalendarDayPart> DayParts { get; init; }

	/// <summary>
	/// The days of the year that have their own name, such as festivals.
	/// </summary>
	public IReadOnlyList<CalendarSpecialDay> SpecialDays { get; init; } = [];

	/// <summary>
	/// The year a new game starts in.
	/// </summary>
	public required int StartYear { get; init; }

	/// <summary>
	/// How a year is shown, with "{year}" where its number goes, such as "{year} CR".
	/// </summary>
	public required string YearFormat { get; init; }

	public IEnumerable<string> Validate()
	{
		var errors = new List<string>();
		errors.AddRange(DataValidation.ValidateIdentifiedItems("days", Days, x => x.Id, x => x.Title));
		errors.AddRange(DataValidation.ValidateIdentifiedItems("weeks", Weeks, x => x.Id, x => x.Title));
		errors.AddRange(DataValidation.ValidateIdentifiedItems("seasons", Seasons, x => x.Id, x => x.Title));
		errors.AddRange(DataValidation.ValidateIdentifiedItems("dayParts", DayParts, x => x.Id, x => x.Title));
		errors.AddRange(ValidateDayPartStarts());
		if (SpecialDays.Count != 0)
		{
			errors.AddRange(DataValidation.ValidateIdentifiedItems("specialDays", SpecialDays, x => x.Id, x => x.Title));
			errors.AddRange(SpecialDays.OfType<CalendarSpecialDay>().SelectMany(ValidateSpecialDay));
		}
		if (StartYear < 1)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("startYear", StartYear, 1));
		if (string.IsNullOrWhiteSpace(YearFormat) || !YearFormat.Contains(YearPlaceholder, StringComparison.Ordinal))
			errors.Add(OurResources.CalendarYearFormatInvalid.FormatCurrentCulture(YearPlaceholder));
		return errors;
	}

	private IEnumerable<string> ValidateDayPartStarts()
	{
		var previousStart = -1;
		foreach (var dayPart in DayParts.OfType<CalendarDayPart>())
		{
			if (dayPart.Start <= previousStart || dayPart.Start >= 24)
				yield return OurResources.CalendarDayPartStartOutOfOrder.FormatCurrentCulture(dayPart.Id, dayPart.Start);
			previousStart = Math.Max(previousStart, dayPart.Start);
		}
	}

	private IEnumerable<string> ValidateSpecialDay(CalendarSpecialDay specialDay)
	{
		if (!Seasons.Any(x => x?.Id == specialDay.Season))
			yield return OurResources.CalendarSpecialDayUnknownPart.FormatCurrentCulture(specialDay.Id, "season", specialDay.Season, "seasons");
		if (!Weeks.Any(x => x?.Id == specialDay.Week))
			yield return OurResources.CalendarSpecialDayUnknownPart.FormatCurrentCulture(specialDay.Id, "week", specialDay.Week, "weeks");
		if (!Days.Any(x => x?.Id == specialDay.Day))
			yield return OurResources.CalendarSpecialDayUnknownPart.FormatCurrentCulture(specialDay.Id, "day", specialDay.Day, "days");
	}
}

/// <summary>
/// A day of the week.
/// </summary>
public sealed class CalendarDay : IIdentified
{
	public required string Id { get; init; }

	public required string Title { get; init; }
}

/// <summary>
/// A week of each season, such as the early weeks.
/// </summary>
public sealed class CalendarWeek : IIdentified
{
	public required string Id { get; init; }

	public required string Title { get; init; }
}

/// <summary>
/// A season of the year.
/// </summary>
public sealed class CalendarSeason : IIdentified
{
	public required string Id { get; init; }

	public required string Title { get; init; }
}

/// <summary>
/// A part of the day, such as morning, which lasts until the next part starts.
/// </summary>
public sealed class CalendarDayPart : IIdentified
{
	public required string Id { get; init; }

	public required string Title { get; init; }

	/// <summary>
	/// The hour the part of the day starts at, from 0 to 23.
	/// </summary>
	public required int Start { get; init; }
}

/// <summary>
/// A day of the year that has its own name, such as a festival. It falls on the same day every year.
/// </summary>
public sealed class CalendarSpecialDay : IIdentified
{
	public required string Id { get; init; }

	public required string Title { get; init; }

	/// <summary>
	/// The id of the season it's in.
	/// </summary>
	public required string Season { get; init; }

	/// <summary>
	/// The id of the week of the season it's in.
	/// </summary>
	public required string Week { get; init; }

	/// <summary>
	/// The id of the day of the week it's on.
	/// </summary>
	public required string Day { get; init; }
}
