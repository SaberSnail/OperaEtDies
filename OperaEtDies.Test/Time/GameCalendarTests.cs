using System.Globalization;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.Time;

namespace OperaEtDies.Test.Time;

[TestFixture]
public sealed class GameCalendarTests
{
	[OneTimeSetUp]
	public void OneTimeSetUp()
	{
		m_originalCulture = CultureInfo.CurrentCulture;
		m_originalUICulture = CultureInfo.CurrentUICulture;
		CultureInfo.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
		CultureInfo.CurrentUICulture = CultureInfo.CreateSpecificCulture("en-US");
	}

	[OneTimeTearDown]
	public void OneTimeTearDown()
	{
		CultureInfo.CurrentCulture = m_originalCulture;
		CultureInfo.CurrentUICulture = m_originalUICulture;
	}

	[SetUp]
	public void SetUp()
	{
		m_calendar = new GameCalendar(TestData.CreateCalendarSettings());
	}

	[Test]
	public void YearHasDaysOfEverySeason()
	{
		Assert.Multiple(() =>
		{
			Assert.That(m_calendar.DaysInWeek, Is.EqualTo(7));
			Assert.That(m_calendar.WeeksInSeason, Is.EqualTo(3));
			Assert.That(m_calendar.SeasonsInYear, Is.EqualTo(4));
			Assert.That(m_calendar.DaysInYear, Is.EqualTo(84));
		});
	}

	[TestCase(1, 1, 1, 1, 0, 0)]
	[TestCase(1, 4, 3, 7, 23, 59)]
	[TestCase(2, 2, 3, 4, 9, 15)]
	[TestCase(1000, 3, 1, 7, 12, 0)]
	public void PartsMatchCreatedTime(int year, int season, int week, int day, int hour, int minute)
	{
		var point = m_calendar.CreateTimePoint(year, season, week, day, hour, minute);
		Assert.That(m_calendar.GetParts(point), Is.EqualTo(new GameTimePointParts(year, season, week, day, hour, minute)));
	}

	[TestCase(1, 1, 1, 7, 1, 1, 2, 1)]
	[TestCase(1, 1, 3, 7, 1, 2, 1, 1)]
	[TestCase(1, 4, 3, 7, 2, 1, 1, 1)]
	public void NextDayRollsOver(int year, int season, int week, int day, int nextYear, int nextSeason, int nextWeek, int nextDay)
	{
		var point = m_calendar.CreateTimePoint(year, season, week, day) + m_calendar.CreateTimeOffset(1, 0, 0);
		Assert.That(point, Is.EqualTo(m_calendar.CreateTimePoint(nextYear, nextSeason, nextWeek, nextDay)));
	}

	[Test]
	public void EveryDayOfTheYearIsInOrder()
	{
		var point = m_calendar.CreateTimePoint(3, 1, 1, 1);
		var oneDay = m_calendar.CreateTimeOffset(1, 0, 0);
		for (var season = 1; season <= 4; season++)
		{
			for (var week = 1; week <= 3; week++)
			{
				for (var day = 1; day <= 7; day++)
				{
					Assert.That(m_calendar.GetParts(point), Is.EqualTo(new GameTimePointParts(3, season, week, day, 0, 0)));
					point += oneDay;
				}
			}
		}
		Assert.That(m_calendar.GetParts(point), Is.EqualTo(new GameTimePointParts(4, 1, 1, 1, 0, 0)));
	}

