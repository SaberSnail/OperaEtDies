using System.Text.Json;
using System.Text.Json.Serialization;
using GoldenAnvil.Utility;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// How people decide what to do, from the schedules, needs, urgency factors, and travel time factors in the data.
/// Whenever someone finishes what they were doing, they choose their next goal from how urgent each of their needs is.
/// </summary>
public sealed class ScheduleSettings
{
	/// <summary>
	/// The name of the data section the settings are read from.
	/// </summary>
	public const string SectionName = "schedules";

	/// <summary>
	/// The role of the need met by sleeping, which people are meeting at the start of a new game.
	/// </summary>
	public const string SleepRole = "sleep";

	/// <summary>
	/// The role of the level need met by socializing with friends.
	/// </summary>
	public const string SocializingRole = "socializing";

	/// <summary>
	/// The role of the trip need for washing the household's clothes, which are taken to be washed when the trip's time at
	/// the place starts, and hung out to dry when its time at home ends.
	/// </summary>
	public const string LaundryRole = "laundry";

	/// <summary>
	/// Settings with no needs, where people stay at home and wake at six, for game data created without schedules.
	/// </summary>
	public static ScheduleSettings None { get; } = new()
	{
		MinimumUrgency = 0,
		UrgencyPower = 1,
		SightingHours = 24,
		Sleep = new SleepSettings { NapBelow = 0, NapMinutes = new IntegerRange { Minimum = 30, Maximum = 30 }, BedtimeLead = 60, DayClothesFactor = 1 },
		Clothing = new ClothingSchedule { ChangeMinutes = 5, DryingMinutes = 60 },
		Talking = new TalkingSettings { RefuseUrgency = 100, WakeLevel = 0, AskAboutMinutes = 1, AskDoingMinutes = 5 },
		Socializing = new SocializingSettings { ChatMinutes = 30, ChatUrgency = 100, ChatGoalWeight = 1, WakeUrgency = 100, NearMinutes = 15, SeekFactor = 1, SeekMinutes = 15, RecentHours = 12, AskPlayerAgainHours = 4, Clues = new SeekClues { Workplace = 1, Home = 1, Sighting = 1, Gathering = 1 } },
		Idle = new IdleSettings { Place = new SchedulePlace { Kind = NeedPlace.Home }, Minutes = new IntegerRange { Minimum = 60, Maximum = 60 } },
		Start = new ScheduleStart { WakeHour = 6, WakeMinutes = new IntegerRange { Minimum = 0, Maximum = 0 } },
	};

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
	/// How naps work, and how people go to bed in time for work.
	/// </summary>
	public required SleepSettings Sleep { get; init; }

	/// <summary>
	/// How long changing clothes and drying the washing take.
	/// </summary>
	public required ClothingSchedule Clothing { get; init; }

	/// <summary>
	/// When people refuse to talk, and what waking someone to talk costs them.
	/// </summary>
	public required TalkingSettings Talking { get; init; }

	/// <summary>
	/// How people stop to chat with friends they meet, and seek them out when they're lonely.
	/// </summary>
	public required SocializingSettings Socializing { get; init; }

	/// <summary>
	/// What people do when none of their needs are urgent enough.
	/// </summary>
	public required IdleSettings Idle { get; init; }

	/// <summary>
	/// How everyone starts a new game.
	/// </summary>
	public required ScheduleStart Start { get; init; }

	/// <summary>
	/// The needs, from the needs section of the data.
	/// </summary>
	[JsonIgnore]
	public IReadOnlyList<Need> Needs { get; internal set; } = [];

	/// <summary>
	/// Factors that make some people slower to travel, from the travel time factors section of the data. Every factor
	/// whose condition a person meets applies.
	/// </summary>
	[JsonIgnore]
	public IReadOnlyList<TravelTimeFactor> TravelTimeFactors { get; internal set; } = [];

	/// <summary>
	/// Factors that make some needs more or less urgent for people who meet a condition, from the urgency factors section
	/// of the data. Every factor whose condition a person meets applies to its needs.
	/// </summary>
	[JsonIgnore]
	public IReadOnlyList<UrgencyFactor> UrgencyFactors { get; internal set; } = [];

	/// <summary>
	/// The id of the need met by sleeping, or null if there's none.
	/// </summary>
	[JsonIgnore]
	public string? SleepNeed => FindRole(SleepRole)?.Id;

