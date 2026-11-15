using System.IO;
using System.Text.Json;
using GoldenAnvil.Utility;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// How people decide what to do, loaded from Schedules.json. Whenever someone finishes what they were doing, they
/// choose their next goal from how urgent each of their needs is.
/// </summary>
public sealed class ScheduleSettings : IValidatedData
{
	public const string FileName = "Schedules.json";

	/// <summary>
	/// Settings with no needs, where people stay at home and wake at six, for game data created without a schedule file.
	/// </summary>
	public static ScheduleSettings None { get; } = new()
	{
		MinimumUrgency = 0,
		UrgencyPower = 1,
		SightingHours = 24,
		Sleep = new SleepSettings { NapBelow = 0, NapMinutes = new IntegerRange { Minimum = 30, Maximum = 30, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 1 } }, BedtimeLead = 60 },
		Talking = new TalkingSettings { RefuseUrgency = 100, WakeLevel = 0, AskAboutMinutes = 1, AskDoingMinutes = 5, SocializeMinutes = 30 },
		Socializing = new SocializingSettings { ChatUrgency = 100, ChatGoalWeight = 1, WakeUrgency = 100, NearMinutes = 15, SeekFactor = 1, SeekMinutes = 15, RecentHours = 12, AskPlayerAgainHours = 4, Clues = new SeekClues { Workplace = 1, Home = 1, Sighting = 1, Gathering = 1 } },
		Needs = [],
		Idle = new IdleSettings { Place = NeedPlace.Home, Minutes = new IntegerRange { Minimum = 60, Maximum = 60, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 1 } } },
		Start = new ScheduleStart { Need = "", WakeHour = 6, WakeMinutes = new IntegerRange { Minimum = 0, Maximum = 0, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 1 } } },
	};

	public static DataLoadResult<ScheduleSettings> Load(string dataDirectory) => Load(dataDirectory, []);

	/// <summary>
	/// Loads the file from the data folder, with the changes from the mod folders applied in order.
	/// </summary>
	public static DataLoadResult<ScheduleSettings> Load(string dataDirectory, IReadOnlyList<string> modDirectories) =>
		JsonDataLoader.Load<ScheduleSettings>(Path.Combine(dataDirectory, FileName), modDirectories, []);

	/// <summary>
	/// Needs less urgent than this aren't considered when choosing a goal.
	/// </summary>
	public required double MinimumUrgency { get; init; }

	/// <summary>
	/// The power each need's urgency is raised to when choosing a goal at random, so more urgent needs are more likely.
	/// </summary>
	public required double UrgencyPower { get; init; }

	/// <summary>
	/// How many hours people remember the last time they saw someone.
	/// </summary>
	public required int SightingHours { get; init; }

	/// <summary>
	/// How naps work, and how people go to bed in time for work. The sleep need is the start need.
	/// </summary>
	public required SleepSettings Sleep { get; init; }

	/// <summary>
	/// When people refuse to talk, and what waking someone to talk costs them.
	/// </summary>
	public required TalkingSettings Talking { get; init; }

	/// <summary>
	/// How people stop to chat with friends they meet, and seek them out when they're lonely.
	/// </summary>
	public required SocializingSettings Socializing { get; init; }

	/// <summary>
	/// Whether a need this urgent is considered when choosing a goal.
	/// </summary>
	public bool IsGoalCandidate(double urgency) => urgency >= MinimumUrgency && urgency > 0;

	/// <summary>
	/// Whether a need this urgent is considered when choosing a goal. The need met by socializing counts for less, by
	/// <see cref="SocializingSettings.SeekFactor"/>.
	/// </summary>
	public bool IsGoalCandidate(Need need, double urgency) => IsGoalCandidate(GetGoalUrgency(need, urgency));

	/// <summary>
	/// How heavily a need this urgent weighs when choosing a goal at random among the candidates. The need met by
	/// socializing counts for less, by <see cref="SocializingSettings.SeekFactor"/>.
	/// </summary>
	public double GetGoalWeight(Need need, double urgency) => GetGoalWeight(GetGoalUrgency(need, urgency));

	private double GetGoalUrgency(Need need, double urgency) => need.Id == Talking.SocializeNeed ? urgency * Socializing.SeekFactor : urgency;

	/// <summary>
	/// How heavily a need this urgent weighs when choosing a goal at random among the candidates.
	/// </summary>
	public double GetGoalWeight(double urgency) => Math.Pow(urgency, UrgencyPower);

	public required IReadOnlyList<Need> Needs { get; init; }

	/// <summary>
	/// What people do when none of their needs are urgent enough.
	/// </summary>
	public required IdleSettings Idle { get; init; }

	/// <summary>
	/// How everyone starts a new game.
	/// </summary>
	public required ScheduleStart Start { get; init; }

	/// <summary>
	/// Factors that make some people slower to travel. Every factor whose condition a person meets applies.
	/// </summary>
	public IReadOnlyList<TravelTimeFactor> TravelTimeFactors { get; init; } = [];

	/// <summary>
	/// Factors that make some needs more or less urgent for people who meet a condition, such as household chores for a
	/// homemaker. Every factor whose condition a person meets applies to its needs.
	/// </summary>
	public IReadOnlyList<UrgencyFactor> UrgencyFactors { get; init; } = [];

	public IEnumerable<string> Validate()
	{
		var errors = new List<string>();
		if (MinimumUrgency < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("minimumUrgency", MinimumUrgency, 0));
		if (!(UrgencyPower > 0))
			errors.Add(OurResources.DataNumberNotPositive.FormatCurrentCulture("urgencyPower", UrgencyPower));

		errors.AddRange(DataValidation.ValidateIdentifiedItems("needs", Needs, x => x.Id, x => x.Title));
		var needs = Needs.OfType<Need>().Where(x => !string.IsNullOrWhiteSpace(x.Id)).ToList();
		var needIds = needs.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		foreach (var need in needs)
			errors.AddRange(need.Validate(needIds));

		if (SightingHours < 1)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("sightingHours", SightingHours, 1));
		if (!(Talking.RefuseUrgency >= 0) || !double.IsFinite(Talking.RefuseUrgency))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("talking.refuseUrgency", Talking.RefuseUrgency, 0));
		if (!(Talking.WakeLevel >= 0))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("talking.wakeLevel", Talking.WakeLevel, 0));
		else if (Talking.WakeLevel > Need.MaximumLevel)
			errors.Add(OurResources.DataNumberTooLarge.FormatCurrentCulture("talking.wakeLevel", Talking.WakeLevel, Need.MaximumLevel));
		foreach (var (name, minutes) in new[] { ("talking.askAboutMinutes", Talking.AskAboutMinutes), ("talking.askDoingMinutes", Talking.AskDoingMinutes), ("talking.socializeMinutes", Talking.SocializeMinutes) })
		{
			if (minutes < 1)
				errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture(name, minutes, 1));
		}
		errors.AddRange(Socializing.Validate());
		errors.AddRange(Sleep.Validate());
		if (Talking.SocializeNeed is { } socializeNeed && needs.FirstOrDefault(x => x.Id == socializeNeed) is not { Kind: NeedKind.Level })
			errors.Add(OurResources.ScheduleSocializeNeedInvalid.FormatCurrentCulture(socializeNeed));

		errors.AddRange(ValidatePlace("idle", Idle.Place, Idle.QuarterKinds));
		if (Idle.Activity is not null && string.IsNullOrWhiteSpace(Idle.Activity))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture("idle.activity"));
		errors.AddRange(CharacterData.ValidateRange("idle.minutes", Idle.Minutes));
		if (Idle.Minutes.Minimum < 1)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("idle.minutes.minimum", Idle.Minutes.Minimum, 1));

		if (needs.FirstOrDefault(x => x.Id == Start.Need) is not { Kind: NeedKind.Level })
			errors.Add(OurResources.ScheduleStartNeedInvalid.FormatCurrentCulture(Start.Need));
		if (Start.WakeHour is < 0 or > 23)
			errors.Add(OurResources.ScheduleHourInvalid.FormatCurrentCulture("start.wakeHour", Start.WakeHour, 23));
		errors.AddRange(CharacterData.ValidateRange("start.wakeMinutes", Start.WakeMinutes));

		for (var index = 0; index < TravelTimeFactors.Count; index++)
		{
			if (TravelTimeFactors[index] is not { } factor)
				errors.Add(OurResources.DataListItemEmpty.FormatCurrentCulture("travelTimeFactors"));
			else if (!(factor.Factor > 0))
				errors.Add(OurResources.DataNumberNotPositive.FormatCurrentCulture($"travelTimeFactors[{index}].factor", factor.Factor));
		}

		for (var index = 0; index < UrgencyFactors.Count; index++)
		{
			var path = $"urgencyFactors[{index}]";
			if (UrgencyFactors[index] is not { } factor)
			{
				errors.Add(OurResources.DataListItemEmpty.FormatCurrentCulture("urgencyFactors"));
			}
			else
			{
				if (!(factor.Factor > 0))
					errors.Add(OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.factor", factor.Factor));
				errors.AddRange(DataValidation.ValidateStrings($"{path}.needs", factor.Needs));
				errors.AddRange(factor.Needs.Where(x => x is not null && !needIds.Contains(x)).Select(x => OurResources.DataIdUnknown.FormatCurrentCulture($"{path}.needs", x, "needs")));
			}
		}

		return errors;
	}

	/// <summary>
	/// Gets the conditions in the file, with where each one is, for checking against the character properties.
	/// </summary>
	internal IEnumerable<(string Path, CharacterCondition Condition)> GetConditions() =>
		Needs.OfType<Need>().Where(x => x.When is not null).Select(x => ($"needs[{x.Id}].when", x.When!))
			.Concat(TravelTimeFactors.OfType<TravelTimeFactor>().Select((x, index) => ($"travelTimeFactors[{index}].when", x.When)))
			.Concat(UrgencyFactors.OfType<UrgencyFactor>().Select((x, index) => ($"urgencyFactors[{index}].when", x.When)));

	/// <summary>
	/// Gets the kinds of quarter the file names, with where each one is, for checking against Locations.json.
	/// </summary>
	internal IEnumerable<(string Path, string QuarterKind)> GetQuarterKinds() =>
		Needs.OfType<Need>().SelectMany(need => (need.QuarterKinds ?? []).OfType<QuarterChoice>().Select(x => ($"needs[{need.Id}].quarterKinds", x.QuarterKind)))
			.Concat((Idle.QuarterKinds ?? []).OfType<QuarterChoice>().Select(x => ("idle.quarterKinds", x.QuarterKind)));

	/// <summary>
	/// Gets the days of the week the file names, with where each one is, for checking against Calendar.json.
	/// </summary>
	internal IEnumerable<(string Path, string Day)> GetDays() =>
		Needs.OfType<Need>().SelectMany(need => (need.Days ?? []).Select(x => ($"needs[{need.Id}].days", x)));

	internal static IEnumerable<string> ValidatePlace(string path, NeedPlace place, IReadOnlyList<QuarterChoice>? quarterKinds)
	{
		if (place == NeedPlace.Quarter)
		{
			if (quarterKinds is null || quarterKinds.Count == 0)
				yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.quarterKinds");
			else if (quarterKinds.Any(x => x is null || string.IsNullOrWhiteSpace(x.QuarterKind)))
				yield return OurResources.DataListItemBlank.FormatCurrentCulture($"{path}.quarterKinds");
		}
		else if (quarterKinds is not null)
		{
			yield return OurResources.ScheduleQuarterKindsNotAllowed.FormatCurrentCulture(path);
		}
	}
}