	[TestCase(0, 1, 1, 1, 0, 0)]
	[TestCase(1, 0, 1, 1, 0, 0)]
	[TestCase(1, 5, 1, 1, 0, 0)]
	[TestCase(1, 1, 0, 1, 0, 0)]
	[TestCase(1, 1, 4, 1, 0, 0)]
	[TestCase(1, 1, 1, 0, 0, 0)]
	[TestCase(1, 1, 1, 8, 0, 0)]
	[TestCase(1, 1, 1, 1, -1, 0)]
	[TestCase(1, 1, 1, 1, 24, 0)]
	[TestCase(1, 1, 1, 1, 0, -1)]
	[TestCase(1, 1, 1, 1, 0, 60)]
	public void OutOfRangeTimeThrows(int year, int season, int week, int day, int hour, int minute)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => m_calendar.CreateTimePoint(year, season, week, day, hour, minute));
	}

	[TestCase(0, 0, 0, 0, 0, 0)]
	[TestCase(1, 2, 3, 1, 2, 3)]
	[TestCase(0, 25, 75, 1, 2, 15)]
	[TestCase(0, 0, -90, 0, -1, -30)]
	public void OffsetParts(int days, int hours, int minutes, long expectedDays, int expectedHours, int expectedMinutes)
	{
		var offset = m_calendar.CreateTimeOffset(days, hours, minutes);
		Assert.That(m_calendar.GetParts(offset), Is.EqualTo(new GameTimeOffsetParts(expectedDays, expectedHours, expectedMinutes)));
	}

	[Test]
	public void StartOfDayIsMidnight()
	{
		var point = m_calendar.CreateTimePoint(2, 3, 2, 5, 17, 45);
		Assert.That(m_calendar.GetStartOfDay(point), Is.EqualTo(m_calendar.CreateTimePoint(2, 3, 2, 5)));
	}

	[Test]
	public void NamedPartsComeFromSettings()
	{
		var point = m_calendar.CreateTimePoint(2, 3, 2, 5, 17, 45);
		Assert.Multiple(() =>
		{
			Assert.That(m_calendar.GetSeason(point).Id, Is.EqualTo("autumn"));
			Assert.That(m_calendar.GetWeek(point).Id, Is.EqualTo("mid"));
			Assert.That(m_calendar.GetDayOfWeek(point).Id, Is.EqualTo("day5"));
		});
	}

	[TestCase(TimeFormat.Short, "Day 5, Mid Autumn, 9:05")]
	[TestCase(TimeFormat.Long, "Day 5, Mid Autumn, year 2, 9:05")]
	public void FormatsTime(TimeFormat format, string expected)
	{
		var point = m_calendar.CreateTimePoint(2, 3, 2, 5, 9, 5);
		Assert.That(m_calendar.FormatTime(point, format), Is.EqualTo(expected));
	}

	[Test]
	public void FormatsYearFromSettings()
	{
		var settings = TestData.CreateCalendarSettings();
		var calendar = new GameCalendar(new CalendarSettings
		{
			Days = settings.Days,
			Weeks = settings.Weeks,
			Seasons = settings.Seasons,
			DayParts = settings.DayParts,
			StartYear = 227,
			YearFormat = "{year} CR",
		});

		Assert.Multiple(() =>
		{
			Assert.That(calendar.FormatYear(227), Is.EqualTo("227 CR"));
			Assert.That(calendar.FormatTime(calendar.CreateTimePoint(1227, 1, 1, 1, 6, 0), TimeFormat.Long), Is.EqualTo("Day 1, Early Spring, 1227 CR, 6:00"));
		});
	}

	[TestCase(0, 0, 15, TimeFormat.Long, "15 minutes")]
	[TestCase(0, 1, 0, TimeFormat.Long, "1 hour")]
	[TestCase(0, 1, 15, TimeFormat.Long, "1 hour, 15 minutes")]
	[TestCase(2, 0, 1, TimeFormat.Long, "2 days, 1 minute")]
	[TestCase(0, 0, 0, TimeFormat.Long, "0 minutes")]
	[TestCase(1, 2, 30, TimeFormat.Short, "1d 2h 30m")]
	public void FormatsOffset(int days, int hours, int minutes, TimeFormat format, string expected)
	{
		var point = m_calendar.CreateTimePoint(1, 1, 1, 1);
		var offset = m_calendar.CreateTimeOffset(days, hours, minutes);
		Assert.That(m_calendar.FormatOffsetFrom(point, offset, format), Is.EqualTo(expected));
	}

	[Test]
	public void FormattingNegativeOffsetThrows()
	{
		var point = m_calendar.CreateTimePoint(1, 1, 1, 1);
		Assert.Throws<ArgumentException>(() => m_calendar.FormatOffsetFrom(point, m_calendar.CreateTimeOffset(0, 0, -1), TimeFormat.Long));
	}

	[Test]
	public void SavesAndLoads()
	{
		var point = m_calendar.CreateTimePoint(3, 2, 1, 6, 14, 30);
		Assert.That(m_calendar.LoadFromString(point.SaveToString()), Is.EqualTo(point));
	}

	[Test]
	public void LoadingTimeBeforeFirstYearThrows()
	{
		var beforeStart = m_calendar.CreateTimePoint(1, 1, 1, 1) - m_calendar.CreateTimeOffset(0, 0, 1);
		Assert.Throws<ArgumentOutOfRangeException>(() => m_calendar.LoadFromString(beforeStart.SaveToString()));
	}

	[Test]
	public void EmptySettingsThrow()
	{
		var settings = TestData.CreateCalendarSettings();
		Assert.Throws<ArgumentException>(() => new GameCalendar(new CalendarSettings { Days = [], Weeks = settings.Weeks, Seasons = settings.Seasons, DayParts = settings.DayParts, StartYear = 1, YearFormat = settings.YearFormat }));
	}

	[TestCase(0, 0, "night")]
	[TestCase(21, 0, "night")]
	[TestCase(5, 59, "night")]
	[TestCase(6, 0, "morning")]
	[TestCase(11, 59, "morning")]
	[TestCase(12, 0, "afternoon")]
	[TestCase(18, 0, "evening")]
	[TestCase(20, 59, "evening")]
	[TestCase(23, 59, "night")]
	public void DayPartIsTheLastToStartWrappingPastMidnight(int hour, int minute, string expectedId)
	{
		var point = m_calendar.CreateTimePoint(1, 1, 1, 1, hour, minute);
		Assert.That(m_calendar.GetDayPart(point).Id, Is.EqualTo(expectedId));
	}

	[Test]
	public void SpecialDaysFallOnTheirDayEveryYear()
	{
		Assert.Multiple(() =>
		{
			Assert.That(m_calendar.GetSpecialDays(m_calendar.CreateTimePoint(1, 2, 2, 4, 15, 0)).Select(x => x.Id), Is.EqualTo(new[] { "midsummer" }));
			Assert.That(m_calendar.GetSpecialDays(m_calendar.CreateTimePoint(5, 2, 2, 4)).Select(x => x.Id), Is.EqualTo(new[] { "midsummer" }));
			Assert.That(m_calendar.GetSpecialDays(m_calendar.CreateTimePoint(1, 2, 2, 5)), Is.Empty);
			Assert.That(m_calendar.GetSpecialDays(m_calendar.CreateTimePoint(1, 3, 2, 4)), Is.Empty);
		});
	}

	private GameCalendar m_calendar = null!;
	private CultureInfo m_originalCulture = null!;
	private CultureInfo m_originalUICulture = null!;
}