	/// <summary>
	/// The id of the level need met by socializing with a friend, or null if there's none.
	/// </summary>
	[JsonIgnore]
	public string? SocializeNeed => FindRole(SocializingRole)?.Id;

	/// <summary>
	/// The id of the trip need for washing the household's clothes, or null if there's none.
	/// </summary>
	[JsonIgnore]
	public string? LaundryNeed => FindRole(LaundryRole)?.Id;

	private Need? FindRole(string role) => Needs.FirstOrDefault(x => x?.Role == role);

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

	private double GetGoalUrgency(Need need, double urgency) => need.Role == SocializingRole ? urgency * Socializing.SeekFactor : urgency;

	/// <summary>
	/// How heavily a need this urgent weighs when choosing a goal at random among the candidates.
	/// </summary>
	public double GetGoalWeight(double urgency) => Math.Pow(urgency, UrgencyPower);

	public IEnumerable<string> Validate()
	{
		const string path = SectionName;
		var errors = new List<string>();
		if (MinimumUrgency < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.minimumUrgency", MinimumUrgency, 0));
		if (!(UrgencyPower > 0))
			errors.Add(OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.urgencyPower", UrgencyPower));

		if (Needs.Count != 0)
			errors.AddRange(DataValidation.ValidateIdentifiedItems("needs", Needs, x => x.Id, x => x.Title));
		var needs = Needs.OfType<Need>().Where(x => !string.IsNullOrWhiteSpace(x.Id)).ToList();
		var needIds = needs.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		foreach (var need in needs)
			errors.AddRange(need.Validate(needIds));

		// The engine finds the sleep and socializing needs by their roles.
		errors.AddRange(DataValidation.ValidateRoles("needs", needs, x => x.Id, x => x.Role, [SleepRole, SocializingRole, LaundryRole]));
		if (needs.Count != 0 && FindRole(SleepRole) is null)
			errors.Add(OurResources.RoleMissing.FormatCurrentCulture("needs", SleepRole));
		foreach (var need in needs.Where(x => x.Role is SleepRole or SocializingRole && x.Kind != NeedKind.Level))
			errors.Add(OurResources.RoleWrongType.FormatCurrentCulture($"needs[{need.Id}]", need.Role, "level need"));
		foreach (var need in needs.Where(x => x.Role == LaundryRole && !(x.IsTrips && x.ReturnHome)))
			errors.Add(OurResources.RoleWrongType.FormatCurrentCulture($"needs[{need.Id}]", need.Role, "trip need that returns home"));

		if (SightingHours < 1)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.sightingHours", SightingHours, 1));
		if (!(Talking.RefuseUrgency >= 0) || !double.IsFinite(Talking.RefuseUrgency))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.talking.refuseUrgency", Talking.RefuseUrgency, 0));
		if (!(Talking.WakeLevel >= 0))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.talking.wakeLevel", Talking.WakeLevel, 0));
		else if (Talking.WakeLevel > Need.MaximumLevel)
			errors.Add(OurResources.DataNumberTooLarge.FormatCurrentCulture($"{path}.talking.wakeLevel", Talking.WakeLevel, Need.MaximumLevel));
		foreach (var (name, minutes) in new[] { ("talking.askAboutMinutes", Talking.AskAboutMinutes), ("talking.askDoingMinutes", Talking.AskDoingMinutes) })
		{
			if (minutes < 1)
				errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.{name}", minutes, 1));
		}
		errors.AddRange(Socializing.Validate());
		errors.AddRange(Sleep.Validate());
		errors.AddRange(Clothing.Validate());

		errors.AddRange(Idle.Place.Validate($"{path}.idle.place"));
		if (Idle.Activity is not null && string.IsNullOrWhiteSpace(Idle.Activity))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture($"{path}.idle.activity"));
		errors.AddRange(CharacterData.ValidateRange($"{path}.idle.minutes", Idle.Minutes));
		if (Idle.Minutes.Minimum < 1)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.idle.minutes.minimum", Idle.Minutes.Minimum, 1));

		if (Start.WakeHour is < 0 or > 23)
			errors.Add(OurResources.ScheduleHourInvalid.FormatCurrentCulture($"{path}.start.wakeHour", Start.WakeHour, 23));
		errors.AddRange(CharacterData.ValidateRange($"{path}.start.wakeMinutes", Start.WakeMinutes));

		errors.AddRange(DataValidation.ValidateIdentifiedItems("travelTimeFactors", TravelTimeFactors, x => x.Id, null, allowEmpty: true));
		foreach (var factor in TravelTimeFactors.OfType<TravelTimeFactor>().Where(x => !(x.Factor > 0)))
			errors.Add(OurResources.DataNumberNotPositive.FormatCurrentCulture($"travelTimeFactors[{factor.Id}].factor", factor.Factor));

		errors.AddRange(DataValidation.ValidateIdentifiedItems("urgencyFactors", UrgencyFactors, x => x.Id, null, allowEmpty: true));
		foreach (var factor in UrgencyFactors.OfType<UrgencyFactor>())
		{
			var factorPath = $"urgencyFactors[{factor.Id}]";
			if (!(factor.Factor > 0))
				errors.Add(OurResources.DataNumberNotPositive.FormatCurrentCulture($"{factorPath}.factor", factor.Factor));
			errors.AddRange(DataValidation.ValidateStrings($"{factorPath}.needs", factor.Needs));
			errors.AddRange(factor.Needs.Where(x => x is not null && !needIds.Contains(x)).Select(x => OurResources.DataIdUnknown.FormatCurrentCulture($"{factorPath}.needs", x, "needs")));
		}

		return errors;
	}

	/// <summary>
	/// Gets the conditions in the schedules, with where each one is, for checking against the character properties.
	/// </summary>
	internal IEnumerable<(string Path, CharacterCondition Condition)> GetConditions() =>
		Needs.OfType<Need>().Where(x => x.When is not null).Select(x => ($"needs[{x.Id}].when", x.When!))
			.Concat(TravelTimeFactors.OfType<TravelTimeFactor>().Select(x => ($"travelTimeFactors[{x.Id}].when", x.When)))
			.Concat(UrgencyFactors.OfType<UrgencyFactor>().Select(x => ($"urgencyFactors[{x.Id}].when", x.When)));

	/// <summary>
	/// Gets the kinds of quarter the schedules name, with where each one is, for checking against the quarter kinds.
	/// </summary>
	internal IEnumerable<(string Path, string QuarterKind)> GetQuarterKinds() =>
		Needs.OfType<Need>().SelectMany(need => (need.QuarterKinds ?? []).OfType<QuarterChoice>().Select(x => ($"needs[{need.Id}].place.quarterKinds", x.QuarterKind)))
			.Concat((Idle.Place.QuarterKinds ?? []).OfType<QuarterChoice>().Select(x => ($"{SectionName}.idle.place.quarterKinds", x.QuarterKind)));

	/// <summary>
	/// Gets the days of the week the schedules name, with where each one is, for checking against the calendar.
	/// </summary>
	internal IEnumerable<(string Path, string Day)> GetDays() =>
		Needs.OfType<Need>().SelectMany(need => (need.Days ?? []).Select(x => ($"needs[{need.Id}].hours.days", x)));
}

/// <summary>
/// Where a need is met, or where someone idle waits.
/// </summary>
[JsonConverter(typeof(SchedulePlaceConverter))]
public sealed class SchedulePlace
{
	public required NeedPlace Kind { get; init; }

