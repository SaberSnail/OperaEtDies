using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.Time;
using OperaEtDies.Town;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.GameState;

/// <summary>
/// Moves people through their tasks as time passes. Whenever someone other than the player runs out of tasks, they
/// choose a new goal from how urgent their needs are, and plan the tasks to reach it. It keeps no state of its own: all
/// of it is in the people.
/// </summary>
internal sealed class Scheduler
{
	public Scheduler(GameWorld world)
	{
		m_world = world;
		m_calendar = world.GameData.Calendar;
		m_settings = world.GameData.Schedules;
	}

	/// <summary>
	/// Runs everyone's tasks from one time to another, in the order they end, then brings everyone's needs up to the end.
	/// If the stop check is met after a task ends, it stops at that time instead, once every task ending then has ended.
	/// Returns the time it ran until.
	/// </summary>
	public TimePoint RunUntil(TimePoint start, TimePoint end, Func<bool>? shouldStop = null)
	{
		// Anyone other than the player without a task decides what to do now.
		foreach (var person in m_world.Npcs.Where(x => x.TaskEnd is null))
			Continue(person, start);

		while (GetNextTaskEnd() is { } next && next.TaskEnd! <= end)
		{
			var time = next.TaskEnd! > start ? next.TaskEnd! : start;
			next.UpdateNeeds(time);
			var task = next.FinishTask();
			if (task is TravelTask travel)
				next.Location = travel.Destination;
			Continue(next, time);

			if (shouldStop?.Invoke() == true)
				end = time;
		}

		foreach (var person in m_world.People)
			person.UpdateNeeds(end);
		return end;
	}

	/// <summary>
	/// Starts a person's next task that takes time, doing any that don't along the way. Someone other than the player
	/// who has no tasks left chooses a new goal first.
	/// </summary>
	public void Continue(Person person, TimePoint time)
	{
		person.UpdateNeeds(time);
		while (person.TaskEnd is null && (person.Tasks.Count != 0 || !person.IsPlayer))
		{
			if (person.Tasks.Count == 0)
				ChooseGoal(person, time);

			var task = person.Tasks[0];
			if (task is AtWorkTask atWork)
			{
				person.IsAtWork = atWork.IsAtWork;
				person.FinishTask();
			}
			else
			{
				person.StartTask(m_calendar.AddMinutes(time, GetMinutes(person, task)));
				m_world.OnTaskStarted(person, task, time);
			}
		}
	}

	/// <summary>
	/// Gives a person a goal and the tasks to reach it: travel to the place their need is met, then time spent there.
	/// They choose at random among their needs that are urgent enough, favoring the more urgent ones, or stay idle if
	/// none are.
	/// </summary>
	public void ChooseGoal(Person person, TimePoint time)
	{
		// Every need that's at all urgent is kept with how urgent it was, so developers can see why the goal was chosen.
		var urgencies = m_settings.Needs
			.Select(need => new NeedUrgency(need, GetUrgency(person, need, time)))
			.Where(x => x.Urgency > 0)
			.ToList();
		var candidates = urgencies.Where(x => m_settings.IsGoalCandidate(x.Urgency)).ToList();

		var distances = GetDistances(person.Location);
		var choice = candidates.Count == 0 ? null : m_world.Random.NextWeighted(candidates, x => m_settings.GetGoalWeight(x.Urgency)).Need;
		var place = choice is null ? null : FindPlace(person, choice.Place, choice.QuarterKinds, distances, choice.Nearest);
		var minutes = choice is { Kind: NeedKind.Quota } && place is not null ? GetShiftMinutes(person, choice, time, distances[place].Minutes) : (int?) null;
		var result = choice is null ? GoalChoiceResult.NothingUrgent : place is null ? GoalChoiceResult.NoPlace : minutes is 0 or < 0 ? GoalChoiceResult.NoTime : GoalChoiceResult.Chosen;
		var goalChoice = new GoalChoice(time, urgencies, choice, result);
		if (result == GoalChoiceResult.Chosen)
		{
			// A trip's time is spent at the place first, and whatever's left at home afterward.
			var placeMinutes = choice!.IsTrips ? Math.Min(choice.GetVisitMinutes(person.Family.Members.Count), minutes!.Value) : minutes;
			var homeMinutes = minutes - placeMinutes ?? 0;
			person.SetPlan(choice, PlanTasks(person, place!, distances, choice.AtWork, placeMinutes, choice.ReturnHome ? person.Home : null, homeMinutes), goalChoice);
		}
		else
		{
			// Without an urgent need that can be met now, the person waits a while before deciding again.
			var idle = m_settings.Idle;
			var idlePlace = FindPlace(person, idle.Place, idle.QuarterKinds, distances, isNearest: false) ?? person.Location;
			person.SetPlan(null, PlanTasks(person, idlePlace, distances, isAtWork: false, m_world.Random.NextInRange(idle.Minutes), returnTo: null, returnMinutes: 0), goalChoice);
		}
	}

