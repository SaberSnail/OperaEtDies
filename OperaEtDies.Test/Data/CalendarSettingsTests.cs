using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class CalendarSettingsTests
{
	[Test]
	public void LoadsShippedCalendar()
	{
		var result = CalendarSettings.Load(JsonDataLoader.DefaultDataDirectory);

		Assert.That(result.Errors, Is.Empty);
		Assert.That(result.Value!.Days, Is.Not.Empty);
	}

	[Test]
	public void ValidSettingsHaveNoErrors()
	{
		Assert.That(TestData.CreateCalendarSettings().Validate(), Is.Empty);
	}

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

	private static CalendarDayPart DayPart(string id, int start) => new() { Id = id, Title = id, Start = start };

	private static CalendarSettings With(IReadOnlyList<CalendarDayPart>? dayParts = null, IReadOnlyList<CalendarSpecialDay>? specialDays = null, int? startYear = null, string? yearFormat = null)
	{
		var settings = TestData.CreateCalendarSettings();
		return new CalendarSettings
		{
			Days = settings.Days,
			Weeks = settings.Weeks,
			Seasons = settings.Seasons,
			DayParts = dayParts ?? settings.DayParts,
			SpecialDays = specialDays ?? settings.SpecialDays,
			StartYear = startYear ?? settings.StartYear,
			YearFormat = yearFormat ?? settings.YearFormat,
		};
	}
}