	/// <summary>
	/// For a quarter, the kinds of quarter it can be, chosen by frequency.
	/// </summary>
	public IReadOnlyList<QuarterChoice>? QuarterKinds { get; init; }

	/// <summary>
	/// For a quarter, whether people go to the nearest quarter of any of its kinds, rather than choosing a kind by frequency.
	/// </summary>
	public bool Nearest { get; init; }

	internal IEnumerable<string> Validate(string path)
	{
		if (Kind == NeedPlace.Quarter)
		{
			if (QuarterKinds is null || QuarterKinds.Count == 0)
				yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.quarterKinds");
			else if (QuarterKinds.Any(x => x is null || string.IsNullOrWhiteSpace(x.QuarterKind)))
				yield return OurResources.DataListItemBlank.FormatCurrentCulture($"{path}.quarterKinds");
		}
	}
}

/// <summary>
/// An amount that can grow with the size of the family: a base amount, plus an amount for each family member.
/// </summary>
[JsonConverter(typeof(FamilyAmountConverter))]
public sealed class FamilyAmount
{
	public double Base { get; init; }

	/// <summary>
	/// The amount added for each member of the family, for a need shared by the family, or null if there's none.
	/// </summary>
	public double? PerMember { get; init; }

	/// <summary>
	/// Gets the amount for a family with the given number of members (0 for a need that isn't shared).
	/// </summary>
	public double GetAmount(int members) => Base + ((PerMember ?? 0) * members);