	/// <summary>
	/// Gets how urgent a need is for a person right now, from 0 to <see cref="Need.MaximumLevel"/>. A level need grows
	/// more urgent as its level rises, within its hours. A quota need is urgent once the spare time left to meet it this
	/// day or week (the time left in its hours, less the time it still needs) drops below its lead, and fully urgent when
	/// there's no spare time left. A weekly need whose days have passed with its quota unmet is overdue, and has its overdue
	/// urgency on any day for the rest of the week. A need the person doesn't have, or can't meet now, isn't urgent. The
	/// urgency is then multiplied by the urgency factors that apply to the person, so it can be more than
	/// <see cref="Need.MaximumLevel"/>.
	/// </summary>
	public double GetUrgency(Person person, Need need, TimePoint time) => GetBaseUrgency(person, need, time) * GetUrgencyFactor(person, need);

	/// <summary>
	/// Gets the product of the urgency factors for a need that apply to a person.
	/// </summary>
	private double GetUrgencyFactor(Person person, Need need) =>
		m_settings.UrgencyFactors
			.Where(x => x.Needs.Contains(need.Id, StringComparer.Ordinal) && CharacterCondition.IsMet(x.When, person.Traits.GetValueId))
			.Aggregate(1.0, (total, x) => total * x.Factor);

	/// <summary>
	/// Whether a person has a need: they meet its condition, and for a need met at a workplace, they have a business.
	/// </summary>
	public static bool HasNeed(Person person, Need need) =>
		CharacterCondition.IsMet(need.When, person.Traits.GetValueId) && (need.Place != NeedPlace.Workplace || person.Business is not null);

	/// <summary>
	/// Whether a person can meet a need in a location: it's their home or workplace, for a need met there, or a quarter of
	/// one of the need's kinds.
	/// </summary>
	public static bool IsPlaceFor(Person person, Need need, Location location) => need.Place switch
	{
		NeedPlace.Home => location == person.Home,
		NeedPlace.Workplace => person.Business is not null && location == person.Workplace,
		_ => location.Template is QuarterTemplate && (need.QuarterKinds ?? []).Any(x => x.QuarterKind == location.QuarterKind),
	};

	/// <summary>
	/// Gets how many minutes a person would spend on a need with a player action option, starting now: until a level need's
	/// level is 0 or a quota need's quota is met (0 if it already is), until the next time it's the option's hour, or the
	/// option's minutes. It's 0 for a quota need that can't be met today, or outside its hours if its action is only open then.
	/// </summary>
	public int GetActionMinutes(Person person, Need need, PlayerActionOption option, TimePoint time)
	{
		person.UpdateNeeds(time);
		if (option.UntilHour is { } hour)
		{
			var parts = m_calendar.GetParts(time);
			var minutes = (((hour * 60) - ((parts.Hour * 60) + parts.Minute)) % MinutesPerDay + MinutesPerDay) % MinutesPerDay;
			return minutes == 0 ? MinutesPerDay : minutes;
		}

		return need.Kind == NeedKind.Quota && !IsPlayerActionOpen(need, time) ? 0 :
			!option.UntilMet ? option.Minutes ?? 0 :
			need.Kind == NeedKind.Level ? (int) Math.Ceiling(Math.Round(person.GetLevel(need) / need.SatisfyPerHour!.Value * 60, 6)) :
			(int) Math.Clamp(GetRemainingMinutes(person, need, time), 0, int.MaxValue);
	}