/// <summary>
/// Something people need, which grows more urgent until they do something about it.
/// </summary>
public sealed class Need : IIdentified
{
	/// <summary>
	/// The highest a level need can be. Level needs run from 0 to this.
	/// </summary>
	public const double MaximumLevel = 100;

	public required string Id { get; init; }

	public required string Title { get; init; }

	public required NeedKind Kind { get; init; }

	/// <summary>
	/// Who has the need. Without it, everyone does.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// Where the need is met.
	/// </summary>
	public required NeedPlace Place { get; init; }

	/// <summary>
	/// For a need met in a quarter, the kinds of quarter it can be met in, chosen by frequency.
	/// </summary>
	public IReadOnlyList<QuarterChoice>? QuarterKinds { get; init; }

	/// <summary>
	/// Whether people are at work while meeting the need.
	/// </summary>
	public bool AtWork { get; init; }

	/// <summary>
	/// For a need met in a quarter, whether people go to the nearest quarter of any of its kinds, rather than choosing a
	/// kind by frequency, such as for fetching water.
	/// </summary>
	public bool Nearest { get; init; }

	/// <summary>
	/// For a level need, how much it grows each hour, unless it's being met or paused.
	/// </summary>
	public double? PerHour { get; init; }

	/// <summary>
	/// For a level need, how much it drops each hour while it's being met.
	/// </summary>
	public double? SatisfyPerHour { get; init; }

