using System.Text.Json;
using GoldenAnvil.Utility;
using OperaEtDies.Data;
using static OperaEtDies.Test.TestData;

namespace OperaEtDies.Test.Data;

/// <summary>
/// What happens when steps fail, durations, bundles, and the other settings steps gained with sequences.
/// </summary>
[TestFixture]
public sealed class StepFailureTests
{
	[Test]
	public void FailuresAreReadAsOneResultOrOnePerCause()
	{
		Assert.That(Read<StepFailure>("\"next\"").Any, Is.EqualTo(new StepFailureResult(StepFailureKind.Next)));
		Assert.That(Read<StepFailure>("\"stop\"").Any, Is.EqualTo(new StepFailureResult(StepFailureKind.Stop)));
		Assert.That(Read<StepFailure>("""{ "skipTo": "home" }""").Any, Is.EqualTo(new StepFailureResult(StepFailureKind.SkipTo, "home")));

		var byCause = Read<StepFailure>("""{ "comments": "Only some causes.", "no": "next", "noShow": { "skipTo": "home" } }""");
		Assert.That(byCause.Get(FailCause.No), Is.EqualTo(new StepFailureResult(StepFailureKind.Next)));
		Assert.That(byCause.Get(FailCause.NoShow), Is.EqualTo(new StepFailureResult(StepFailureKind.SkipTo, "home")));
		Assert.That(byCause.Get(FailCause.Left), Is.Null);

		foreach (var json in new[] { "\"later\"", "3", """{ "flying": "next" }""", """{ "skipTo": "home", "no": "next" }""", "{}", """{ "no": { "goTo": "home" } }""" })
			Assert.That(() => Read<StepFailure>(json), Throws.InstanceOf<JsonException>(), json);
	}

	[Test]
	public void LoopResultsAreReadWithOrWithoutTheLoop()
	{
		Assert.That(Read<StepFailure>("\"loopContinue\"").Any, Is.EqualTo(new StepFailureResult(StepFailureKind.LoopContinue)));
		Assert.That(Read<StepFailure>("\"loopBreak\"").Any, Is.EqualTo(new StepFailureResult(StepFailureKind.LoopBreak)));
		Assert.That(Read<StepFailure>("""{ "loopBreak": "work" }""").Any, Is.EqualTo(new StepFailureResult(StepFailureKind.LoopBreak, "work")));
		var byCause = Read<StepFailure>("""{ "no": "loopContinue", "nobody": { "loopContinue": "work" } }""");
		Assert.That((byCause.Get(FailCause.No), byCause.Get(FailCause.Nobody)), Is.EqualTo((new StepFailureResult(StepFailureKind.LoopContinue), new StepFailureResult(StepFailureKind.LoopContinue, "work"))));

		// They're written back the way they're read.
		foreach (var json in new[] { "\"loopContinue\"", """{"loopBreak":"work"}""", """{"no":"loopContinue","nobody":{"loopContinue":"work"}}""" })
			Assert.That(JsonSerializer.Serialize(Read<StepFailure>(json)), Is.EqualTo(json));
		foreach (var json in new[] { """{ "skipTo": "home", "loopBreak": "work" }""", """{ "no": { "loopContinue": "a", "loopBreak": "b" } }""" })
			Assert.That(() => Read<StepFailure>(json), Throws.InstanceOf<JsonException>(), json);
	}