	internal IEnumerable<string> Validate(string path, bool isWhole)
	{
		if (!(Base >= 0) || !double.IsFinite(Base))
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.base", Base, 0);
		else if (isWhole && Base != Math.Floor(Base))
			yield return OurResources.DataNumberNotWhole.FormatCurrentCulture($"{path}.base", Base);
		if (PerMember is { } perMember)
		{
			if (!(perMember >= 0) || !double.IsFinite(perMember))
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.perMember", perMember, 0);
			else if (isWhole && perMember != Math.Floor(perMember))
				yield return OurResources.DataNumberNotWhole.FormatCurrentCulture($"{path}.perMember", perMember);
		}
	}
}

/// <summary>
/// Something people need, which grows more urgent until they do something about it. A need is either a level need or a
/// quota need, depending on which of <see cref="Level"/> and <see cref="Quota"/> it has.
/// </summary>
public sealed class Need : IIdentified
{
	/// <summary>
	/// The highest a level need can be. Level needs run from 0 to this.
	/// </summary>
	public const double MaximumLevel = 100;

	public required string Id { get; init; }

	public required string Title { get; init; }

	/// <summary>
	/// What the engine uses the need for, such as <see cref="ScheduleSettings.SleepRole"/>, or null for none.
	/// </summary>
	public string? Role { get; init; }

	/// <summary>
	/// Who has the need. Without it, everyone does.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// Where the need is met.
	/// </summary>
	[JsonPropertyName("place")]
	public required SchedulePlace PlaceSetting { get; init; }

	/// <summary>
	/// Whether people are at work while meeting the need.
	/// </summary>
	public bool AtWork { get; init; }

	/// <summary>
	/// Whether people go home after meeting the need, such as to carry water back.
	/// </summary>
	public bool ReturnHome { get; init; }

	/// <summary>
	/// The hours and days the need can be met in. A quota need must have them. A level need can only have a start and an
	/// end.
	/// </summary>
	public Hours? Hours { get; init; }

	/// <summary>
	/// The settings of a level need: a level that grows steadily over time, and drops while it's being met.
	/// </summary>
	public LevelNeedSettings? Level { get; init; }

	/// <summary>
	/// The settings of a quota need: a quota of time or trips to spend each day or week, which grows more urgent as time
	/// runs out.
	/// </summary>
	public QuotaNeedSettings? Quota { get; init; }

	/// <summary>
	/// What someone meeting the need is shown to be doing, such as "Sleeping". Defaults to the title.
	/// </summary>
	public string? Activity { get; init; }

	/// <summary>
	/// How the player can spend time on the need where it's met, such as sleeping at home, or null if they can't.
	/// </summary>
	public PlayerAction? PlayerAction { get; init; }

	[JsonIgnore]
	public NeedKind Kind => Level is null ? NeedKind.Quota : NeedKind.Level;

	[JsonIgnore]
	public NeedPlace Place => PlaceSetting.Kind;

	[JsonIgnore]
	public IReadOnlyList<QuarterChoice>? QuarterKinds => PlaceSetting.QuarterKinds;

	[JsonIgnore]
	public bool Nearest => PlaceSetting.Nearest;

	[JsonIgnore]
	public int? Start => Hours?.Start;

	[JsonIgnore]
	public int? End => Hours?.End;

	[JsonIgnore]
	public IReadOnlyList<string>? Days => Hours?.Days;

	[JsonIgnore]
	public bool OnSpecialDays => Hours?.OnSpecialDays ?? true;

	[JsonIgnore]
	public double? PerHour => Level?.PerHour;

	[JsonIgnore]
	public double? SatisfyPerHour => Level?.SatisfyPerHour;

	[JsonIgnore]
	public IReadOnlyList<string>? PausedBy => Level?.PausedBy;

	[JsonIgnore]
	public IntegerRange? StartLevel => Level?.StartLevel;

	[JsonIgnore]
	public double? UrgentFrom => Level?.UrgentFrom;

	[JsonIgnore]
	public double? WakeUrgency => Level?.WakeUrgency;

	[JsonIgnore]
	public IReadOnlyList<NeedLevel>? Levels => Level?.Levels;