	/// <summary>
	/// For a level need, the needs that stop it growing while they're being met, such as sleep.
	/// </summary>
	public IReadOnlyList<string>? PausedBy { get; init; }

	/// <summary>
	/// For a level need, its level at the start of a new game. Defaults to 0.
	/// </summary>
	public IntegerRange? StartLevel { get; init; }

	/// <summary>
	/// For a level need, the level it starts to become urgent at. Its urgency rises from 0 there to fully urgent at
	/// <see cref="MaximumLevel"/>. Defaults to 0.
	/// </summary>
	public double? UrgentFrom { get; init; }

	/// <summary>
	/// For a level need, how urgent it must get to wake someone who's asleep, if it's also more urgent than sleep is by then.
	/// Without it, the need never wakes anyone.
	/// </summary>
	public double? WakeUrgency { get; init; }

	/// <summary>
	/// For a level need, the words for how high it is, shown to the player. Without them, the need isn't shown.
	/// </summary>
	public IReadOnlyList<NeedLevel>? Levels { get; init; }

	/// <summary>
	/// For a quota need, whether its quota is for each day or each week. Defaults to each day.
	/// </summary>
	public NeedPeriod Period { get; init; } = NeedPeriod.Day;

	/// <summary>
	/// For a quota need, the days of the week it can be met on. Defaults to every day.
	/// </summary>
	public IReadOnlyList<string>? Days { get; init; }

	/// <summary>
	/// For a quota need, whether it can be met on special days. Defaults to true.
	/// </summary>
	public bool OnSpecialDays { get; init; } = true;

	/// <summary>
	/// The hour the need can be met from. A quota need must have it. A level need can have it with <see cref="End"/>, and
	/// its hours run past midnight if it starts after it ends.
	/// </summary>
	public int? Start { get; init; }

	/// <summary>
	/// The hour the need can be met until: up to 24 for a quota need, or up to 23 for a level need.
	/// </summary>
	public int? End { get; init; }

	/// <summary>
	/// For a quota need measured in time, how many minutes it takes each period.
	/// </summary>
	public int? Minutes { get; init; }

	/// <summary>
	/// For a quota need measured in time and shared by the family, the minutes it takes for each family member, added to
	/// <see cref="Minutes"/>.
	/// </summary>
	public int? MinutesPerMember { get; init; }

	/// <summary>
	/// For a quota need measured in time, the most minutes spent on it at once, before deciding again what to do.
	/// </summary>
	public int? Shift { get; init; }

	/// <summary>
	/// For a quota need measured in trips, how many trips it takes each period.
	/// </summary>
	public double? Visits { get; init; }

	/// <summary>
	/// For a quota need measured in trips and shared by the family, the trips it takes for each family member, added to
	/// <see cref="Visits"/>. The total is rounded up.
	/// </summary>
	public double? VisitsPerMember { get; init; }

	/// <summary>
	/// For a quota need measured in trips, how many minutes each trip takes once the person gets there.
	/// </summary>
	public int? VisitMinutes { get; init; }

	/// <summary>
	/// For a quota need measured in trips and shared by the family, the minutes each trip takes there for each family
	/// member, added to <see cref="VisitMinutes"/>.
	/// </summary>
	public int? VisitMinutesPerMember { get; init; }

