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
	/// Settings with no needs, where people stay at home and wake at six, for game data created without schedules.
	/// </summary>
	public static ScheduleSettings None { get; } = new()
	{
		MinimumUrgency = 0,
		UrgencyPower = 1,
		SightingHours = 24,
		Sleep = new SleepSettings { NapBelow = 0, NapMinutes = new IntegerRange { Minimum = 30, Maximum = 30 }, BedtimeLead = 60, DayClothesFactor = 1 },
		Clothing = new ClothingSchedule { ChangeMinutes = 5},
		Talking = new TalkingSettings { RefuseUrgency = 100, AgreeUrgency = 100, WakeLevel = 0, AskAboutMinutes = 1, AskDoingMinutes = 5 },
		Socializing = new SocializingSettings { ChatMinutes = 30, ChatUrgency = 100, ChatGoalWeight = 1, WakeUrgency = 100, NearMinutes = 15, SeekFactor = 1, RecentHours = 12, AskPlayerAgainHours = 4, Clues = new SeekClues { Workplace = 1, Home = 1, Sighting = 1, Gathering = 1 } },
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
	/// How long the player can keep a bundle to finish their steps later (see <see cref="GameState.StepSequence.KeptSince"/>)
	/// before they're reminded of it.
	/// </summary>
	public Duration CarryWarningDelay { get; init; } = new(240);

	/// <summary>
	/// How long after someone says no to starting an appointment they're asked again, while they stay where it is.
	/// </summary>
	public Duration AskToStartAgain { get; init; } = new(15);

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
	/// The other ways the player is offered to wait, and to spend less time on a quota need than anyone else would.
	/// </summary>
	public PlayerTimeSettings Player { get; init; } = new();

	/// <summary>
	/// The needs, from the needs section of the data.
	/// </summary>
	[JsonIgnore]
	public IReadOnlyList<Need> Needs
	{
		get => m_needs;
		internal set
		{
			m_needs = value;
			ConnectStepLists();
		}
	}

	/// <summary>
	/// The step lists "call" steps can carry out, from the step lists section of the data.
	/// </summary>
	[JsonIgnore]
	public IReadOnlyList<StepList> StepLists
	{
		get => m_stepLists;
		internal set
		{
			m_stepLists = value;
			ConnectStepLists();
		}
	}

	/// <summary>
	/// Gives every need the step lists by id, so their "call" steps can carry them out.
	/// </summary>
	private void ConnectStepLists()
	{
		var byId = m_stepLists.OfType<StepList>().Where(x => !string.IsNullOrWhiteSpace(x.Id)).GroupBy(x => x.Id, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
		foreach (var need in m_needs.OfType<Need>())
		{
			need.StepLists = byId;
			need.ProfessionSteps = m_professionSteps;
			need.ProfessionActivities = m_professionActivities;
		}
	}

	/// <summary>
	/// The activities of the professions that have them, by profession id, from the professions section of the data, which
	/// are shown for needs that are work.
	/// </summary>
	[JsonIgnore]
	public IReadOnlyDictionary<string, string> ProfessionActivities
	{
		get => m_professionActivities;
		internal set
		{
			m_professionActivities = value;
			ConnectStepLists();
		}
	}

	/// <summary>
	/// The steps of the professions that have their own, by profession id, from the professions section of the data,
	/// which replace the steps of needs that are work for the people in a business in that profession.
	/// </summary>
	[JsonIgnore]
	public IReadOnlyDictionary<string, IReadOnlyList<Step>> ProfessionSteps
	{
		get => m_professionSteps;
		internal set
		{
			m_professionSteps = value;
			ConnectStepLists();
		}
	}

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

	private Need? FindRole(string role) => Needs.FirstOrDefault(x => x?.Role == role);

	private IReadOnlyList<Need> m_needs = [];
	private IReadOnlyList<StepList> m_stepLists = [];
	private IReadOnlyDictionary<string, IReadOnlyList<Step>> m_professionSteps = new Dictionary<string, IReadOnlyList<Step>>(StringComparer.Ordinal);
	private IReadOnlyDictionary<string, string> m_professionActivities = new Dictionary<string, string>(StringComparer.Ordinal);

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

		errors.AddRange(DataValidation.ValidateIdentifiedItems("stepLists", StepLists, x => x.Id, null, allowEmpty: true));
		if (Needs.Count != 0)
			errors.AddRange(DataValidation.ValidateIdentifiedItems("needs", Needs, x => x.Id, x => x.Title));
		var needs = Needs.OfType<Need>().Where(x => !string.IsNullOrWhiteSpace(x.Id)).ToList();
		var needIds = needs.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		foreach (var need in needs)
			errors.AddRange(need.Validate(needIds));

		// A profession's steps are checked as the steps of each need that's work, which they replace.
		foreach (var (professionId, steps) in ProfessionSteps)
		{
			var workNeeds = needs.Where(x => x.IsWork).ToList();
			if (workNeeds.Count == 0)
				errors.Add(OurResources.ProfessionStepsWithoutWork.FormatCurrentCulture(professionId));
			foreach (var need in workNeeds)
				errors.AddRange(need.ValidateProfessionSteps($"professions[{professionId}].business", professionId, steps));
		}

		// The engine finds the sleep and socializing needs by their roles.
		errors.AddRange(DataValidation.ValidateRoles("needs", needs, x => x.Id, x => x.Role, [SleepRole, SocializingRole]));
		if (needs.Count != 0 && FindRole(SleepRole) is null)
			errors.Add(OurResources.RoleMissing.FormatCurrentCulture("needs", SleepRole));
		foreach (var need in needs.Where(x => x.Role is SleepRole or SocializingRole && x.Kind != NeedKind.Level))
			errors.Add(OurResources.RoleWrongType.FormatCurrentCulture($"needs[{need.Id}]", need.Role, "level need"));

		if (SightingHours < 1)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.sightingHours", SightingHours, 1));
		if (!(Talking.RefuseUrgency >= 0) || !double.IsFinite(Talking.RefuseUrgency))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.talking.refuseUrgency", Talking.RefuseUrgency, 0));
		if (!(Talking.AgreeUrgency >= 0) || !double.IsFinite(Talking.AgreeUrgency))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.talking.agreeUrgency", Talking.AgreeUrgency, 0));
		if (!(Talking.WakeLevel >= 0))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.talking.wakeLevel", Talking.WakeLevel, 0));
		else if (Talking.WakeLevel > Need.MaximumLevel)
			errors.Add(OurResources.DataNumberTooLarge.FormatCurrentCulture($"{path}.talking.wakeLevel", Talking.WakeLevel, Need.MaximumLevel));
		foreach (var (name, minutes) in new[] { ("talking.askAboutMinutes", Talking.AskAboutMinutes.Minutes), ("talking.askDoingMinutes", Talking.AskDoingMinutes.Minutes) })
		{
			if (minutes < 1)
				errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.{name}", minutes, 1));
		}
		errors.AddRange(Socializing.Validate());
		errors.AddRange(Sleep.Validate());
		errors.AddRange(Clothing.Validate());
		errors.AddRange(Player.Validate());

		errors.AddRange(Idle.Place.Validate($"{path}.idle.place", allowRemembered: false));
		if (Idle.Activity is not null && string.IsNullOrWhiteSpace(Idle.Activity))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture($"{path}.idle.activity"));
		errors.AddRange(CharacterData.ValidateRange($"{path}.idle.minutes", Idle.Minutes));
		if (Idle.Minutes.Minimum < 1)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.idle.minutes.minimum", Idle.Minutes.Minimum.Minutes, 1));

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

		// A step list no need or profession calls is checked on its own, without what depends on the need calling it.
		var stepListsById = GetStepListsById();
		var called = GetCalledStepLists(stepListsById);
		foreach (var list in stepListsById.Values.Where(x => !called.Contains(x.Id)))
			errors.AddRange(Need.ValidateUncalledStepList(list, stepListsById));

		// The needs steps meet for their partners must be needs.
		foreach (var (stepPath, step) in GetAllSteps().Where(x => x.Step.PartnerMeets is { } id && !string.IsNullOrWhiteSpace(id) && !needIds.Contains(id)))
			errors.Add(OurResources.DataIdUnknown.FormatCurrentCulture($"{stepPath}.partnerMeets", step.PartnerMeets, "needs"));

		// Steps in a step list are checked for each need that calls it, so the same problem can be found more than once.
		return errors.Distinct(StringComparer.Ordinal);
	}

	/// <summary>
	/// Gets warnings about the schedules that don't stop the game loading: step lists that no need or profession calls,
	/// which are never carried out.
	/// </summary>
	public IReadOnlyList<string> GetWarnings()
	{
		var stepListsById = GetStepListsById();
		var called = GetCalledStepLists(stepListsById);
		return stepListsById.Keys.Where(x => !called.Contains(x)).Order(StringComparer.Ordinal).Select(x => OurResources.StepListNotCalled.FormatCurrentCulture(x)).ToList();
	}

	private Dictionary<string, StepList> GetStepListsById() =>
		StepLists.OfType<StepList>().Where(x => !string.IsNullOrWhiteSpace(x.Id)).GroupBy(x => x.Id, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

	/// <summary>
	/// Gets the ids of the step lists the needs' and professions' steps call, directly or through other step lists.
	/// </summary>
	private HashSet<string> GetCalledStepLists(IReadOnlyDictionary<string, StepList> stepListsById)
	{
		var called = new HashSet<string>(StringComparer.Ordinal);
		var pending = new Queue<IReadOnlyList<Step?>>(Needs.OfType<Need>().SelectMany(x => new[] { (IReadOnlyList<Step?>) (x.StepSettings ?? []), x.Finally ?? [] }).Concat(ProfessionSteps.Values));
		while (pending.TryDequeue(out var steps))
		{
			foreach (var (_, step) in GetSteps("", steps))
			{
				if (step is { Kind: StepKind.Call, List: { } id } && called.Add(id) && stepListsById.TryGetValue(id, out var list))
				{
					pending.Enqueue(list.Steps ?? []);
					pending.Enqueue(list.Finally ?? []);
				}
			}
		}

		return called;
	}

	/// <summary>
	/// Gets every step of the needs, step lists, and professions, with where each one is, and the steps they carry out.
	/// </summary>
	private IEnumerable<(string Path, Step Step)> GetAllSteps() =>
		Needs.OfType<Need>().SelectMany(need => GetSteps($"needs[{need.Id}]", need.StepSettings ?? []).Concat(GetSteps($"needs[{need.Id}]", need.Finally ?? [], "finally")))
			.Concat(StepLists.OfType<StepList>().SelectMany(x => GetSteps($"stepLists[{x.Id}]", x.Steps ?? []).Concat(GetSteps($"stepLists[{x.Id}]", x.Finally ?? [], "finally"))))
			.Concat(ProfessionSteps.SelectMany(x => GetSteps($"professions[{x.Key}].business", x.Value)));

	/// <summary>
	/// Gets the steps of a list (by default, "steps"), with where each one is, and the steps they carry out (but not the
	/// steps of the step lists they call).
	/// </summary>
	private static IEnumerable<(string Path, Step Step)> GetSteps(string path, IReadOnlyList<Step?> steps, string list = "steps")
	{
		for (var index = 0; index < steps.Count; index++)
		{
			var stepPath = $"{path}.{list}[{index}]";
			if (steps[index] is not { } step)
				continue;
			yield return (stepPath, step);
			foreach (var (nestedPath, nestedSteps, nestedList) in GetNestedLists(stepPath, step))
			{
				foreach (var nested in GetSteps(nestedPath, nestedSteps, nestedList))
					yield return nested;
			}
		}
	}

	/// <summary>
	/// Gets the lists of steps a step holds, other than its options' (its own steps and "finally", and its appointment's),
	/// with the path each is under and its name there.
	/// </summary>
	private static IEnumerable<(string Path, IReadOnlyList<Step?> Steps, string List)> GetNestedLists(string stepPath, Step step)
	{
		yield return (stepPath, step.Steps ?? [], "steps");
		yield return (stepPath, step.Finally ?? [], "finally");
		yield return ($"{stepPath}.appointment", step.Appointment?.Steps ?? [], "steps");
		yield return ($"{stepPath}.appointment", step.Appointment?.Finally ?? [], "finally");
		var options = step.Options ?? [];
		for (var optionIndex = 0; optionIndex < options.Count; optionIndex++)
		{
			yield return ($"{stepPath}.options[{optionIndex}]", options[optionIndex]?.Steps ?? [], "steps");
			yield return ($"{stepPath}.options[{optionIndex}]", options[optionIndex]?.Finally ?? [], "finally");
		}
	}

	/// <summary>
	/// Gets the conditions in the schedules, with where each one is, for checking against the character properties.
	/// </summary>
	internal IEnumerable<(string Path, CharacterCondition Condition)> GetConditions() =>
		Needs.OfType<Need>().Where(x => x.When is not null).Select(x => ($"needs[{x.Id}].when", x.When!))
			.Concat(TravelTimeFactors.OfType<TravelTimeFactor>().Select(x => ($"travelTimeFactors[{x.Id}].when", x.When)))
			.Concat(UrgencyFactors.OfType<UrgencyFactor>().Select(x => ($"urgencyFactors[{x.Id}].when", x.When)))
			.Concat(GetAllSteps().SelectMany(x => GetCharacterConditions(x.Path, x.Step)));

	/// <summary>
	/// Gets the character conditions of a step (who it talks with, and its chance's factors), with where each one is.
	/// </summary>
	private static IEnumerable<(string Path, CharacterCondition Condition)> GetCharacterConditions(string stepPath, Step step)
	{
		if (step.Who is { } who)
			yield return ($"{stepPath}.who", who);
		var factors = step.Chance?.Factors ?? [];
		for (var factorIndex = 0; factorIndex < factors.Count; factorIndex++)
		{
			if (factors[factorIndex]?.When is { } when)
				yield return ($"{stepPath}.chance.factors[{factorIndex}].when", when);
		}
	}

	/// <summary>
	/// Gets the text the player is shown that can name the other person (see <see cref="OtherText"/>), with where each one
	/// is, for checking against the character properties and templates: what steps tell the player, and what an
	/// appointment's two say about starting it.
	/// </summary>
	internal IEnumerable<(string Path, string Text)> GetOtherTexts() =>
		GetAllSteps().SelectMany(x => new (string Path, LongText? Text)[]
		{
			($"{x.Path}.player.text.text", x.Step.Player?.Text),
			($"{x.Path}.player.asked.text", x.Step.Player?.Asked),
			($"{x.Path}.appointment.start.text.text", x.Step.Appointment?.Start?.Text),
			($"{x.Path}.appointment.start.asked.text", x.Step.Appointment?.Start?.Asked),
			($"{x.Path}.appointment.ready.text.text", x.Step.Appointment?.Ready?.Text),
			($"{x.Path}.appointment.ready.asked.text", x.Step.Appointment?.Ready?.Asked),
		})
		.Where(x => x.Text?.Text is not null)
		.Select(x => (x.Path, x.Text!.Text));

	/// <summary>
	/// Gets the kinds of quarter the schedules name, with where each one is, for checking against the quarter kinds.
	/// </summary>
	internal IEnumerable<(string Path, string QuarterKind)> GetQuarterKinds() =>
		Needs.OfType<Need>().SelectMany(need => need.PlaceSetting is { } place ? [($"needs[{need.Id}].place", place)] : Array.Empty<(string, SchedulePlace)>())
			.Concat(GetAllSteps().SelectMany(x => GetStepPlaces(x.Path, x.Step)))
			.Append(($"{SectionName}.idle.place", Idle.Place))
			.SelectMany(place => (place.Item2.QuarterKinds ?? []).OfType<QuarterChoice>().Select(x => ($"{place.Item1}.quarterKinds", x.QuarterKind)));

	/// <summary>
	/// Gets the professions step conditions name, with where each one is, for checking against the professions.
	/// </summary>
	internal IEnumerable<(string Path, string Profession)> GetBusinessProfessions() =>
		GetAllSteps().SelectMany(x => GetStepConditions(x.Path, x.Step))
			.Where(x => x.Condition.BusinessProfession is not null)
			.Select(x => ($"{x.Path}.businessProfession", x.Condition.BusinessProfession!));

	/// <summary>
	/// Gets the conditions of a step and its options, with where each one is.
	/// </summary>
	private static IEnumerable<(string Path, StepCondition Condition)> GetStepConditions(string stepPath, Step step)
	{
		foreach (var condition in Need.GetConditionsWithin($"{stepPath}.when", step.When))
			yield return condition;
		var options = step.Options ?? [];
		for (var optionIndex = 0; optionIndex < options.Count; optionIndex++)
		{
			foreach (var condition in Need.GetConditionsWithin($"{stepPath}.options[{optionIndex}].when", options[optionIndex]?.When))
				yield return condition;
		}
	}

	/// <summary>
	/// Gets the places named in a step (where it goes, and where its and its options' conditions are met), with where each
	/// one is.
	/// </summary>
	private static IEnumerable<(string Path, SchedulePlace Place)> GetStepPlaces(string stepPath, Step step)
	{
		if (step.Place is { } place)
			yield return ($"{stepPath}.place", place);
		foreach (var (conditionPath, condition) in Need.GetConditionsWithin($"{stepPath}.when", step.When).Where(x => x.Condition.At is not null))
			yield return ($"{conditionPath}.at", condition.At!);
		if (step.Appointment?.Place is { } appointmentPlace)
			yield return ($"{stepPath}.appointment.place", appointmentPlace);
		if (step.Job?.Place is { } jobPlace)
			yield return ($"{stepPath}.job.place", jobPlace);
		var options = step.Options ?? [];
		for (var optionIndex = 0; optionIndex < options.Count; optionIndex++)
		{
			foreach (var (conditionPath, condition) in Need.GetConditionsWithin($"{stepPath}.options[{optionIndex}].when", options[optionIndex]?.When).Where(x => x.Condition.At is not null))
				yield return ($"{conditionPath}.at", condition.At!);
		}
	}

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

	/// <summary>
	/// For a remembered place, the name a "remember" step remembered it by.
	/// </summary>
	public string? Remembered { get; init; }

	internal IEnumerable<string> Validate(string path, bool allowRemembered = true)
	{
		if (Kind == NeedPlace.Quarter)
		{
			if (QuarterKinds is null || QuarterKinds.Count == 0)
				yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.quarterKinds");
			else if (QuarterKinds.Any(x => x is null || string.IsNullOrWhiteSpace(x.QuarterKind)))
				yield return OurResources.DataListItemBlank.FormatCurrentCulture($"{path}.quarterKinds");
		}

		if (Kind == NeedPlace.Remembered)
		{
			if (!allowRemembered)
				yield return OurResources.SchedulePlaceRememberedNotAllowed.FormatCurrentCulture($"{path}.remembered");
			else if (string.IsNullOrWhiteSpace(Remembered))
				yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.remembered");
		}

		if (Kind is NeedPlace.PartnerHome or NeedPlace.JobSite && !allowRemembered)
			yield return OurResources.SchedulePlaceRememberedNotAllowed.FormatCurrentCulture(path);
	}
}