	[JsonIgnore]
	public NeedPeriod Period => Quota?.Period ?? NeedPeriod.Day;

	[JsonIgnore]
	public int? Lead => Quota?.Lead;

	[JsonIgnore]
	public bool SharedByFamily => Quota?.SharedByFamily ?? false;

	[JsonIgnore]
	public double? OverdueUrgency => Quota?.OverdueUrgency;

	/// <summary>
	/// For a quota need measured in trips with time at home afterward, what someone is shown to be doing during that time.
	/// Defaults to <see cref="Activity"/>.
	/// </summary>
	[JsonIgnore]
	public string? HomeActivity => Quota?.Trips?.Home?.Activity;

	/// <summary>
	/// For a quota need measured in time, the most minutes spent on it at once.
	/// </summary>
	[JsonIgnore]
	public int? Shift => Quota?.Time?.Shift;

	/// <summary>
	/// Whether the quota is measured in trips rather than time.
	/// </summary>
	[JsonIgnore]
	public bool IsTrips => Quota?.Trips is not null;

	/// <summary>
	/// For a quota need measured in trips, how many minutes each trip takes at the place for a family with the given number
	/// of members (or for one person, for a need that isn't shared).
	/// </summary>
	public int GetVisitMinutes(int members) => (int) (Quota?.Trips?.Minutes.GetAmount(GetMemberCount(members)) ?? 0);

	/// <summary>
	/// For a quota need measured in trips, how many minutes each trip takes at home afterward.
	/// </summary>
	public int GetHomeMinutes(int members) => (int) (Quota?.Trips?.Home?.Minutes.GetAmount(GetMemberCount(members)) ?? 0);

	/// <summary>
	/// For a quota need, the most minutes spent on it at once: a shift, or one trip (at the place and at home afterward).
	/// </summary>
	public int GetShiftMinutes(int members) => IsTrips ? GetVisitMinutes(members) + GetHomeMinutes(members) : Shift ?? 0;

	/// <summary>
	/// For a quota need, how many minutes it takes each period for a family with the given number of members (or for one
	/// person, for a need that isn't shared). Trips are counted by the minutes spent at the place and at home afterward.
	/// </summary>
	public long GetQuotaMinutes(int members) =>
		Quota?.Trips is { } trips
			? (long) Math.Ceiling(trips.Visits.GetAmount(GetMemberCount(members))) * GetShiftMinutes(members)
			: (long) (Quota?.Time?.Minutes.GetAmount(GetMemberCount(members)) ?? 0);

	private int GetMemberCount(int members) => SharedByFamily ? members : 0;

	internal IEnumerable<string> Validate(IReadOnlySet<string> needIds)
	{
		var path = $"needs[{Id}]";
		foreach (var error in PlaceSetting.Validate($"{path}.place"))
			yield return error;
		if (ReturnHome && Place == NeedPlace.Home)
			yield return OurResources.NeedSettingNotAllowed.FormatCurrentCulture(Id, "returnHome", "home");
		if (Activity is not null && string.IsNullOrWhiteSpace(Activity))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.activity");

		if ((Level is null) == (Quota is null))
		{
			yield return OurResources.NeedKindInvalid.FormatCurrentCulture(Id);
			yield break;
		}

		if (PlayerAction is { } playerAction)
		{
			foreach (var error in playerAction.Validate($"{path}.playerAction", Quota is not null, IsTrips, ReturnHome))
				yield return error;
		}

		if (Hours is { } hours)
		{
			foreach (var error in hours.Validate($"{path}.hours", allowDays: Quota is not null))
				yield return error;
		}

		var errors = Level is { } level ? ValidateLevel(path, level, needIds) : ValidateQuota(path, Quota!);
		foreach (var error in errors)
			yield return error;
	}