	/// <summary>
	/// For a quota need measured in trips that returns home, the minutes each trip takes at home afterward, such as hanging
	/// up washing.
	/// </summary>
	public int? HomeMinutes { get; init; }

	/// <summary>
	/// For a quota need measured in trips that returns home and is shared by the family, the minutes each trip takes at
	/// home afterward for each family member, added to <see cref="HomeMinutes"/>.
	/// </summary>
	public int? HomeMinutesPerMember { get; init; }

	/// <summary>
	/// For a quota need, how many minutes of spare time are left when it starts to become urgent. It's fully urgent
	/// when there's just enough time left to meet it.
	/// </summary>
	public int? Lead { get; init; }

	/// <summary>
	/// For a quota need, whether it's met for the whole family by anyone in it, such as shopping.
	/// </summary>
	public bool SharedByFamily { get; init; }

	/// <summary>
	/// Whether people go home after meeting the need, such as to carry water back.
	/// </summary>
	public bool ReturnHome { get; init; }

	/// <summary>
	/// For a quota need measured by the week with a list of days, the urgency it has once those days have passed this week
	/// and its quota still isn't met. It can then be met on any day for the rest of the week, such as laundry on a missed
	/// wash day.
	/// </summary>
	public double? OverdueUrgency { get; init; }

	/// <summary>
	/// What someone meeting the need is shown to be doing, such as "Sleeping". Defaults to the title.
	/// </summary>
	public string? Activity { get; init; }

	/// <summary>
	/// For a quota need measured in trips with time at home afterward, what someone is shown to be doing during that time,
	/// such as "Hanging out the washing". Defaults to <see cref="Activity"/>.
	/// </summary>
	public string? HomeActivity { get; init; }

	/// <summary>
	/// How the player can spend time on the need where it's met, such as sleeping at home, or null if they can't.
	/// </summary>
	public PlayerAction? PlayerAction { get; init; }

	/// <summary>
	/// Whether the quota is measured in trips rather than time.
	/// </summary>
	public bool IsTrips => VisitMinutes is not null || VisitMinutesPerMember is not null;

	/// <summary>
	/// For a quota need measured in trips, how many minutes each trip takes at the place for a family with the given number
	/// of members (or for one person, for a need that isn't shared).
	/// </summary>
	public int GetVisitMinutes(int members) => (VisitMinutes ?? 0) + ((VisitMinutesPerMember ?? 0) * GetMemberCount(members));

	/// <summary>
	/// For a quota need measured in trips, how many minutes each trip takes at home afterward.
	/// </summary>
	public int GetHomeMinutes(int members) => (HomeMinutes ?? 0) + ((HomeMinutesPerMember ?? 0) * GetMemberCount(members));

	/// <summary>
	/// For a quota need, the most minutes spent on it at once: a shift, or one trip (at the place and at home afterward).
	/// </summary>
	public int GetShiftMinutes(int members) => IsTrips ? GetVisitMinutes(members) + GetHomeMinutes(members) : Shift ?? 0;

	/// <summary>
	/// For a quota need, how many minutes it takes each period for a family with the given number of members (or for one
	/// person, for a need that isn't shared). Trips are counted by the minutes spent at the place and at home afterward.
	/// </summary>
	public long GetQuotaMinutes(int members) =>
		IsTrips
			? (long) Math.Ceiling((Visits ?? 0) + ((VisitsPerMember ?? 0) * GetMemberCount(members))) * GetShiftMinutes(members)
			: (Minutes ?? 0) + ((long) (MinutesPerMember ?? 0) * GetMemberCount(members));

	private int GetMemberCount(int members) => SharedByFamily ? members : 0;