	/// <summary>
	/// Gets how many minutes a person would spend on one part of a trip, starting now, or null if they can't do that part:
	/// one trip's time at the place, if the quota isn't met and can be met today (and now, if its action is only open within its hours) and they aren't partway through a trip
	/// for it, or the time at home to finish a trip they've been to the place for (which can be 0).
	/// </summary>
	public int? GetTripMinutes(Person person, Need need, bool isHome, TimePoint time)
	{
		var members = person.Family.Members.Count;
		return isHome ? (person.PendingTrip == need ? need.GetHomeMinutes(members) : null) :
			person.PendingTrip == need || !IsPlayerActionOpen(need, time) || GetRemainingMinutes(person, need, time) <= 0 ? null : need.GetVisitMinutes(members);
	}

	/// <summary>
	/// Whether a quota need's player action can be done at the time: on a day the need can be met, and within its hours
	/// too if the action is only open then.
	/// </summary>
	public bool IsPlayerActionOpen(Need need, TimePoint time) =>
		need.PlayerAction?.OnlyInHours == true ? IsQuotaOpen(need, time) : IsQuotaDayOpen(need, time);

	private double GetBaseUrgency(Person person, Need need, TimePoint time)
	{
		if (!HasNeed(person, need))
			return 0;
		if (need.Kind == NeedKind.Level)
			return need.IsWithinHours(m_calendar.GetParts(time).Hour) ? need.GetLevelUrgency(person.GetLevel(need)) : 0;

		var remaining = GetRemainingMinutes(person, need, time);
		var available = GetAvailableMinutes(need, time);
		var spare = available - remaining;
		return remaining <= 0 || !IsQuotaOpen(need, time) ? 0 :
			need.OverdueUrgency is { } overdue && available == 0 ? overdue :
			Need.MaximumLevel * Math.Clamp(1 - ((double) spare / need.Lead!.Value), 0, 1);
	}

	/// <summary>
	/// Finds where a need would be met for a person, from where they are.
	/// </summary>
	internal Location? FindPlace(Person person, Need need) => FindPlace(person, need.Place, need.QuarterKinds, GetDistances(person.Location), need.Nearest);

	/// <summary>
	/// Gets how many minutes the quickest way from where a person is to a location takes, at the usual walking speed.
	/// </summary>
	internal int GetTravelMinutesTo(Person person, Location location) =>
		GetDistances(person.Location).TryGetValue(location, out var route) ? route.Minutes : int.MaxValue;

	/// <summary>
	/// Gets how long it takes a person to walk a path between two locations next to each other. Some people walk more
	/// slowly than others.
	/// </summary>
	public int GetTravelMinutes(Person person, Location from, Location to)
	{
		var factor = m_settings.TravelTimeFactors.Where(x => CharacterCondition.IsMet(x.When, person.Traits.GetValueId)).Aggregate(1.0, (total, x) => total * x.Factor);
		return (int) Math.Ceiling(m_world.GetTravelMinutes(from, to) * factor);
	}

	private Person? GetNextTaskEnd()
	{
		Person? next = null;
		foreach (var person in m_world.People)
		{
			if (person.TaskEnd is { } end && (next is null || end < next.TaskEnd!))
				next = person;
		}

		return next;
	}

	private int GetMinutes(Person person, PersonTask task) => task switch
	{
		TravelTask travel => GetTravelMinutes(person, person.Location, travel.Destination),
		BusyTask { Minutes: { } minutes } => Math.Max(minutes, 1),
		BusyTask when person.Goal is { Kind: NeedKind.Level, SatisfyPerHour: { } satisfy } goal => Math.Max((int) Math.Ceiling(person.GetLevel(goal) / satisfy * 60), 1),
		_ => 1,
	};

