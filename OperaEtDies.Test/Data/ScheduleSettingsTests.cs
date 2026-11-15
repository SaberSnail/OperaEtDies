using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class ScheduleSettingsTests
{
	[Test]
	public void LoadsShippedSchedules()
	{
		var result = ScheduleSettings.Load(JsonDataLoader.DefaultDataDirectory);

		Assert.That(result.Errors, Is.Empty);
		Assert.That(result.Value!.Needs, Is.Not.Empty);
	}

	[Test]
	public void ValidSettingsHaveNoErrors()
	{
		Assert.That(TestData.CreateScheduleSettings().Validate(), Is.Empty);
	}

	[Test]
	public void SettingsMustMatchTheKindOfNeed()
	{
		var level = new Need { Id = "level", Title = "Level", Kind = NeedKind.Level, Place = NeedPlace.Home, PerHour = 1, Minutes = 5 };
		var daily = new Need { Id = "daily", Title = "Daily", Kind = NeedKind.Quota, Place = NeedPlace.Home, Start = 6, End = 12, Minutes = 60, Shift = 60, PerHour = 2 };

		Assert.That(With(level, daily).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.NeedSettingMissing.FormatCurrentCulture("level", "satisfyPerHour", "level"),
			OurResources.NeedSettingNotAllowed.FormatCurrentCulture("level", "minutes", "level"),
			OurResources.NeedSettingMissing.FormatCurrentCulture("daily", "lead", "quota"),
			OurResources.NeedSettingNotAllowed.FormatCurrentCulture("daily", "perHour", "quota"),
		}));
	}

	[Test]
	public void QuotasAreMeasuredInTimeOrTrips()
	{
		var mixed = new Need { Id = "mixed", Title = "Mixed", Kind = NeedKind.Quota, Place = NeedPlace.Home, Start = 6, End = 12, Minutes = 60, Shift = 60, VisitMinutes = 5, Lead = 60 };
		var noMinutes = new Need { Id = "noMinutes", Title = "No minutes", Kind = NeedKind.Quota, Place = NeedPlace.Quarter, QuarterKinds = [new QuarterChoice { QuarterKind = "square" }],
			Start = 6, End = 12, Visits = 2, Lead = 60 };
		var unshared = new Need { Id = "unshared", Title = "Unshared", Kind = NeedKind.Quota, Place = NeedPlace.Home, Start = 6, End = 12, Minutes = 60, MinutesPerMember = 10, Shift = 60, Lead = 60 };
		var homeward = new Need { Id = "homeward", Title = "Homeward", Kind = NeedKind.Quota, Place = NeedPlace.Home, Start = 6, End = 12, Minutes = 60, Shift = 60, Lead = 60, ReturnHome = true };
		var stayAway = new Need { Id = "stayAway", Title = "Stay away", Kind = NeedKind.Quota, Place = NeedPlace.Quarter, QuarterKinds = [new QuarterChoice { QuarterKind = "square" }],
			Start = 6, End = 12, Visits = 1, VisitMinutes = 5, HomeMinutes = 10, Lead = 60 };
		var noTime = new Need { Id = "noTime", Title = "No time", Kind = NeedKind.Quota, Place = NeedPlace.Quarter, QuarterKinds = [new QuarterChoice { QuarterKind = "square" }],
			Start = 6, End = 12, Visits = 1, VisitMinutes = 0, Lead = 60 };

		Assert.That(With(mixed, noMinutes, unshared, homeward, stayAway, noTime).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.NeedQuotaMixed.FormatCurrentCulture("mixed"),
			OurResources.NeedSettingMissing.FormatCurrentCulture("noMinutes", "visitMinutes", "quota"),
			OurResources.NeedPerMemberNotShared.FormatCurrentCulture("unshared"),
			OurResources.NeedSettingNotAllowed.FormatCurrentCulture("homeward", "returnHome", "home"),
			OurResources.NeedHomeMinutesWithoutReturn.FormatCurrentCulture("stayAway"),
			OurResources.NeedVisitMinutesNotPositive.FormatCurrentCulture("noTime"),
			OurResources.ScheduleStartNeedInvalid.FormatCurrentCulture("mixed"),
		}));
	}

	[Test]
	public void OverdueAndActivitiesMustFitTheNeed()
	{
		var daily = new Need { Id = "daily", Title = "Daily", Kind = NeedKind.Quota, Place = NeedPlace.Home, Start = 6, End = 12, Minutes = 60, Shift = 60, Lead = 60, Days = ["day1"], OverdueUrgency = 50 };
		var everyDay = new Need { Id = "everyDay", Title = "Every day", Kind = NeedKind.Quota, Period = NeedPeriod.Week, Place = NeedPlace.Home, Start = 6, End = 12, Minutes = 60, Shift = 60, Lead = 60, OverdueUrgency = 50 };
		var tooUrgent = new Need { Id = "tooUrgent", Title = "Too urgent", Kind = NeedKind.Quota, Period = NeedPeriod.Week, Place = NeedPlace.Home, Start = 6, End = 12, Minutes = 60, Shift = 60, Lead = 60, Days = ["day1"], OverdueUrgency = 150 };
		var noHomeTime = new Need { Id = "noHomeTime", Title = "No home time", Kind = NeedKind.Quota, Place = NeedPlace.Quarter, QuarterKinds = [new QuarterChoice { QuarterKind = "square" }],
			Start = 6, End = 12, Visits = 1, VisitMinutes = 5, Lead = 60, ReturnHome = true, HomeActivity = "Resting" };
		var blank = new Need { Id = "blank", Title = "Blank", Activity = " ", Kind = NeedKind.Level, Place = NeedPlace.Home, PerHour = 1, SatisfyPerHour = 1 };

		var settings = With(blank, daily, everyDay, tooUrgent, noHomeTime);
		Assert.That(settings.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.NeedOverdueNotAllowed.FormatCurrentCulture("daily"),
			OurResources.NeedOverdueNotAllowed.FormatCurrentCulture("everyDay"),
			OurResources.NeedLevelOutOfRange.FormatCurrentCulture("needs[tooUrgent].overdueUrgency", OperaEtDies.Data.Need.MaximumLevel),
			OurResources.NeedHomeActivityWithoutHomeMinutes.FormatCurrentCulture("noHomeTime"),
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
			new Need { Id = "acting", Title = "Acting", Kind = NeedKind.Level, Place = NeedPlace.Home, PerHour = 1, SatisfyPerHour = 1, PlayerAction = action },
			new Need { Id = "empty", Title = "Empty", Kind = NeedKind.Level, Place = NeedPlace.Home, PerHour = 1, SatisfyPerHour = 1, PlayerAction = empty });

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
		var square = new[] { new QuarterChoice { QuarterKind = "square" } };
		Need Trip(string id, bool returnHome, PlayerAction action) => new()
		{
			Id = id, Title = id, Kind = NeedKind.Quota, Place = NeedPlace.Quarter, QuarterKinds = square, Start = 6, End = 12, Visits = 1, VisitMinutes = 5, Lead = 60,
			ReturnHome = returnHome, PlayerAction = action,
		};

		var settings = With(
			new Need { Id = "timed", Title = "Timed", Kind = NeedKind.Level, Place = NeedPlace.Home, PerHour = 1, SatisfyPerHour = 1,
				PlayerAction = new PlayerAction { Title = "Rest", Result = "You rested", Return = "Go home.", Options = [new PlayerActionOption { Minutes = 5 }], OnlyInHours = true } },
			Trip("inHours", returnHome: true, new PlayerAction { Title = "Fetch", Result = "You fetched", Return = "Go home.", Home = home, OnlyInHours = true }),
			Trip("good", returnHome: true, new PlayerAction { Title = "Fetch", Result = "You fetched", Return = "Go home.", Home = home }),
			Trip("withOptions", returnHome: true, new PlayerAction { Title = "Fetch", Result = "You fetched", Return = "Go home.", Home = home, Options = [new PlayerActionOption { Minutes = 5 }] }),
			Trip("noReturn", returnHome: true, new PlayerAction { Title = "Fetch", Result = "You fetched", Home = new PlayerActionHome { Title = " ", Result = "" } }),
			Trip("staying", returnHome: false, new PlayerAction { Title = "Fetch", Result = "You fetched", Return = "Go home.", Home = home }));


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
	public void UrgencyFactorsAndNearestPlacesMustBeValid()
	{
		var valid = TestData.CreateScheduleSettings();
		var atHome = new Need { Id = "atHome", Title = "At home", Kind = NeedKind.Level, Place = NeedPlace.Home, PerHour = 1, SatisfyPerHour = 1, Nearest = true };
		var settings = new ScheduleSettings
		{
			MinimumUrgency = valid.MinimumUrgency,
			UrgencyPower = valid.UrgencyPower,
			SightingHours = valid.SightingHours,
			Sleep = valid.Sleep,
			Talking = WithoutSocializing(valid.Talking),
			Socializing = valid.Socializing,
			Needs = [.. valid.Needs, atHome],
			Idle = valid.Idle,
			Start = valid.Start,
			UrgencyFactors = [new UrgencyFactor { When = TestData.Condition("profession", "homemaker"), Needs = ["upkeep", "dusting"], Factor = 0 }],
		};

		Assert.That(settings.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.NeedSettingNotAllowed.FormatCurrentCulture("atHome", "nearest", "home"),
			OurResources.DataNumberNotPositive.FormatCurrentCulture("urgencyFactors[0].factor", 0),
			OurResources.DataIdUnknown.FormatCurrentCulture("urgencyFactors[0].needs", "dusting", "needs"),
		}));
	}

	[Test]
	public void QuotasGrowWithTheFamily()
	{
		var timed = new Need { Id = "timed", Title = "Timed", Kind = NeedKind.Quota, Place = NeedPlace.Home, Start = 6, End = 12, Minutes = 45, MinutesPerMember = 15, Shift = 30, Lead = 60, SharedByFamily = true };
		var trips = new Need { Id = "trips", Title = "Trips", Kind = NeedKind.Quota, Place = NeedPlace.Home, Start = 6, End = 12, Visits = 1, VisitsPerMember = 0.5, VisitMinutes = 10, Lead = 60, SharedByFamily = true };
		var personal = new Need { Id = "personal", Title = "Personal", Kind = NeedKind.Quota, Place = NeedPlace.Home, Start = 6, End = 12, Minutes = 45, Shift = 30, Lead = 60 };
		var laundry = new Need { Id = "laundry", Title = "Laundry", Kind = NeedKind.Quota, Place = NeedPlace.Quarter, QuarterKinds = [new QuarterChoice { QuarterKind = "square" }],
			Start = 6, End = 12, Visits = 1, VisitMinutesPerMember = 20, HomeMinutesPerMember = 10, Lead = 60, SharedByFamily = true, ReturnHome = true };

		Assert.That((timed.GetQuotaMinutes(2), timed.GetQuotaMinutes(6), timed.GetShiftMinutes(2)), Is.EqualTo((75L, 135L, 30)));
		Assert.That((trips.GetQuotaMinutes(2), trips.GetQuotaMinutes(3), trips.GetShiftMinutes(2), trips.IsTrips), Is.EqualTo((20L, 30L, 10, true)));

		// One trip, whose time at the place and at home afterward grows with the family.
		Assert.That((laundry.GetVisitMinutes(3), laundry.GetHomeMinutes(3), laundry.GetShiftMinutes(3), laundry.GetQuotaMinutes(3)), Is.EqualTo((60, 30, 90, 90L)));
		Assert.That(personal.GetQuotaMinutes(5), Is.EqualTo(45L));
	}

	[Test]
	public void HoursMustFitTheKindOfNeed()
	{
		var overnight = new Need { Id = "overnight", Title = "Overnight", Kind = NeedKind.Level, Place = NeedPlace.Home, PerHour = 1, SatisfyPerHour = 1, Start = 21, End = 4 };
		var halfHours = new Need { Id = "halfHours", Title = "Half", Kind = NeedKind.Level, Place = NeedPlace.Home, PerHour = 1, SatisfyPerHour = 1, Start = 21 };
		var pastMidnight = new Need { Id = "pastMidnight", Title = "Past midnight", Kind = NeedKind.Quota, Place = NeedPlace.Home, Start = 22, End = 2, Minutes = 240, Shift = 60, Lead = 60 };
		var lateLong = new Need { Id = "lateLong", Title = "Late and long", Kind = NeedKind.Quota, Place = NeedPlace.Home, Start = 23, End = 1, Minutes = 180, Shift = 60, Lead = 60 };
		var never = new Need { Id = "never", Title = "Never", Kind = NeedKind.Quota, Place = NeedPlace.Home, Start = 12, End = 12, Minutes = 60, Shift = 60, Lead = 60 };
		var tooLong = new Need { Id = "tooLong", Title = "Too long", Kind = NeedKind.Quota, Place = NeedPlace.Home, Start = 6, End = 7, Minutes = 90, Shift = 60, Lead = 60 };

		Assert.That(With(overnight, halfHours, pastMidnight, lateLong, never, tooLong).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.NeedSettingMissing.FormatCurrentCulture("halfHours", "end", "level"),
			OurResources.NeedMinutesTooLong.FormatCurrentCulture("lateLong", 180, 120),
			OurResources.NeedHoursInvalid.FormatCurrentCulture("never", 12, 12),
			OurResources.NeedMinutesTooLong.FormatCurrentCulture("tooLong", 90, 60),
		}));
	}

	[Test]
	public void NeedsMustReferToNeedsAndPlaces()
	{
		var paused = new Need { Id = "paused", Title = "Paused", Kind = NeedKind.Level, Place = NeedPlace.Quarter, PerHour = 1, SatisfyPerHour = 1, PausedBy = ["sleep"] };
		var home = new Need { Id = "home", Title = "Home", Kind = NeedKind.Level, Place = NeedPlace.Home, PerHour = 1, SatisfyPerHour = 1, QuarterKinds = [new QuarterChoice { QuarterKind = "square" }] };

		Assert.That(With(paused, home).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.DataListEmpty.FormatCurrentCulture("needs[paused].quarterKinds"),
			OurResources.DataIdUnknown.FormatCurrentCulture("needs[paused].pausedBy", "sleep", "needs"),
			OurResources.ScheduleQuarterKindsNotAllowed.FormatCurrentCulture("needs[home]"),
		}));
	}

	[Test]
	public void StartNeedMustBeALevelNeed()
	{
		var valid = TestData.CreateScheduleSettings();
		var settings = new ScheduleSettings
		{
			MinimumUrgency = valid.MinimumUrgency,
			UrgencyPower = valid.UrgencyPower,
			SightingHours = valid.SightingHours,
			Sleep = valid.Sleep,
			Talking = WithoutSocializing(valid.Talking),
			Socializing = valid.Socializing,
			Needs = valid.Needs,
			Idle = valid.Idle,
			Start = new ScheduleStart { Need = "work", WakeHour = 6, WakeMinutes = valid.Start.WakeMinutes },
		};

		Assert.That(settings.Validate(), Is.EqualTo(new[] { OurResources.ScheduleStartNeedInvalid.FormatCurrentCulture("work") }));
	}

	[Test]
	public void LevelUrgencyRisesFromItsThreshold()
	{
		var need = TestData.CreateScheduleSettings().Needs[0];

		Assert.That(need.GetLevelUrgency(50), Is.Zero);
		Assert.That(need.GetLevelUrgency(92.5), Is.EqualTo(50).Within(0.001));
		Assert.That(need.GetLevelUrgency(100), Is.EqualTo(100));
		Assert.That((need.IsWithinHours(21), need.IsWithinHours(3), need.IsWithinHours(4), need.IsWithinHours(12)), Is.EqualTo((true, true, false, false)));
		Assert.That((need.GetLevelTitle(10), need.GetLevelTitle(60)), Is.EqualTo(("Rested", "Tired")));
	}

	[Test]
	public void SchedulesMustMatchTheOtherFiles()
	{
		var valid = TestData.CreateScheduleSettings();
		var outing = new Need
		{
			Id = "outing", Title = "Outing", Kind = NeedKind.Quota, Place = NeedPlace.Quarter, QuarterKinds = [new QuarterChoice { QuarterKind = "beach" }], Days = ["someday"],
			When = TestData.Condition("profession", "pirate"), Start = 6, End = 12, Minutes = 60, Shift = 60, Lead = 60,
		};
		var schedules = new ScheduleSettings { MinimumUrgency = 0, UrgencyPower = 1, SightingHours = 24, Sleep = valid.Sleep, Talking = WithoutSocializing(valid.Talking), Socializing = valid.Socializing, Needs = [.. valid.Needs, outing], Idle = valid.Idle, Start = valid.Start };

		var errors = GameDataValidation.Validate("data", TestData.CreateCharacterData(), TestData.CreateLocationList(), TestData.CreateProfessionList(), TestData.CreateFamilySettings(),
			TestData.CreateCalendarSettings(), schedules);

		Assert.That(errors.Select(x => x.Message), Is.EquivalentTo(new[]
		{
			OurResources.DataIdUnknown.FormatCurrentCulture("needs[outing].quarterKinds", "beach", "quarterKinds"),
			OurResources.DataIdUnknown.FormatCurrentCulture("needs[outing].days", "someday", "days"),
			OurResources.ConditionValueUnknown.FormatCurrentCulture("needs[outing].when.values.profession", "pirate", "profession"),
		}));
		Assert.That(errors.Select(x => x.FilePath).Distinct(), Is.EqualTo(new[] { System.IO.Path.Combine("data", ScheduleSettings.FileName) }));
	}

	private static ScheduleSettings With(params Need[] needs)
	{
		var valid = TestData.CreateScheduleSettings();
		return new ScheduleSettings { MinimumUrgency = valid.MinimumUrgency, UrgencyPower = valid.UrgencyPower, SightingHours = valid.SightingHours, Sleep = valid.Sleep, Talking = WithoutSocializing(valid.Talking), Socializing = valid.Socializing, Needs = needs, Idle = valid.Idle, Start = new ScheduleStart { Need = needs[0].Id, WakeHour = 6, WakeMinutes = valid.Start.WakeMinutes } };
	}

	private static TalkingSettings WithoutSocializing(TalkingSettings talking) => new()
	{
		RefuseUrgency = talking.RefuseUrgency,
		WakeLevel = talking.WakeLevel,
		AskAboutMinutes = talking.AskAboutMinutes,
		AskDoingMinutes = talking.AskDoingMinutes,
		SocializeMinutes = talking.SocializeMinutes,
	};
}