	internal IEnumerable<string> Validate(IReadOnlySet<string> needIds)
	{
		var path = $"needs[{Id}]";
		foreach (var error in ScheduleSettings.ValidatePlace(path, Place, QuarterKinds))
			yield return error;
		if (Nearest && Place != NeedPlace.Quarter)
			yield return OurResources.NeedSettingNotAllowed.FormatCurrentCulture(Id, "nearest", JsonNamingPolicy.CamelCase.ConvertName(Place.ToString()));
		if (ReturnHome && Place == NeedPlace.Home)
			yield return OurResources.NeedSettingNotAllowed.FormatCurrentCulture(Id, "returnHome", "home");

		if (Activity is not null && string.IsNullOrWhiteSpace(Activity))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.activity");
		if (HomeActivity is not null && string.IsNullOrWhiteSpace(HomeActivity))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.homeActivity");
		if (PlayerAction is { } playerAction)
		{
			foreach (var error in playerAction.Validate($"{path}.playerAction", Kind == NeedKind.Quota, IsTrips, ReturnHome))
				yield return error;
		}

		var isLevel = Kind == NeedKind.Level;
		var kind = isLevel ? "level" : "quota";
		var levelSettings = new (string Name, bool IsSet)[]
		{
			("perHour", PerHour is not null), ("satisfyPerHour", SatisfyPerHour is not null), ("pausedBy", PausedBy is not null),
			("startLevel", StartLevel is not null), ("levels", Levels is not null), ("urgentFrom", UrgentFrom is not null),
			("start", Start is not null), ("end", End is not null), ("wakeUrgency", WakeUrgency is not null),
		};
		var quotaSettings = new (string Name, bool IsSet)[]
		{
			("period", Period != NeedPeriod.Day), ("days", Days is not null), ("start", Start is not null), ("end", End is not null),
			("minutes", Minutes is not null), ("minutesPerMember", MinutesPerMember is not null), ("shift", Shift is not null),
			("visits", Visits is not null), ("visitsPerMember", VisitsPerMember is not null), ("visitMinutes", VisitMinutes is not null),
			("visitMinutesPerMember", VisitMinutesPerMember is not null), ("homeMinutes", HomeMinutes is not null), ("homeMinutesPerMember", HomeMinutesPerMember is not null),
			("lead", Lead is not null), ("onSpecialDays", !OnSpecialDays), ("sharedByFamily", SharedByFamily), ("overdueUrgency", OverdueUrgency is not null),
			("homeActivity", HomeActivity is not null),
		};
		var required = isLevel ? new[] { "perHour", "satisfyPerHour" } : ["start", "end", "lead"];
		foreach (var (name, isSet) in levelSettings.Concat(quotaSettings).DistinctBy(x => x.Name))
		{
			var isAllowed = (isLevel ? levelSettings : quotaSettings).Any(x => x.Name == name);
			if (!isSet && required.Contains(name))
				yield return OurResources.NeedSettingMissing.FormatCurrentCulture(Id, name, kind);
			else if (isSet && !isAllowed)
				yield return OurResources.NeedSettingNotAllowed.FormatCurrentCulture(Id, name, kind);
		}

		if (!isLevel)
		{
			// A quota is measured either in time (minutes, with shifts) or in trips (visits of a number of minutes each).
			var isTimed = Minutes is not null || MinutesPerMember is not null || Shift is not null;
			var isTrips = Visits is not null || VisitsPerMember is not null || VisitMinutes is not null || VisitMinutesPerMember is not null || HomeMinutes is not null || HomeMinutesPerMember is not null;
			if (isTimed && isTrips)
				yield return OurResources.NeedQuotaMixed.FormatCurrentCulture(Id);
			else if (isTrips && VisitMinutes is null && VisitMinutesPerMember is null)
				yield return OurResources.NeedSettingMissing.FormatCurrentCulture(Id, "visitMinutes", kind);
			else if (isTrips && Visits is null && VisitsPerMember is null)
				yield return OurResources.NeedSettingMissing.FormatCurrentCulture(Id, "visits", kind);
			else if (!isTrips && (Minutes is null && MinutesPerMember is null))
				yield return OurResources.NeedSettingMissing.FormatCurrentCulture(Id, "minutes", kind);
			else if (!isTrips && Shift is null)
				yield return OurResources.NeedSettingMissing.FormatCurrentCulture(Id, "shift", kind);

			if (!SharedByFamily && (MinutesPerMember is not null || VisitsPerMember is not null || VisitMinutesPerMember is not null || HomeMinutesPerMember is not null))
				yield return OurResources.NeedPerMemberNotShared.FormatCurrentCulture(Id);
			if (OverdueUrgency is { } overdue)
			{
				if (Period != NeedPeriod.Week || Days is null)
					yield return OurResources.NeedOverdueNotAllowed.FormatCurrentCulture(Id);
				else if (overdue is < 0 or > MaximumLevel)
					yield return OurResources.NeedLevelOutOfRange.FormatCurrentCulture($"{path}.overdueUrgency", MaximumLevel);
			}

			if (HomeActivity is not null && HomeMinutes is null && HomeMinutesPerMember is null)
				yield return OurResources.NeedHomeActivityWithoutHomeMinutes.FormatCurrentCulture(Id);
			if (!ReturnHome && (HomeMinutes is not null || HomeMinutesPerMember is not null))
				yield return OurResources.NeedHomeMinutesWithoutReturn.FormatCurrentCulture(Id);
		}

		if (isLevel)
		{
			if (PerHour < 0)
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.perHour", PerHour, 0);
			if (SatisfyPerHour is { } satisfy && !(satisfy > 0))
				yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.satisfyPerHour", satisfy);
			foreach (var pausedBy in PausedBy ?? [])
			{
				if (!needIds.Contains(pausedBy ?? ""))
					yield return OurResources.DataIdUnknown.FormatCurrentCulture($"{path}.pausedBy", pausedBy, "needs");
			}

			if (UrgentFrom is < 0 or >= MaximumLevel)
				yield return OurResources.NeedUrgentFromOutOfRange.FormatCurrentCulture($"{path}.urgentFrom", UrgentFrom, MaximumLevel);
			if (WakeUrgency is { } wakeUrgency && !(wakeUrgency >= 0))
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.wakeUrgency", wakeUrgency, 0);
			if (Start is null != End is null)
				yield return OurResources.NeedSettingMissing.FormatCurrentCulture(Id, Start is null ? "start" : "end", kind);
			else if (Start is { } levelStart && End is { } levelEnd && (levelStart is < 0 or > 23 || levelEnd is < 0 or > 23 || levelStart == levelEnd))
				yield return OurResources.NeedLevelHoursInvalid.FormatCurrentCulture(Id, levelStart, levelEnd);

			if (StartLevel is { } startLevel)
			{
				foreach (var error in CharacterData.ValidateRange($"{path}.startLevel", startLevel))
					yield return error;
				if (startLevel.Minimum < 0 || startLevel.Maximum > MaximumLevel)
					yield return OurResources.NeedLevelOutOfRange.FormatCurrentCulture($"{path}.startLevel", MaximumLevel);
			}

			if (Levels is { } levels)
			{
				if (levels.Count == 0)
					yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.levels");
				var previous = double.NegativeInfinity;
				foreach (var level in levels)
				{
					if (level is null || string.IsNullOrWhiteSpace(level.Title))
					{
						yield return OurResources.DataListItemBlank.FormatCurrentCulture($"{path}.levels");
					}
					else
					{
						if (level.Minimum <= previous || level.Minimum < 0 || level.Minimum > MaximumLevel)
							yield return OurResources.NeedLevelsOutOfOrder.FormatCurrentCulture(path, MaximumLevel);
						previous = level.Minimum;
					}
				}
			}
		}
		else if (Start is { } start && End is { } end)
		{
			// With one person in the family, the quota must fit in the hours the need can be met. Hours that end before they
			// start run past midnight.
			var days = Period == NeedPeriod.Week ? Days?.Count ?? 7 : 1;
			var hours = start < end ? end - start : 24 - start + end;
			if (start is < 0 or > 23 || end is < 1 or > 24 || start == end)
				yield return OurResources.NeedHoursInvalid.FormatCurrentCulture(Id, start, end);
			else if (GetQuotaMinutes(1) > hours * 60 * days)
				yield return OurResources.NeedMinutesTooLong.FormatCurrentCulture(Id, GetQuotaMinutes(1), hours * 60 * days);

			if (Minutes is < 0)
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.minutes", Minutes, 0);
			if (MinutesPerMember is < 0)
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.minutesPerMember", MinutesPerMember, 0);
			if (Visits is < 0)
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.visits", Visits, 0);
			if (VisitsPerMember is < 0)
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.visitsPerMember", VisitsPerMember, 0);
			if (Shift is < 1)
				yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.shift", Shift);
			if (VisitMinutes is < 0)
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.visitMinutes", VisitMinutes, 0);
			foreach (var (name, value) in new[] { ("visitMinutesPerMember", VisitMinutesPerMember), ("homeMinutes", HomeMinutes), ("homeMinutesPerMember", HomeMinutesPerMember) })
			{
				if (value is < 0)
					yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.{name}", value, 0);
			}

			if (IsTrips && GetVisitMinutes(1) < 1)
				yield return OurResources.NeedVisitMinutesNotPositive.FormatCurrentCulture(Id);
			if (Lead is < 1)
				yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.lead", Lead);
			if (Days is not null)
			{
				foreach (var error in DataValidation.ValidateStrings($"{path}.days", Days))
					yield return error;
			}
		}
	}