	/// <summary>
	/// Gets how long to spend on a quota need this time: a shift or a trip, unless less is left to do or less time is left
	/// in today's hours once the person gets there.
	/// </summary>
	private int GetShiftMinutes(Person person, Need need, TimePoint time, int travelMinutes)
	{
		var remaining = GetRemainingMinutes(person, need, time);
		var available = GetMinutesUntilEnd(need, time) - travelMinutes;
		return (int) Math.Min(Math.Min(need.GetShiftMinutes(person.Family.Members.Count), remaining), available);
	}

	/// <summary>
	/// Gets how many minutes of a quota need are left this day or week. For a need the family shares, the quota depends on
	/// the family's size, and the time the whole family has spent on it counts, along with the time other family members
	/// already plan to spend on it, so two people don't set out to do the same thing.
	/// </summary>
	internal static long GetRemainingMinutes(Person person, Need need, TimePoint time)
	{
		var people = need.SharedByFamily ? person.Family.Members : [person];
		foreach (var member in people)
			member.UpdateNeeds(time);
		return need.GetQuotaMinutes(people.Count) - people.Sum(x => x.GetProgress(need)) - people.Where(x => x != person).Sum(x => x.GetPlannedMinutes(need, time));
	}

	private long GetMinutesUntilEnd(Need need, TimePoint time) =>
		m_calendar.GetTotalMinutes(m_calendar.GetStartOfDay(time) + m_calendar.CreateTimeOffset(0, need.End!.Value, 0) - time);

	/// <summary>
	/// Whether a quota need can be met now: it's within its hours, and it can be met today, or it's overdue (its days have
	/// passed this week) and today isn't a special day it skips.
	/// </summary>
	private bool IsQuotaOpen(Need need, TimePoint time)
	{
		var hour = m_calendar.GetParts(time).Hour;
		return hour >= need.Start!.Value && hour < need.End!.Value && IsQuotaDayOpen(need, time);
	}

	/// <summary>
	/// Whether a quota need can be met on the time's day, at some time within its hours: it can be met today, or it's
	/// overdue and today isn't a special day it skips.
	/// </summary>
	private bool IsQuotaDayOpen(Need need, TimePoint time) =>
		IsQuotaDay(need, time) || (need.OverdueUrgency is not null && (need.OnSpecialDays || m_calendar.GetSpecialDays(time).Count == 0) && GetAvailableMinutes(need, time) == 0);

	/// <summary>
	/// Whether a quota need can be met on the time's day: it's one of its days, and it isn't a special day it skips.
	/// </summary>
	private bool IsQuotaDay(Need need, TimePoint time) =>
		(need.Days is null || need.Days.Contains(m_calendar.GetDayOfWeek(time).Id, StringComparer.Ordinal)) &&
		(need.OnSpecialDays || m_calendar.GetSpecialDays(time).Count == 0);

	/// <summary>
	/// Gets how many minutes are left in a quota need's hours from now to the end of its day or week.
	/// </summary>
	private long GetAvailableMinutes(Need need, TimePoint time)
	{
		var day = m_calendar.GetStartOfDay(time);
		var periodEnd = need.Period == NeedPeriod.Day ? m_calendar.AddMinutes(day, 24 * 60) : m_calendar.GetStartOfWeek(time) + m_calendar.CreateTimeOffset(m_calendar.DaysInWeek, 0, 0);
		var minutes = 0L;
		for (; day < periodEnd; day = m_calendar.AddMinutes(day, 24 * 60))
		{
			var start = day + m_calendar.CreateTimeOffset(0, need.Start!.Value, 0);
			var end = day + m_calendar.CreateTimeOffset(0, need.End!.Value, 0);
			var from = start > time ? start : time;
			if (end > from && IsQuotaDay(need, day))
				minutes += m_calendar.GetTotalMinutes(end - from);
		}

		return minutes;
	}