	private static IEnumerable<string> ValidateLevel(string path, LevelNeedSettings level, IReadOnlySet<string> needIds)
	{
		if (level.PerHour < 0)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.level.perHour", level.PerHour, 0);
		if (!(level.SatisfyPerHour > 0))
			yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.level.satisfyPerHour", level.SatisfyPerHour);
		foreach (var pausedBy in level.PausedBy ?? [])
		{
			if (!needIds.Contains(pausedBy ?? ""))
				yield return OurResources.DataIdUnknown.FormatCurrentCulture($"{path}.level.pausedBy", pausedBy, "needs");
		}

		if (level.UrgentFrom is < 0 or >= MaximumLevel)
			yield return OurResources.NeedUrgentFromOutOfRange.FormatCurrentCulture($"{path}.level.urgentFrom", level.UrgentFrom, MaximumLevel);
		if (level.WakeUrgency is { } wakeUrgency && !(wakeUrgency >= 0))
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.level.wakeUrgency", wakeUrgency, 0);

		if (level.StartLevel is { } startLevel)
		{
			foreach (var error in CharacterData.ValidateRange($"{path}.level.startLevel", startLevel))
				yield return error;
			if (startLevel.Minimum < 0 || startLevel.Maximum > MaximumLevel)
				yield return OurResources.NeedLevelOutOfRange.FormatCurrentCulture($"{path}.level.startLevel", MaximumLevel);
		}

		if (level.Levels is { } levels)
		{
			if (levels.Count == 0)
				yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.level.levels");
			var previous = double.NegativeInfinity;
			foreach (var item in levels)
			{
				if (item is null || string.IsNullOrWhiteSpace(item.Title))
				{
					yield return OurResources.DataListItemBlank.FormatCurrentCulture($"{path}.level.levels");
				}
				else
				{
					if (item.Minimum <= previous || item.Minimum < 0 || item.Minimum > MaximumLevel)
						yield return OurResources.NeedLevelsOutOfOrder.FormatCurrentCulture($"{path}.level.levels", MaximumLevel);
					previous = item.Minimum;
				}
			}
		}
	}

	private IEnumerable<string> ValidateQuota(string path, QuotaNeedSettings quota)
	{
		if (Hours is null)
			yield return OurResources.NeedSettingMissing.FormatCurrentCulture(Id, "hours", "quota");
		if ((quota.Time is null) == (quota.Trips is null))
			yield return OurResources.NeedQuotaMeasureInvalid.FormatCurrentCulture(Id);

		var amounts = new List<(string Path, FamilyAmount Amount, bool IsWhole)>();
		if (quota.Time is { } time)
		{
			amounts.Add(($"{path}.quota.time.minutes", time.Minutes, true));
			if (time.Shift < 1)
				yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.quota.time.shift", time.Shift);
		}

		if (quota.Trips is { } trips)
		{
			amounts.Add(($"{path}.quota.trips.visits", trips.Visits, false));
			amounts.Add(($"{path}.quota.trips.minutes", trips.Minutes, true));
			if (trips.Home is { } home)
			{
				amounts.Add(($"{path}.quota.trips.home.minutes", home.Minutes, true));
				if (home.Activity is not null && string.IsNullOrWhiteSpace(home.Activity))
					yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.quota.trips.home.activity");
				if (!ReturnHome)
					yield return OurResources.NeedHomeMinutesWithoutReturn.FormatCurrentCulture(Id);
			}

			if (GetVisitMinutes(1) < 1)
				yield return OurResources.NeedVisitMinutesNotPositive.FormatCurrentCulture(Id);
		}

		foreach (var (amountPath, amount, isWhole) in amounts)
		{
			foreach (var error in amount.Validate(amountPath, isWhole))
				yield return error;
		}

		if (!quota.SharedByFamily && amounts.Any(x => x.Amount.PerMember is not null))
			yield return OurResources.NeedPerMemberNotShared.FormatCurrentCulture(Id);

		if (quota.OverdueUrgency is { } overdue)
		{
			if (quota.Period != NeedPeriod.Week || Days is null)
				yield return OurResources.NeedOverdueNotAllowed.FormatCurrentCulture(Id);
			else if (overdue is < 0 or > MaximumLevel)
				yield return OurResources.NeedLevelOutOfRange.FormatCurrentCulture($"{path}.quota.overdueUrgency", MaximumLevel);
		}

		if (quota.Lead < 1)
			yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.quota.lead", quota.Lead);

		// With one person in the family, the quota must fit in the hours the need can be met.
		if (Hours is { Start: >= 0 and <= 23, End: >= 1 and <= 24 } hours && hours.Start != hours.End)
		{
			var days = quota.Period == NeedPeriod.Week ? Days?.Count ?? 7 : 1;
			var available = hours.Length * 60L * days;
			if (GetQuotaMinutes(1) > available)
				yield return OurResources.NeedMinutesTooLong.FormatCurrentCulture(Id, GetQuotaMinutes(1), available);
		}
	}