	/// <summary>
	/// Whether the need can be met in the given hour of the day.
	/// </summary>
	public bool IsWithinHours(int hour) => (Start, End) switch
	{
		({ } start, { } end) when start < end => hour >= start && hour < end,
		({ } start, { } end) => hour >= start || hour < end,
		_ => true,
	};

	/// <summary>
	/// Gets how urgent a level need is at the given level, from 0 to <see cref="MaximumLevel"/>.
	/// </summary>
	public double GetLevelUrgency(double level)
	{
		var from = UrgentFrom ?? 0;
		return Math.Clamp((level - from) / (MaximumLevel - from), 0, 1) * MaximumLevel;
	}

	/// <summary>
	/// Gets the words for a level of the need, or null if it isn't shown to the player.
	/// </summary>
	public string? GetLevelTitle(double level) => Levels is { Count: > 0 } levels ? (levels.LastOrDefault(x => x.Minimum <= level) ?? levels[0]).Title : null;
}

/// <summary>
/// How a need grows.
/// </summary>
public enum NeedKind
{
	/// <summary>
	/// A level that grows steadily over time, and drops while it's being met, such as fatigue.
	/// </summary>
	Level,

	/// <summary>
	/// A quota of time or trips to spend on something each day or week within certain hours, such as work or fetching
	/// water, which grows more urgent as time runs out.
	/// </summary>
	Quota,
}

/// <summary>
/// How often a quota need's quota starts again.
/// </summary>
public enum NeedPeriod
{
	Day,
	Week,
}

/// <summary>
/// Where a need is met.
/// </summary>
public enum NeedPlace
{
	/// <summary>
	/// The person's home.
	/// </summary>
	Home,

	/// <summary>
	/// The person's workplace. Only people with a business have a need met at their workplace.
	/// </summary>
	Workplace,

	/// <summary>
	/// A quarter of one of the listed kinds. Nearer quarters are more likely to be chosen.
	/// </summary>
	Quarter,
}

/// <summary>
/// A kind of quarter a need can be met in.
/// </summary>
public sealed class QuarterChoice : IHasFrequency
{
	public required string QuarterKind { get; init; }

	public FrequencyLevel Frequency { get; init; } = new(Utility.Random.Frequency.Common);
}

/// <summary>
/// The word for a need from a level up.
/// </summary>
public sealed class NeedLevel
{
	public required double Minimum { get; init; }

	public required string Title { get; init; }
}

/// <summary>
/// How the player can spend time on a need, with a choice of how long, such as sleeping until rested or for an hour.
/// </summary>
public sealed class PlayerAction
{
	/// <summary>
	/// The name of the action, such as "Sleep".
	/// </summary>
	public required string Title { get; init; }

	/// <summary>
	/// The start of the sentence telling the player what they did, followed by how long it took, such as "You slept".
	/// </summary>
	public required string Result { get; init; }

	/// <summary>
	/// The choices of how long to spend, for a need whose quota isn't measured in trips. A trip always spends one trip's
	/// time at the place.
	/// </summary>
	public IReadOnlyList<PlayerActionOption> Options { get; init; } = [];

	/// <summary>
	/// For a trip that returns home, what to tell the player once they've spent the trip's time at the place, such as
	/// "Take it home to finish."
	/// </summary>
	public string? Return { get; init; }