/// <summary>
/// An amount that can grow with the size of the family: a base amount, plus an amount for each family member.
/// </summary>
[JsonConverter(typeof(FamilyAmountConverter))]
public class FamilyAmount
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
/// A length of time that can grow with the size of the family: a base number of minutes, plus a number for each family
/// member, either of which can be a duration's text.
/// </summary>
[JsonConverter(typeof(FamilyDurationConverter))]
public sealed class FamilyDuration : FamilyAmount
{
}

/// <summary>
/// Something people need, which grows more urgent until they do something about it. A need is either a level need or a
/// quota need, depending on which of <see cref="Level"/> and <see cref="Quota"/> it has. What people do to meet it is
/// either a list of steps, or going to its place and spending time there.
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
	/// The id of the icon that stands for the need where it's shown to the player, from the icons section.
	/// </summary>
	public required string Icon { get; init; }

	/// <summary>
	/// What the engine uses the need for, such as <see cref="ScheduleSettings.SleepRole"/>, or null for none.
	/// </summary>
	public string? Role { get; init; }

	/// <summary>
	/// Who has the need. Without it, everyone does.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// Where the need is met, for a need without steps: people go there and spend time there.
	/// </summary>
	[AllowedFor(nameof(StepSettings), Unset = true)]
	[RequiredFor(nameof(StepSettings), Unset = true)]
	[JsonPropertyName("place")]
	public SchedulePlace? PlaceSetting { get; init; }

	/// <summary>
	/// What people do to meet the need, in order, for a need without a place.
	/// </summary>
	[AllowedFor(nameof(PlaceSetting), Unset = true)]
	[RequiredFor(nameof(PlaceSetting), Unset = true)]
	[JsonPropertyName("steps")]
	public IReadOnlyList<Step>? StepSettings { get; init; }

	/// <summary>
	/// What happens when one of its steps fails, for causes nothing closer to the step says anything about. Without it,
	/// the steps are given up.
	/// </summary>
	public StepFailure? OnFail { get; init; }

	/// <summary>
	/// Steps carried out whenever its steps are left, however that happens, for a need that's work, after a profession's
	/// steps too. Only for a need with steps.
	/// </summary>
	[AllowedFor(nameof(StepSettings))]
	public IReadOnlyList<Step>? Finally { get; init; }

	/// <summary>
	/// The hours and days the need can be met in. A quota need must have them. A level need can only have a start and an
	/// end.
	/// </summary>
	[RequiredFor(nameof(Quota))]
	public Hours? Hours { get; init; }

	/// <summary>
	/// The settings of a level need: a level that grows steadily over time, and drops while it's being met.
	/// </summary>
	[AllowedFor(nameof(Quota), Unset = true)]
	[RequiredFor(nameof(Quota), Unset = true)]
	public LevelNeedSettings? Level { get; init; }

	/// <summary>
	/// The settings of a quota need: a quota of time or runs to spend each day or week, which grows more urgent as time
	/// runs out.
	/// </summary>
	[AllowedFor(nameof(Level), Unset = true)]
	[RequiredFor(nameof(Level), Unset = true)]
	public QuotaNeedSettings? Quota { get; init; }

	/// <summary>
	/// What someone meeting the need is shown to be doing, such as "Sleeping", unless the step they're doing says
	/// otherwise. Defaults to the title.
	/// </summary>
	public string? Activity { get; init; }

	[JsonIgnore]
	public NeedKind Kind => Level is null ? NeedKind.Quota : NeedKind.Level;

	/// <summary>
	/// The steps people take to meet the need, in the order they're carried out: its own, or for a need with a place, going
	/// there and spending time there (which counts toward a quota measured in time). A "repeat" step comes after the steps
	/// it repeats, where it decides whether to go back to the first of them.
	/// </summary>
	[JsonIgnore]
	public IReadOnlyList<Step> Steps => Layout.Steps;

	/// <summary>
	/// Gets the index in <see cref="Steps"/> of the first step a "repeat" step repeats.
	/// </summary>
	public int GetLoopStart(int repeatStep) => Layout.LoopStarts[repeatStep];

	/// <summary>
	/// Gets the index in <see cref="Steps"/> of the step carried out after one: the next one, or after the last step of a
	/// "choose" step's option (or the "choose" step, when it chooses none), the step after all its options.
	/// </summary>
	public int GetNextStep(int step) => Layout.Next.TryGetValue(step, out var next) ? next : step + 1;

	/// <summary>
	/// Gets the first of the steps of the appointment an "ask" step makes, the index of its "meet" step, and the index after
	/// the last of them, or null if it doesn't make one.
	/// </summary>
	public (int Start, int Meet, int End)? GetAppointmentSteps(int askStep) => Layout.Appointments.TryGetValue(askStep, out var steps) ? steps : null;

	/// <summary>
	/// Gets a "choose" step's options, with the index in <see cref="Steps"/> of the first of their steps and the index
	/// after the last of them.
	/// </summary>
	public IReadOnlyList<(StepOption Option, int Start, int End)> GetOptions(int chooseStep) => Layout.Options.GetValueOrDefault(chooseStep) ?? [];

	/// <summary>
	/// Whether a step is among some "finally" steps, which are carried out whenever the steps before them are left, and go
	/// on to the next step whatever happens.
	/// </summary>
	public bool IsInFinally(int step) => Layout.IsInFinally(step);

	/// <summary>
	/// Gets the lists of steps with "finally" steps that going from one step to another leaves, innermost first (see
	/// <see cref="StepLayout.GetBlocksLeft"/>).
	/// </summary>
	internal IReadOnlyList<FinallyBlock> GetBlocksLeft(int from, int to) => Layout.GetBlocksLeft(from, to);

	/// <summary>
	/// Gets the list of steps whose "finally" steps start at a step, or null if there's none.
	/// </summary>
	internal FinallyBlock? GetBlockFinallyAt(int finallyStart) => Layout.GetBlockFinallyAt(finallyStart);

	/// <summary>
	/// Whether a step is part of a "choose" step's option, so only some runs carry it out.
	/// </summary>
	public bool IsOptional(int step) => Layout.Optional.Contains(step);

	/// <summary>
	/// Gets what happens when a step fails for a cause, with the index of the step to carry on from for skipping to a step
	/// (see <see cref="StepLayout.GetFailure"/>).
	/// </summary>
	public (StepFailureKind Kind, int? Target) GetFailure(int step, FailCause cause) => Layout.GetFailure(step, cause);

	/// <summary>
	/// Gets the "choose" step and the start of the option a step is in, the innermost if there are several, or null if it
	/// isn't in one.
	/// </summary>
	public (int Choose, int Start)? GetOptionOf(int step) => Layout.GetOption(step);

	/// <summary>
	/// The step lists "call" steps can carry out, by id, set along with the needs.
	/// </summary>
	internal IReadOnlyDictionary<string, StepList>? StepLists
	{
		get => m_stepLists;
		set
		{
			m_stepLists = value;
			m_layout = null;
			m_ownLayout = null;
		}
	}

	/// <summary>
	/// The steps of the professions that have their own, by profession id, set along with the needs. For a need that's
	/// work, they replace the need's own steps for the people in a business in that profession.
	/// </summary>
	internal IReadOnlyDictionary<string, IReadOnlyList<Step>>? ProfessionSteps
	{
		get => m_professionSteps;
		set
		{
			m_professionSteps = value;
			m_layout = null;
		}
	}

	/// <summary>
	/// The need's steps laid out: for a need that's work, when professions have steps of their own, a "choose" step that
	/// takes the steps of the person's business's profession, or failing that, the need's own steps.
	/// </summary>
	private StepLayout Layout => m_layout ??= StepLayout.Create(IsWork && m_professionSteps is { Count: > 0 } professionSteps
		? [new Step
		{
			Kind = StepKind.Choose,
			First = true,
			Options = [.. professionSteps.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new StepOption { When = new StepCondition { BusinessProfession = x.Key }, Steps = x.Value, HasOwnIds = true }),
				new StepOption { Steps = OwnSteps.OfType<Step>().ToList() }],
		}]
		: OwnSteps, m_stepLists, OnFail, Finally);

	/// <summary>
	/// The need's own steps laid out, without any profession's.
	/// </summary>
	private StepLayout OwnLayout => m_ownLayout ??= StepLayout.Create(OwnSteps, m_stepLists, OnFail, Finally);

	/// <summary>
	/// Gets every step someone takes for the need, including its finally steps and the steps of the step lists they call:
	/// for a need that's work, those of their business's profession, if it has its own.
	/// </summary>
	public IReadOnlyList<Step> GetSteps(string? businessProfession) =>
		(IsWork && businessProfession is not null && m_professionSteps?.GetValueOrDefault(businessProfession) is { } steps
			? StepLayout.Create(steps, m_stepLists, OnFail, Finally)
			: OwnLayout).Steps;

	private IReadOnlyList<Step?> OwnSteps => StepSettings ?? (PlaceSetting is { } place
		? [new Step { Kind = StepKind.GoTo, Place = place }, new Step { Kind = StepKind.Do, MeetsQuota = Quota?.Time is not null }]
		: []);

	/// <summary>
	/// Whether a step takes from the time chosen for the goal: a "do" step without minutes of its own (which takes the
	/// rest of it), or a "do" or "wander" step that meets a quota measured in time (which takes at most what's left of it).
	/// </summary>
	public bool TakesChosenTime(Step step) =>
		(step.Kind == StepKind.Do && step.Minutes is null) ||
		(step.Kind is StepKind.Do or StepKind.Wander or StepKind.Seek or StepKind.Talk or StepKind.Ask && step.MeetsQuota && Quota?.Time is not null);

	/// <summary>
	/// The need's main place: where the first "do" step of its own steps is done (the place of the last "goTo" step before
	/// it), or null if there's none.
	/// </summary>
	[JsonIgnore]
	public SchedulePlace? MainPlace => OwnLayout.GetPlaceOf(OwnLayout.Steps.ToList().FindIndex(x => x.Kind == StepKind.Do));

	[JsonIgnore]
	public NeedPlace? Place => MainPlace?.Kind;

	[JsonIgnore]
	public IReadOnlyList<QuarterChoice>? QuarterKinds => MainPlace?.QuarterKinds;

	[JsonIgnore]
	public bool Nearest => MainPlace?.Nearest ?? false;

	/// <summary>
	/// Whether the need has a "seek" step that looks for a friend, so only people with friends to look for choose it.
	/// </summary>
	[JsonIgnore]
	public bool Seeks => Steps.Any(x => x is { Kind: StepKind.Seek, Who.Friends: true });

	/// <summary>
	/// Whether the need is work: some of its own steps are at a workplace. Only people with a business have it.
	/// </summary>
	[JsonIgnore]
	public bool IsWork => OwnLayout.Steps.Any(x => x.Place?.Kind == NeedPlace.Workplace);

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
	/// For a quota need measured in time, the most minutes spent on it at once.
	/// </summary>
	[JsonIgnore]
	public int? Shift => Quota?.Time?.Shift;

	/// <summary>
	/// Whether the quota is measured in runs (each time the steps are carried out) rather than time.
	/// </summary>
	[JsonIgnore]
	public bool IsRuns => Quota?.Runs is not null;

	/// <summary>
	/// Gets where a step is done: the place of the last "goTo" step before it, or null if there's none.
	/// </summary>
	public SchedulePlace? GetPlaceOf(int step) => Layout.GetPlaceOf(step);

	/// <summary>
	/// Gets what someone doing a step of the need is shown to be doing: the step's activity, or for a need that's work, the
	/// activity of the profession of the business they're in, if it has one, or otherwise the need's.
	/// </summary>
	public string GetActivity(int? step, string? businessProfession = null) =>
		(step is { } index && index >= 0 && index < Steps.Count ? Steps[index].Activity : null) ??
		(IsWork && businessProfession is not null ? m_professionActivities?.GetValueOrDefault(businessProfession) : null) ??
		Activity ?? Title;

	/// <summary>
	/// The activities of the professions that have them, by profession id, set along with the needs, which are shown for a
	/// need that's work.
	/// </summary>
	internal IReadOnlyDictionary<string, string>? ProfessionActivities
	{
		get => m_professionActivities;
		set => m_professionActivities = value;
	}

	/// <summary>
	/// Gets what someone seen meeting the need at a place was probably doing: the activity of the first "do" step done at
	/// a place like it, of the steps of their business's profession if it has its own, or the need's own steps otherwise,
	/// or the need's activity.
	/// </summary>
	public string GetActivityAt(Func<SchedulePlace, bool> isPlace, string? businessProfession = null)
	{
		var (start, end) = GetProfessionRange(businessProfession);
		var index = Enumerable.Range(start, end - start).FirstOrDefault(x => Steps[x].Kind == StepKind.Do && GetPlaceOf(x) is { } place && isPlace(place), -1);
		return GetActivity(index, businessProfession);
	}

	/// <summary>
	/// Gets the range of the steps someone in a business in a profession carries out: those of the profession's own steps,
	/// if it has them, or the need's own steps otherwise. Without professions' steps, it's all the steps.
	/// </summary>
	private (int Start, int End) GetProfessionRange(string? businessProfession)
	{
		var options = IsWork && m_professionSteps is { Count: > 0 } ? GetOptions(0) : [];
		var option = options.Count == 0 ? default : options.FirstOrDefault(x => x.Option.When?.BusinessProfession == businessProfession, options[^1]);
		return options.Count == 0 ? (0, Steps.Count) : (option.Start, option.End);
	}

	/// <summary>
	/// For a quota need measured in runs, the most minutes the "do" steps of a run could take for a family with the given
	/// number of members (or for one person, for a need that isn't shared), which is what each run counts for toward the
	/// quota (see <see cref="GetRunMinuteRange"/>).
	/// </summary>
	public int GetRunMinutes(int members) => GetRunMinuteRange(members).Max;

	/// <summary>
	/// For a quota need measured in runs, the fewest and most minutes the "do" steps of a run could take for a family with
	/// the given number of members (or for one person, for a need that isn't shared). Each way through the steps is
	/// followed: every option of a "choose" step (and none, for one that takes the first and might find none), and steps
	/// with conditions both carried out and passed by. The steps a "repeat" step repeats count once.
	/// </summary>
	public MinuteRange GetRunMinuteRange(int members) => GetMinuteRangeFrom(members, 0);

	/// <summary>
	/// The fewest and most minutes the "do" steps from one of the steps to the end could take for a family with the given
	/// number of members (or for one person, for a need that isn't shared), following every way as
	/// <see cref="GetRunMinuteRange"/> does. "do" steps without minutes of their own count for nothing.
	/// </summary>
	public MinuteRange GetMinuteRangeFrom(int members, int start)
	{
		var memberCount = GetMemberCount(members);
		var ranges = new MinuteRange?[Steps.Count];

		// The range of the minutes from a step to the end of the steps, worked out once for each step. A step comes before
		// every step that can come after it, so working back from the end finds each one when it's needed.
		MinuteRange From(int index) => index >= Steps.Count ? MinuteRange.Zero : ranges[index] ?? MinuteRange.Zero;
		for (var index = Steps.Count - 1; index >= 0; index--)
		{
			var step = Steps[index];
			var next = From(GetNextStep(index));
			ranges[index] = step.Kind switch
			{
				StepKind.Choose => GetOptions(index).Select(x => x.End > x.Start ? From(x.Start) : next)
					.Concat(step.First && GetOptions(index).All(x => x.Option.When is not null) ? [next] : [])
					.Aggregate((MinuteRange?) null, (total, x) => total?.Or(x) ?? x) ?? next,
				StepKind.Do when step.Minutes is { } amount => step.When is null ? next + (int) amount.GetAmount(memberCount) : (next + (int) amount.GetAmount(memberCount)).Or(next),
				StepKind.Stop => step.When is null ? MinuteRange.Zero : MinuteRange.Zero.Or(next),
				_ => next,
			};
		}

		return From(start);
	}

	/// <summary>
	/// For a quota need, the most minutes spent on it at once: a shift, or one run.
	/// </summary>
	public int GetShiftMinutes(int members) => IsRuns ? GetRunMinutes(members) : Shift ?? 0;

	/// <summary>
	/// For a quota need, how many minutes it takes each period for a family with the given number of members (or for one
	/// person, for a need that isn't shared). Runs are counted by the minutes their "do" steps take.
	/// </summary>
	public long GetQuotaMinutes(int members) =>
		Quota?.Runs is not null
			? GetQuotaRuns(members) * GetRunMinutes(members)
			: (long) (Quota?.Time?.Minutes.GetAmount(GetMemberCount(members)) ?? 0);

	/// <summary>
	/// For a quota need measured in runs, how many runs it takes each period. The total is rounded up.
	/// </summary>
	public long GetQuotaRuns(int members) => (long) Math.Ceiling(Quota?.Runs?.GetAmount(GetMemberCount(members)) ?? 0);

	private int GetMemberCount(int members) => SharedByFamily ? members : 0;

	internal IEnumerable<string> Validate(IReadOnlySet<string> needIds)
	{
		var path = $"needs[{Id}]";
		if (Activity is not null && string.IsNullOrWhiteSpace(Activity))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.activity");

		if (IsBroken(nameof(Level), Level is not null) || IsBroken(nameof(Quota), Quota is not null))
		{
			yield return OurResources.NeedKindInvalid.FormatCurrentCulture(Id);
			yield break;
		}

		if (IsBroken(nameof(PlaceSetting), PlaceSetting is not null) || IsBroken(nameof(StepSettings), StepSettings is not null))
			yield return OurResources.NeedPlaceOrSteps.FormatCurrentCulture(Id);
		if (IsBroken(nameof(Finally), Finally is not null))
			yield return OurResources.NeedFinallyWithoutSteps.FormatCurrentCulture(Id);
		if (PlaceSetting is { } placeSetting)
		{
			foreach (var error in placeSetting.Validate($"{path}.place", allowRemembered: false))
				yield return error;
		}

		if (StepSettings is { } steps)
		{
			foreach (var error in ValidateSteps(path, steps))
				yield return error;
		}

		if (Hours is { } hours)
		{
			foreach (var error in hours.Validate($"{path}.hours", this))
				yield return error;
		}

		var errors = Level is { } level ? ValidateLevel(path, level, needIds) : ValidateQuota(path, Quota!);
		foreach (var error in errors)
			yield return error;
	}

	/// <summary>
	/// Whether one of the need's settings breaks the rules marked on it, which say which settings each kind of need allows
	/// and requires.
	/// </summary>
	private bool IsBroken(string property, bool isSet) => DataRules.Check(this, property, isSet) != DataRuleViolation.None;

	private IEnumerable<string> ValidateSteps(string path, IReadOnlyList<Step> steps)
	{
		if (steps.Count == 0)
		{
			yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.steps");
			yield break;
		}

		foreach (var error in ValidateScope(this, m_stepLists, path, steps, Finally, new StepNames(), [], OnFail is null ? [] : [($"{path}.onFail", OnFail)], loops: []))
			yield return error;

		if (!OwnLayout.Steps.Any(x => x.Kind is StepKind.Do or StepKind.Wander or StepKind.Seek))
			yield return OurResources.NeedStepsWithoutDo.FormatCurrentCulture(Id);
		var countsRuns = OwnLayout.Steps.Any(x => x.Kind == StepKind.CountRun);
		if (countsRuns && !IsRuns)
			yield return OurResources.NeedCountRunNotRuns.FormatCurrentCulture(Id);
		else if (!countsRuns && IsRuns)
			yield return OurResources.NeedRunsWithoutCountRun.FormatCurrentCulture(Id);
		if (Quota?.Time is not null && !OwnLayout.Steps.Any(x => x is { Kind: StepKind.Do, MeetsQuota: true }))
			yield return OurResources.NeedTimeWithoutMeetsQuota.FormatCurrentCulture(Id);
	}

	/// <summary>
	/// Validates a profession's steps, which replace the need's own for the people in a business in that profession: as
	/// the need's own steps would be, with at least one "do" step, and one that meets a quota measured in time.
	/// </summary>
	internal IEnumerable<string> ValidateProfessionSteps(string path, string professionId, IReadOnlyList<Step?> steps)
	{
		if (steps.Count == 0)
		{
			yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.steps");
			yield break;
		}

		foreach (var error in ValidateScope(this, m_stepLists, path, steps, null, new StepNames(), [], [], loops: []))
			yield return error;
		var laidOut = StepLayout.Create(steps, m_stepLists).Steps;
		if (!laidOut.Any(x => x.Kind == StepKind.Do))
			yield return OurResources.ProfessionStepsWithoutDo.FormatCurrentCulture(professionId);
		if (Quota?.Time is not null && !laidOut.Any(x => x is { Kind: StepKind.Do or StepKind.Wander, MeetsQuota: true }))
			yield return OurResources.ProfessionStepsWithoutMeetsQuota.FormatCurrentCulture(professionId, Id);
	}

	/// <summary>
	/// Validates a step list that no need or profession calls, on its own: without the need calling it, and without knowing
	/// what the steps that would come before it took, remembered, or asked for.
	/// </summary>
	internal static IEnumerable<string> ValidateUncalledStepList(StepList list, IReadOnlyDictionary<string, StepList> stepLists) =>
		ValidateScope(null, stepLists, $"stepLists[{list.Id}]", list.Steps ?? [], list.Finally, new StepNames { IsOpen = true }, [list.Id],
			list.OnFail is null ? [] : [($"stepLists[{list.Id}].onFail", list.OnFail)], loops: null);

	/// <summary>
	/// Validates the steps of a need, step list, or appointment, and its "finally" steps, as <see cref="ValidateStepList"/>
	/// does, along with the "onFail" settings of the need or step list, and checks that every step they skip to has a
	/// unique id among them. The loops are the ids of the "repeat" steps it's in, innermost first, or null if they aren't
	/// known, as for a step list no need calls.
	/// </summary>
	private static IEnumerable<string> ValidateScope(Need? need, IReadOnlyDictionary<string, StepList>? stepLists, string path, IReadOnlyList<Step?> steps, IReadOnlyList<Step?>? finallySteps, StepNames names,
		IReadOnlyList<string> calling, IReadOnlyList<(string Path, StepFailure Failure)> failures, IReadOnlyList<string?>? loops, bool isAppointment = false, bool isFinally = false)
	{
		var scope = new StepScope();
		foreach (var (failurePath, failure) in failures)
		{
			foreach (var error in ValidateFailure(failurePath, failure, loops, scope))
				yield return error;
		}

		var context = new StepListContext(need, stepLists, names, calling, scope);
		foreach (var error in ValidateStepList(context, path, "steps", steps, loops, isFinally, isAppointment))
			yield return error;
		foreach (var error in ValidateStepList(context, path, "finally", finallySteps ?? [], loops, isFinally: true))
			yield return error;
		foreach (var (skipPath, id) in scope.Skips.Where(x => !scope.Ids.Contains(x.Id)))
			yield return OurResources.StepSkipToUnknown.FormatCurrentCulture(skipPath, id);
	}

	/// <summary>
	/// Checks what happens when a step fails, given the ids of the "repeat" steps it's in, innermost first (or null if
	/// they aren't known): going on with or leaving a loop needs one around it (the one it names, if it does), and
	/// skipping to a "repeat" step it's in isn't allowed, since it's unclear whether that's going on with it or starting it
	/// again. The steps it skips to are noted, to check once all the ids are known.
	/// </summary>
	private static IEnumerable<string> ValidateFailure(string path, StepFailure failure, IReadOnlyList<string?>? loops, StepScope scope)
	{
		scope.AddSkips(path, failure);
		foreach (var result in loops is null ? [] : failure.Results)
		{
			if (result.Kind is StepFailureKind.LoopContinue or StepFailureKind.LoopBreak)
			{
				if (loops!.Count == 0)
					yield return OurResources.StepLoopResultWithoutLoop.FormatCurrentCulture(path, result.Kind == StepFailureKind.LoopContinue ? "loopContinue" : "loopBreak");
				else if (result.Target is { } id && !loops.Contains(id, StringComparer.Ordinal))
					yield return OurResources.StepLoopResultUnknown.FormatCurrentCulture(path, id);
			}
			else if (result is { Kind: StepFailureKind.SkipTo, Target: { } id } && loops!.Contains(id, StringComparer.Ordinal))
			{
				yield return OurResources.StepSkipToEnclosingLoop.FormatCurrentCulture(path, id);
			}
		}
	}

	/// <summary>
	/// Validates a list of steps (a need's or step list's "steps" or "finally"), and the steps that "repeat" and "group"
	/// steps, "choose" options, and called step lists in it carry out, given the names of the bundles and places taken and
	/// remembered by the steps before them, the step lists being called, the ids of the steps of the need, step list, or
	/// appointment they're in, and the ids of the "repeat" steps they're in. Steps in a called step list are reported at
	/// their place in the step list. A "meet" step is only allowed directly among an appointment's steps, and steps in a
	/// "finally" can't have "onFail", since they always go on to the next one.
	/// </summary>
	private static IEnumerable<string> ValidateStepList(StepListContext context, string path, string list, IReadOnlyList<Step?> steps, IReadOnlyList<string?>? loops, bool isFinally, bool isAppointment = false)
	{
		var (need, stepLists, names, calling, scope) = context;
		for (var index = 0; index < steps.Count; index++)
		{
			var stepPath = $"{path}.{list}[{index}]";
			if (steps[index] is not { } step)
			{
				yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.{list}");
				continue;
			}

			foreach (var error in step.Validate(stepPath, need))
				yield return error;
			if (step.Kind == StepKind.Meet && !isAppointment)
				yield return OurResources.StepMeetNotAllowed.FormatCurrentCulture(stepPath);
			if (step.Id is { } stepId && !string.IsNullOrWhiteSpace(stepId) && !scope.Ids.Add(stepId))
				yield return OurResources.StepIdDuplicate.FormatCurrentCulture($"{stepPath}.id", stepId);
			if (step.OnFail is { } onFail)
			{
				if (isFinally)
					yield return OurResources.StepOnFailInFinally.FormatCurrentCulture($"{stepPath}.onFail");

				// A "repeat" step's own "onFail" is for the steps in it, so it's inside its loop.
				foreach (var error in ValidateFailure($"{stepPath}.onFail", onFail, step.Kind == StepKind.Repeat && loops is not null ? [step.Id, .. loops] : loops, scope))
					yield return error;
			}

			// A bundle made in place of another carries its things, as the same kind.
			StepItemKind? replacedKind = null;
			if (step.Kind == StepKind.Make && step.Replaces is { } replaced && !string.IsNullOrWhiteSpace(replaced))
			{
				if (names.Bundles.Remove(replaced, out replacedKind))
					names.Replaced[replaced] = replacedKind;
				else if (!names.IsOpen)
					yield return OurResources.StepBundleUnknown.FormatCurrentCulture($"{stepPath}.replaces", replaced);
			}
			if (step.Kind is StepKind.Take or StepKind.Make && step.Bundle is { } taken && !string.IsNullOrWhiteSpace(taken.Id))
				names.Bundles[taken.Id] = step.Kind == StepKind.Make ? replacedKind : step.ItemKind;
			if (step.Kind == StepKind.Put && step.Bundle is { } bundle && !string.IsNullOrWhiteSpace(bundle.Id))
			{
				if (!names.TryGetBundle(bundle.Id, isFinally, out var bundleKind))
				{
					if (!names.IsOpen)
						yield return OurResources.StepBundleUnknown.FormatCurrentCulture($"{stepPath}.bundle", bundle.Id);
				}
				else if (bundleKind != step.ItemKind)
					yield return OurResources.StepPutKindMismatch.FormatCurrentCulture($"{stepPath}.kind", bundle.Id);
			}
			var conditionPlaces = GetConditionsWithin($"{stepPath}.when", step.When).Select(x => ($"{x.Path}.at", x.Condition.At));
			foreach (var (placePath, place) in conditionPlaces.Prepend(($"{stepPath}.place", step.Place)))
			{
				if (place is { Kind: NeedPlace.Remembered, Remembered: { } remembered } && !string.IsNullOrWhiteSpace(remembered) && !names.Places.Contains(remembered) && !names.IsOpen)
					yield return OurResources.StepPlaceUnknown.FormatCurrentCulture($"{placePath}.remembered", remembered);
			}

			if (step.Kind == StepKind.Remember && step.Name is { } name)
				names.Places.Add(name);
			foreach (var error in ValidateNames($"{stepPath}.when", step.When, names, isFinally))
				yield return error;
			if (step.Kind == StepKind.Ask && step.Name is { } answer)
				names.Answers.Add(answer);

			if (step is { Kind: StepKind.Repeat, Steps: { } repeated })
			{
				if (need is not null && need.Quota?.Time is null)
					yield return OurResources.StepRepeatNotAllowed.FormatCurrentCulture(stepPath);
				if (repeated.Count == 0)
					yield return OurResources.DataListEmpty.FormatCurrentCulture($"{stepPath}.steps");
				else if (!StepLayout.Create(repeated, stepLists).Steps.Any(x => x is { Kind: StepKind.Do, MeetsQuota: true }))
					yield return OurResources.StepRepeatWithoutMeetsQuota.FormatCurrentCulture(stepPath);
				foreach (var error in ValidateStepList(context, stepPath, "steps", repeated, loops is null ? null : [step.Id, .. loops], isFinally))
					yield return error;
			}

			if (step is { Kind: StepKind.Group, Steps: { } grouped })
			{
				if (grouped.Count == 0)
					yield return OurResources.DataListEmpty.FormatCurrentCulture($"{stepPath}.steps");
				foreach (var error in ValidateStepList(context, stepPath, "steps", grouped, loops, isFinally))
					yield return error;
			}

			foreach (var error in ValidateStepList(context, stepPath, "finally", step.Finally ?? [], loops, isFinally: true))
				yield return error;
			if (step is { Kind: StepKind.Choose, Options: { } options })
			{
				if (options.Count == 0)
					yield return OurResources.DataListEmpty.FormatCurrentCulture($"{stepPath}.options");
				for (var optionIndex = 0; optionIndex < options.Count; optionIndex++)
				{
					var optionPath = $"{stepPath}.options[{optionIndex}]";
					if (options[optionIndex] is not { } option)
					{
						yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{stepPath}.options");
						continue;
					}

					// The player chooses among the options of a "choose" step that doesn't take the first, so each needs a title.
					if (!step.First && string.IsNullOrWhiteSpace(option.Player?.Title))
						yield return OurResources.DataValueMissing.FormatCurrentCulture($"{optionPath}.player.title");
					if (option.OnFail is { } optionFailure)
					{
						if (isFinally)
							yield return OurResources.StepOnFailInFinally.FormatCurrentCulture($"{optionPath}.onFail");
						foreach (var error in ValidateFailure($"{optionPath}.onFail", optionFailure, loops, scope))
							yield return error;
					}
					if (option.When is { } when)
					{
						foreach (var error in when.Validate($"{optionPath}.when", need))
							yield return error;
						foreach (var error in ValidateNames($"{optionPath}.when", when, names, isFinally))
							yield return error;
					}

					foreach (var error in ValidateStepList(context, optionPath, "steps", option.Steps ?? [], loops, isFinally))
						yield return error;
					foreach (var error in ValidateStepList(context, optionPath, "finally", option.Finally ?? [], loops, isFinally: true))
						yield return error;
				}
			}

			if (step is { Kind: StepKind.Ask, Appointment.Steps: { } appointmentSteps })
			{
				// The appointment's steps are carried out on their own, later, so they start with nothing taken, remembered, or
				// asked. They take set times, so the one asked knows how long they'll be.
				var appointmentPath = $"{stepPath}.appointment";
				var laidOut = StepLayout.Create(appointmentSteps, stepLists);
				if (!laidOut.Steps.Any(x => x.Kind == StepKind.Do))
					yield return OurResources.StepAppointmentWithoutDo.FormatCurrentCulture(appointmentPath);
				if (laidOut.Steps.Any(x => x.Appointment is not null))
					yield return OurResources.StepAppointmentNested.FormatCurrentCulture(appointmentPath);
				for (var appointmentIndex = 0; appointmentIndex < appointmentSteps.Count; appointmentIndex++)
				{
					if (appointmentSteps[appointmentIndex] is { Kind: StepKind.Do, Minutes: null })
						yield return OurResources.StepSettingMissing.FormatCurrentCulture($"{appointmentPath}.steps[{appointmentIndex}].minutes", "do");
				}

				// Going there and meeting is its one "meet" step, directly among its steps. The steps before it are done once they set
				// off, before walking there, so they can only go somewhere, handle things, change clothes, choose, or stop.
				var meetCount = appointmentSteps.Count(x => x?.Kind == StepKind.Meet);
				if (meetCount != 1 || laidOut.Steps.Count(x => x.Kind == StepKind.Meet) != meetCount)
					yield return OurResources.StepAppointmentMeetCount.FormatCurrentCulture(appointmentPath);
				var meetIndex = laidOut.Steps.ToList().FindIndex(x => x.Kind == StepKind.Meet);
				if (meetIndex >= 0 && laidOut.Steps.Take(meetIndex).Any(x => x.Kind is not (StepKind.GoTo or StepKind.Take or StepKind.Put or StepKind.Make or StepKind.Dress or StepKind.Undress or
					StepKind.Remember or StepKind.Choose or StepKind.Stop)))
				{
					yield return OurResources.StepAppointmentBeforeMeet.FormatCurrentCulture(appointmentPath);
				}

				foreach (var error in ValidateScope(need, stepLists, appointmentPath, appointmentSteps, step.Appointment.Finally, new StepNames(), calling, [], loops: loops is null ? null : [], isAppointment: true))
					yield return error;
			}

			if (step is { Kind: StepKind.Call, List: { } id } && !string.IsNullOrWhiteSpace(id))
			{
				if (stepLists is null || !stepLists.TryGetValue(id, out var calledList))
				{
					yield return OurResources.DataIdUnknown.FormatCurrentCulture($"{stepPath}.list", id, "stepLists");
				}
				else if (calling.Contains(id, StringComparer.Ordinal))
				{
					yield return OurResources.StepListCalledInItself.FormatCurrentCulture($"{stepPath}.list", id);
				}
				else
				{
					foreach (var error in ValidateScope(need, stepLists, $"stepLists[{id}]", calledList.Steps ?? [], calledList.Finally, names, [.. calling, id],
						calledList.OnFail is null ? [] : [($"stepLists[{id}].onFail", calledList.OnFail)], loops, isFinally: isFinally))
						yield return error;
				}
			}
		}
	}

	/// <summary>
	/// Checks that the answers a condition names were asked for by steps before it, and the bundles it names were taken or
	/// made by them.
	/// </summary>
	private static IEnumerable<string> ValidateNames(string path, StepCondition? when, StepNames names, bool isFinally)
	{
		foreach (var (conditionPath, condition) in GetConditionsWithin(path, when))
		{
			if (condition.Carrying is { } carrying && !string.IsNullOrWhiteSpace(carrying) && !names.TryGetBundle(carrying, isFinally, out _) && !names.IsOpen)
				yield return OurResources.StepBundleUnknown.FormatCurrentCulture($"{conditionPath}.carrying", carrying);
			foreach (var (name, answer) in new[] { ("answeredYes", condition.AnsweredYes), ("answeredNo", condition.AnsweredNo) })
			{
				if (answer is not null && !names.Answers.Contains(answer) && !names.IsOpen)
					yield return OurResources.StepAnswerUnknown.FormatCurrentCulture($"{conditionPath}.{name}", answer);
			}
		}
	}

	/// <summary>
	/// Gets a condition and the conditions it's made of, each with its path, or nothing if there's no condition.
	/// </summary>
	internal static IEnumerable<(string Path, StepCondition Condition)> GetConditionsWithin(string path, StepCondition? when) =>
		when is null ? [] : when.GetNested(path).Prepend((path, when));

	/// <summary>
	/// The names of the bundles, places, and answers taken into, remembered, and asked for by the steps validated so far.
	/// </summary>
	private sealed class StepNames
	{
		/// <summary>
		/// Whether names that weren't taken, remembered, or asked for by the steps validated so far are allowed, since steps
		/// that would come before could have, as for a step list no need calls.
		/// </summary>
		public bool IsOpen { get; init; }

		/// <summary>
		/// The bundles taken or made, with the kind of things taken into them, or null for a bundle that was made.
		/// </summary>
		public Dictionary<string, StepItemKind?> Bundles { get; } = new(StringComparer.Ordinal);

		public HashSet<string> Places { get; } = new(StringComparer.Ordinal);

		public HashSet<string> Answers { get; } = new(StringComparer.Ordinal);

		/// <summary>
		/// The bundles other bundles were made in place of, with the kind of things taken into them. "Finally" steps can still
		/// name them, since they can be carried out before the bundle is replaced.
		/// </summary>
		public Dictionary<string, StepItemKind?> Replaced { get; } = new(StringComparer.Ordinal);

		/// <summary>
		/// Gets whether a bundle was taken or made, and the kind of things taken into it, counting those replaced since, for
		/// "finally" steps.
		/// </summary>
		public bool TryGetBundle(string id, bool isFinally, out StepItemKind? kind) => Bundles.TryGetValue(id, out kind) || (isFinally && Replaced.TryGetValue(id, out kind));
	}

	/// <summary>
	/// What validating a list of steps keeps track of: the need they're for, if known, the step lists, the names taken so
	/// far, the step lists being called, and the ids of the steps of the need, step list, or appointment they're in.
	/// </summary>
	private sealed record StepListContext(Need? Need, IReadOnlyDictionary<string, StepList>? StepLists, StepNames Names, IReadOnlyList<string> Calling, StepScope Scope);

	/// <summary>
	/// The ids of the steps of a need, step list, or appointment, and the steps "onFail" settings among them skip to.
	/// </summary>
	private sealed class StepScope
	{
		public HashSet<string> Ids { get; } = new(StringComparer.Ordinal);

		public System.Collections.ObjectModel.Collection<(string Path, string Id)> Skips { get; } = [];

		public void AddSkips(string path, StepFailure failure)
		{
			foreach (var result in failure.Results.Where(x => x.Kind == StepFailureKind.SkipTo))
				Skips.Add(($"{path}.skipTo", result.Target ?? ""));
		}
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
		if (IsBroken(nameof(Hours), Hours is not null))
			yield return OurResources.NeedSettingMissing.FormatCurrentCulture(Id, "hours", "quota");
		if (DataRules.Check(quota, nameof(QuotaNeedSettings.Time), quota.Time is not null) != DataRuleViolation.None || DataRules.Check(quota, nameof(QuotaNeedSettings.Runs), quota.Runs is not null) != DataRuleViolation.None)
			yield return OurResources.NeedQuotaMeasureInvalid.FormatCurrentCulture(Id);

		var amounts = new List<(string Path, FamilyAmount Amount, bool IsWhole)>();
		if (quota.Time is { } time)
		{
			amounts.Add(($"{path}.quota.time.minutes", time.Minutes, true));
			if (time.Shift < 1)
				yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.quota.time.shift", time.Shift);
		}

		if (quota.Runs is { } runs)
		{
			amounts.Add(($"{path}.quota.runs", runs, false));

			// A run's time is only worked out once it's otherwise valid: measured in runs alone, with minutes for each "do" step.
			if (quota.Time is null && StepSettings is null)
				yield return OurResources.NeedRunsWithoutCountRun.FormatCurrentCulture(Id);
			else if (quota.Time is null && Steps.Where(x => x?.Kind == StepKind.Do).All(x => x.Minutes is not null) && GetRunMinutes(1) < 1)
				yield return OurResources.NeedRunMinutesNotPositive.FormatCurrentCulture(Id);
		}

		foreach (var (amountPath, amount, isWhole) in amounts)
		{
			foreach (var error in amount.Validate(amountPath, isWhole))
				yield return error;
		}

		var stepAmounts = Steps.Where(x => x?.Minutes is not null).Select(x => x.Minutes!);
		if (!quota.SharedByFamily && amounts.Select(x => x.Amount).Concat(stepAmounts).Any(x => x.PerMember is not null))
			yield return OurResources.NeedPerMemberNotShared.FormatCurrentCulture(Id);

		if (quota.OverdueUrgency is { } overdue)
		{
			if (!DataRules.IsAllowed(quota, nameof(QuotaNeedSettings.OverdueUrgency)) || Days is null)
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

	/// <summary>
	/// Whether a level of the need is in its last, worst level, of two or more shown to the player.
	/// </summary>
	public bool IsWorstLevel(double level) => Levels is { Count: > 1 } levels && level >= levels[^1].Minimum;

	private StepLayout? m_layout;
	private StepLayout? m_ownLayout;
	private IReadOnlyDictionary<string, IReadOnlyList<Step>>? m_professionSteps;
	private IReadOnlyDictionary<string, string>? m_professionActivities;
	private IReadOnlyDictionary<string, StepList>? m_stepLists;
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
/// The settings of a quota need: a quota of time or runs to spend each day or week within its hours, such as work or
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
	[AllowedFor(nameof(Period), NeedPeriod.Week)]
	public double? OverdueUrgency { get; init; }

	/// <summary>
	/// Whether it's met for the whole family by anyone in it, such as shopping.
	/// </summary>
	public bool SharedByFamily { get; init; }

	/// <summary>
	/// A quota measured in time.
	/// </summary>
	[AllowedFor(nameof(Runs), Unset = true)]
	[RequiredFor(nameof(Runs), Unset = true)]
	public QuotaTime? Time { get; init; }

	/// <summary>
	/// A quota measured in runs: how many times each period its steps must be carried out to the end. The total is rounded
	/// up. A run is counted by its "countRun" step.
	/// </summary>
	[AllowedFor(nameof(Time), Unset = true)]
	[RequiredFor(nameof(Time), Unset = true)]
	public FamilyAmount? Runs { get; init; }
}

/// <summary>
/// A quota measured in time, spent in shifts.
/// </summary>
public sealed class QuotaTime
{
	/// <summary>
	/// How many minutes it takes each period.
	/// </summary>
	public required FamilyDuration Minutes { get; init; }

	/// <summary>
	/// The most minutes spent on it at once, before deciding again what to do.
	/// </summary>
	public required int Shift { get; init; }
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
	/// A quota of time or runs to spend on something each day or week within certain hours, such as work or fetching
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

	/// <summary>
	/// The place a "remember" step remembered, by its name.
	/// </summary>
	Remembered,

	/// <summary>
	/// The home of another household, chosen at random, with nearer homes more likely.
	/// </summary>
	OtherHome,

	/// <summary>
	/// The home of the person the steps last talked with or asked.
	/// </summary>
	PartnerHome,

	/// <summary>
	/// The place of the person's job for the need, taken on by an "ask" step, while it isn't done.
	/// </summary>
	JobSite,
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
/// How long people take over their clothes.
/// </summary>
public sealed class ClothingSchedule
{
	/// <summary>
	/// How many minutes changing clothes takes.
	/// </summary>
	public required Duration ChangeMinutes { get; init; }

	internal IEnumerable<string> Validate()
	{
		const string path = $"{ScheduleSettings.SectionName}.clothing";
		if (ChangeMinutes < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.changeMinutes", ChangeMinutes.Minutes, 1);
	}
}

/// <summary>
/// The other ways the player is offered to wait, besides waiting until something changes, and to spend less time on a
/// quota need measured in time than the shift anyone else would choose.
/// </summary>
public sealed class PlayerTimeSettings
{
	/// <summary>
	/// The other ways to wait, in the order they're offered.
	/// </summary>
	public IReadOnlyList<PlayerTimeChoice> Waits { get; init; } = [];

	/// <summary>
	/// The shorter shifts, in the order they're offered.
	/// </summary>
	public IReadOnlyList<PlayerTimeChoice> Shifts { get; init; } = [];

	internal IEnumerable<string> Validate()
	{
		const string path = $"{ScheduleSettings.SectionName}.player";
		foreach (var (name, choices) in new[] { ("waits", Waits), ("shifts", Shifts) })
		{
			for (var index = 0; index < choices.Count; index++)
			{
				var choice = choices[index];
				var choicePath = $"{path}.{name}[{index}]";
				if (choice is null)
					yield return OurResources.DataValueMissing.FormatCurrentCulture(choicePath);
				else if ((choice.Minutes is null) == (choice.Until is null))
					yield return OurResources.PlayerTimeChoiceInvalid.FormatCurrentCulture(choicePath);
				else if (choice.Minutes is { } minutes && minutes < 1)
					yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{choicePath}.minutes", minutes.Minutes, 1);
			}
		}
	}
}

/// <summary>
/// How long the player can choose to wait or spend on something: so many minutes, or until a time.
/// </summary>
public sealed class PlayerTimeChoice
{
	/// <summary>
	/// So many minutes.
	/// </summary>
	public Duration? Minutes { get; init; }

	/// <summary>
	/// Until a time worked out when it's offered.
	/// </summary>
	public PlayerTimeUntil? Until { get; init; }
}

/// <summary>
/// A time the player can choose to wait or spend on something until.
/// </summary>
public enum PlayerTimeUntil
{
	/// <summary>
	/// The start of the next part of the day.
	/// </summary>
	NextDayPart,
}

/// <summary>
/// What people do when none of their needs are urgent enough: wait somewhere for a while, then decide again.
/// </summary>
public sealed class IdleSettings
{
	public required SchedulePlace Place { get; init; }

	public required DurationRange Minutes { get; init; }

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
	public required DurationRange NapMinutes { get; init; }

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
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.napMinutes.minimum", NapMinutes.Minimum.Minutes, 1);
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
	public required Duration ChatMinutes { get; init; }

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
		if (ChatMinutes < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.chatMinutes", ChatMinutes.Minutes, 1);
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
	/// Someone asked to spend time with the one asking (an "ask" step's <see cref="Step.ForMinutes"/>) only says yes if
	/// none of their needs would be at least this urgent by the end of that time.
	/// </summary>
	public required double AgreeUrgency { get; init; }

	/// <summary>
	/// How much someone woken up to talk has their sleep need rise.
	/// </summary>
	public required double WakeLevel { get; init; }

	/// <summary>
	/// How long it takes to ask someone whether they've seen a person.
	/// </summary>
	public required Duration AskAboutMinutes { get; init; }

	/// <summary>
	/// How long it takes to ask someone what they're doing.
	/// </summary>
	public required Duration AskDoingMinutes { get; init; }
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