	/// <summary>
	/// Whether the need can be met in the given hour of the day.
	/// </summary>
	public bool IsWithinHours(int hour) => Hours?.Contains(hour) ?? true;

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
/// The settings of a level need: a level from 0 to <see cref="Need.MaximumLevel"/> that grows steadily over time, and
/// drops while it's being met, such as fatigue.
/// </summary>
public sealed class LevelNeedSettings
{
	/// <summary>
	/// How much the level grows each hour, unless it's being met or paused.
	/// </summary>
	public required double PerHour { get; init; }

	/// <summary>
	/// How much the level drops each hour while it's being met.
	/// </summary>
	public required double SatisfyPerHour { get; init; }

	/// <summary>
	/// The needs that stop it growing while they're being met, such as sleep.
	/// </summary>
	public IReadOnlyList<string>? PausedBy { get; init; }

	/// <summary>
	/// The level it starts to become urgent at. Its urgency rises from 0 there to fully urgent at
	/// <see cref="Need.MaximumLevel"/>. Defaults to 0.
	/// </summary>
	public double? UrgentFrom { get; init; }

	/// <summary>
	/// How urgent it must get to wake someone who's asleep, if it's also more urgent than sleep is by then. Without it, the
	/// need never wakes anyone.
	/// </summary>
	public double? WakeUrgency { get; init; }

	/// <summary>
	/// Its level at the start of a new game. Defaults to 0.
	/// </summary>
	public IntegerRange? StartLevel { get; init; }

	/// <summary>
	/// The words for how high it is, shown to the player. Without them, the need isn't shown.
	/// </summary>
	public IReadOnlyList<NeedLevel>? Levels { get; init; }
}

/// <summary>
/// The settings of a quota need: a quota of time or trips to spend each day or week within its hours, such as work or
/// fetching water, which grows more urgent as time runs out.
/// </summary>
public sealed class QuotaNeedSettings
{
	/// <summary>
	/// Whether the quota is for each day or each week.
	/// </summary>
	public NeedPeriod Period { get; init; } = NeedPeriod.Day;

	/// <summary>
	/// How many minutes of spare time are left when it starts to become urgent. It's fully urgent when there's just enough
	/// time left to meet it.
	/// </summary>
	public required int Lead { get; init; }

	/// <summary>
	/// For a quota by the week with days, the urgency it has once those days have passed this week and its quota still
	/// isn't met. It can then be met on any day for the rest of the week.
	/// </summary>
	public double? OverdueUrgency { get; init; }

	/// <summary>
	/// Whether it's met for the whole family by anyone in it, such as shopping.
	/// </summary>
	public bool SharedByFamily { get; init; }

	/// <summary>
	/// A quota measured in time.
	/// </summary>
	public QuotaTime? Time { get; init; }

	/// <summary>
	/// A quota measured in trips.
	/// </summary>
	public QuotaTrips? Trips { get; init; }
}

/// <summary>
/// A quota measured in time, spent in shifts.
/// </summary>
public sealed class QuotaTime
{
	/// <summary>
	/// How many minutes it takes each period.
	/// </summary>
	public required FamilyAmount Minutes { get; init; }

	/// <summary>
	/// The most minutes spent on it at once, before deciding again what to do.
	/// </summary>
	public required int Shift { get; init; }
}

/// <summary>
/// A quota measured in trips to the place the need is met.
/// </summary>
public sealed class QuotaTrips
{
	/// <summary>
	/// How many trips it takes each period. The total is rounded up.
	/// </summary>
	public required FamilyAmount Visits { get; init; }

	/// <summary>
	/// How many minutes each trip takes once the person gets there.
	/// </summary>
	public required FamilyAmount Minutes { get; init; }

	/// <summary>
	/// The time each trip takes at home afterward, such as hanging up washing, for a need that returns home.
	/// </summary>
	public QuotaTripHome? Home { get; init; }
}

/// <summary>
/// The time each trip takes at home afterward.
/// </summary>
public sealed class QuotaTripHome
{
	public required FamilyAmount Minutes { get; init; }

