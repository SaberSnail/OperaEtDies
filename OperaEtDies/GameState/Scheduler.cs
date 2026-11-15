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
		m_socializeNeed = m_settings.Needs.FirstOrDefault(x => x.Id == m_settings.SocializeNeed);
		m_sleepNeed = m_settings.Needs.FirstOrDefault(x => x.Id == m_settings.SleepNeed && x.Kind == NeedKind.Level);
	}

	/// <summary>
	/// Runs everyone's tasks from one time to another, in the order they end, then brings everyone's needs up to the end.
	/// If the stop check is met after a task ends, it stops at that time instead, once every task ending then has ended.
	/// Returns the time it ran until.
	/// </summary>
	public TimePoint RunUntil(TimePoint start, TimePoint end, Func<bool>? shouldStop = null)
	{
		// Anyone the game decides for who has no task decides what to do now.
		foreach (var person in m_world.People.Where(x => m_world.IsAutomatic(x) && x.TaskEnd is null))
			Continue(person, start);

		while (GetNextTaskEnd() is { } next && next.TaskEnd! <= end)
		{
			var time = next.TaskEnd! > start ? next.TaskEnd! : start;
			next.UpdateNeeds(time);
			var task = next.FinishTask();
			if (task is TravelTask travel)
				m_world.MovePerson(next, travel.Destination, time);
			Continue(next, time);

			// The player has nothing to do until they choose, but still sees who's around, such as when they wake.
			if (next.TaskEnd is null)
				GameWorld.SeeEachOther(next, time);

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
		while (person.TaskEnd is null && (person.Tasks.Count != 0 || m_world.IsAutomatic(person)))
		{
			if (person.Tasks.Count == 0)
				ChooseGoal(person, time);

			var task = person.Tasks[0];
			if (task.ResumeMinutes is not null && person.Goal is { Kind: NeedKind.Quota } goal && !CanResume(person, goal, time))
			{
				// An interruption can leave too little time to finish what the person was doing, so they choose again.
				person.IsAtWork = false;
				person.SetPlan(null, []);
			}
			else if (task is AtWorkTask atWork)
			{
				person.IsAtWork = atWork.IsAtWork;
				person.FinishTask();
			}
			else
			{
				var minutes = GetMinutes(person, task);

				// Something can wake someone before they're rested, such as hunger.
				if (task is BusyTask && person.Goal is { } sleepGoal && sleepGoal == m_sleepNeed && m_world.IsAutomatic(person))
					minutes = GetMinutesUntilWoken(person, time, minutes);
				person.StartTask(m_calendar.AddMinutes(time, minutes));
				m_world.OnTaskStarted(person, task, time);
				if (task is TravelTask travel && person.CurrentTask == task)
					CallOutToFriend(person, travel.Destination, minutes, time);
			}
		}
	}

	/// <summary>
	/// Whether someone can carry on with a quota need after being interrupted: it's within its hours, and isn't done yet.
	/// </summary>
	private bool CanResume(Person person, Need need, TimePoint time) => IsQuotaOpen(person, need, time) && GetRemainingMinutes(person, need, time) > 0;

	/// <summary>
	/// Whether someone refuses to be interrupted, because their goal is too urgent right now.
	/// </summary>
	public bool IsTooBusyToTalk(Person person, TimePoint time) => person.Goal is { } goal && GetUrgency(person, goal, time) >= m_settings.Talking.RefuseUrgency;

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
		// Someone without friends has no one to seek out.
		var hasFriends = GetFriends(person).Any();
		// Someone who has already napped since they last slept until rested doesn't nap again.
		var isNap = IsNap(person, time);
		var candidates = urgencies.Where(x => m_settings.IsGoalCandidate(x.Need, x.Urgency) && (x.Need != m_socializeNeed || hasFriends) && (x.Need != m_sleepNeed || !isNap || !person.HasNapped)).ToList();

		// Seeking out a friend means looking for them somewhere, for a while, rather than going to the need's usual place.
		var distances = GetDistances(person.Location);
		var choice = candidates.Count == 0 ? null : m_world.Random.NextWeighted(candidates, x => m_settings.GetGoalWeight(x.Need, x.Urgency)).Need;
		var seeking = choice is not null && choice == m_socializeNeed ? ChooseWhereToSeek(person, distances, time) : null;
		var place = choice is null ? null : choice == m_socializeNeed ? seeking?.Place : FindPlace(person, choice.Place, choice.QuarterKinds, distances, choice.Nearest);
		var minutes = seeking is not null ? m_settings.Socializing.SeekMinutes :
			choice is not null && choice == m_sleepNeed && isNap ? m_world.Random.NextInRange(m_settings.Sleep.NapMinutes) :
			choice is { Kind: NeedKind.Quota } && place is not null ? GetShiftMinutes(person, choice, time, distances[place].Minutes) : (int?) null;
		var result = choice is null ? GoalChoiceResult.NothingUrgent : place is null ? GoalChoiceResult.NoPlace : minutes is 0 or < 0 ? GoalChoiceResult.NoTime : GoalChoiceResult.Chosen;
		var goalChoice = new GoalChoice(time, urgencies, choice, result, seeking);
		if (result == GoalChoiceResult.Chosen)
		{
			// A trip's time is spent at the place first, and whatever's left at home afterward.
			var placeMinutes = choice!.IsTrips ? Math.Min(choice.GetVisitMinutes(person.Family.Members.Count), minutes!.Value) : minutes;
			var homeMinutes = minutes - placeMinutes ?? 0;
			if (choice == m_sleepNeed)
				person.HasNapped = isNap;
			person.SetPlan(choice, PlanTasks(person, place!, distances, choice.AtWork, placeMinutes, choice.ReturnHome ? person.Home : null, homeMinutes), goalChoice);
		}
		else
		{
			// Without an urgent need that can be met now, the person waits a while before deciding again.
			var idle = m_settings.Idle;
			var idlePlace = FindPlace(person, idle.Place.Kind, idle.Place.QuarterKinds, distances, isNearest: false) ?? person.Location;
			person.SetPlan(null, PlanTasks(person, idlePlace, distances, isAtWork: false, m_world.Random.NextInRange(idle.Minutes), returnTo: null, returnMinutes: 0), goalChoice);
		}
	}

	/// <summary>
	/// Reacts to someone starting a task where they are: if they and a friend there are willing, one of them stops to
	/// socialize with the other. Someone a friend is waiting for, having been called, chats with
	/// them. Someone looking for a friend who isn't there asks someone else there about their friends instead, when they
	/// start looking around.
	/// </summary>
	internal void OnTaskStarted(Person person, PersonTask task, TimePoint time)
	{
		if (m_socializeNeed is not null && task is not (TalkTask or WaitTask) && !TryChatWithWaitingFriend(person, time) && !TryStartChat(person, time) &&
			person.Goal == m_socializeNeed && task is BusyTask { ResumeMinutes: null })
		{
			AskAboutFriends(person, time);
		}
	}

	/// <summary>
	/// Has someone looking for a friend call out to them as they set off into a place the friend is just leaving, so the
	/// friend waits there for them, for as long as the walk takes and a minute more, so they're still there when they arrive.
	/// </summary>
	private void CallOutToFriend(Person person, Location destination, int minutes, TimePoint time)
	{
		if (person.Goal == m_socializeNeed && person.GoalChoice?.Seeking?.Friend is { } friend && friend.Location == destination && friend.CurrentTask is TravelTask &&
			m_world.IsAutomatic(friend))
		{
			var wait = new WaitTask(person, minutes + 1);
			friend.Interrupt(wait, time, m_calendar.AddMinutes(time, minutes + 1));
			m_world.OnTaskStarted(friend, wait, time);
		}
	}

	/// <summary>
	/// Has someone chat with a friend where they are who is waiting for them, having been called. Returns whether they did.
	/// </summary>
	private bool TryChatWithWaitingFriend(Person person, TimePoint time)
	{
		var friend = person.Location.People.FirstOrDefault(x => x.CurrentTask is WaitTask wait && wait.For == person);
		if (friend is not null)
		{
			// They're done waiting.
			friend.UpdateNeeds(time);
			friend.FinishTask();
			ChatWith(person, friend, time);
		}
		return friend is not null;
	}

	/// <summary>
	/// Gets the people someone counts as friends.
	/// </summary>
	internal IEnumerable<Person> GetFriends(Person person)
	{
		var settings = m_world.GameData.Relationships;
		return person.Relationships.Where(x => settings.IsFriend(x.Value.Score)).Select(x => x.Key);
	}

	/// <summary>
	/// Starts the chat that someone (or a friend where they are) is most willing to have, if anyone is willing enough.
	/// Returns whether a chat started.
	/// </summary>
	private bool TryStartChat(Person person, TimePoint time)
	{
		var friends = GetFriends(person).Where(x => x.Location == person.Location).ToList();
		var best = friends
			.SelectMany(friend => new[] { (Initiator: person, Other: friend), (Initiator: friend, Other: person) })
			.Select(x => (x.Initiator, x.Other, Willingness: GetChatWillingness(x.Initiator, x.Other, time)))
			.Where(x => x.Willingness >= m_settings.Socializing.ChatUrgency)
			.OrderByDescending(x => x.Willingness)
			.FirstOrDefault();
		if (best.Initiator is not null)
			StartChat(best.Initiator, best.Other, time);
		return best.Initiator is not null;
	}

	/// <summary>
	/// Gets how willing someone is to stop and chat with a friend: how urgent socializing is for them, times how close
	/// they are, less how urgent whatever else they're doing is. It's negative infinity if they can't: either of them is
	/// already talking, the one starting it is the player (unless headless) or is asleep, the friend is too busy to talk, the
	/// friend is asleep and socializing isn't urgent enough to wake them, or the friend is the player (unless headless) and
	/// can't be asked right now.
	/// </summary>
	private double GetChatWillingness(Person initiator, Person other, TimePoint time)
	{
		var urgency = GetUrgency(initiator, m_socializeNeed!, time);
		var canChat = m_world.IsAutomatic(initiator) && (m_world.IsAutomatic(other) || CanAskPlayer(initiator, other, time)) && initiator.CurrentTask is not (null or TalkTask) && !initiator.IsAsleep &&
			other.CurrentTask is not TalkTask && !IsTooBusyToTalk(other, time) && (!other.IsAsleep || urgency >= m_settings.Socializing.WakeUrgency);
		var goalUrgency = initiator.Goal is { } goal && goal != m_socializeNeed ? GetUrgency(initiator, goal, time) : 0;
		return canChat && urgency > 0
			? (urgency * initiator.GetRelationship(other).Score / RelationshipSettings.MaximumScore) - (goalUrgency * m_settings.Socializing.ChatGoalWeight)
			: double.NegativeInfinity;
	}

	/// <summary>
	/// Whether someone can ask the player to chat: no one else is waiting for an answer, and the player hasn't turned them
	/// down lately.
	/// </summary>
	private bool CanAskPlayer(Person initiator, Person player, TimePoint time) =>
		player.TalkRequest is null && (initiator.GetRelationship(player).LastDeclined is not { } declined ||
			m_calendar.GetTotalMinutes(time - declined) >= m_settings.Socializing.AskPlayerAgainHours * 60);

	/// <summary>
	/// Has someone socialize with a friend, first asking what they're doing unless they already know, from having been told
	/// since the friend chose their goal. The player is asked first (unless headless), and chats once they agree.
	/// </summary>
	private void StartChat(Person initiator, Person other, TimePoint time)
	{
		if (!m_world.IsAutomatic(other))
			other.TalkRequest = initiator;
		else
			ChatWith(initiator, other, time);
	}

	/// <summary>
	/// Has someone start socializing with a friend now, first asking what they're doing unless they already know.
	/// </summary>
	internal void ChatWith(Person initiator, Person other, TimePoint time)
	{
		var talking = m_settings.Talking;
		var isKnown = initiator.Sightings.GetValueOrDefault(other) is { Source: null, Plan: not null };
		if (!isKnown)
			initiator.See(other, GameWorld.Observe(other, time) with { Plan = GameWorld.GetPlan(other) });
		m_world.StartTalking(initiator, other, m_settings.Socializing.ChatMinutes + (isKnown ? 0 : talking.AskDoingMinutes), time, isSocializing: true);
	}

	/// <summary>
	/// Has someone looking for a friend ask someone else where they are (other than a friend, the player unless headless,
	/// or anyone asleep, talking, or too busy) whether they've seen any of their friends, learning what they know.
	/// </summary>
	private void AskAboutFriends(Person person, TimePoint time)
	{
		var friends = GetFriends(person).ToList();
		var asked = person.Location.People.FirstOrDefault(x => x != person && m_world.IsAutomatic(x) && !friends.Contains(x) && !x.IsAsleep &&
			x.CurrentTask is not TalkTask && !IsTooBusyToTalk(x, time));
		if (asked is not null)
		{
			foreach (var friend in friends)
			{
				if (asked.Sightings.TryGetValue(friend, out var sighting))
					person.LearnSighting(friend, sighting, asked);
			}
			m_world.StartTalking(person, asked, m_settings.Talking.AskAboutMinutes, time);
		}
	}

	/// <summary>
	/// Chooses a friend to look for and where, from clues to where each friend might be: their workplace during their work
	/// hours, or their home otherwise; where they were last seen (or said they were going), for a fresh sighting; and where
	/// they might be gathering with others for a need only met on some days, such as market day. Places close by, and
	/// friends not talked with lately, are more likely. The place they're in now is never chosen. Returns null if there's
	/// nowhere to look.
	/// </summary>
	private SeekChoice? ChooseWhereToSeek(Person person, IReadOnlyDictionary<Location, Route> distances, TimePoint time)
	{
		var socializing = m_settings.Socializing;
		var clues = socializing.Clues;
		var candidates = new List<(SeekChoice Choice, double Weight)>();
		foreach (var friend in GetFriends(person))
		{
			var lastTalked = person.GetRelationship(friend).LastTalked;
			var hours = lastTalked is { } last ? m_calendar.GetTotalMinutes(time - last) / 60.0 : double.PositiveInfinity;
			var recency = double.IsPositiveInfinity(hours) ? 1 : hours / (hours + socializing.RecentHours);

			void Add(Location? place, SeekClue clue, double weight)
			{
				if (place is not null && place != person.Location && weight > 0 && distances.TryGetValue(place, out var route))
					candidates.Add((new SeekChoice(friend, place, clue), weight * recency / (1 + (route.Minutes / socializing.NearMinutes))));
			}

			var isWorking = m_settings.Needs.Any(x => x.Place == NeedPlace.Workplace && HasNeed(friend, x) && IsQuotaOpen(friend, x, time));
			if (isWorking)
				Add(friend.Workplace, SeekClue.Workplace, clues.Workplace);
			else
				Add(friend.Home, SeekClue.Home, clues.Home);

			if (person.Sightings.TryGetValue(friend, out var sighting))
			{
				var age = m_calendar.GetTotalMinutes(time - sighting.Time) / 60.0;
				Add(sighting.Plan?.Destination ?? sighting.Destination ?? sighting.Location, SeekClue.Sighting, clues.Sighting * Math.Max(1 - (age / m_settings.SightingHours), 0));
			}

			foreach (var need in m_settings.Needs.Where(x => x.Days is not null && x.Place == NeedPlace.Quarter && HasNeed(friend, x) && IsQuotaOpen(friend, x, time) && GetRemainingMinutes(friend, x, time) > 0))
				Add(FindPlace(friend, need.Place, need.QuarterKinds, distances, need.Nearest), SeekClue.Gathering, clues.Gathering);
		}

		return candidates.Count == 0 ? null : m_world.Random.NextWeighted(candidates, x => x.Weight).Choice;
	}

	/// <summary>
	/// Whether sleep would be a nap for someone right now: they're less tired than a full sleep needs, and it isn't bedtime.
	/// </summary>
	private bool IsNap(Person person, TimePoint time) =>
		m_sleepNeed is { } sleep && person.GetLevel(sleep) < m_settings.Sleep.NapBelow && GetBedtimeUrgency(person, time) == 0;

	/// <summary>
	/// Gets how urgent it is for someone to go to bed so they're rested for their next work hours, like a chore's deadline:
	/// urgent once the time before then, less the sleep they still need, drops below the bedtime lead, and fully urgent
	/// once there's no time to spare. It's 0 for someone with no work, or who isn't tired (below the level sleep starts to
	/// become urgent at), since they have no sleep to fit in.
	/// </summary>
	private double GetBedtimeUrgency(Person person, TimePoint time)
	{
		var workStart = GetNextWorkStart(person, time);
		var urgency = 0.0;
		if (workStart is { } start && m_sleepNeed is { SatisfyPerHour: { } satisfy } sleep && person.GetLevel(sleep) > (sleep.UrgentFrom ?? 0))
		{
			var spare = m_calendar.GetTotalMinutes(start - time) - (person.GetLevel(sleep) / satisfy * 60);
			urgency = Need.MaximumLevel * Math.Clamp(1 - (spare / m_settings.Sleep.BedtimeLead), 0, 1);
		}

		return urgency;
	}

	/// <summary>
	/// Gets when someone's work hours next start, on a day they can work, within the next week, or null if they don't work.
	/// </summary>
	private TimePoint? GetNextWorkStart(Person person, TimePoint time)
	{
		var day = m_calendar.GetStartOfDay(time);
		return m_settings.Needs
			.Where(x => x.Place == NeedPlace.Workplace && x.Kind == NeedKind.Quota && HasNeed(person, x))
			.SelectMany(need => Enumerable.Range(0, m_calendar.DaysInWeek + 1)
				.Select(days => m_calendar.AddMinutes(day, days * 24 * 60))
				.Where(x => IsQuotaDay(need, x))
				.Select(x => x + m_calendar.CreateTimeOffset(0, GetHours(person, need).Start, 0)))
			.Where(x => x > time)
			.Cast<TimePoint?>()
			.Min();
	}

	/// <summary>
	/// Gets how many minutes someone sleeps before something wakes them, up to the minutes they'd sleep otherwise: a need
	/// with a wake urgency (that sleep doesn't pause) wakes them once it's at least that urgent, and more urgent than sleep
	/// is by then. It's worked out from how their needs will change as they sleep, a few minutes at a time.
	/// </summary>
	private int GetMinutesUntilWoken(Person person, TimePoint time, int minutes)
	{
		var sleep = m_sleepNeed!;
		var wakers = m_settings.Needs
			.Where(x => x.WakeUrgency is not null && x.Kind == NeedKind.Level && x != sleep && HasNeed(person, x) && x.PausedBy?.Contains(sleep.Id, StringComparer.Ordinal) != true)
			.Select(x => (Need: x, Level: person.GetLevel(x), Factor: GetUrgencyFactor(person, x)))
			.ToList();
		var (sleepLevel, sleepFactor) = (person.GetLevel(sleep), GetUrgencyFactor(person, sleep));
		var woken = minutes;
		for (var minute = WakeCheckMinutes; minute < woken && wakers.Count != 0; minute += WakeCheckMinutes)
		{
			var hour = m_calendar.GetParts(m_calendar.AddMinutes(time, minute)).Hour;
			var sleepUrgency = sleep.GetLevelUrgency(Math.Max(sleepLevel - (sleep.SatisfyPerHour!.Value * minute / 60), 0)) * sleepFactor;
			var isWoken = wakers.Any(x =>
			{
				var urgency = x.Need.IsWithinHours(hour) ? x.Need.GetLevelUrgency(Math.Min(x.Level + (x.Need.PerHour!.Value * minute / 60), Need.MaximumLevel)) * x.Factor : 0;
				return urgency >= x.Need.WakeUrgency && urgency > sleepUrgency;
			});
			if (isWoken)
				woken = minute;
		}

		return woken;
	}

	/// <summary>
	/// Whether someone is within their work hours on a day they work.
	/// </summary>
	internal bool IsInWorkHours(Person person, TimePoint time) =>
		m_settings.Needs.Any(x => x.Place == NeedPlace.Workplace && x.Kind == NeedKind.Quota && HasNeed(person, x) && IsQuotaOpen(person, x, time));

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

		return need.Kind == NeedKind.Quota && !IsPlayerActionOpen(person, need, time) ? 0 :
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
			person.PendingTrip == need || !IsPlayerActionOpen(person, need, time) || GetRemainingMinutes(person, need, time) <= 0 ? null : need.GetVisitMinutes(members);
	}

	/// <summary>
	/// Whether a quota need's player action can be done at the time: on a day the need can be met, and within its hours
	/// too if the action is only open then.
	/// </summary>
	public bool IsPlayerActionOpen(Person person, Need need, TimePoint time) =>
		need.PlayerAction?.OnlyInHours == true ? IsQuotaOpen(person, need, time) : IsQuotaDayOpen(person, need, time);

	private double GetBaseUrgency(Person person, Need need, TimePoint time)
	{
		if (!HasNeed(person, need))
			return 0;
		if (need.Kind == NeedKind.Level)
			return Math.Max(need.IsWithinHours(m_calendar.GetParts(time).Hour) ? need.GetLevelUrgency(person.GetLevel(need)) : 0, need == m_sleepNeed ? GetBedtimeUrgency(person, time) : 0);

		var remaining = GetRemainingMinutes(person, need, time);
		var available = GetAvailableMinutes(person, need, time);
		var spare = available - remaining;
		return remaining <= 0 || !IsQuotaOpen(person, need, time) ? 0 :
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

	private int GetMinutes(Person person, PersonTask task) => task.ResumeMinutes ?? task switch
	{
		TalkTask talk => Math.Max(talk.Minutes, 1),
		WaitTask wait => Math.Max(wait.Minutes, 1),
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
		var available = GetMinutesUntilEnd(person, need, time) - travelMinutes;
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

	private long GetMinutesUntilEnd(Person person, Need need, TimePoint time)
	{
		var hour = m_calendar.GetParts(time).Hour;
		var end = GetStretches(person, need).Where(x => hour >= x.Start && hour < x.End).Select(x => x.End).DefaultIfEmpty(hour).First();
		return m_calendar.GetTotalMinutes(m_calendar.GetStartOfDay(time) + m_calendar.CreateTimeOffset(0, end, 0) - time);
	}

	/// <summary>
	/// Gets the hours a person can spend on a quota need: their profession's work hours for a need met at their workplace,
	/// if it has its own, or the need's hours otherwise.
	/// </summary>
	private static (int Start, int End) GetHours(Person person, Need need) =>
		need.Place == NeedPlace.Workplace && person.Business?.Profession.WorkHours is { } hours ? (hours.Start, hours.End) : (need.Start!.Value, need.End!.Value);

	/// <summary>
	/// Gets the stretches of each day a person can spend on a quota need. Hours that end before they start run past midnight,
	/// so they're two stretches: from midnight to the end, and from the start to midnight, each part of its own day.
	/// </summary>
	internal static IReadOnlyList<(int Start, int End)> GetStretches(Person person, Need need)
	{
		var (start, end) = GetHours(person, need);
		return start < end ? [(start, end)] : [(0, end), (start, 24)];
	}

	/// <summary>
	/// Whether a quota need can be met now: it's within its hours, and it can be met today, or it's overdue (its days have
	/// passed this week) and today isn't a special day it skips.
	/// </summary>
	private bool IsQuotaOpen(Person person, Need need, TimePoint time)
	{
		var hour = m_calendar.GetParts(time).Hour;
		return GetStretches(person, need).Any(x => hour >= x.Start && hour < x.End) && IsQuotaDayOpen(person, need, time);
	}

	/// <summary>
	/// Whether a quota need can be met on the time's day, at some time within its hours: it can be met today, or it's
	/// overdue and today isn't a special day it skips.
	/// </summary>
	private bool IsQuotaDayOpen(Person person, Need need, TimePoint time) =>
		IsQuotaDay(need, time) || (need.OverdueUrgency is not null && (need.OnSpecialDays || m_calendar.GetSpecialDays(time).Count == 0) && GetAvailableMinutes(person, need, time) == 0);

	/// <summary>
	/// Whether a quota need can be met on the time's day: it's one of its days, and it isn't a special day it skips.
	/// </summary>
	private bool IsQuotaDay(Need need, TimePoint time) =>
		(need.Days is null || need.Days.Contains(m_calendar.GetDayOfWeek(time).Id, StringComparer.Ordinal)) &&
		(need.OnSpecialDays || m_calendar.GetSpecialDays(time).Count == 0);

	/// <summary>
	/// Gets how many minutes are left in a quota need's hours from now to the end of its day or week.
	/// </summary>
	private long GetAvailableMinutes(Person person, Need need, TimePoint time)
	{
		var stretches = GetStretches(person, need);
		var day = m_calendar.GetStartOfDay(time);
		var periodEnd = need.Period == NeedPeriod.Day ? m_calendar.AddMinutes(day, 24 * 60) : m_calendar.GetStartOfWeek(time) + m_calendar.CreateTimeOffset(m_calendar.DaysInWeek, 0, 0);
		var minutes = 0L;
		for (; day < periodEnd; day = m_calendar.AddMinutes(day, 24 * 60))
		{
			foreach (var (startHour, endHour) in stretches)
			{
				var start = day + m_calendar.CreateTimeOffset(0, startHour, 0);
				var end = day + m_calendar.CreateTimeOffset(0, endHour, 0);
				var from = start > time ? start : time;
				if (end > from && IsQuotaDay(need, day))
					minutes += m_calendar.GetTotalMinutes(end - from);
			}
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

	/// <summary>
	/// How often, in minutes, waking is checked for while working out how long someone sleeps.
	/// </summary>
	private const int WakeCheckMinutes = 5;

	private readonly GameWorld m_world;
	private readonly GameCalendar m_calendar;
	private readonly ScheduleSettings m_settings;
	private readonly Need? m_socializeNeed;
	private readonly Need? m_sleepNeed;
}