	/// <summary>
	/// Finds where a need is met for a person: their home, their workplace, or a quarter of one of the kinds. For the
	/// nearest place, it's the nearest quarter of any of the kinds. Otherwise the kind is chosen by frequency, and nearer
	/// quarters of that kind are more likely. Returns null if there's no such place.
	/// </summary>
	private Location? FindPlace(Person person, NeedPlace place, IReadOnlyList<QuarterChoice>? quarterKinds, IReadOnlyDictionary<Location, Route> distances, bool isNearest)
	{
		if (place == NeedPlace.Home)
			return person.Home;
		if (place == NeedPlace.Workplace)
			return person.Business is null ? null : person.Workplace;

		var quarters = m_world.Locations.Where(x => x.Template is QuarterTemplate && distances.ContainsKey(x)).ToLookup(x => x.QuarterKind ?? "", StringComparer.Ordinal);
		var choices = (quarterKinds ?? []).Where(x => quarters[x.QuarterKind].Any()).ToList();
		if (choices.Count == 0)
			return null;

		var kind = isNearest ? null : m_world.GameData.FrequencyChances.Choose(m_world.Random, choices, quarterKinds).QuarterKind;
		return kind is null
			? choices.SelectMany(x => quarters[x.QuarterKind]).MinBy(x => distances[x].Minutes)
			: m_world.Random.NextWeighted(quarters[kind].ToList(), x => 1.0 / (1 + distances[x].Minutes));
	}

	/// <summary>
	/// Plans the tasks to reach a place and spend time there: a path to walk, then starting work if needed, the time
	/// spent, stopping work, and the path back to where they return to, if anywhere, with time spent there.
	/// </summary>
	private IReadOnlyList<PersonTask> PlanTasks(Person person, Location place, IReadOnlyDictionary<Location, Route> distances, bool isAtWork, int? minutes, Location? returnTo, int returnMinutes)
	{
		var tasks = GetPath(person.Location, place, distances).Select(PersonTask (x) => new TravelTask(x)).ToList();
		if (isAtWork)
			tasks.Add(new AtWorkTask(true));
		tasks.Add(new BusyTask(minutes));
		if (isAtWork)
			tasks.Add(new AtWorkTask(false));
		if (returnTo is not null && returnTo != place)
		{
			tasks.AddRange(GetPath(place, returnTo, GetDistances(place)).Select(PersonTask (x) => new TravelTask(x)));
			if (returnMinutes > 0)
				tasks.Add(new BusyTask(returnMinutes));
		}

		return tasks;
	}

	/// <summary>
	/// Gets the locations along the quickest way from one location to another, not counting the first, given the quickest
	/// ways from the first.
	/// </summary>
	private static IReadOnlyList<Location> GetPath(Location from, Location to, IReadOnlyDictionary<Location, Route> distances)
	{
		var path = new List<Location>();
		for (var location = to; location != from && distances.TryGetValue(location, out var route); location = route.Previous!)
			path.Insert(0, location);
		return path;
	}

	/// <summary>
	/// Finds the quickest way to every location from the given one, using the travel time of each path.
	/// </summary>
	private Dictionary<Location, Route> GetDistances(Location start)
	{
		var routes = new Dictionary<Location, Route> { [start] = new Route(0, null) };
		var queue = new PriorityQueue<Location, int>();
		queue.Enqueue(start, 0);
		while (queue.TryDequeue(out var location, out var minutes))
		{
			if (minutes == routes[location].Minutes)
			{
				foreach (var next in location.Connections)
				{
					var total = minutes + m_world.GetTravelMinutes(location, next);
					if (!routes.TryGetValue(next, out var existing) || total < existing.Minutes)
					{
						routes[next] = new Route(total, location);
						queue.Enqueue(next, total);
					}
				}
			}
		}

		return routes;
	}

	/// <summary>
	/// The quickest way to a location: how many minutes it takes, and the location before it on the way.
	/// </summary>
	private sealed record Route(int Minutes, Location? Previous);

	private const int MinutesPerDay = 24 * 60;

	private readonly GameWorld m_world;
	private readonly GameCalendar m_calendar;
	private readonly ScheduleSettings m_settings;
}
