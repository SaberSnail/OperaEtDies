using GoldenAnvil.Utility;
using OperaEtDies.Data;
using static OperaEtDies.Test.TestData;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class ScheduleSettingsTests
{
	[Test]
	public void LoadsShippedSchedules()
	{
		var result = GameData.Load(JsonDataLoader.DefaultDataDirectory);

		Assert.That(result.Errors, Is.Empty);
		Assert.That(result.Value!.Schedules.Needs, Is.Not.Empty);
		Assert.That(result.Value.Schedules.SleepNeed, Is.Not.Null);
	}

	[Test]
	public void ValidSettingsHaveNoErrors()
	{
		Assert.That(CreateScheduleSettings().Validate(), Is.Empty);
	}

	[Test]
	public void NeedsAreLevelOrQuota()
	{
		var both = new Need { Id = "both", Title = "Both", PlaceSetting = Home, Level = Level(), Quota = Timed(60) };
		var neither = new Need { Id = "neither", Title = "Neither", PlaceSetting = Home };
		var noHours = new Need { Id = "noHours", Title = "No hours", PlaceSetting = Home, Quota = Timed(60) };
		var levelDays = new Need { Id = "levelDays", Title = "Level days", PlaceSetting = Home, Hours = new Hours { Start = 6, End = 12, Days = ["day1"] }, Level = Level() };

		Assert.That(With(both, neither, noHours, levelDays).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.NeedKindInvalid.FormatCurrentCulture("both"),
			OurResources.NeedKindInvalid.FormatCurrentCulture("neither"),
			OurResources.NeedSettingMissing.FormatCurrentCulture("noHours", "hours", "quota"),
			OurResources.HoursSettingNotAllowed.FormatCurrentCulture("needs[levelDays].hours.days"),
		}));
	}

	[Test]
	public void QuotasAreMeasuredInTimeOrTrips()
	{
		var mixed = Quota("mixed", Home, new QuotaNeedSettings { Lead = 60, Time = new QuotaTime { Minutes = Amount(60), Shift = 60 }, Trips = new QuotaTrips { Visits = Amount(1), Minutes = Amount(5) } });
		var unshared = Quota("unshared", Home, new QuotaNeedSettings { Lead = 60, Time = new QuotaTime { Minutes = Amount(60, 10), Shift = 60 } });
		var homeward = Quota("homeward", Home, Timed(60), returnHome: true);
		var stayAway = Quota("stayAway", Quarters(false, "square"), new QuotaNeedSettings { Lead = 60, Trips = new QuotaTrips { Visits = Amount(1), Minutes = Amount(5), Home = new QuotaTripHome { Minutes = Amount(10) } } });
		var noTime = Quota("noTime", Quarters(false, "square"), new QuotaNeedSettings { Lead = 60, Trips = new QuotaTrips { Visits = Amount(1), Minutes = Amount(0) } });
		var fraction = Quota("fraction", Home, new QuotaNeedSettings { Lead = 60, Time = new QuotaTime { Minutes = Amount(60.5), Shift = 60 } });

		Assert.That(With(mixed, unshared, homeward, stayAway, noTime, fraction).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.NeedQuotaMeasureInvalid.FormatCurrentCulture("mixed"),
			OurResources.NeedPerMemberNotShared.FormatCurrentCulture("unshared"),
			OurResources.NeedSettingNotAllowed.FormatCurrentCulture("homeward", "returnHome", "home"),
			OurResources.NeedHomeMinutesWithoutReturn.FormatCurrentCulture("stayAway"),
			OurResources.NeedVisitMinutesNotPositive.FormatCurrentCulture("noTime"),
			OurResources.DataNumberNotWhole.FormatCurrentCulture("needs[fraction].quota.time.minutes.base", 60.5),
		}));
	}

	[Test]
	public void OverdueAndActivitiesMustFitTheNeed()
	{
		var daily = Quota("daily", Home, new QuotaNeedSettings { Lead = 60, OverdueUrgency = 50, Time = new QuotaTime { Minutes = Amount(60), Shift = 60 } }, days: ["day1"]);
		var everyDay = Quota("everyDay", Home, new QuotaNeedSettings { Period = NeedPeriod.Week, Lead = 60, OverdueUrgency = 50, Time = new QuotaTime { Minutes = Amount(60), Shift = 60 } });
		var tooUrgent = Quota("tooUrgent", Home, new QuotaNeedSettings { Period = NeedPeriod.Week, Lead = 60, OverdueUrgency = 150, Time = new QuotaTime { Minutes = Amount(60), Shift = 60 } }, days: ["day1"]);
		var blankHome = Quota("blankHome", Quarters(false, "square"), new QuotaNeedSettings { Lead = 60, Trips = new QuotaTrips { Visits = Amount(1), Minutes = Amount(5), Home = new QuotaTripHome { Minutes = Amount(5), Activity = " " } } }, returnHome: true);
		var blank = new Need { Id = "blank", Title = "Blank", Activity = " ", PlaceSetting = Home, Level = Level() };

		Assert.That(With(blank, daily, everyDay, tooUrgent, blankHome).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.NeedOverdueNotAllowed.FormatCurrentCulture("daily"),
			OurResources.NeedOverdueNotAllowed.FormatCurrentCulture("everyDay"),
			OurResources.NeedLevelOutOfRange.FormatCurrentCulture("needs[tooUrgent].quota.overdueUrgency", OperaEtDies.Data.Need.MaximumLevel),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[blankHome].quota.trips.home.activity"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[blank].activity"),
		}));
	}

	[Test]
	public void PlayerActionsMustBeValid()
	{
		var action = new PlayerAction
		{
			Title = " ",
			Result = "",
			Options =
			[
				new PlayerActionOption { Title = "Until met", UntilMet = true },
				new PlayerActionOption { Minutes = 60 },
				new PlayerActionOption { Title = "Both", UntilMet = true, Minutes = 30 },
				new PlayerActionOption { Title = "Neither" },
				new PlayerActionOption { UntilHour = 24 },
				new PlayerActionOption { Minutes = 0 },
			],
		};
		var empty = new PlayerAction { Title = "Empty", Result = "You did nothing", Options = [] };
		var settings = With(
			new Need { Id = "acting", Title = "Acting", PlaceSetting = Home, Level = Level(), PlayerAction = action },
			new Need { Id = "empty", Title = "Empty", PlaceSetting = Home, Level = Level(), PlayerAction = empty });

		Assert.That(settings.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.DataValueMissing.FormatCurrentCulture("needs[acting].playerAction.title"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[acting].playerAction.result"),
			OurResources.PlayerActionOptionInvalid.FormatCurrentCulture("needs[acting].playerAction.options[2]"),
			OurResources.PlayerActionOptionInvalid.FormatCurrentCulture("needs[acting].playerAction.options[3]"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[acting].playerAction.options[4].title"),
			OurResources.ScheduleHourInvalid.FormatCurrentCulture("needs[acting].playerAction.options[4].untilHour", 24, 23),
			OurResources.DataNumberNotPositive.FormatCurrentCulture("needs[acting].playerAction.options[5].minutes", 0),
			OurResources.DataListEmpty.FormatCurrentCulture("needs[empty].playerAction.options"),
		}));
	}

	[Test]
	public void PlayerActionsMustFitTheirNeed()
	{
		var home = new PlayerActionHome { Title = "Put it away", Result = "You put it away" };
		Need Trip(string id, bool returnHome, PlayerAction action) =>
			Quota(id, Quarters(false, "square"), new QuotaNeedSettings { Lead = 60, Trips = new QuotaTrips { Visits = Amount(1), Minutes = Amount(5) } }, returnHome: returnHome, action: action);

		var settings = With(
			new Need { Id = "timed", Title = "Timed", PlaceSetting = Home, Level = Level(),
				PlayerAction = new PlayerAction { Title = "Rest", Result = "You rested", Return = Text("Go home."), Options = [new PlayerActionOption { Minutes = 5 }], OnlyInHours = true } },
			Trip("inHours", returnHome: true, new PlayerAction { Title = "Fetch", Result = "You fetched", Return = Text("Go home."), Home = home, OnlyInHours = true }),
			Trip("good", returnHome: true, new PlayerAction { Title = "Fetch", Result = "You fetched", Return = Text("Go home."), Home = home }),
			Trip("withOptions", returnHome: true, new PlayerAction { Title = "Fetch", Result = "You fetched", Return = Text("Go home."), Home = home, Options = [new PlayerActionOption { Minutes = 5 }] }),
			Trip("noReturn", returnHome: true, new PlayerAction { Title = "Fetch", Result = "You fetched", Home = new PlayerActionHome { Title = " ", Result = "" } }),
			Trip("staying", returnHome: false, new PlayerAction { Title = "Fetch", Result = "You fetched", Return = Text("Go home."), Home = home }));

		Assert.That(settings.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture("needs[withOptions].playerAction.options"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[noReturn].playerAction.return"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[noReturn].playerAction.home.title"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[noReturn].playerAction.home.result"),
			OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture("needs[staying].playerAction.return"),
			OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture("needs[staying].playerAction.home"),
			OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture("needs[timed].playerAction.return"),
			OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture("needs[timed].playerAction.onlyInHours"),
		}));
	}

	[Test]
	public void FactorsMustBeValid()
	{
		var settings = CreateScheduleSettings();
		settings.UrgencyFactors = [new UrgencyFactor { Id = "chores", When = Condition("profession", "homemaker"), Needs = ["upkeep", "dusting"], Factor = 0 }];
		settings.TravelTimeFactors = [new TravelTimeFactor { Id = "slow", When = Condition("profession", "infirm"), Factor = -1 }, new TravelTimeFactor { Id = "slow", When = Condition("profession", "infirm"), Factor = 2 }];

		Assert.That(settings.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.DataNumberNotPositive.FormatCurrentCulture("urgencyFactors[chores].factor", 0),
			OurResources.DataIdUnknown.FormatCurrentCulture("urgencyFactors[chores].needs", "dusting", "needs"),
			OurResources.DataNumberNotPositive.FormatCurrentCulture("travelTimeFactors[slow].factor", -1),
			OurResources.DataListItemIdDuplicate.FormatCurrentCulture("slow", "travelTimeFactors"),
		}));
	}

	[Test]
	public void QuotasGrowWithTheFamily()
	{
		var timed = Quota("timed", Home, new QuotaNeedSettings { Lead = 60, SharedByFamily = true, Time = new QuotaTime { Minutes = Amount(45, 15), Shift = 30 } });
		var trips = Quota("trips", Home, new QuotaNeedSettings { Lead = 60, SharedByFamily = true, Trips = new QuotaTrips { Visits = Amount(1, 0.5), Minutes = Amount(10) } });
		var personal = Quota("personal", Home, Timed(45));
		var laundry = Quota("laundry", Quarters(false, "square"), new QuotaNeedSettings
		{
			Lead = 60, SharedByFamily = true,
			Trips = new QuotaTrips { Visits = Amount(1), Minutes = Amount(0, 20), Home = new QuotaTripHome { Minutes = Amount(0, 10) } },
		}, returnHome: true);

		Assert.That((timed.GetQuotaMinutes(2), timed.GetQuotaMinutes(6), timed.GetShiftMinutes(2)), Is.EqualTo((75L, 135L, 30)));
		Assert.That((trips.GetQuotaMinutes(2), trips.GetQuotaMinutes(3), trips.GetShiftMinutes(2), trips.IsTrips), Is.EqualTo((20L, 30L, 10, true)));

		// One trip, whose time at the place and at home afterward grows with the family.
		Assert.That((laundry.GetVisitMinutes(3), laundry.GetHomeMinutes(3), laundry.GetShiftMinutes(3), laundry.GetQuotaMinutes(3)), Is.EqualTo((60, 30, 90, 90L)));
		Assert.That(personal.GetQuotaMinutes(5), Is.EqualTo(45L));
	}

	[Test]
	public void HoursMustBeValid()
	{
		var overnight = new Need { Id = "overnight", Title = "Overnight", PlaceSetting = Home, Level = Level(), Hours = Hours(21, 4) };
		var pastMidnight = Quota("pastMidnight", Home, Timed(240), Hours(22, 2));
		var lateLong = Quota("lateLong", Home, Timed(180), Hours(23, 1));
		var never = Quota("never", Home, Timed(60), Hours(12, 12));
		var tooLong = Quota("tooLong", Home, Timed(90), Hours(6, 7));
		var midnight = new Need { Id = "midnight", Title = "Midnight", PlaceSetting = Home, Level = Level(), Hours = Hours(20, 0) };

		Assert.That(With(overnight, pastMidnight, lateLong, never, tooLong, midnight).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.NeedMinutesTooLong.FormatCurrentCulture("lateLong", 180, 120),
			OurResources.HoursInvalid.FormatCurrentCulture("needs[never].hours", 12, 12),
			OurResources.NeedMinutesTooLong.FormatCurrentCulture("tooLong", 90, 60),
			OurResources.HoursInvalid.FormatCurrentCulture("needs[midnight].hours", 20, 0),
		}));
	}

	[Test]
	public void NeedsMustReferToNeedsAndPlaces()
	{
		var paused = new Need { Id = "paused", Title = "Paused", PlaceSetting = new SchedulePlace { Kind = NeedPlace.Quarter }, Level = new LevelNeedSettings { PerHour = 1, SatisfyPerHour = 1, PausedBy = ["missing"] } };

		Assert.That(With(paused).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.DataListEmpty.FormatCurrentCulture("needs[paused].place.quarterKinds"),
			OurResources.DataIdUnknown.FormatCurrentCulture("needs[paused].level.pausedBy", "missing", "needs"),
		}));
	}

	[Test]
	public void RolesMustBeValid()
	{
		var sleep = new Need { Id = "sleep", Title = "Sleep", Role = ScheduleSettings.SleepRole, PlaceSetting = Home, Quota = Timed(60), Hours = Hours(6, 12) };
		var other = new Need { Id = "other", Title = "Other", Role = ScheduleSettings.SleepRole, PlaceSetting = Home, Level = Level() };
		var odd = new Need { Id = "odd", Title = "Odd", Role = "dreaming", PlaceSetting = Home, Level = Level() };
		var settings = CreateScheduleSettings();
		settings.Needs = [sleep, other, odd];
		settings.UrgencyFactors = [];

		Assert.That(settings.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.RoleUnknown.FormatCurrentCulture("needs[odd]", "dreaming", "\"sleep\", \"socializing\""),
			OurResources.RoleDuplicate.FormatCurrentCulture("needs", "sleep", "\"sleep\", \"other\""),
			OurResources.RoleWrongType.FormatCurrentCulture("needs[sleep]", "sleep", "level need"),
		}));
	}

	[Test]
	public void SleepNeedIsRequired()
	{
		var settings = CreateScheduleSettings();
		settings.Needs = [new Need { Id = "hunger", Title = "Hunger", PlaceSetting = Home, Level = Level() }];
		settings.UrgencyFactors = [];

		Assert.That(settings.Validate(), Is.EqualTo(new[] { OurResources.RoleMissing.FormatCurrentCulture("needs", ScheduleSettings.SleepRole) }));
	}

	[Test]
	public void LevelUrgencyRisesFromItsThreshold()
	{
		var need = CreateScheduleSettings().Needs[0];

		Assert.That(need.GetLevelUrgency(50), Is.Zero);
		Assert.That(need.GetLevelUrgency(92.5), Is.EqualTo(50).Within(0.001));
		Assert.That(need.GetLevelUrgency(100), Is.EqualTo(100));
		Assert.That((need.IsWithinHours(21), need.IsWithinHours(3), need.IsWithinHours(4), need.IsWithinHours(12)), Is.EqualTo((true, true, false, false)));
		Assert.That((need.GetLevelTitle(10), need.GetLevelTitle(60)), Is.EqualTo(("Rested", "Tired")));
	}

	[Test]
	public void SchedulesMustMatchTheRestOfTheData()
	{
		var schedules = CreateScheduleSettings();
		var outing = new Need
		{
			Id = "outing", Title = "Outing", PlaceSetting = Quarters(false, "beach"), When = Condition("profession", "pirate"),
			Hours = new Hours { Start = 6, End = 12, Days = ["someday"] }, Quota = Timed(60),
		};
		schedules.Needs = [.. schedules.Needs, outing];
		var gameData = CreateGameData();

		var errors = GameDataValidation.Validate(CreateCharacterData(), CreateLocationList(), CreateProfessionList(), gameData.Families, CreateRelationshipSettings(), CreateCalendarSettings(), schedules);

		Assert.That(errors, Is.EquivalentTo(new[]
		{
			OurResources.DataIdUnknown.FormatCurrentCulture("needs[outing].place.quarterKinds", "beach", "quarterKinds"),
			OurResources.DataIdUnknown.FormatCurrentCulture("needs[outing].hours.days", "someday", "days"),
			OurResources.ConditionValueUnknown.FormatCurrentCulture("needs[outing].when.values.profession", "pirate", "profession"),
		}));
	}

	private static LevelNeedSettings Level() => new() { PerHour = 1, SatisfyPerHour = 1 };

	private static QuotaNeedSettings Timed(int minutes) => new() { Lead = 60, Time = new QuotaTime { Minutes = Amount(minutes), Shift = 60 } };

	private static Need Quota(string id, SchedulePlace place, QuotaNeedSettings quota, Hours? hours = null, bool returnHome = false, PlayerAction? action = null) =>
		new() { Id = id, Title = id, PlaceSetting = place, Quota = quota, Hours = hours ?? Hours(6, 12), ReturnHome = returnHome, PlayerAction = action };

	private static Need Quota(string id, SchedulePlace place, QuotaNeedSettings quota, IReadOnlyList<string> days, bool returnHome = false) =>
		new() { Id = id, Title = id, PlaceSetting = place, Quota = quota, Hours = new Hours { Start = 6, End = 12, Days = days }, ReturnHome = returnHome };

	private static Need Quota(string id, SchedulePlace place, QuotaNeedSettings quota, bool returnHome) => Quota(id, place, quota, hours: null, returnHome: returnHome);

	/// <summary>
	/// Creates the test schedules with the given needs, and a sleep need so they're valid apart from the given needs.
	/// </summary>
	private static ScheduleSettings With(params Need[] needs)
	{
		var settings = CreateScheduleSettings();
		settings.Needs = [new Need { Id = "sleep", Title = "Sleep", Role = ScheduleSettings.SleepRole, PlaceSetting = Home, Level = Level() }, .. needs];
		settings.UrgencyFactors = [];
		return settings;
	}
}
