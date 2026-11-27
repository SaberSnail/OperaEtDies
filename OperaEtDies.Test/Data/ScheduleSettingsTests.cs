using GoldenAnvil.Utility;
using OperaEtDies.Data;
using static OperaEtDies.Test.TestData;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class ScheduleSettingsTests
{
	[Test]
	public void NeedsAreLevelOrQuota()
	{
		var both = new Need { Id = "both", Title = "Both", Icon = "icon", PlaceSetting = Home, Level = Level(), Quota = Timed(60) };
		var neither = new Need { Id = "neither", Title = "Neither", Icon = "icon", PlaceSetting = Home };
		var noHours = new Need { Id = "noHours", Title = "No hours", Icon = "icon", PlaceSetting = Home, Quota = Timed(60) };
		var levelDays = new Need { Id = "levelDays", Title = "Level days", Icon = "icon", PlaceSetting = Home, Hours = new Hours { Start = 6, End = 12, Days = ["day1"] }, Level = Level() };

		Assert.That(With(both, neither, noHours, levelDays).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.NeedKindInvalid.FormatCurrentCulture("both"),
			OurResources.NeedKindInvalid.FormatCurrentCulture("neither"),
			OurResources.NeedSettingMissing.FormatCurrentCulture("noHours", "hours", "quota"),
			OurResources.HoursSettingNotAllowed.FormatCurrentCulture("needs[levelDays].hours.days"),
		}));
	}

	[Test]
	public void QuotasAreMeasuredInTimeOrRuns()
	{
		var mixed = Quota("mixed", Home, new QuotaNeedSettings { Lead = 60, Time = new QuotaTime { Minutes = Amount(60), Shift = 60 }, Runs = Amount(1) });
		var unshared = Quota("unshared", Home, new QuotaNeedSettings { Lead = 60, Time = new QuotaTime { Minutes = Amount(60, 10), Shift = 60 } });
		var unsharedStep = Runs("unsharedStep", Amount(1), shared: false, Do(Amount(5, 5)), CountRun());
		var noTime = Runs("noTime", Amount(1), shared: true, Do(Amount(0)), CountRun());
		var fraction = Quota("fraction", Home, new QuotaNeedSettings { Lead = 60, Time = new QuotaTime { Minutes = Amount(60.5), Shift = 60 } });

		Assert.That(With(mixed, unshared, unsharedStep, noTime, fraction).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.NeedQuotaMeasureInvalid.FormatCurrentCulture("mixed"),
			OurResources.NeedPerMemberNotShared.FormatCurrentCulture("unshared"),
			OurResources.NeedPerMemberNotShared.FormatCurrentCulture("unsharedStep"),
			OurResources.NeedRunMinutesNotPositive.FormatCurrentCulture("noTime"),
			OurResources.DataNumberNotWhole.FormatCurrentCulture("needs[fraction].quota.time.minutes.base", 60.5),
		}));
	}

	[Test]
	public void StepsMustFitTheirNeed()
	{
		var both = new Need { Id = "both", Title = "Both", Icon = "icon", PlaceSetting = Home, StepSettings = [Do(Amount(5))], Level = Level() };
		var neither = new Need { Id = "neither", Title = "Neither", Icon = "icon", Level = Level() };
		var empty = new Need { Id = "empty", Title = "Empty", Icon = "icon", StepSettings = [], Level = Level() };
		var idle = new Need { Id = "idle", Title = "Idle", Icon = "icon", StepSettings = [GoTo(Home)], Level = Level() };
		var uncounted = Runs("uncounted", Amount(1), shared: false, Do(Amount(5)));
		var counted = Quota("counted", new QuotaNeedSettings { Lead = 60, Time = new QuotaTime { Minutes = Amount(60), Shift = 60 } }, new Step { Kind = StepKind.Do, MeetsQuota = true }, CountRun());
		var unmet = Quota("unmet", new QuotaNeedSettings { Lead = 60, Time = new QuotaTime { Minutes = Amount(60), Shift = 60 } }, new Step { Kind = StepKind.Do });
		var lost = Runs("lost", Amount(1), shared: false, Do(Amount(5)), new Step { Kind = StepKind.Put, Bundle = Bundle("washing") }, CountRun());

		Assert.That(With(both, neither, empty, idle, uncounted, counted, unmet, lost).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.NeedPlaceOrSteps.FormatCurrentCulture("both"),
			OurResources.NeedPlaceOrSteps.FormatCurrentCulture("neither"),
			OurResources.DataListEmpty.FormatCurrentCulture("needs[empty].steps"),
			OurResources.NeedStepsWithoutDo.FormatCurrentCulture("idle"),
			OurResources.NeedRunsWithoutCountRun.FormatCurrentCulture("uncounted"),
			OurResources.NeedCountRunNotRuns.FormatCurrentCulture("counted"),
			OurResources.NeedTimeWithoutMeetsQuota.FormatCurrentCulture("unmet"),
			OurResources.StepBundleUnknown.FormatCurrentCulture("needs[lost].steps[1].bundle", "washing"),
		}));
	}

	[Test]
	public void StepSettingsMustFitTheirKind()
	{
		var level = new Need
		{
			Id = "level", Title = "Level", Icon = "icon", Level = Level(),
			StepSettings =
			[
				new Step { Kind = StepKind.GoTo, Minutes = Amount(5) },
				new Step { Kind = StepKind.Do, MeetsQuota = true, Bundle = Bundle("bag", "Bag"), When = new StepCondition { DoneForToday = true } },
				new Step { Kind = StepKind.Take, Outfit = OutfitKind.Work, AfterDelay = new Duration(5) },
				new Step { Kind = StepKind.Put, Bundle = Bundle(" "), Owner = StepOwner.Household, Accessories = false },
				new Step { Kind = StepKind.Dress, AtWork = true, When = new StepCondition() },
				new Step { Kind = StepKind.Undress, Place = Home, Activity = "Undressing" },
			],
		};
		var runs = Runs("runs", Amount(1), shared: false, new Step { Kind = StepKind.Do }, CountRun());

		Assert.That(With(level, runs).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[level].steps[0].place", "goTo"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[level].steps[0].minutes", "goTo"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[level].steps[1].bundle", "do"),
			OurResources.StepMeetsQuotaNotAllowed.FormatCurrentCulture("needs[level].steps[1].meetsQuota"),
			OurResources.StepConditionQuotaOnly.FormatCurrentCulture("needs[level].steps[1].when.doneForToday"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[level].steps[2].kind", "take"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[level].steps[2].from", "take"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[level].steps[2].bundle", "take"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[level].steps[2].afterDelay", "take"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[level].steps[3].owner", "put"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[level].steps[3].accessories", "put"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[level].steps[3].bundle.id"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[level].steps[4].outfit", "dress"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[level].steps[4].atWork", "dress"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[level].steps[4].when"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[level].steps[5].outfit", "undress"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[level].steps[5].place", "undress"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[level].steps[5].activity", "undress"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[runs].steps[0].minutes", "do"),
		}));
	}

	[Test]
	public void OverdueAndActivitiesMustFitTheNeed()
	{
		var daily = Quota("daily", Home, new QuotaNeedSettings { Lead = 60, OverdueUrgency = 50, Time = new QuotaTime { Minutes = Amount(60), Shift = 60 } }, days: ["day1"]);
		var everyDay = Quota("everyDay", Home, new QuotaNeedSettings { Period = NeedPeriod.Week, Lead = 60, OverdueUrgency = 50, Time = new QuotaTime { Minutes = Amount(60), Shift = 60 } });
		var tooUrgent = Quota("tooUrgent", Home, new QuotaNeedSettings { Period = NeedPeriod.Week, Lead = 60, OverdueUrgency = 150, Time = new QuotaTime { Minutes = Amount(60), Shift = 60 } }, days: ["day1"]);
		var blankStep = new Need { Id = "blankStep", Title = "Blank step", Icon = "icon", StepSettings = [new Step { Kind = StepKind.Do, Activity = " " }], Level = Level() };
		var blank = new Need { Id = "blank", Title = "Blank", Icon = "icon", Activity = " ", PlaceSetting = Home, Level = Level() };

		Assert.That(With(blank, daily, everyDay, tooUrgent, blankStep).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.NeedOverdueNotAllowed.FormatCurrentCulture("daily"),
			OurResources.NeedOverdueNotAllowed.FormatCurrentCulture("everyDay"),
			OurResources.NeedLevelOutOfRange.FormatCurrentCulture("needs[tooUrgent].quota.overdueUrgency", OperaEtDies.Data.Need.MaximumLevel),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[blankStep].steps[0].activity"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[blank].activity"),
		}));
	}

	[Test]
	public void PlayerActionsMustBeValid()
	{
		var action = new StepPlayer { Title = " ", Result = "" };
		var settings = With(new Need { Id = "acting", Title = "Acting", Icon = "icon", StepSettings = [new Step { Kind = StepKind.Do, Player = action }], Level = Level() });

		Assert.That(settings.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.DataValueMissing.FormatCurrentCulture("needs[acting].steps[0].player.title"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[acting].steps[0].player.result"),
		}));
	}

	[Test]
	public void PlayerActionsMustFitTheirStep()
	{
		var settings = With(
			new Need
			{
				Id = "level", Title = "Level", Icon = "icon", Level = Level(),
				StepSettings =
				[
					GoTo(Home, player: new StepPlayer { Title = "Go", Result = "You went" }),
					new Step { Kind = StepKind.Do, Player = new StepPlayer { Title = "Rest", Result = "You rested", Text = Text("Go home.") } },
					GoTo(Home, player: new StepPlayer()),
					new Step { Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "a", Minutes = Amount(5), Chance = new StepChance { Base = 50 }, Player = new StepPlayer { Title = "Ask", Text = Text("Will you?") } },
					new Step { Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "b", Minutes = Amount(5), Chance = new StepChance { Base = 50 }, Player = new StepPlayer { Result = "You asked", Text = Text(" ") } },
				],
			},
			Runs("runs", Amount(1), shared: false,
				Do(Amount(5), new StepPlayer { Title = "Fetch", Result = "You fetched" }),
				new Step { Kind = StepKind.CountRun, Player = new StepPlayer { Text = Text("Done.") } }));

		Assert.That(settings.Validate(), Is.EquivalentTo(new[]
		{
			OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture("needs[level].steps[0].player.title"),
			OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture("needs[level].steps[0].player.result"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[level].steps[0].player.text"),
			OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture("needs[level].steps[1].player.text"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[level].steps[2].player.text"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[level].steps[4].player.title"),
			OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture("needs[level].steps[4].player.result"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[level].steps[4].player.text.text"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[runs].steps[1].player", "countRun"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[runs].steps[1].player.title"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[runs].steps[1].player.result"),
			OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture("needs[runs].steps[1].player.text"),
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
	public void RepeatsMustFitTheirNeed()
	{
		var work = new Step { Kind = StepKind.Do, Minutes = Amount(60), MeetsQuota = true };
		var timed = Quota("timed", new QuotaNeedSettings { Lead = 60, Time = new QuotaTime { Minutes = Amount(60), Shift = 60 } },
			new Step { Kind = StepKind.Repeat, Reconsider = true, Steps = [work] },
			new Step { Kind = StepKind.Repeat, Steps = [Do(Amount(5))], Player = new StepPlayer { Title = "Again", Result = "You did it again" } },
			new Step { Kind = StepKind.Repeat, Steps = [] },
			new Step { Kind = StepKind.Repeat },
			new Step { Kind = StepKind.Do, MeetsQuota = true, Reconsider = true, Steps = [work] });
		var runs = Runs("runs", Amount(1), shared: false, new Step { Kind = StepKind.Repeat, Steps = [Do(Amount(5))] }, CountRun());

		Assert.That(With(timed, runs).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[timed].steps[1].player", "repeat"),
			OurResources.StepRepeatWithoutMeetsQuota.FormatCurrentCulture("needs[timed].steps[1]"),
			OurResources.DataListEmpty.FormatCurrentCulture("needs[timed].steps[2].steps"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[timed].steps[3].steps", "repeat"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[timed].steps[4].steps", "do"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[timed].steps[4].reconsider", "do"),
			OurResources.StepRepeatNotAllowed.FormatCurrentCulture("needs[runs].steps[0]"),
			OurResources.StepRepeatWithoutMeetsQuota.FormatCurrentCulture("needs[runs].steps[0]"),
		}));
	}

	[Test]
	public void RepeatedStepsComeBeforeTheirRepeat()
	{
		var work = CreateScheduleSettings().Needs.Single(x => x.Id == "work");

		// The steps a repeat repeats come before it, so it can go back to the first of them.
		var kinds = work.Steps.Select(x => x.Kind).ToList();
		Assert.That(kinds, Is.EqualTo(new[] { StepKind.GoTo, StepKind.Take, StepKind.GoTo, StepKind.Dress, StepKind.Do, StepKind.Repeat, StepKind.Undress }));
		Assert.That(work.GetLoopStart(5), Is.EqualTo(4));
		Assert.That(work.TakesChosenTime(work.Steps[4]), Is.True);
	}

	[Test]
	public void QuotasGrowWithTheFamily()
	{
		var timed = Quota("timed", Home, new QuotaNeedSettings { Lead = 60, SharedByFamily = true, Time = new QuotaTime { Minutes = Amount(45, 15), Shift = 30 } });
		var runs = Runs("runs", Amount(1, 0.5), shared: true, Do(Amount(10)), CountRun());
		var personal = Quota("personal", Home, Timed(45));
		var laundry = Runs("laundry", Amount(1), shared: true, GoTo(Quarters(false, "square")), Do(Amount(0, 20)), GoTo(Home), Do(Amount(0, 10)), CountRun());

		Assert.That((timed.GetQuotaMinutes(2), timed.GetQuotaMinutes(6), timed.GetShiftMinutes(2)), Is.EqualTo((75L, 135L, 30)));
		Assert.That((runs.GetQuotaRuns(2), runs.GetQuotaMinutes(2), runs.GetQuotaMinutes(3), runs.GetShiftMinutes(2), runs.IsRuns), Is.EqualTo((2L, 20L, 30L, 10, true)));

		// One run, whose time at the place and at home afterward grows with the family.
		Assert.That((laundry.GetRunMinutes(3), laundry.GetShiftMinutes(3), laundry.GetQuotaMinutes(3)), Is.EqualTo((90, 90, 90L)));
		Assert.That(personal.GetQuotaMinutes(5), Is.EqualTo(45L));
	}

	[Test]
	public void NeedsWithAPlaceGoThereAndSpendTime()
	{
		var timed = Quota("timed", Quarters(true, "square"), Timed(60));
		var laundry = CreateScheduleSettings().Needs.Single(x => x.Id == "laundry");

		Assert.That(timed.Steps.Select(x => (x.Kind, x.MeetsQuota)), Is.EqualTo(new[] { (StepKind.GoTo, false), (StepKind.Do, true) }));
		Assert.That((timed.Place, timed.Nearest, timed.IsWork), Is.EqualTo(((NeedPlace?) NeedPlace.Quarter, true, false)));

		// The main place is where the first "do" step is done, and each step's activity defaults to the need's.
		Assert.That((laundry.Place, laundry.GetActivity(3), laundry.GetActivity(5)), Is.EqualTo(((NeedPlace?) NeedPlace.Quarter, "Washing", "Hanging out the washing")));
		Assert.That(laundry.GetActivityAt(x => x.Kind == NeedPlace.Home), Is.EqualTo("Hanging out the washing"));
		Assert.That(CreateScheduleSettings().Needs.Single(x => x.Id == "work").IsWork, Is.True);
	}

	[Test]
	public void HoursMustBeValid()
	{
		var overnight = new Need { Id = "overnight", Title = "Overnight", Icon = "icon", PlaceSetting = Home, Level = Level(), Hours = Hours(21, 4) };
		var pastMidnight = Quota("pastMidnight", Home, Timed(240), Hours(22, 2));
		var lateLong = Quota("lateLong", Home, Timed(180), Hours(23, 1));
		var never = Quota("never", Home, Timed(60), Hours(12, 12));
		var tooLong = Quota("tooLong", Home, Timed(90), Hours(6, 7));
		var midnight = new Need { Id = "midnight", Title = "Midnight", Icon = "icon", PlaceSetting = Home, Level = Level(), Hours = Hours(20, 0) };

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
		var paused = new Need { Id = "paused", Title = "Paused", Icon = "icon", PlaceSetting = new SchedulePlace { Kind = NeedPlace.Quarter }, Level = new LevelNeedSettings { PerHour = 1, SatisfyPerHour = 1, PausedBy = ["missing"] } };

		Assert.That(With(paused).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.DataListEmpty.FormatCurrentCulture("needs[paused].place.quarterKinds"),
			OurResources.DataIdUnknown.FormatCurrentCulture("needs[paused].level.pausedBy", "missing", "needs"),
		}));
	}

	[Test]
	public void RolesMustBeValid()
	{
		var sleep = new Need { Id = "sleep", Title = "Sleep", Icon = "icon", Role = ScheduleSettings.SleepRole, PlaceSetting = Home, Quota = Timed(60), Hours = Hours(6, 12) };
		var other = new Need { Id = "other", Title = "Other", Icon = "icon", Role = ScheduleSettings.SleepRole, PlaceSetting = Home, Level = Level() };
		var odd = new Need { Id = "odd", Title = "Odd", Icon = "icon", Role = "dreaming", PlaceSetting = Home, Level = Level() };
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
		settings.Needs = [new Need { Id = "hunger", Title = "Hunger", Icon = "icon", PlaceSetting = Home, Level = Level() }];
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
			Id = "outing", Title = "Outing", Icon = "icon", PlaceSetting = Quarters(false, "beach"), When = Condition("profession", "pirate"),
			Hours = new Hours { Start = 6, End = 12, Days = ["someday"] }, Quota = Timed(60),
		};
		var errand = new Need
		{
			Id = "errand", Title = "Errand", Icon = "icon", Level = Level(),
			StepSettings = [new Step { Kind = StepKind.Do, When = new StepCondition { BusinessProfession = "pirate" } }, new Step { Kind = StepKind.Do, When = new StepCondition { BusinessProfession = "homemaker" } }],
		};
		schedules.Needs = [.. schedules.Needs, outing, errand];
		var gameData = CreateGameData();

		var errors = GameDataValidation.Validate(CreateCharacterData(), CreateLocationList(), CreateProfessionList(), gameData.Families, CreateRelationshipSettings(), CreateCalendarSettings(), schedules);

		Assert.That(errors, Is.EquivalentTo(new[]
		{
			OurResources.DataIdUnknown.FormatCurrentCulture("needs[outing].place.quarterKinds", "beach", "quarterKinds"),
			OurResources.DataIdUnknown.FormatCurrentCulture("needs[outing].hours.days", "someday", "days"),
			OurResources.ConditionValueUnknown.FormatCurrentCulture("needs[outing].when.values.profession", "pirate", "profession"),
			OurResources.DataIdUnknown.FormatCurrentCulture("needs[errand].steps[0].when.businessProfession", "pirate", "professions"),
			OurResources.DataIdUnknown.FormatCurrentCulture("needs[errand].steps[1].when.businessProfession", "homemaker", "professions"),
		}));
	}

	private static LevelNeedSettings Level() => new() { PerHour = 1, SatisfyPerHour = 1 };

	private static QuotaNeedSettings Timed(int minutes) => new() { Lead = 60, Time = new QuotaTime { Minutes = Amount(minutes), Shift = 60 } };

	private static Need Quota(string id, SchedulePlace place, QuotaNeedSettings quota, Hours? hours = null) =>
		new() { Id = id, Title = id, Icon = "icon", PlaceSetting = place, Quota = quota, Hours = hours ?? Hours(6, 12) };

	private static Need Quota(string id, SchedulePlace place, QuotaNeedSettings quota, IReadOnlyList<string> days) =>
		new() { Id = id, Title = id, Icon = "icon", PlaceSetting = place, Quota = quota, Hours = new Hours { Start = 6, End = 12, Days = days } };

	private static Need Quota(string id, QuotaNeedSettings quota, params Step[] steps) =>
		new() { Id = id, Title = id, Icon = "icon", StepSettings = steps, Quota = quota, Hours = Hours(6, 12) };

	private static Need Runs(string id, FamilyAmount runs, bool shared, params Step[] steps) => Quota(id, new QuotaNeedSettings { Lead = 60, SharedByFamily = shared, Runs = runs }, steps);

	private static Step CountRun() => new() { Kind = StepKind.CountRun };

	/// <summary>
	/// Creates the test schedules with the given needs, and a sleep need so they're valid apart from the given needs.
	/// </summary>
	private static ScheduleSettings With(params Need[] needs)
	{
		var settings = CreateScheduleSettings();
		settings.Needs = [new Need { Id = "sleep", Title = "Sleep", Icon = "icon", Role = ScheduleSettings.SleepRole, PlaceSetting = Home, Level = Level() }, .. needs];
		settings.UrgencyFactors = [];
		return settings;
	}
}