	[Test]
	public void LoopResultsGoToTheNearestLoopOrTheOneNamed()
	{
		StepFailure On(FailCause cause, StepFailureResult result) => new() { ByCause = new Dictionary<FailCause, StepFailureResult> { [cause] = result } };
		var need = Need(
			new Step
			{
				Kind = StepKind.Repeat,
				Id = "outer",
				Steps =
				[
					new Step
					{
						Kind = StepKind.Repeat,
						Steps =
						[
							new Step { Kind = StepKind.Do, Minutes = Amount(5), OnFail = On(FailCause.No, new StepFailureResult(StepFailureKind.LoopContinue)) },
							new Step { Kind = StepKind.Do, Minutes = Amount(5), OnFail = On(FailCause.No, new StepFailureResult(StepFailureKind.LoopBreak, "outer")) },
						],
						OnFail = On(FailCause.Nobody, new StepFailureResult(StepFailureKind.LoopBreak)),
					},
				],
			},
			Do(Amount(5)));

		// Laid out: the two "do" steps, the inner "repeat" step, the outer one, and the last "do" step. Going on with a loop
		// goes to its "repeat" step, and leaving it goes on after it. A "repeat" step's own result is for its own loop.
		Assert.That(need.GetFailure(0, FailCause.No), Is.EqualTo((StepFailureKind.LoopContinue, (int?) 2)));
		Assert.That(need.GetFailure(1, FailCause.No), Is.EqualTo((StepFailureKind.LoopBreak, (int?) 4)));
		Assert.That(need.GetFailure(0, FailCause.Nobody), Is.EqualTo((StepFailureKind.LoopBreak, (int?) 3)));
	}

	[Test]
	public void LoopsFinallyStepsAndCarryingAreChecked()
	{
		StepFailure Always(StepFailureResult result) => new() { Any = result };
		var need = Need(
			new Step { Kind = StepKind.Do, Minutes = Amount(5), OnFail = Always(new StepFailureResult(StepFailureKind.LoopContinue)) },
			new Step
			{
				Kind = StepKind.Repeat,
				Id = "loop",
				Steps =
				[
					new Step { Kind = StepKind.Do, Minutes = Amount(5), MeetsQuota = true, OnFail = Always(new StepFailureResult(StepFailureKind.SkipTo, "loop")) },
					new Step { Kind = StepKind.Do, Minutes = Amount(5), OnFail = Always(new StepFailureResult(StepFailureKind.LoopBreak, "other")) },
				],
				Finally = [new Step { Kind = StepKind.Do, Minutes = Amount(5), OnFail = Always(new StepFailureResult(StepFailureKind.Next)) }],
			},
			new Step { Kind = StepKind.Group, Steps = [] },
			new Step { Kind = StepKind.Do, Minutes = Amount(5), When = new StepCondition { Carrying = "washing" } });

		Assert.That(With(need).Validate(), Is.SupersetOf(new[]
		{
			OurResources.StepLoopResultWithoutLoop.FormatCurrentCulture("needs[n].steps[0].onFail", "loopContinue"),
			OurResources.StepSkipToEnclosingLoop.FormatCurrentCulture("needs[n].steps[1].steps[0].onFail", "loop"),
			OurResources.StepLoopResultUnknown.FormatCurrentCulture("needs[n].steps[1].steps[1].onFail", "other"),
			OurResources.StepOnFailInFinally.FormatCurrentCulture("needs[n].steps[1].finally[0].onFail"),
			OurResources.DataListEmpty.FormatCurrentCulture("needs[n].steps[2].steps"),
			OurResources.StepBundleUnknown.FormatCurrentCulture("needs[n].steps[3].when.carrying", "washing"),
		}));

		// Skipping to a loop from outside it starts it again, and "finally" steps can name a bundle replaced since.
		var fine = Need(
			new Step { Kind = StepKind.Do, Minutes = Amount(5), Id = "before", OnFail = Always(new StepFailureResult(StepFailureKind.SkipTo, "loop")) },
			new Step { Kind = StepKind.Repeat, Id = "loop", Steps = [new Step { Kind = StepKind.Do, Minutes = Amount(5), MeetsQuota = true, OnFail = Always(new StepFailureResult(StepFailureKind.LoopContinue)) }] },
			new Step
			{
				Kind = StepKind.Group,
				Steps =
				[
					new Step { Kind = StepKind.Make, Bundle = Bundle("empty", "Empty bucket") },
					new Step { Kind = StepKind.Make, Bundle = Bundle("full", "Full bucket"), Replaces = "empty" },
				],
				Finally = [new Step { Kind = StepKind.Put, Bundle = Bundle("empty"), When = new StepCondition { Carrying = "empty" } }],
			});
		Assert.That(With(fine).Validate().Where(x => !x.Contains("repeat", StringComparison.Ordinal) && !x.Contains("meetsQuota", StringComparison.Ordinal)), Is.Empty);
	}