	/// <summary>
	/// What someone is shown to be doing during that time, such as "Hanging out the washing". Defaults to the need's activity.
	/// </summary>
	public string? Activity { get; init; }
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
	public LongText? Return { get; init; }

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
		if (isReturnTrip && string.IsNullOrWhiteSpace(Return?.Text))
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
/// How long people take over their clothes: changing into or out of work clothes, and the washing drying once it's hung
/// out.
/// </summary>
public sealed class ClothingSchedule
{
	/// <summary>
	/// How many minutes changing clothes takes, other than changing into or out of work clothes as part of starting or
	/// stopping work, which takes no time.
	/// </summary>
	public required int ChangeMinutes { get; init; }

	/// <summary>
	/// How many minutes after the washing is hung out it's dry and can be worn again.
	/// </summary>
	public required int DryingMinutes { get; init; }

	internal IEnumerable<string> Validate()
	{
		const string path = $"{ScheduleSettings.SectionName}.clothing";
		if (ChangeMinutes < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.changeMinutes", ChangeMinutes, 1);
		if (DryingMinutes < 0)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.dryingMinutes", DryingMinutes, 0);
	}
}

/// <summary>
/// What people do when none of their needs are urgent enough: wait somewhere for a while, then decide again.
/// </summary>
public sealed class IdleSettings
{
	public required SchedulePlace Place { get; init; }

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

	/// <summary>
	/// How fast sleep meets the sleep need while wearing everyday or work clothes, as a share of its usual rate.
	/// </summary>
	public required double DayClothesFactor { get; init; }

	internal IEnumerable<string> Validate()
	{
		const string path = $"{ScheduleSettings.SectionName}.sleep";
		if (!(NapBelow >= 0) || NapBelow > Need.MaximumLevel)
			yield return OurResources.NeedLevelOutOfRange.FormatCurrentCulture($"{path}.napBelow", Need.MaximumLevel);
		foreach (var error in CharacterData.ValidateRange($"{path}.napMinutes", NapMinutes))
			yield return error;
		if (NapMinutes.Minimum < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.napMinutes.minimum", NapMinutes.Minimum, 1);
		if (BedtimeLead < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.bedtimeLead", BedtimeLead, 1);
		if (!(DayClothesFactor > 0))
			yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.dayClothesFactor", DayClothesFactor);
		else if (DayClothesFactor > 1)
			yield return OurResources.DataNumberTooLarge.FormatCurrentCulture($"{path}.dayClothesFactor", DayClothesFactor, 1);
	}
}

/// <summary>
/// How people stop to chat with friends they meet, and seek them out when they're lonely.
/// </summary>
public sealed class SocializingSettings
{
	/// <summary>
	/// How long a chat with a friend takes.
	/// </summary>
	public required int ChatMinutes { get; init; }

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
		const string path = $"{ScheduleSettings.SectionName}.socializing";
		foreach (var (name, value) in new[] { ("chatUrgency", ChatUrgency), ("chatGoalWeight", ChatGoalWeight), ("wakeUrgency", WakeUrgency), ("clues.workplace", Clues.Workplace), ("clues.home", Clues.Home), ("clues.sighting", Clues.Sighting), ("clues.gathering", Clues.Gathering) })
		{
			if (!(value >= 0) || !double.IsFinite(value))
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.{name}", value, 0);
		}
		foreach (var (name, value) in new[] { ("seekFactor", SeekFactor), ("recentHours", RecentHours), ("nearMinutes", NearMinutes), ("askPlayerAgainHours", AskPlayerAgainHours) })
		{
			if (!(value > 0) || !double.IsFinite(value))
				yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.{name}", value);
		}
		if (SeekMinutes < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.seekMinutes", SeekMinutes, 1);
		if (ChatMinutes < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.chatMinutes", ChatMinutes, 1);
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
	/// How much someone woken up to talk has their sleep need rise.
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
}

/// <summary>
/// How everyone starts a new game: asleep at home, meeting the sleep need, and waking up around the given hour.
/// </summary>
public sealed class ScheduleStart
{
	public required int WakeHour { get; init; }

	/// <summary>
	/// How many minutes before or after the wake hour each person wakes.
	/// </summary>
	public required IntegerRange WakeMinutes { get; init; }
}

/// <summary>
/// Makes some needs more or less urgent for people who meet a condition, by multiplying their urgency.
/// </summary>
public sealed class UrgencyFactor : IIdentified
{
	public required string Id { get; init; }

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
public sealed class TravelTimeFactor : IIdentified
{
	public required string Id { get; init; }

	public required CharacterCondition When { get; init; }

	public required double Factor { get; init; }
}