	/// <summary>
	/// For a trip that returns home, the action at home that finishes it, once the player has been to the place.
	/// </summary>
	public PlayerActionHome? Home { get; init; }

	/// <summary>
	/// For a quota need, whether the player can only do it within the need's hours on its days, such as shopping at a
	/// market that's only open then. Otherwise they can do it at any hour of its days. While it's closed, the player is
	/// told when it's open instead.
	/// </summary>
	public bool OnlyInHours { get; init; }

	internal IEnumerable<string> Validate(string path, bool isQuota, bool isTrips, bool isReturnHome)
	{
		if (OnlyInHours && !isQuota)
			yield return OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture($"{path}.onlyInHours");
		if (string.IsNullOrWhiteSpace(Title))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.title");
		if (string.IsNullOrWhiteSpace(Result))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.result");
		if (isTrips && Options.Count != 0)
			yield return OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture($"{path}.options");
		else if (!isTrips && Options.Count == 0)
			yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.options");

		var isReturnTrip = isTrips && isReturnHome;
		if (isReturnTrip && string.IsNullOrWhiteSpace(Return))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.return");
		else if (!isReturnTrip && Return is not null)
			yield return OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture($"{path}.return");
		if (isReturnTrip && Home is null)
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.home");
		else if (!isReturnTrip && Home is not null)
			yield return OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture($"{path}.home");
		if (Home is { } home)
		{
			if (string.IsNullOrWhiteSpace(home.Title))
				yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.home.title");
			if (string.IsNullOrWhiteSpace(home.Result))
				yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.home.result");
		}

		for (var index = 0; index < Options.Count; index++)
		{
			var optionPath = $"{path}.options[{index}]";
			if (Options[index] is not { } option)
			{
				yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.options");
			}
			else
			{
				if ((option.UntilMet ? 1 : 0) + (option.UntilHour is null ? 0 : 1) + (option.Minutes is null ? 0 : 1) != 1)
					yield return OurResources.PlayerActionOptionInvalid.FormatCurrentCulture(optionPath);
				if (option.Title is null ? option.Minutes is null : string.IsNullOrWhiteSpace(option.Title))
					yield return OurResources.DataValueMissing.FormatCurrentCulture($"{optionPath}.title");
				if (option.UntilHour is { } hour && hour is < 0 or > 23)
					yield return OurResources.ScheduleHourInvalid.FormatCurrentCulture($"{optionPath}.untilHour", hour, 23);
				if (option.Minutes is { } minutes && minutes <= 0)
					yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{optionPath}.minutes", minutes);
			}
		}
	}
}

/// <summary>
/// The action at home that finishes a trip, such as putting away the water.
/// </summary>
public sealed class PlayerActionHome
{
	public required string Title { get; init; }

	/// <summary>
	/// The start of the sentence telling the player what they did, such as "You put away the water", followed by how long
	/// it took if it took any time.
	/// </summary>
	public required string Result { get; init; }
}

/// <summary>
/// How long the player spends on a player action: until the need is met (a level need's level is 0, or a quota need's
/// quota is met), until the next time it's a given hour, or for a number of minutes. It has exactly one of these.
/// </summary>
public sealed class PlayerActionOption
{
	/// <summary>
	/// What the option is called, such as "Until rested". Without it, an option for a number of minutes is called by how
	/// long it takes.
	/// </summary>
	public string? Title { get; init; }

	public bool UntilMet { get; init; }

	public int? UntilHour { get; init; }

	public int? Minutes { get; init; }
}

/// <summary>
/// What people do when none of their needs are urgent enough: wait somewhere for a while, then decide again.
/// </summary>
public sealed class IdleSettings
{
	public required NeedPlace Place { get; init; }

	public IReadOnlyList<QuarterChoice>? QuarterKinds { get; init; }

	public required IntegerRange Minutes { get; init; }

	/// <summary>
	/// What someone idle is shown to be doing, such as "Resting". Without it, nothing is shown.
	/// </summary>
	public string? Activity { get; init; }
}

/// <summary>
/// How naps work, and how people go to bed in time for work.
/// </summary>
public sealed class SleepSettings
{
	/// <summary>
	/// Sleep chosen while the sleep need is below this level is a nap, unless it's bedtime: it lasts for
	/// <see cref="NapMinutes"/> rather than until rested. Someone only naps once between full sleeps.
	/// </summary>
	public required double NapBelow { get; init; }

	/// <summary>
	/// How long a nap lasts.
	/// </summary>
	public required IntegerRange NapMinutes { get; init; }

	/// <summary>
	/// Sleep grows urgent once the time before someone's next work hours, less the sleep they still need, drops below this
	/// many minutes, and is fully urgent when there's no time to spare.
	/// </summary>
	public required int BedtimeLead { get; init; }