	[Test]
	public void DurationsAreMinutesOrHoursAndDays()
	{
		Assert.That(Read<Duration>("60").Minutes, Is.EqualTo(60));
		Assert.That(Read<Duration>("\"1:00\"").Minutes, Is.EqualTo(60));
		Assert.That(Read<Duration>("\"2:15:35\"").Minutes, Is.EqualTo((((2 * 24) + 15) * 60) + 35));
		foreach (var json in new[] { "-5", "1.5", "\"60\"", "\"1:60\"", "\"1:5\"", "\"1:24:00\"", "\"a:00\"", "\"1:00:00:00\"" })
			Assert.That(() => Read<Duration>(json), Throws.InstanceOf<JsonException>(), json);
	}

	[Test]
	public void OtherLengthsOfTimeCanBeDurations()
	{
		var step = Read<Step>("""{ "step": "wander", "place": { "quarterKinds": [ { "quarterKind": "square" } ] }, "minutes": "1:30", "stopMinutes": { "minimum": 10, "maximum": "1:00" } }""");
		Assert.That(step.Minutes!.GetAmount(0), Is.EqualTo(90));
		Assert.That((step.StopMinutes!.Minimum.Minutes, step.StopMinutes.Maximum.Minutes), Is.EqualTo((10, 60)));
		Assert.That(Read<FamilyDuration>("""{ "base": "1:00", "perMember": 15 }""").GetAmount(2), Is.EqualTo(90));
		Assert.That(Read<ClothingSchedule>("""{ "changeMinutes": "0:05" }""").ChangeMinutes.Minutes, Is.EqualTo(5));

		// Amounts that aren't lengths of time stay numbers.
		Assert.That(() => Read<FamilyAmount>("\"1:00\""), Throws.InstanceOf<JsonException>());
	}

	[Test]
	public void BundlesAreAnIdOrAnIdAndTitle()
	{
		Assert.That(Read<StepBundle>("\"washing\"") is { Id: "washing", Title: null }, Is.True);
		Assert.That(Read<StepBundle>("""{ "id": "washing", "title": "Washing" }""") is { Id: "washing", Title: "Washing" }, Is.True);
		Assert.That(() => Read<StepBundle>("""{ "title": "Washing" }"""), Throws.InstanceOf<JsonException>());
	}

