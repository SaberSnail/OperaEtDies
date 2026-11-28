using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class CalendarSettingsTests
{
	[Test]
	public void EmptySpecialDaysAreValid()
	{
		var settings = With(specialDays: []);
		Assert.That(settings.Validate(), Is.Empty);
	}

	[Test]
	public void EmptyListsAreErrors()
	{
		var valid = TestData.CreateCalendarSettings();
		var settings = new CalendarSettings { Days = [], Weeks = [], Seasons = [], DayParts = [], SpecialDays = [], StartYear = 1, YearFormat = "{year}" };
		Assert.That(settings.Validate().Count(), Is.EqualTo(4), string.Join("\n", settings.Validate()));
		Assert.That(valid.Validate(), Is.Empty);
	}

	[Test]
	public void DayPartsCanStartAfterMidnight()
	{
		var settings = With(dayParts: [DayPart("morning", 6), DayPart("night", 21)]);
		Assert.That(settings.Validate(), Is.Empty);
	}

	[Test]
	public void FirstDayPartCantStartBeforeMidnight()
	{
		var settings = With(dayParts: [DayPart("early", -1), DayPart("morning", 6)]);
		Assert.That(settings.Validate(), Has.Exactly(1).Contains("\"early\""));
	}

	[TestCase(6)]
	[TestCase(5)]
	[TestCase(24)]
	public void DayPartsMustStartInOrder(int start)
	{
		var settings = With(dayParts: [DayPart("night", 0), DayPart("morning", 6), DayPart("late", start)]);
		Assert.That(settings.Validate(), Has.Exactly(1).Contains("\"late\""));
	}

	[Test]
	public void DayPartAnnouncementsMustBeFilledIn()
	{
		var settings = With(dayParts: [DayPart("morning", 6), new CalendarDayPart { Id = "night", Title = "Night", Start = 21, Announcement = TestData.Text(" ") }]);
		Assert.That(settings.Validate(), Has.Exactly(1).Contains("dayParts[night].announcement"));
	}

	[Test]
	public void SpecialDaysMustUseKnownParts()
	{
		var settings = With(specialDays: [new CalendarSpecialDay { Id = "feast", Title = "Feast", Season = "monsoon", Week = "first", Day = "day9" }]);
		var errors = settings.Validate().ToList();
		Assert.Multiple(() =>
		{
			Assert.That(errors, Has.Count.EqualTo(3));
			Assert.That(errors, Has.Exactly(1).Contains("\"monsoon\""));
			Assert.That(errors, Has.Exactly(1).Contains("\"first\""));
			Assert.That(errors, Has.Exactly(1).Contains("\"day9\""));
		});
	}

	[TestCase(0)]
	[TestCase(-3)]
	public void StartYearMustBeAtLeastOne(int startYear)
	{
		var settings = With(startYear: startYear);
		Assert.That(settings.Validate(), Has.Exactly(1).Contains("startYear"));
	}

	[TestCase("")]
	[TestCase("CR")]
	[TestCase("{0} CR")]
	public void YearFormatMustIncludeTheYear(string yearFormat)
	{
		var settings = With(yearFormat: yearFormat);
		Assert.That(settings.Validate(), Has.Exactly(1).Contains("yearFormat"));
	}

	[TestCase(6, 18, true)]
	[TestCase(0, 24, true)]
	[TestCase(-1, 18, false)]
	[TestCase(24, 25, false)]
	[TestCase(12, 12, false)]
	[TestCase(18, 6, false)]
	[TestCase(6, 25, false)]
	public void SunsetMustBeAfterSunrise(int sunrise, int sunset, bool isValid)
	{
		var settings = With(sunrise: sunrise, sunset: sunset, skies: []);
		Assert.That(settings.Validate(), isValid ? Is.Empty : Has.Exactly(1).Contains("sets at"));
	}

	[Test]
	public void SkiesMustNotStartAtTheSameTime()
	{
		// Seven hours after sunrise is five hours before sunset.
		var settings = With(skies: [Sky("a", CalendarSkyAnchor.Sunrise, 420), Sky("b", CalendarSkyAnchor.Sunset, -300)]);
		Assert.That(settings.Validate(), Has.Exactly(1).Contains("\"b\"").And.Contains("\"a\""));
	}

	[Test]
	public void SkyMinutesMustBeLessThanADay()
	{
		var settings = With(skies: [Sky("a", CalendarSkyAnchor.Sunrise, -1440)]);
		Assert.That(settings.Validate(), Has.Exactly(1).Contains("\"a\""));
	}

	[TestCase(CalendarSkyAnchor.Sunrise, -60, 300)]
	[TestCase(CalendarSkyAnchor.Sunset, 60, 1140)]
	[TestCase(CalendarSkyAnchor.Sunrise, -480, 1320)]
	[TestCase(CalendarSkyAnchor.Sunset, 420, 60)]
	public void SkyStartWrapsAroundTheDay(CalendarSkyAnchor from, int minutes, int expected) =>
		Assert.That(With().GetSkyStart(Sky("a", from, minutes)), Is.EqualTo(expected));

	private static CalendarSky Sky(string id, CalendarSkyAnchor from, int minutes) => new() { Id = id, From = from, Minutes = minutes };

	private static CalendarDayPart DayPart(string id, int start) => new() { Id = id, Title = id, Start = start };

	private static CalendarSettings With(IReadOnlyList<CalendarDayPart>? dayParts = null, IReadOnlyList<CalendarSpecialDay>? specialDays = null, int? startYear = null, string? yearFormat = null, IReadOnlyList<CalendarSky>? skies = null, int sunrise = 6, int sunset = 18)
	{
		var settings = TestData.CreateCalendarSettings();
		return new CalendarSettings
		{
			Days = settings.Days,
			Weeks = settings.Weeks,
			Seasons = settings.Seasons,
			DayParts = dayParts ?? settings.DayParts,
			SpecialDays = specialDays ?? settings.SpecialDays,
			Skies = skies ?? settings.Skies,
			Sunrise = sunrise,
			Sunset = sunset,
			StartYear = startYear ?? settings.StartYear,
			YearFormat = yearFormat ?? settings.YearFormat,
		};
	}
}