	internal IEnumerable<string> Validate()
	{
		if (!(NapBelow >= 0) || NapBelow > Need.MaximumLevel)
			yield return OurResources.NeedLevelOutOfRange.FormatCurrentCulture("sleep.napBelow", Need.MaximumLevel);
		foreach (var error in CharacterData.ValidateRange("sleep.napMinutes", NapMinutes))
			yield return error;
		if (NapMinutes.Minimum < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture("sleep.napMinutes.minimum", NapMinutes.Minimum, 1);
		if (BedtimeLead < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture("sleep.bedtimeLead", BedtimeLead, 1);
	}
}

/// <summary>
/// How people stop to chat with friends they meet, and seek them out when they're lonely.
/// </summary>
public sealed class SocializingSettings
{
	/// <summary>
	/// How willing someone must be to stop and chat with a friend they meet: their socializing urgency, times their
	/// relationship score out of 100, less their current goal's urgency times <see cref="ChatGoalWeight"/>.
	/// </summary>
	public required double ChatUrgency { get; init; }

	/// <summary>
	/// How much the urgency of what someone is doing holds them back from stopping to chat.
	/// </summary>
	public required double ChatGoalWeight { get; init; }

	/// <summary>
	/// How urgent socializing must be for someone to wake a friend to chat.
	/// </summary>
	public required double WakeUrgency { get; init; }

	/// <summary>
	/// A place this many minutes away is half as likely to be chosen to look for a friend as one close by.
	/// </summary>
	public required double NearMinutes { get; init; }

	/// <summary>
	/// What socializing's urgency is multiplied by when choosing a goal, so seeking out friends gives way to other goals.
	/// </summary>
	public required double SeekFactor { get; init; }

	/// <summary>
	/// How long someone seeking a friend looks around a place before choosing what to do again.
	/// </summary>
	public required int SeekMinutes { get; init; }

	/// <summary>
	/// Someone talked with this many hours ago is half as likely to be sought out as someone not talked with lately.
	/// </summary>
	public required double RecentHours { get; init; }

	/// <summary>
	/// After the player turns down someone's request to chat, that person doesn't ask again for this many hours.
	/// </summary>
	public required double AskPlayerAgainHours { get; init; }

	/// <summary>
	/// How much each kind of clue to where a friend is weighs when choosing where to look.
	/// </summary>
	public required SeekClues Clues { get; init; }

	internal IEnumerable<string> Validate()
	{
		foreach (var (name, value) in new[] { ("chatUrgency", ChatUrgency), ("chatGoalWeight", ChatGoalWeight), ("wakeUrgency", WakeUrgency), ("clues.workplace", Clues.Workplace), ("clues.home", Clues.Home), ("clues.sighting", Clues.Sighting), ("clues.gathering", Clues.Gathering) })
		{
			if (!(value >= 0) || !double.IsFinite(value))
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"socializing.{name}", value, 0);
		}
		foreach (var (name, value) in new[] { ("seekFactor", SeekFactor), ("recentHours", RecentHours), ("nearMinutes", NearMinutes), ("askPlayerAgainHours", AskPlayerAgainHours) })
		{
			if (!(value > 0) || !double.IsFinite(value))
				yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"socializing.{name}", value);
		}
		if (SeekMinutes < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture("socializing.seekMinutes", SeekMinutes, 1);
	}
}

/// <summary>
/// How much each kind of clue to where a friend is weighs when someone chooses where to look for them.
/// </summary>
public sealed class SeekClues
{
	/// <summary>
	/// The friend's workplace, while they're within their work hours.
	/// </summary>
	public required double Workplace { get; init; }

	/// <summary>
	/// The friend's home, outside their work hours.
	/// </summary>
	public required double Home { get; init; }

	/// <summary>
	/// Where the friend was last seen, or said they were going, for a fresh sighting. It counts for less as the sighting
	/// gets older.
	/// </summary>
	public required double Sighting { get; init; }

	/// <summary>
	/// A place the friend may be gathering with others today, for a need only met on some days, such as market day.
	/// </summary>
	public required double Gathering { get; init; }
}

/// <summary>
/// When people refuse to talk, and what waking someone to talk costs them.
/// </summary>
public sealed class TalkingSettings
{
	/// <summary>
	/// Someone whose goal is at least this urgent refuses to stop and talk.
	/// </summary>
	public required double RefuseUrgency { get; init; }

	/// <summary>
	/// How much someone woken up to talk has their sleep need (the start need) rise.
	/// </summary>
	public required double WakeLevel { get; init; }

	/// <summary>
	/// How long it takes to ask someone whether they've seen a person.
	/// </summary>
	public required int AskAboutMinutes { get; init; }

	/// <summary>
	/// How long it takes to ask someone what they're doing.
	/// </summary>
	public required int AskDoingMinutes { get; init; }

	/// <summary>
	/// How long socializing with someone takes.
	/// </summary>
	public required int SocializeMinutes { get; init; }

	/// <summary>
	/// The id of the level need met by socializing with a friend, or null if there isn't one. It's only met that way, so it's
	/// never chosen as a goal.
	/// </summary>
	public string? SocializeNeed { get; init; }
}

/// <summary>
/// How everyone starts a new game: asleep at home, meeting the given need, and waking up around the given hour.
/// </summary>
public sealed class ScheduleStart
{
	/// <summary>
	/// The level need people are meeting while asleep.
	/// </summary>
	public required string Need { get; init; }

	public required int WakeHour { get; init; }

	/// <summary>
	/// How many minutes before or after the wake hour each person wakes.
	/// </summary>
	public required IntegerRange WakeMinutes { get; init; }
}

/// <summary>
/// Makes some needs more or less urgent for people who meet a condition, by multiplying their urgency.
/// </summary>
public sealed class UrgencyFactor
{
	public required CharacterCondition When { get; init; }

	/// <summary>
	/// The ids of the needs it applies to.
	/// </summary>
	public required IReadOnlyList<string> Needs { get; init; }

	public required double Factor { get; init; }
}

/// <summary>
/// Makes travel take longer for people who meet a condition.
/// </summary>
public sealed class TravelTimeFactor
{
	public required CharacterCondition When { get; init; }

	public required double Factor { get; init; }
}