	[Test]
	public void FailuresSkipToStepsWithIdsOfTheirOwnSteps()
	{
		var need = Need(
			new Step { Kind = StepKind.Do, Minutes = Amount(5), Id = "first", OnFail = new StepFailure { Any = new StepFailureResult(StepFailureKind.SkipTo, "missing") } },
			new Step { Kind = StepKind.Do, Minutes = Amount(5), Id = "first" },
			new Step
			{
				Kind = StepKind.Choose,
				Options = [new StepOption { Steps = [Do(Amount(5))], OnFail = new StepFailure { Any = new StepFailureResult(StepFailureKind.SkipTo, "first") } }],
			});

		Assert.That(With(need).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.StepIdDuplicate.FormatCurrentCulture("needs[n].steps[1].id", "first"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[n].steps[2].options[0].player.title"),
			OurResources.StepSkipToUnknown.FormatCurrentCulture("needs[n].steps[0].onFail.skipTo", "missing"),
		}));
	}

	[Test]
	public void BundlesMustBeTakenOrMadeBeforeTheyrePutDown()
	{
		var need = Need(
			new Step { Kind = StepKind.Make, Bundle = Bundle("empty", "Empty bucket") },
			new Step { Kind = StepKind.Make, Bundle = Bundle("full", "Full bucket"), Replaces = "nothing" },
			Do(Amount(5)),
			new Step { Kind = StepKind.Put, Bundle = Bundle("full"), ItemKind = StepItemKind.Clothing, To = StepItemPlace.Wardrobe },
			new Step { Kind = StepKind.Put, Bundle = Bundle("empty", "Title"), AfterDelay = new Duration(5) });

		Assert.That(With(need).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.StepBundleUnknown.FormatCurrentCulture("needs[n].steps[1].replaces", "nothing"),
			OurResources.StepPutKindMismatch.FormatCurrentCulture("needs[n].steps[3].kind", "full"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[n].steps[4].afterDelay", "put"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[n].steps[4].bundle.title", "put"),
		}));
	}

	[Test]
	public void FailuresAreLeftToWhatTheStepIsIn()
	{
		var next = new StepFailureResult(StepFailureKind.Next);
		var need = Need(
			Do(Amount(5)),
			new Step
			{
				Kind = StepKind.Repeat,
				OnFail = new StepFailure { ByCause = new Dictionary<FailCause, StepFailureResult> { [FailCause.Left] = next } },
				Steps = [new Step { Kind = StepKind.Do, Minutes = Amount(5), MeetsQuota = true, OnFail = new StepFailure { ByCause = new Dictionary<FailCause, StepFailureResult> { [FailCause.No] = new(StepFailureKind.SkipTo, "start") } } }],
			});
		need = new Need { Id = need.Id, Title = need.Title, Icon = "icon", Category = NeedCategory.Personal, Hours = need.Hours, Quota = need.Quota, StepSettings = [new Step { Kind = StepKind.Do, Minutes = Amount(5), Id = "start" }, .. need.StepSettings!.Skip(1)], OnFail = new StepFailure { Any = next } };

		// The step's own result comes first, then the repeat's, then the need's, and without any, the steps are given up.
		Assert.That(need.GetFailure(1, FailCause.No), Is.EqualTo((StepFailureKind.SkipTo, (int?) 0)));
		Assert.That(need.GetFailure(1, FailCause.Left), Is.EqualTo((StepFailureKind.Next, (int?) null)));
		Assert.That(need.GetFailure(1, FailCause.Hours), Is.EqualTo((StepFailureKind.Next, (int?) null)));
		var bare = Need(Do(Amount(5)));
		Assert.That(bare.GetFailure(0, FailCause.Hours), Is.EqualTo((StepFailureKind.Stop, (int?) null)));
	}

	[Test]
	public void ConditionsCanBeNegatedOrCombined()
	{
		var need = Need(
			new Step { Kind = StepKind.Do, Minutes = Amount(5), When = new StepCondition { Not = new StepCondition() } },
			new Step { Kind = StepKind.Do, Minutes = Amount(5), When = new StepCondition { Any = [] } },
			new Step { Kind = StepKind.Do, Minutes = Amount(5), When = new StepCondition { Any = [new StepCondition { AnsweredYes = "asked" }] } },
			new Step { Kind = StepKind.Do, Minutes = Amount(5), When = new StepCondition { Not = new StepCondition { HasJob = true } } });

		Assert.That(With(need).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.DataValueMissing.FormatCurrentCulture("needs[n].steps[0].when.not"),
			OurResources.DataListEmpty.FormatCurrentCulture("needs[n].steps[1].when.any"),
			OurResources.StepAnswerUnknown.FormatCurrentCulture("needs[n].steps[2].when.any[0].answeredYes", "asked"),
		}));
	}

	[Test]
	public void StepListsNoNeedCallsAreCheckedOnTheirOwnWithAWarning()
	{
		var settings = With(Need(Do(Amount(5)), new Step { Kind = StepKind.Call, List = "called" }));
		settings.StepLists =
		[
			new StepList { Id = "called", Steps = [Do(Amount(1))] },
			new StepList { Id = "unused", Steps = [new Step { Kind = StepKind.Wait }, new Step { Kind = StepKind.GoTo, Place = new SchedulePlace { Kind = NeedPlace.Remembered, Remembered = "earlier" } }] },
		];

		// What depends on the steps before it, such as a remembered place, could come from a caller, so it isn't reported.
		Assert.That(settings.Validate(), Is.EquivalentTo(new[] { OurResources.StepSettingMissing.FormatCurrentCulture("stepLists[unused].steps[0].minutes", "wait") }));
		Assert.That(settings.GetWarnings(), Is.EqualTo(new[] { OurResources.StepListNotCalled.FormatCurrentCulture("unused") }));
	}

	[Test]
	public void JobsPartnersNeedsAndDelaysAreChecked()
	{
		var partnerHome = new SchedulePlace { Kind = NeedPlace.PartnerHome };
		Step Ask(StepJob? job = null, StepAppointment? appointment = null, string? partnerMeets = null) => new()
		{
			Kind = StepKind.Ask,
			With = StepTarget.Anyone,
			Name = "asked",
			Minutes = Amount(5),
			Chance = new StepChance { Base = 50 },
			Player = new StepPlayer { Title = "Ask" },
			Job = job,
			Appointment = appointment,
			PartnerMeets = partnerMeets,
		};
		var need = Need(
			Do(Amount(5)),
			Ask(new StepJob { Place = new SchedulePlace { Kind = NeedPlace.JobSite } }),
			Ask(new StepJob { Place = partnerHome, Minutes = 60 }, AtPartnerHome([Do(Amount(5))])),
			Ask(partnerMeets: "flying"),
			new Step { Kind = StepKind.Take, ItemKind = StepItemKind.Clothing, From = StepItemPlace.Wardrobe, Bundle = Bundle("washing", "Washing") },
			new Step { Kind = StepKind.Put, ItemKind = StepItemKind.Clothing, To = StepItemPlace.Wardrobe, Bundle = Bundle("washing"), AfterDelay = new Duration(5) },
			new Step { Kind = StepKind.Wait, Minutes = Amount(5), PartnerMeets = "sleep", DelayActivity = "Drying" });

		Assert.That(With(need).Validate(), Is.EquivalentTo(new[]
		{
			OurResources.StepJobPlaceInvalid.FormatCurrentCulture("needs[n].steps[1].job.place"),
			OurResources.DataValueMissing.FormatCurrentCulture("needs[n].steps[1].job.minutes"),
			OurResources.StepJobWithAppointment.FormatCurrentCulture("needs[n].steps[2].job"),
			OurResources.DataIdUnknown.FormatCurrentCulture("needs[n].steps[3].partnerMeets", "flying", "needs"),
			OurResources.StepSettingMissing.FormatCurrentCulture("needs[n].steps[5].delayActivity", "put"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[n].steps[6].partnerMeets", "wait"),
			OurResources.StepSettingNotAllowed.FormatCurrentCulture("needs[n].steps[6].delayActivity", "wait"),
		}));
	}

	private static Need Need(params Step[] steps) => new() { Id = "n", Title = "N", Icon = "icon", Category = NeedCategory.Personal, StepSettings = steps, Level = new LevelNeedSettings { PerHour = 1, SatisfyPerHour = 1 } };

	private static ScheduleSettings With(params Need[] needs)
	{
		var settings = CreateScheduleSettings();
		settings.Needs = [new Need { Id = "sleep", Title = "Sleep", Icon = "icon", Category = NeedCategory.Personal, Role = ScheduleSettings.SleepRole, PlaceSetting = Home, Level = new LevelNeedSettings { PerHour = 1, SatisfyPerHour = 1 } }, .. needs];
		settings.UrgencyFactors = [];
		foreach (var need in settings.Needs)
			need.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		return settings;
	}

	private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonDataLoader.Options)!;
}
