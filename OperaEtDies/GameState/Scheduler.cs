using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.Time;
using OperaEtDies.Town;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.GameState;

/// <summary>
/// Moves people through their tasks as time passes, and carries out the steps of their sequences (see Docs/Steps.md).
/// Whenever someone runs out of tasks, the step they're on is checked, and if it isn't done, the game carries it out,
/// working out its tasks, for anyone it decides for (and for the player, while a step is done for them). Someone other
/// than the player who has no steps left chooses a new goal from how urgent their needs are. It keeps no state of its
/// own: all of it is in the people.
/// </summary>
internal sealed partial class Scheduler
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
	/// Anyone the game decides for who has no task decides what to do first, unless it carries on from where it ran until
	/// before, as time passing in slices does, so it runs the same however it's sliced. Returns the time it ran until.
	/// </summary>
	public TimePoint RunUntil(TimePoint start, TimePoint end, Func<bool>? shouldStop = null, bool isContinuing = false)
	{
		// Anyone the game decides for who has no task decides what to do now.
		if (!isContinuing)
		{
			foreach (var person in m_world.People.Where(x => m_world.IsAutomatic(x) && x.TaskEnd is null))
				Continue(person, start);
		}

		while (true)
		{
			// Setting off for an appointment comes before any task that ends at the same time.
			var next = GetNextTaskEnd();
			if (GetNextSetOff(start) is { } setOff && setOff.Time <= end && (next is null || setOff.Time <= next.TaskEnd!))
			{
				var setOffTime = setOff.Time > start ? setOff.Time : start;
				DecideSetOff(setOff.Sequence, setOff.Person, setOffTime);
				if (shouldStop?.Invoke() == true)
					end = setOffTime;
				continue;
			}

			if (next is null || next.TaskEnd! > end)
				break;

			var time = next.TaskEnd! > start ? next.TaskEnd! : start;
			next.UpdateNeeds(time);

			// What the people following someone have met depends on what that person was doing, which is about to change.
			foreach (var follower in GetFollowers(next, time))
				follower.UpdateNeeds(time);
			var wasAsleep = next.IsAsleep;
			var task = next.FinishTask();
			if (wasAsleep)
				OnCompanyChanged(next.Location);
			if (task is TravelTask travel)
			{
				// Anyone following comes along.
				var followers = GetFollowers(next, time);
				m_world.MovePerson(next, travel.Destination, time);
				foreach (var follower in followers)
					m_world.MovePerson(follower, travel.Destination, time);
			}
			else if (task is ChangeClothesTask change)
			{
				ClothingRules.Change(next, change.Outfit, change.IsTakingOff, time);
			}
			else if (task is FollowTask)
			{
				// A client who stayed as long as they would is done with their appointment.
				foreach (var appointment in m_world.Appointments.Where(x => x.IsStarted && x.Client == next).ToList())
					m_world.RemoveAppointment(appointment);
			}

			Continue(next, time);

			// Everyone reacts to who came, went, woke, or fell asleep.
			ReactToCompany(time);

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
	/// Starts a person's next task that takes time, carrying out their current sequence's steps whenever the tasks run
	/// out, up to a step that gives them tasks, or for the player, one they take themselves. Someone the game decides for
	/// who has no tasks or steps left chooses a new goal first, or goes idle if the goal they just chose took no time.
	/// </summary>
	public void Continue(Person person, TimePoint time)
	{
		person.UpdateNeeds(time);

		// A job that's done is over, so the next "goTo" step to a job site has nowhere to go until another is taken on.
		foreach (var job in person.Jobs.Where(x => x.IsDone).ToList())
			m_world.RemoveJob(job);
		var hasChosen = false;
		var stepsCarriedOut = 0;
		while (person.TaskEnd is null)
		{
			if (person.Tasks.Count != 0)
			{
				StartNextTask(person, time);
			}
			else if (person.Current is { } sequence)
			{
				// Steps that go round without taking any time would never end, so the person gives up on them.
				if (++stepsCarriedOut > MaxStepsWithoutTime)
					EndSequence(person, sequence, time);
				else if (!AdvanceStep(person, sequence, time))
					break;
			}
			else if (!m_world.IsAutomatic(person))
			{
				break;
			}
			else if (hasChosen)
			{
				GoIdle(person, GetDistances(person.Location), person.GoalChoice, time);
			}
			else
			{
				ChooseGoal(person, time);
				hasChosen = true;
			}
		}
	}

	/// <summary>
	/// Starts the first of a person's tasks, or finishes it straight away if it takes no time.
	/// </summary>
	private void StartNextTask(Person person, TimePoint time)
	{
		var task = person.Tasks[0];
		if (task is BusyTask { ResumeMinutes: not null } && person.Current is { } sequence && sequence.Need.Kind == NeedKind.Quota && !CanResume(person, sequence.Need, time))
		{
			// An interruption can leave too little time to finish the time they were spending on it, so its step fails.
			person.IsAtWork = false;
			person.SetTasks([]);
			FailStep(person, sequence, FailCause.Hours, time);
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

	/// <summary>
	/// Starts a sequence of a need's steps for a person, which becomes their current one, from a step, with the time
	/// chosen for it, and the tasks to do first, such as changing out of night clothes. Returns the sequence.
	/// </summary>
	internal static StepSequence StartSequence(Person person, Need need, int step, int? minutes, IEnumerable<PersonTask> tasks, GoalChoice? choice)
	{
		var sequence = new StepSequence(need, step, minutes) { Place = person.Location };
		person.AddSequence(sequence);
		person.SetTasks(tasks, choice);
		return sequence;
	}

	/// <summary>
	/// Gets the fewest minutes someone could walk (and change) before reaching a need's first "do" step, from where they are.
	/// </summary>
	internal int GetMinutesBeforeFirstDo(Person person, Need need, TimePoint time) => EstimateTravel(person, need, time).BeforeFirstDo.Min;

	/// <summary>
	/// Gets the time to choose for a sequence of a need's steps as it starts: a shift (or a run's worth, for a quota
	/// measured in runs) for a quota need, or null for a level need, which goes on until it's met.
	/// </summary>
	internal int? GetSequenceMinutes(Person person, Need need, TimePoint time) =>
		need.Kind == NeedKind.Quota && !need.IsRuns ? Math.Max(GetShiftMinutes(person, need, time), 0) : null;

	/// <summary>
	/// Carries a person's sequence on by a step: passes it if its condition isn't met or it's already done, fails it if it
	/// can't be done, or carries it out, if the game carries out this step for them (anyone the game decides for, a step
	/// done for the player, and steps the player doesn't take themselves). Returns false if it's left for the player to
	/// take, or true if the sequence moved on or the person was given tasks. A sequence carried on passively (one the
	/// player isn't focused on) is never given tasks.
	/// </summary>
	internal bool AdvanceStep(Person person, StepSequence sequence, TimePoint time, bool isPassive = false)
	{
		// The steps of an appointment wait until the person sets off for it.
		if (!sequence.HasSetOff)
			return false;

		// Someone taking part in someone else's steps follows them until they're let go: the game has them follow, until
		// they're done following, and the player stays with them. The client of an appointment first meets the worker there,
		// with its "meet" step.
		if (sequence.Role != SequenceRole.Steps && sequence.Appointment is not { IsStarted: false })
		{
			var isFollowing = sequence.Leader is not null && m_world.Appointments.Any(x => x.IsStarted && x.Client == person && x.Worker == sequence.Leader) ||
				(sequence.Role == SequenceRole.Follower && !IsFollowingOver(sequence, time));
			var isOver = m_world.IsAutomatic(person) || !isFollowing;
			if (isOver)
				EndSequence(person, sequence, time);
			return isOver;
		}

		if (sequence.IsDone)
		{
			EndSequence(person, sequence, time);
			return true;
		}

		var index = sequence.Step;
		var step = sequence.CurrentStep!;
		var handler = GetHandler(step);
		var context = new StepContext(person, sequence, step, index, time);
		var isTasksDone = sequence.IsCarriedOut && person.Current == sequence && person.Tasks.Count == 0;
		var check = isTasksDone && sequence.IsCut ? StepCheck.Fail(FailCause.Hours) :
			isTasksDone ? handler.AfterTasks(this, context) :
			!IsConditionMet(person, sequence.Need, sequence, step.When, time) ? StepCheck.Passed :
			step.Hours is { } hours && !hours.Contains(m_calendar.GetParts(time).Hour) ? StepCheck.Fail(FailCause.Hours) :
			sequence.Place is { } place && place != person.Location && !handler.IsMoving ? StepCheck.Fail(FailCause.Left) :
			ClothingRules.IsMissingNeededClothes(person, sequence, time) ? StepCheck.Fail(FailCause.Missing) :
			handler.Check(this, context);

		var isProgressed = true;
		if (check.State is StepState.Done or StepState.Passed)
		{
			CompleteStep(person, sequence, handler);
		}
		else if (check is { State: StepState.Failed, Cause: FailCause.Left } && GetSeekStepFor(sequence.Need, index) is { } seek)
		{
			// Leaving before talking with someone the steps found goes back to looking for someone.
			MoveOn(sequence, seek);
		}
		else if (check.State == StepState.Failed)
		{
			FailStep(person, sequence, check.Cause!.Value, time, check.Who);
		}
		else if (isTasksDone)
		{
			// A step that goes on until its time is up, such as wandering, is carried out again.
			sequence.IsCarriedOut = false;
		}
		else if (!IsCarriedOutByGame(person, sequence, handler, context) || (isPassive && !handler.IsForPlayer(this, context)))
		{
			isProgressed = false;
		}
		else
		{
			Apply(person, sequence, handler.CarryOut(this, context), time);
		}

		return isProgressed;
	}

	/// <summary>
	/// Whether the game carries out a step of a person's sequence rather than leaving it to them: always for anyone the
	/// game decides for, and for the player, a step being done for them, every step while all of them are being done for
	/// them (other than asking whether to keep going), and steps the player doesn't take themselves.
	/// </summary>
	private bool IsCarriedOutByGame(Person person, StepSequence sequence, StepHandler handler, StepContext context) =>
		m_world.IsAutomatic(person) || sequence.Takeover == StepTakeover.Step || handler.IsForPlayer(this, context) ||
		(sequence.Takeover == StepTakeover.All && !handler.IsAskingPlayer(this, context));

	/// <summary>
	/// Applies what carrying out a step did.
	/// </summary>
	private void Apply(Person person, StepSequence sequence, StepWork work, TimePoint time)
	{
		switch (work)
		{
		case StepWork.WithTasks tasks:
			person.AddTasks(tasks.Tasks);
			sequence.IsCarriedOut = true;
			break;
		case StepWork.Finished:
			CompleteStep(person, sequence, GetHandler(sequence.CurrentStep!));
			break;
		case StepWork.Failed failed:
			FailStep(person, sequence, failed.Cause, time, failed.Who);
			break;
		case StepWork.GoTo goTo:
			Leave(sequence, goTo.Step);
			break;
		}
	}

	/// <summary>
	/// Gets the "seek" step a "talk" or "ask" step with the one sought goes back to when the person leaves before it's done:
	/// the "seek" step before it that leads to it. Returns null for any other step, or if there's none.
	/// </summary>
	private static int? GetSeekStepFor(Need need, int step)
	{
		if (need.Steps[step] is not { Kind: StepKind.Talk or StepKind.Ask, With: StepTarget.Sought })
			return null;

		var seek = Enumerable.Range(0, step).LastOrDefault(x => need.Steps[x].Kind == StepKind.Seek, -1);
		var next = seek;
		while (next >= 0 && next < step)
			next = need.GetNextStep(next);
		return seek >= 0 && next == step ? seek : null;
	}

	/// <summary>
	/// Goes on from the step a sequence is on to another, ending a step being done for the player.
	/// </summary>
	private static void MoveOn(StepSequence sequence, int step)
	{
		sequence.GoTo(step);
		if (sequence.Takeover == StepTakeover.Step)
			sequence.Takeover = StepTakeover.None;
	}

	/// <summary>
	/// Goes on to the next step once one is done. Once a step that goes somewhere is done, the steps after it are done
	/// where it went.
	/// </summary>
	private static void CompleteStep(Person person, StepSequence sequence, StepHandler handler)
	{
		if (handler.IsMoving)
			sequence.Place = person.Location;
		MoveOn(sequence, GetNextStep(sequence, sequence.Step));
	}

	/// <summary>
	/// Gets the step a sequence goes on to after one: the next, or while it's leaving its steps, once the "finally" steps
	/// it's carrying out are done, the next ones to carry out, or else where it was going.
	/// </summary>
	internal static int GetNextStep(StepSequence sequence, int index)
	{
		var next = sequence.Need.GetNextStep(index);
		return sequence.UnwindRunning is { } running && sequence.Need.GetBlockFinallyAt(running) is { } block && block.IsInFinally(index) && !block.IsInFinally(next)
			? sequence.ContinueUnwinding()
			: next;
	}

	/// <summary>
	/// Goes on from the step a sequence is on to another (or its end), first carrying out the "finally" steps of each list
	/// of steps that leaves, innermost first. One that's already carrying out "finally" steps on its way somewhere carries
	/// on with them, but ends once they're done, for going to its end.
	/// </summary>
	private static void Leave(StepSequence sequence, int target)
	{
		var need = sequence.Need;
		if (sequence.IsUnwinding)
		{
			if (target >= need.Steps.Count)
				sequence.UnwindToEnd();
			MoveOn(sequence, GetNextStep(sequence, sequence.Step));
		}
		else
		{
			var blocks = sequence.IsDone || sequence.Role != SequenceRole.Steps ? [] : need.GetBlocksLeft(sequence.Step, target);
			MoveOn(sequence, blocks.Count == 0 ? target : sequence.StartUnwinding(blocks.Select(x => x.FinallyStart).ToList(), target));
		}
	}

	/// <summary>
	/// Gives up a sequence: it ends, unless it has "finally" steps to carry out first, when it finishes up, carrying them
	/// out on its way to its end. Returns whether it's finishing up.
	/// </summary>
	internal bool GiveUp(Person person, StepSequence sequence, TimePoint time)
	{
		var need = sequence.Need;
		var isFinishing = !sequence.IsUnwinding && sequence.HasSetOff && sequence.Role == SequenceRole.Steps && !sequence.IsDone && need.GetBlocksLeft(sequence.Step, need.Steps.Count).Count != 0;
		if (isFinishing)
			Leave(sequence, need.Steps.Count);
		else
			EndSequence(person, sequence, time);
		return isFinishing;
	}

	/// <summary>
	/// Whether giving up a sequence would leave any of its "finally" steps to carry out: one whose condition is met that
	/// isn't done already, in the lists of steps it's in.
	/// </summary>
	internal bool HasStepsToFinishUp(Person person, StepSequence sequence, TimePoint time)
	{
		var need = sequence.Need;
		return !sequence.IsUnwinding && sequence.HasSetOff && sequence.Role == SequenceRole.Steps && !sequence.IsDone &&
			need.GetBlocksLeft(sequence.Step, need.Steps.Count)
				.SelectMany(x => Enumerable.Range(x.FinallyStart, x.End - x.FinallyStart))
				.Any(x => IsConditionMet(person, need, sequence, need.Steps[x].When, time) && !IsStepDone(person, sequence, x));
	}

	/// <summary>
	/// For the player, finds the step to keep a sequence at, to finish it later, when its steps would be given up while
	/// they carry a bundle it made that a later step puts down: the last "goTo" step before that step, or the step itself
	/// if there's none. Returns null if there's no such step.
	/// </summary>
	internal static int? FindKeptStep(StepSequence sequence)
	{
		var need = sequence.Need;
		int? lastGoTo = null;
		var seen = new HashSet<int>();
		for (var index = sequence.IsDone ? need.Steps.Count : need.GetNextStep(sequence.Step); index < need.Steps.Count && seen.Add(index); index = need.GetNextStep(index))
		{
			var step = need.Steps[index];
			if (step.Kind == StepKind.GoTo)
				lastGoTo = index;
			else if (step is { Kind: StepKind.Put, ItemKind: null, Bundle: { } bundle } && sequence.Bundles.ContainsKey(bundle.Id))
				return lastGoTo ?? index;
		}

		return null;
	}

	/// <summary>
	/// Has the player keep a sequence whose steps would be given up, holding it at the step that takes the bundle it made
	/// home, to finish later (see <see cref="FindKeptStep"/>). Returns whether there was one to keep.
	/// </summary>
	internal bool Keep(Person person, StepSequence sequence, TimePoint time)
	{
		var kept = !m_world.IsAutomatic(person) && sequence.Role == SequenceRole.Steps && !sequence.IsUnwinding ? FindKeptStep(sequence) : null;
		if (kept is { } step)
		{
			MoveOn(sequence, step);
			sequence.Place = null;
			sequence.KeptSince ??= time;
		}

		return kept is not null;
	}

	/// <summary>
	/// Fails the step a sequence is on, for a cause: it goes on to the next step, gives up the sequence, or skips to a
	/// step, as its "onFail" says (see <see cref="Need.GetFailure"/>). A "meet" step that fails always gives it up, which
	/// calls off the appointment, at once. Giving up the steps carries out their "finally" steps first, and the player
	/// keeps a bundle the steps made that a later step puts down, to finish them later. The player is told, with who it
	/// was down to, if anyone in particular, and whether it gives up the steps.
	/// </summary>
	internal void FailStep(Person person, StepSequence sequence, FailCause cause, TimePoint time, Person? who = null)
	{
		var index = sequence.Step;
		var isMeet = sequence.IsDone || sequence.CurrentStep?.Kind == StepKind.Meet;
		var (kind, target) = isMeet ? (StepFailureKind.Stop, null) : sequence.Need.GetFailure(index, cause);
		var isKept = kind == StepFailureKind.Stop && !isMeet && !m_world.IsAutomatic(person) && sequence.Role == SequenceRole.Steps && !sequence.IsUnwinding && FindKeptStep(sequence) is not null;
		m_world.OnStepFailed(person, sequence, index, cause, time, who, isEnding: kind == StepFailureKind.Stop && !isKept);

		// The player is told if they missed an appointment: the other gave up waiting for them, or never came.
		if (cause == FailCause.NoShow && sequence.Appointment is { } missed && missed.GetOther(person) is var other && (!m_world.IsAutomatic(person) || !m_world.IsAutomatic(other)))
			m_world.OnAppointmentMissed(missed, isOtherWaiting: !m_world.IsAutomatic(other), time);
		if (kind == StepFailureKind.Next)
			MoveOn(sequence, GetNextStep(sequence, index));
		else if (target is { } skipTo)
			Leave(sequence, skipTo);
		else if (isMeet)
			EndSequence(person, sequence, time);
		else if (!isKept || !Keep(person, sequence, time))
			GiveUp(person, sequence, time);
	}

	/// <summary>
	/// Ends a sequence once it's done or given up: bundles it left are unpacked (their items stay carried), anyone
	/// following the person is let go, and an appointment whose steps it was is over. The one put on hold most recently,
	/// if any, becomes current. One put on hold for an appointment carries on from where the person is.
	/// </summary>
	internal void EndSequence(Person person, StepSequence sequence, TimePoint time)
	{
		// Before they set off, it's only something they agreed to, so ending it leaves what they're doing alone.
		var hasSetOff = sequence.HasSetOff;
		person.RemoveSequence(sequence);
		if (hasSetOff && person.Current is { Place: null } resumed)
			resumed.Place = person.Location;
		foreach (var id in sequence.Bundles.Keys.ToList())
			m_world.Unpack(sequence.RemoveBundle(id)!);
		if (sequence.Appointment is { } appointment && m_world.Appointments.Contains(appointment))
		{
			if (appointment.IsStarted)
				Release(person, time);
			else
				CallOff(appointment, time);
		}
		else if (hasSetOff && !person.Sequences.Any(x => x.Partner is { } partner && GetFollowers(person, time).Contains(partner)))
		{
			Release(person, time);
		}

		if (hasSetOff && m_world.IsAutomatic(person))
			person.IsAtWork = false;
	}

	/// <summary>
	/// Finds where a "goTo" step would take a person from where they are: for quarters, the nearest one of its kinds.
	/// Returns null if there's no such place.
	/// </summary>
	internal Location? FindPlace(Person person, StepSequence? sequence, SchedulePlace place) => ResolvePlace(person, sequence, place, GetDistances(person.Location), isEstimate: true);

	/// <summary>
	/// Gets the options of a "choose" step a person could choose now: those whose condition is met and whose places they
	/// can get to, with the index of the first of their steps and the index after the last of them.
	/// </summary>
	internal IReadOnlyList<(StepOption Option, int Start, int End)> GetAvailableOptions(Person person, StepSequence? sequence, Need goal, int chooseStep, TimePoint time)
	{
		var distances = GetDistances(person.Location);
		return goal.GetOptions(chooseStep)
			.Where(x => IsConditionMet(person, goal, sequence, x.Option.When, time) &&
				(goal.Steps[chooseStep].First || Enumerable.Range(x.Start, x.End - x.Start).Select(index => goal.Steps[index]).Where(step => step is { Kind: StepKind.GoTo, Place.Kind: not (NeedPlace.Remembered or NeedPlace.PartnerHome or NeedPlace.JobSite) })
					.All(step => ResolvePlace(person, sequence, step.Place!, distances, isEstimate: true) is not null)))
			.ToList();
	}

	/// <summary>
	/// Chooses one of a "choose" step's options: the first whose condition is met, for a step that takes the first, or
	/// otherwise one chosen by frequency among those that can be chosen (see <see cref="GetAvailableOptions"/>). For a quota
	/// need, options whose steps could take longer than what's left of its hours are left out, unless none would fit.
	/// Returns the option, or null if there's none.
	/// </summary>
	private (StepOption Option, int Start, int End)? ChooseOption(Person person, StepSequence? sequence, Need goal, int chooseStep, TimePoint time)
	{
		var options = goal.GetOptions(chooseStep);
		var available = GetAvailableOptions(person, sequence, goal, chooseStep, time);
		if (!goal.Steps[chooseStep].First && goal.Kind == NeedKind.Quota && available.Count > 1)
		{
			var fitting = available.Where(x => GetMostMinutesFrom(person, goal, GetOptionStart(goal, chooseStep, x), time) <= GetMinutesUntilEnd(person, goal, time)).ToList();
			if (fitting.Count != 0)
				available = fitting;
		}

		return available.Count == 0 ? null :
			goal.Steps[chooseStep].First ? available[0] :
			available[FindChosen(available, m_world.GameData.FrequencyChances.Choose(m_world.Random, available.Select(y => y.Option).ToList(), options.Select(y => y.Option).ToList()))];
	}

	/// <summary>
	/// Gets the most minutes a person could take carrying out a need's steps from one of them to the end: the minutes of its
	/// "do" steps, along with the walking from where they are.
	/// </summary>
	private long GetMostMinutesFrom(Person person, Need goal, int start, TimePoint time)
	{
		var walking = WalkSteps(person, goal, start, goal.Steps.Count, isCountingChanges: false, time).Select(x => x.Total).DefaultIfEmpty(0).Max();
		return goal.GetMinuteRangeFrom(person.Family.Members.Count, start).Max + walking;
	}

	/// <summary>
	/// Goes on from a "choose" step with one of its options: to the first of its steps, or past the "choose" step if it has
	/// none.
	/// </summary>
	internal static int GetOptionStart(Need goal, int chooseStep, (StepOption Option, int Start, int End) option) =>
		option.End > option.Start ? option.Start : goal.GetNextStep(chooseStep);

	/// <summary>
	/// Gets the index of the option chosen among the options available.
	/// </summary>
	private static int FindChosen(IReadOnlyList<(StepOption Option, int Start, int End)> available, StepOption chosen) => Enumerable.Range(0, available.Count).First(x => available[x].Option == chosen);

	/// <summary>
	/// Whether someone fits who a step talks with, asks, or looks for, for the person taking it: they meet its condition on
	/// their character, and are in the person's household or not, if it says. Anyone fits a step without one.
	/// </summary>
	internal static bool IsWho(Person person, Person other, StepWho? who) =>
		CharacterCondition.IsMet(who, x => x == StepWho.FamilyKey ? (other.Family == person.Family ? StepWho.FamilyMember : StepWho.FamilyOther) : other.GetConditionValueId(x));

	/// <summary>
	/// Whether someone is someone a "talk", "ask", or "seek" step could talk with or find, for the person taking it: they
	/// fit its who (and are a friend, if it says). Without a who that says whether they're family, anyone can be, the
	/// person's own household included.
	/// </summary>
	internal bool IsSought(Person person, Person other, StepWho? who) =>
		other != person && IsWho(person, other, who) && (who is not { Friends: true } || GetFriends(person).Contains(other));

	/// <summary>
	/// Picks who a "talk" or "ask" step talks with: someone there who isn't asleep, talking, or too busy to talk (and for
	/// the player, unless the world is headless, who can be asked now), who meets the step's condition, who's the one the
	/// step talks with. Returns null if there's no one.
	/// </summary>
	private Person? ChooseWhoToTalkWith(Person person, StepSequence sequence, Step step, TimePoint time)
	{
		var friends = step.Who is { Friends: true } ? GetFriends(person).ToHashSet() : null;
		var candidates = person.Location.People.Where(x => x != person && (m_world.IsAutomatic(x) || CanAskPlayer(person, x, time)) && !x.IsAsleep &&
			x.CurrentTask is not TalkTask && !IsTooBusyToTalk(x, time) && IsWho(person, x, step.Who) && (friends is null || friends.Contains(x))).ToList();
		return step.With switch
		{
			StepTarget.Sought when sequence.Seeking is { Friend: null } => candidates.Count == 0 ? null : candidates[m_world.Random.Next(candidates.Count)],
			StepTarget.Sought => candidates.FirstOrDefault(x => x == sequence.Seeking?.Friend),
			StepTarget.Partner => candidates.FirstOrDefault(x => x == sequence.Partner),
			_ => candidates.Count == 0 ? null : candidates[m_world.Random.Next(candidates.Count)],
		};
	}

	/// <summary>
	/// Talks with someone, or asks them a question, for a "talk" or "ask" step of a sequence. They become its partner. A
	/// question is answered yes by chance, and remembered by the step's name. Saying yes to something that takes time is
	/// only possible if the one asked can spare it, now, or at an appointment, which a yes makes if neither would be too shy
	/// for it (a shy no is remembered too). The player decides whether to say yes themselves, which is given. Returns
	/// whether they said yes, and the appointment it made, if any.
	/// </summary>
	internal (bool IsYes, Appointment? Appointment) TalkOrAskWith(Person person, StepSequence sequence, Step step, int index, Person other, TimePoint time, bool? playerAnswer = null)
	{
		var goal = sequence.Need;
		sequence.Partner = other;
		var minutes = GetTalkMinutes(person, sequence, step);
		var isYes = false;
		Person? shy = null;
		Appointment? made = null;
		if (step.Kind == StepKind.Ask)
		{
			var forMinutes = step.ForMinutes is { } amount ? (int) amount.GetAmount(goal.SharedByFamily ? person.Family.Members.Count : 0) : 0;
			isYes = playerAnswer ?? m_world.Random.NextDouble() * 100 < GetChance(other, person, step.Chance!, time);
			if (isYes && step.Appointment is { } appointment)
			{
				made = MakeAppointment(person, sequence, other, goal, index, appointment, forMinutes, minutes, time, out shy);
				isYes = made is not null;
			}
			else if (isYes && forMinutes > 0 && playerAnswer is null)
			{
				isYes = CanSpare(other, forMinutes, time);
			}

			// A yes to a job takes it on, at its place, worked out with the one asked as the partner.
			if (isYes && step.Job is { } job)
			{
				var site = ResolvePlace(person, sequence, job.Place!, GetDistances(person.Location), isEstimate: false);
				if (site is not null)
					m_world.AddJob(person, other, goal, site, job.Minutes!.Value.Minutes);
				isYes = site is not null;
			}

			sequence.SetAnswer(step.Name!, isYes, shy);
		}

		// Talking that meets the goal's quota counts toward it rather than socializing, and the one talked with has the need
		// the step meets for its partner met, if they have it.
		var partnerNeed = step.PartnerMeets is { } partnerMeets ? m_settings.Needs.FirstOrDefault(x => x.Id == partnerMeets && HasNeed(other, x)) : null;
		m_world.StartTalking(person, other, minutes, time, isSocializing: step.Kind == StepKind.Talk && !step.MeetsQuota,
			firstNeed: step.MeetsQuota && goal.Quota?.Time is not null ? goal : null, secondNeed: partnerNeed);
		if (step.MeetsQuota && goal.TakesChosenTime(step))
			sequence.UseMinutes(minutes);
		return (isYes, made);
	}

	/// <summary>
	/// Gets how long a "talk" or "ask" step takes.
	/// </summary>
	private static int GetTalkMinutes(Person person, StepSequence sequence, Step step) =>
		Math.Max((int) step.Minutes!.GetAmount(sequence.Need.SharedByFamily ? person.Family.Members.Count : 0), 1);

	/// <summary>
	/// Has someone ask the player to talk for the "talk" step they're on: the player is asked whether they will, while the
	/// one asking waits. Time stops until the player answers (see <see cref="AnswerPlayerTalk"/>).
	/// </summary>
	private static StepWork AskPlayerToTalk(Person person, StepSequence sequence, Person player)
	{
		sequence.Partner = player;
		player.TalkRequest = person;
		return StepWork.Give([new WaitTask(player, AskWaitMinutes)]);
	}

	/// <summary>
	/// Whether someone asking the player to talk is doing it for a "talk" step of theirs, rather than to chat.
	/// </summary>
	internal static bool IsTalkStepRequest(Person asker, Person player) => asker.Current is { CurrentStep.Kind: StepKind.Talk, Partner: { } partner } && partner == player;

	/// <summary>
	/// Gets how long talking takes for the "talk" step someone is asking the player to talk for, or null if they're asking
	/// to chat.
	/// </summary>
	internal static int? GetTalkStepMinutes(Person asker, Person player) =>
		IsTalkStepRequest(asker, player) && asker.Current is { CurrentStep: { } step } sequence ? GetTalkMinutes(asker, sequence, step) : null;

	/// <summary>
	/// Has the player answer someone who asked them to talk for a "talk" step: they talk, as anyone else would, or the step
	/// fails as "no", and for a no, the one asking doesn't ask again for a while. Returns whether they're talking.
	/// </summary>
	internal bool AnswerPlayerTalk(Person asker, Person player, bool isYes, TimePoint time)
	{
		var sequence = asker.Current!;
		asker.UpdateNeeds(time);
		asker.SetTasks([]);
		var isTalking = isYes && asker.Location == player.Location && player.CurrentTask is not TalkTask;
		if (isTalking)
		{
			TalkOrAskWith(asker, sequence, sequence.CurrentStep!, sequence.Step, player, time);
			sequence.IsCarriedOut = true;
		}
		else
		{
			if (!isYes)
				asker.GetRelationship(player).LastDeclined = time;
			FailStep(asker, sequence, FailCause.No, time, player);
		}

		return isTalking;
	}

	/// <summary>
	/// Has someone ask the player the question of the "ask" step they're on: the player is asked to answer, while they
	/// wait. Time stops until the player answers (see <see cref="AnswerPlayerAsk"/>).
	/// </summary>
	private static StepWork AskPlayer(Person person, StepSequence sequence, Person player)
	{
		sequence.Partner = player;
		player.AskRequest = person;
		return StepWork.Give([new WaitTask(player, AskWaitMinutes)]);
	}

	/// <summary>
	/// Has the player answer the question someone asked them, as anyone else would answer it, except that the player
	/// decides whether to say yes. They talk for as long as asking takes, and the one asking carries on once it's over.
	/// Returns the appointment a yes made, if any.
	/// </summary>
	internal Appointment? AnswerPlayerAsk(Person player, bool isYes, TimePoint time)
	{
		var asker = player.AskRequest ?? throw new InvalidOperationException("No one is asking the player anything.");
		player.AskRequest = null;
		Appointment? made = null;
		if (asker.Current is { CurrentStep.Kind: StepKind.Ask } sequence)
		{
			asker.UpdateNeeds(time);
			asker.SetTasks([]);
			if (!isYes)
				asker.GetRelationship(player).LastDeclined = time;
			made = TalkOrAskWith(asker, sequence, sequence.CurrentStep!, sequence.Step, player, time, isYes).Appointment;
			sequence.IsCarriedOut = true;
		}

		return made;
	}

	/// <summary>
	/// Gets the chance, from 0 to 100, that someone says yes when asked a question: the base chance, plus some for how well
	/// they think of the one asking, multiplied by every factor whose condition they meet, less some for how urgent the
	/// goal they're busy with is.
	/// </summary>
	internal double GetChance(Person asked, Person asking, StepChance chance, TimePoint time)
	{
		var score = asked.Relationships.TryGetValue(asking, out var relationship) ? relationship.Score : 0;
		var factor = chance.Factors.Where(x => CharacterCondition.IsMet(x.When, asked.GetConditionValueId)).Aggregate(1.0, (total, x) => total * x.Factor);
		var busy = asked.Goal is { } goal ? GetUrgency(asked, goal, time) : 0;
		return Math.Clamp(((chance.Base + (chance.PerScore * score / RelationshipSettings.MaximumScore)) * factor) - (chance.Busy * busy), 0, 100);
	}

	/// <summary>
	/// Gets how many minutes it is from a time until the next time it's an hour, or 0 if it's that hour exactly.
	/// </summary>
	private int GetMinutesUntilHour(TimePoint time, int hour)
	{
		var parts = m_calendar.GetParts(time);
		return (((hour * 60) - ((parts.Hour * 60) + parts.Minute)) % MinutesPerDay + MinutesPerDay) % MinutesPerDay;
	}

	/// <summary>
	/// Gets how many minutes are left of a step's hours from a time, or null if it has none.
	/// </summary>
	private int? GetMinutesLeftInHours(Step step, TimePoint time) => step.Hours is { } hours ? GetMinutesUntilHour(time, hours.End % 24) is var left and > 0 ? left : MinutesPerDay : null;

	/// <summary>
	/// Whether a condition of a person's steps for a need is met now: every setting it has is met (its "not" isn't, and at
	/// least one of its "any" is), and it's met without one. Without a sequence, such as for an estimate, conditions on what
	/// its steps kept track of aren't met.
	/// </summary>
	internal bool IsConditionMet(Person person, Need goal, StepSequence? sequence, StepCondition? when, TimePoint time) =>
		when is null ||
		((when.Not is not { } not || !IsConditionMet(person, goal, sequence, not, time)) &&
			(when.Any is not { } any || any.Any(x => IsConditionMet(person, goal, sequence, x, time))) &&
			(when.HasJob is not { } hasJob || (person.GetJob(goal) is { IsDone: false }) == hasJob) &&
			(when.Carrying is not { } carrying || (sequence?.Bundles.ContainsKey(carrying) ?? false)) &&
			(when.OutfitAtHome is not { } kind || ClothingRules.IsOutfitAtHome(person, kind, time)) &&
			(when.DoneForToday is not { } isDone || IsDoneForToday(person, goal, time) == isDone) &&
			(when.At is not { } at || IsPlaceFor(person, sequence, at, person.Location)) &&
			(when.Hours is not { } hours || hours.Contains(m_calendar.GetParts(time).Hour)) &&
			(when.ShiftLeft is not { } shiftLeft || sequence?.MinutesLeft >= shiftLeft) &&
			(when.BusinessProfession is not { } profession || person.Business?.Profession.Id == profession) &&
			(when.AnsweredYes is not { } yes || (sequence?.Answers.GetValueOrDefault(yes) ?? false)) &&
			(when.AnsweredNo is not { } no || !(sequence?.Answers.GetValueOrDefault(no) ?? false)));

	/// <summary>
	/// Whether a person is done with a quota need for the day: its quota is met, or it's closed. For the player, it always
	/// is, since what they started ends when they say so, such as by not working any longer.
	/// </summary>
	private bool IsDoneForToday(Person person, Need goal, TimePoint time) =>
		!m_world.IsAutomatic(person) ||
		goal.Kind == NeedKind.Quota && (GetRemainingMinutes(person, goal, time) <= 0 || !IsQuotaOpen(person, goal, time));

	/// <summary>
	/// Finds the place a step of a person's goal leads to, from where they are, or for a remembered place, where its
	/// sequence remembered, or for a job site, where their job for the sequence's need (or the need given, without a
	/// sequence) is. For an estimate, the nearest quarter of the kinds is used rather than choosing one at random. Returns
	/// null if there's no such place.
	/// </summary>
	private Location? ResolvePlace(Person person, StepSequence? sequence, SchedulePlace place, IReadOnlyDictionary<Location, Route> distances, bool isEstimate, Need? need = null) =>
		place.Kind == NeedPlace.Remembered ? sequence?.RememberedPlaces.GetValueOrDefault(place.Remembered ?? "") :
		place.Kind == NeedPlace.PartnerHome ? sequence?.Partner?.Home :
		place.Kind == NeedPlace.JobSite ? (sequence?.Need ?? need) is { } jobNeed && person.GetJob(jobNeed) is { IsDone: false } job ? job.Place : null :
		FindPlace(person, place.Kind, place.QuarterKinds, distances, place.Nearest || isEstimate);

	/// <summary>
	/// Estimates how many minutes of walking a person carrying out a need's steps would do, from where they are, with the
	/// steps' conditions as they are now: before its first "do" step, before its last, and in all, each as the fewest and
	/// most it could be. It follows every way they could go: each option of a "choose" step they could choose (only the
	/// first whose condition is met, for one that takes the first), and counts the steps a "repeat" step repeats once.
	/// </summary>
	private TravelEstimate EstimateTravel(Person person, Need goal, TimePoint time)
	{
		// The estimate is used many times as needs' urgencies are worked out, so it's kept for as long as the person stays
		// where they are at the same time. It's only an estimate, so things changing in between doesn't matter.
		if (m_estimates.TryGetValue((person, goal), out var kept) && kept.Location == person.Location && kept.Time == time)
			return kept.Estimate;

		var ways = WalkSteps(person, goal, 0, goal.Steps.Count, isCountingChanges: false, time);
		var estimate = new TravelEstimate(GetRange(ways.Select(x => x.BeforeFirstDo)), GetRange(ways.Select(x => x.BeforeLastDo)), GetRange(ways.Select(x => x.Total)));
		m_estimates[(person, goal)] = (person.Location, time, estimate);
		return estimate;
	}

	private static MinuteRange GetRange(IEnumerable<int> minutes) => minutes.Aggregate((MinuteRange?) null, (range, x) => range?.Or(MinuteRange.Exactly(x)) ?? MinuteRange.Exactly(x)) ?? MinuteRange.Zero;

	/// <summary>
	/// Follows each way a person could go through some of a need's steps, from one step up to another (or the end), from
	/// where they are, with the steps' conditions as they are now, and gets the minutes of walking along each way: before
	/// its first "do" step, before its last, and in all, along with the time changing clothes takes, if it counts it, and
	/// where it ends. Every
	/// option of a "choose" step they could choose is followed (only the first whose condition is met, for one that takes
	/// the first), up to a limit on how many ways there are, past which only the first option is followed.
	/// </summary>
	private IReadOnlyList<(int BeforeFirstDo, int BeforeLastDo, int Total, Location End)> WalkSteps(Person person, Need goal, int start, int end, bool isCountingChanges, TimePoint time)
	{
		var ways = new List<(int BeforeFirstDo, int BeforeLastDo, int Total, Location End)>();
		var branches = 1;
		void Walk(int index, Location location, int minutes, int? beforeFirstDo, int? beforeLastDo, int count)
		{
			for (; index < end && count < goal.Steps.Count; count++)
			{
				var step = goal.Steps[index];
				var next = goal.GetNextStep(index);
				if (step.Kind == StepKind.Choose)
				{
					var options = GetAvailableOptions(person, null, goal, index, time);
					if (step.First)
						options = options.Take(1).ToList();
					if (options.Count > 1 && branches + options.Count - 1 <= MaxEstimateWays)
					{
						branches += options.Count - 1;
						foreach (var option in options.Skip(1))
							Walk(GetOptionStart(goal, index, option), location, minutes, beforeFirstDo, beforeLastDo, count + 1);
					}

					next = options.Count != 0 ? GetOptionStart(goal, index, options[0]) : next;
				}
				else if (IsConditionMet(person, goal, null, step.When, time))
				{
					if (step.Kind == StepKind.Stop)
						break;
					if (step is { Kind: StepKind.GoTo or StepKind.Wander, Place: { } stepPlace } && ResolvePlace(person, null, stepPlace, GetDistances(location), isEstimate: true, goal) is { } place &&
						GetDistances(location).TryGetValue(place, out var route))
					{
						minutes += route.Minutes;
						location = place;
					}

					if (isCountingChanges && step.Kind is StepKind.Dress or StepKind.Undress && GetStepChange(person, step, time) is not null)
						minutes += m_settings.Clothing.ChangeMinutes;
					if (step.Kind == StepKind.Do)
					{
						beforeFirstDo ??= minutes;
						beforeLastDo = minutes;
					}
				}

				index = next;
			}

			ways.Add((beforeFirstDo ?? minutes, beforeLastDo ?? minutes, minutes, location));
		}

		Walk(start, person.Location, 0, null, null, 0);
		return ways;
	}

	/// <summary>
	/// The walking a person carrying out a need's steps would do: before its first "do" step, before its last, and in all,
	/// each as the fewest and most minutes it could be.
	/// </summary>
	private readonly record struct TravelEstimate(MinuteRange BeforeFirstDo, MinuteRange BeforeLastDo, MinuteRange Total);

	/// <summary>
	/// Whether someone could carry on repeating steps after a pass: some of the time chosen for the sequence is left, and
	/// the need can still be met (a quota need is open and not done).
	/// </summary>
	private bool ShouldRepeat(Person person, StepSequence sequence, TimePoint time) =>
		sequence.MinutesLeft is > 0 && (sequence.Need.Kind != NeedKind.Quota || (IsQuotaOpen(person, sequence.Need, time) && GetRemainingMinutes(person, sequence.Need, time) > 0));

	/// <summary>
	/// Gets the one of someone's needs other than their goal that weighs the most of those that would be considered as a
	/// goal now, weigh more than the goal would, and whose steps can be done, or null if none do.
	/// </summary>
	internal Need? GetMoreUrgentNeed(Person person, Need goal, TimePoint time)
	{
		var goalWeight = m_settings.GetGoalWeight(goal, GetUrgency(person, goal, time));
		var hasFriends = GetFriends(person).Any();
		var distances = GetDistances(person.Location);
		return m_settings.Needs.Where(x => x != goal && (!x.Seeks || hasFriends))
			.Select(x => (Need: x, Urgency: GetUrgency(person, x, time)))
			.Where(x => m_settings.IsGoalCandidate(x.Need, x.Urgency) && m_settings.GetGoalWeight(x.Need, x.Urgency) > goalWeight)
			.OrderByDescending(x => m_settings.GetGoalWeight(x.Need, x.Urgency))
			.Select(x => x.Need)
			.FirstOrDefault(x => CanDoSteps(person, x, distances, time));
	}

	/// <summary>
	/// Whether a person could start on a need's steps from where they are: they can get to every place its steps go to,
	/// and for a need that looks for a friend, there's somewhere to look.
	/// </summary>
	private bool CanDoSteps(Person person, Need need, IReadOnlyDictionary<Location, Route> distances, TimePoint time) =>
		CanReachPlaces(person, need, distances, time) && (!need.Seeks || ChooseWhereToSeek(person, distances, time, isCheck: true) is not null);

	/// <summary>
	/// Whether a person can get to every place their goal's steps go to on their way through them (see
	/// <see cref="IsOnTheWay"/>).
	/// </summary>
	private bool CanReachPlaces(Person person, Need goal, IReadOnlyDictionary<Location, Route> distances, TimePoint time) =>
		Enumerable.Range(0, goal.Steps.Count)
			.Where(index => goal.Steps[index] is { Kind: StepKind.GoTo, Place.Kind: not (NeedPlace.Remembered or NeedPlace.PartnerHome or NeedPlace.JobSite) } && IsOnTheWay(person, goal, index, time))
			.All(index => ResolvePlace(person, null, goal.Steps[index].Place!, distances, isEstimate: true) is not null);

	/// <summary>
	/// Whether a person can get to every place their goal's steps go to, from where they are.
	/// </summary>
	internal bool CanReachPlaces(Person person, Need goal, TimePoint time) => CanReachPlaces(person, goal, GetDistances(person.Location), time);

	/// <summary>
	/// Whether a step of a need is one a person would come to on every way through its steps, as things are now: it isn't
	/// in an option of a "choose" step, or it's in the option a "choose" step with "first" would take for them (such as
	/// the steps of their profession), in each "choose" step it's in.
	/// </summary>
	private bool IsOnTheWay(Person person, Need goal, int step, TimePoint time)
	{
		var isOnTheWay = true;
		while (isOnTheWay && goal.GetOptionOf(step) is { } option)
		{
			var taken = goal.Steps[option.Choose].First ? goal.GetOptions(option.Choose).Where(x => IsConditionMet(person, goal, null, x.Option.When, time)).Select(x => (int?) x.Start).FirstOrDefault() : null;
			isOnTheWay = taken == option.Start;
			step = option.Choose;
		}

		return isOnTheWay;
	}

	/// <summary>
	/// Whether someone can carry on with a quota need after being interrupted: it's within its hours, and isn't done yet.
	/// </summary>
	private bool CanResume(Person person, Need need, TimePoint time) => IsQuotaOpen(person, need, time) && GetRemainingMinutes(person, need, time) > 0;

	/// <summary>
	/// Whether someone refuses to be interrupted, because they're following someone, on their way to an appointment, or
	/// their goal is too urgent right now. Someone waiting to meet someone doesn't stop for anyone the game decides for, so
	/// as not to miss them, but the player can talk with them, since they're only waiting.
	/// </summary>
	public bool IsTooBusyToTalk(Person person, TimePoint time, bool isPlayerAsking = false) =>
		person.CurrentTask is MeetTask ? !isPlayerAsking :
		person.CurrentTask is FollowTask || IsOnTheWayToAppointment(person) || (person.Goal is { } goal && GetUrgency(person, goal, time) >= m_settings.Talking.RefuseUrgency);

	/// <summary>
	/// Whether someone has set off for an appointment that hasn't started, and is doing what it takes to get there, such as
	/// walking there or changing clothes first.
	/// </summary>
	private static bool IsOnTheWayToAppointment(Person person) => person.Current is { HasSetOff: true, Appointment.IsStarted: false };

	/// <summary>
	/// Gets the people following someone: those the game has following them, and the player, while they're taking part in
	/// their steps and aren't doing anything else.
	/// </summary>
	internal IReadOnlyList<Person> GetFollowers(Person leader, TimePoint time) =>
		m_world.People.Where(x => (x.CurrentTask is FollowTask follow && follow.Leader == leader) || (x.TaskEnd is null && IsLedBy(x, leader, time))).ToList();

	/// <summary>
	/// Whether someone is taking part in another's steps and following them now.
	/// </summary>
	internal static bool IsLedBy(Person person, Person leader, TimePoint time) =>
		person.Current is { Role: not SequenceRole.Steps, Leader: { } led } current && led == leader && !IsFollowingOver(current, time);

	/// <summary>
	/// Whether the time someone follows another for, as a "lead" step's minutes, is up.
	/// </summary>
	private static bool IsFollowingOver(StepSequence sequence, TimePoint time) => sequence.Role == SequenceRole.Follower && sequence.Until is { } until && time >= until;

	/// <summary>
	/// Lets go of anyone following someone. They stop following and choose what to do next, or if something interrupted
	/// their following, such as changing clothes with the one they follow, once they're done with it.
	/// </summary>
	internal void Release(Person leader, TimePoint time)
	{
		// An appointment is over once the worker is done with its steps.
		foreach (var appointment in m_world.Appointments.Where(x => x.IsStarted && x.Worker == leader).ToList())
			m_world.RemoveAppointment(appointment);
		// Anyone following them as part of their steps is done with that.
		foreach (var person in m_world.People.Where(x => x != leader))
		{
			foreach (var sequence in person.Sequences.Where(x => x.Role != SequenceRole.Steps && x.Leader == leader).ToList())
				EndSequence(person, sequence, time);
		}

		bool IsFollowing(PersonTask task) => task is FollowTask follow && follow.Leader == leader;
		foreach (var follower in m_world.People.Where(x => x.Tasks.Any(IsFollowing)).ToList())
		{
			if (follower.CurrentTask is FollowTask)
			{
				follower.UpdateNeeds(time);
				follower.SetTasks([]);
				Continue(follower, time);
			}
			else
			{
				follower.DropTasksAfterCurrent();
			}
		}
	}

	/// <summary>
	/// Gives a person a goal and starts its steps, chosen at random among their needs that are urgent enough, favoring
	/// the more urgent ones, or has them stay idle if none are. Someone still in work clothes after their work for the day,
	/// or in night clothes when they aren't going to sleep, changes out of them first. Someone at home first puts away the
	/// household's clothes they're still carrying, such as washing left over from steps they gave up on.
	/// </summary>
	public void ChooseGoal(Person person, TimePoint time)
	{
		if (person.Location == person.Home)
			ClothingRules.PutAway(person);

		// Every need that's at all urgent is kept with how urgent it was, so developers can see why the goal was chosen.
		var urgencies = m_settings.Needs
			.Select(need => new NeedUrgency(need, GetUrgency(person, need, time)))
			.Where(x => x.Urgency > 0)
			.ToList();
		// Someone without friends has no one to seek out.
		var hasFriends = GetFriends(person).Any();
		// Someone who has already napped since they last slept until rested doesn't nap again.
		var isNap = IsNap(person, time);
		var candidates = urgencies.Where(x => m_settings.IsGoalCandidate(x.Need, x.Urgency) && (!x.Need.Seeks || hasFriends) && (x.Need != m_sleepNeed || !isNap || !person.HasNapped)).ToList();

		// Only a need whose steps can get to their places is chosen, such as work in the fields, in a village without any.
		// If none can, the one that would have been chosen is kept, so developers can see why.
		var distances = GetDistances(person.Location);
		var doable = candidates.Where(x => CanDoSteps(person, x.Need, distances, time)).ToList();
		var choices = doable.Count != 0 ? doable : candidates;
		var choice = choices.Count == 0 ? null : m_world.Random.NextWeighted(choices, x => m_settings.GetGoalWeight(x.Need, x.Urgency)).Need;
		var hasPlace = doable.Count != 0;

		var minutes = choice is not null && choice == m_sleepNeed && isNap ? m_world.Random.NextInRange(m_settings.Sleep.NapMinutes) :
			choice is { Kind: NeedKind.Quota } && hasPlace ? GetShiftMinutes(person, choice, time) : (int?) null;
		var result = choice is null ? GoalChoiceResult.NothingUrgent : !hasPlace ? GoalChoiceResult.NoPlace : minutes is 0 or < 0 ? GoalChoiceResult.NoTime : GoalChoiceResult.Chosen;
		var goalChoice = new GoalChoice(time, urgencies, choice, result);

		if (result == GoalChoiceResult.Chosen)
		{
			if (choice == m_sleepNeed)
				person.HasNapped = isNap;
			StartSequence(person, choice!, 0, choice!.IsRuns ? null : minutes, GetChangingOut(person, choice, time), goalChoice);
		}
		else
		{
			GoIdle(person, distances, goalChoice, time);
		}
	}

	/// <summary>
	/// Has a person wait a while somewhere before deciding again, without a goal, as when none of their needs is urgent
	/// enough.
	/// </summary>
	private void GoIdle(Person person, IReadOnlyDictionary<Location, Route> distances, GoalChoice? goalChoice, TimePoint time)
	{
		var idle = m_settings.Idle;
		var place = FindPlace(person, idle.Place.Kind, idle.Place.QuarterKinds, distances, isNearest: false) ?? person.Location;
		IReadOnlyList<PersonTask> tasks = [.. GetChangingOut(person, null, time), .. GetPath(person.Location, place, distances).Select(PersonTask (x) => new TravelTask(x)), new BusyTask(m_world.Random.NextInRange(idle.Minutes))];
		person.SetTasks(tasks, goalChoice);
	}

	/// <summary>
	/// Gets the change of clothes someone makes before starting on a goal (or being idle, without one): out of their work
	/// clothes, if they're still wearing them once their work for the day is over, and out of their night clothes, if they
	/// aren't going to sleep.
	/// </summary>
	private IReadOnlyList<PersonTask> GetChangingOut(Person person, Need? goal, TimePoint time)
	{
		var tasks = new List<PersonTask>();

		// Someone left wearing nothing, such as after swimming, gets dressed again first.
		if (person.GetOutfits(OutfitKind.Nothing).FirstOrDefault(x => ClothingRules.IsChangedInto(person, x)) is { } nothing)
			tasks.Add(new ChangeClothesTask(nothing, IsTakingOff: true));
		var isWorking = goal is { IsWork: true } || (IsInWorkHours(person, time) && HasWorkToday(person, time));
		var isSleeping = goal is not null && goal == m_sleepNeed;
		if (!isWorking && !isSleeping && person.GetOutfits(OutfitKind.Work).FirstOrDefault(x => x.Pieces.Any(y => y.Wearer == person)) is { } work)
			tasks.Add(new ChangeClothesTask(work, IsTakingOff: true));
		if (!isSleeping && person.GetOutfits(OutfitKind.Night).FirstOrDefault(x => ClothingRules.IsChangedInto(person, x)) is { } night)
			tasks.Add(new ChangeClothesTask(night, IsTakingOff: true));
		return tasks;
	}

	/// <summary>
	/// Reacts to someone starting a task where they are: if they and a friend there are willing, one of them stops to
	/// socialize with the other. Someone a friend is waiting for, having been called, chats with
	/// them. Someone looking for a friend who isn't there asks someone else there about their friends instead, when they
	/// start looking around.
	/// </summary>
	internal void OnTaskStarted(Person person, PersonTask task, TimePoint time)
	{
		// Falling asleep changes who can see the people there, and so does someone passing through stopping on their way, such
		// as to talk, since they're staying a while.
		if (person.IsAsleep || (task is not TravelTask && person.Tasks.Count > 1 && person.Tasks[1] is TravelTask))
			OnCompanyChanged(person.Location);
		if (m_socializeNeed is not null && task is not (TalkTask or WaitTask) && !TryChatWithWaitingFriend(person, time) && !TryStartChat(person, time) &&
			task is BusyTask { Step: { } step, ResumeMinutes: null } && person.Goal is { } goal && step < goal.Steps.Count && goal.Steps[step].Kind == StepKind.Seek && person.Seeking?.Friend is not null)
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
		if (person.Seeking?.Friend is { } friend && friend.Location == destination && friend.CurrentTask is TravelTask &&
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
	/// Starts the chat that someone (or a friend where they are) is most willing to have, if anyone is willing enough and
	/// the friend asked agrees. The player is asked, and decides for themselves. Returns whether a chat started, or the
	/// player was asked.
	/// </summary>
	private bool TryStartChat(Person person, TimePoint time)
	{
		var friends = GetFriends(person).Where(x => x.Location == person.Location).ToList();
		var best = friends
			.SelectMany(friend => new[] { (Initiator: person, Other: friend), (Initiator: friend, Other: person) })
			.Select(x => (x.Initiator, x.Other, Willingness: GetChatWillingness(x.Initiator, x.Other, time)))
			.Where(x => x.Willingness >= m_settings.Socializing.ChatUrgency)
			.OrderByDescending(x => x.Willingness)
			.FirstOrDefault(x => !m_world.IsAutomatic(x.Other) || WillChat(x.Other, time));
		if (best.Initiator is not null)
			StartChat(best.Initiator, best.Other, time);
		return best.Initiator is not null;
	}

	/// <summary>
	/// Decides, for someone the game decides for who's asked to chat, whether they agree: they do unless they're too busy
	/// to talk (see <see cref="IsTooBusyToTalk"/>). The player decides for themselves.
	/// </summary>
	private bool WillChat(Person asked, TimePoint time) => !IsTooBusyToTalk(asked, time);

	/// <summary>
	/// Gets how willing someone is to stop and chat with a friend: how urgent socializing is for them, times how close
	/// they are, less how urgent whatever else they're doing is. It's negative infinity if they can't: either of them is
	/// already talking, the one starting it is the player (unless headless), is asleep, or is on their way to an
	/// appointment, the friend is asleep and
	/// socializing isn't urgent enough to wake them, or the friend is the player (unless headless) and can't be asked right
	/// now. Whether the friend agrees is theirs to decide (see <see cref="WillChat"/>).
	/// </summary>
	private double GetChatWillingness(Person initiator, Person other, TimePoint time)
	{
		var urgency = GetUrgency(initiator, m_socializeNeed!, time);
		var canChat = m_world.IsAutomatic(initiator) && (m_world.IsAutomatic(other) || CanAskPlayer(initiator, other, time)) && initiator.CurrentTask is not (null or TalkTask or FollowTask or MeetTask) && !IsOnTheWayToAppointment(initiator) && !initiator.IsAsleep &&
			other.CurrentTask is not TalkTask && (!other.IsAsleep || urgency >= m_settings.Socializing.WakeUrgency);
		var goalUrgency = initiator.Goal is { } goal && goal != m_socializeNeed ? GetUrgency(initiator, goal, time) : 0;
		return canChat && urgency > 0
			? (urgency * initiator.GetRelationship(other).Score / RelationshipSettings.MaximumScore) - (goalUrgency * m_settings.Socializing.ChatGoalWeight)
			: double.NegativeInfinity;
	}

	/// <summary>
	/// Whether someone can ask the player to chat, or ask them something: they're the player, no one else is waiting for an
	/// answer, they aren't doing anything (other than following), and they haven't turned them down lately.
	/// </summary>
	private bool CanAskPlayer(Person initiator, Person player, TimePoint time) =>
		IsPlayerFree(player) &&
		(initiator.GetRelationship(player).LastDeclined is not { } declined || m_calendar.GetTotalMinutes(time - declined) >= m_settings.Socializing.AskPlayerAgainHours * 60);

	/// <summary>
	/// Whether the player is free to be asked something: no one is waiting for an answer from them, they aren't being asked
	/// whether to set off for an appointment, and they aren't doing anything (other than following).
	/// </summary>
	private static bool IsPlayerFree(Person player) =>
		player.IsPlayer && player.TalkRequest is null && player.AskRequest is null && player.StartRequest is null &&
		!player.Sequences.Any(x => x.SetOff == SetOffState.Due) && player.TaskEnd is null;

	/// <summary>
	/// Has someone socialize with a friend who agreed, first asking what they're doing unless they already know, from
	/// having been told since the friend chose their goal. The player is asked first (unless headless), and chats once they
	/// agree.
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
	/// Chooses where a "seek" step looks for anyone who meets its condition: a place like the step's (for a quarter, one of
	/// its kinds, chosen by frequency; for another household's home, any of them), or without a place, any quarter. Among
	/// quarters and homes, those where the person has lately seen people who meet it and nearer ones are more likely.
	/// Without a place, or for a home, the place they're in now isn't chosen. Returns null if there's no such place.
	/// </summary>
	private SeekChoice? ChooseWhereToSeekPeople(Person person, Step step, IReadOnlyDictionary<Location, Route> distances, TimePoint time)
	{
		var seen = person.Sightings.Where(x => IsWho(person, x.Key, step.Who) &&
				m_calendar.GetTotalMinutes(time - x.Value.Time) < m_settings.SightingHours * 60)
			.GroupBy(x => x.Value.Location).ToDictionary(x => x.Key, x => x.Count());
		Location? ChooseQuarter(IReadOnlyList<Location> quarters) => quarters.Count == 0 ? null :
			m_world.Random.NextWeighted(quarters, x => (1 + seen.GetValueOrDefault(x)) / (1 + (distances[x].Minutes / m_settings.Socializing.NearMinutes)));

		m_quarters ??= m_world.Locations.Where(x => x.Template is QuarterTemplate).ToLookup(x => x.QuarterKind ?? "", StringComparer.Ordinal);
		Location? chosen;
		if (step.Place is not { } place)
		{
			chosen = ChooseQuarter(m_quarters.SelectMany(x => x).Where(x => x != person.Location && distances.ContainsKey(x)).ToList());
		}
		else if (place.Kind == NeedPlace.Quarter)
		{
			var choices = (place.QuarterKinds ?? []).Where(x => m_quarters[x.QuarterKind].Any(distances.ContainsKey)).ToList();
			var kind = choices.Count == 0 ? null : m_world.GameData.FrequencyChances.Choose(m_world.Random, choices, place.QuarterKinds).QuarterKind;
			chosen = ChooseQuarter(kind is null ? [] : m_quarters[kind].Where(distances.ContainsKey).ToList());
		}
		else if (place.Kind == NeedPlace.OtherHome)
		{
			// People know where everyone lives and works, so homes where people who meet the condition work at home are more
			// likely, as someone is more likely to be in.
			var homes = m_world.Families.Where(x => x.Home != person.Home && x.Home != person.Location && distances.ContainsKey(x.Home))
				.GroupBy(x => x.Home).ToDictionary(x => x.Key, x => x.SelectMany(family => family.Members).Count(member => member.Workplace == x.Key && IsSought(person, member, step.Who)));
			chosen = homes.Count == 0 ? null : m_world.Random.NextWeighted(homes.Keys.ToList(),
				x => (HomeLookWeight + homes[x] + seen.GetValueOrDefault(x)) / (1 + (distances[x].Minutes / m_settings.Socializing.NearMinutes)));
		}
		else
		{
			chosen = ResolvePlace(person, null, place, distances, isEstimate: false);
		}

		return chosen is null ? null : new SeekChoice(null, chosen, SeekClue.People);
	}

	/// <summary>
	/// Chooses which friend a "seek" step looks for (among those who meet its condition, if it has one) and where, from
	/// clues. Returns null if there's nowhere to look.
	/// </summary>
	/// <summary>
	/// Chooses a friend to look for and where, from clues to where each friend might be: their workplace during their work
	/// hours, or their home otherwise; where they were last seen (or said they were going), for a fresh sighting; and where
	/// they might be gathering with others for a need only met on some days, such as market day. Places close by, and
	/// friends not talked with lately, are more likely. The place they're in now is never chosen. Returns null if there's
	/// nowhere to look.
	/// </summary>
	private SeekChoice? ChooseWhereToSeek(Person person, IReadOnlyDictionary<Location, Route> distances, TimePoint time, bool isCheck = false, StepWho? who = null)
	{
		var socializing = m_settings.Socializing;
		var clues = socializing.Clues;
		var candidates = new List<(SeekChoice Choice, double Weight)>();
		foreach (var friend in GetFriends(person).Where(x => IsWho(person, x, who)))
		{
			var lastTalked = person.GetRelationship(friend).LastTalked;
			var hours = lastTalked is { } last ? m_calendar.GetTotalMinutes(time - last) / 60.0 : double.PositiveInfinity;
			var recency = double.IsPositiveInfinity(hours) ? 1 : hours / (hours + socializing.RecentHours);

			void Add(Location? place, SeekClue clue, double weight)
			{
				if (place is not null && place != person.Location && weight > 0 && distances.TryGetValue(place, out var route))
					candidates.Add((new SeekChoice(friend, place, clue), weight * recency / (1 + (route.Minutes / socializing.NearMinutes))));
			}

			var isWorking = m_settings.Needs.Any(x => x.IsWork && HasNeed(friend, x) && IsQuotaOpen(friend, x, time));
			if (isWorking)
				Add(friend.Workplace, SeekClue.Workplace, clues.Workplace);
			else
				Add(friend.Home, SeekClue.Home, clues.Home);

			if (person.Sightings.TryGetValue(friend, out var sighting))
			{
				var age = m_calendar.GetTotalMinutes(time - sighting.Time) / 60.0;
				Add(sighting.Plan?.Destination ?? sighting.Destination ?? sighting.Location, SeekClue.Sighting, clues.Sighting * Math.Max(1 - (age / m_settings.SightingHours), 0));
			}

			foreach (var need in m_settings.Needs.Where(x => x.Days is not null && x.MainPlace is { Kind: NeedPlace.Quarter } && HasNeed(friend, x) && IsQuotaOpen(friend, x, time) && GetRemainingMinutes(friend, x, time) > 0))
				Add(FindPlace(friend, NeedPlace.Quarter, need.QuarterKinds, distances, need.Nearest), SeekClue.Gathering, clues.Gathering);
		}

		return candidates.Count == 0 ? null : isCheck ? candidates[0].Choice : m_world.Random.NextWeighted(candidates, x => x.Weight).Choice;
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
			.Where(x => x.IsWork && x.Kind == NeedKind.Quota && HasNeed(person, x))
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
		var sleepRate = person.GetSatisfyPerHour(sleep);
		var woken = minutes;
		for (var minute = WakeCheckMinutes; minute < woken && wakers.Count != 0; minute += WakeCheckMinutes)
		{
			var hour = m_calendar.GetParts(m_calendar.AddMinutes(time, minute)).Hour;
			var sleepUrgency = sleep.GetLevelUrgency(Math.Max(sleepLevel - (sleepRate * minute / 60), 0)) * sleepFactor;
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
	/// Whether someone still has work to do today: a need met at their workplace that can be met today and isn't done.
	/// </summary>
	internal bool HasWorkToday(Person person, TimePoint time) =>
		m_settings.Needs.Any(x => x.IsWork && x.Kind == NeedKind.Quota && HasNeed(person, x) && IsQuotaDayOpen(person, x, time) && GetRemainingMinutes(person, x, time) > 0);

	/// <summary>
	/// Whether someone is within their work hours on a day they work.
	/// </summary>
	internal bool IsInWorkHours(Person person, TimePoint time) =>
		m_settings.Needs.Any(x => x.IsWork && x.Kind == NeedKind.Quota && HasNeed(person, x) && IsQuotaOpen(person, x, time));

	/// <summary>
	/// Gets how urgent a need is for a person right now, from 0 to <see cref="Need.MaximumLevel"/>. A level need grows
	/// more urgent as its level rises, within its hours. A quota need is urgent once the spare time left to meet it this
	/// day or week (the time left in its hours, less the time it still needs and the walking each shift or run of it
	/// takes) drops below its lead, and fully urgent when
	/// there's no spare time left. A weekly need whose days have passed with its quota unmet is overdue, and has its overdue
	/// urgency on any day for the rest of the week. A need the person doesn't have, or can't meet now, isn't urgent. The
	/// urgency is then multiplied by the urgency factors that apply to the person, so it can be more than
	/// <see cref="Need.MaximumLevel"/>.
	/// </summary>
	public double GetUrgency(Person person, Need need, TimePoint time) => GetBaseUrgency(person, need, time) * GetUrgencyFactor(person, need);

	/// <summary>
	/// Whether someone can spare some minutes from now, such as to go with someone who asked them: none of their needs
	/// would be at least the agree urgency by the end of them.
	/// </summary>
	internal bool CanSpare(Person person, int minutes, TimePoint time) => CanSpareAt(person, time, minutes, time);

	/// <summary>
	/// Whether someone could spare some minutes from a time, now or later: none of their needs would be at least the agree
	/// urgency by the end of them (see <see cref="GetUrgencyAt"/>).
	/// </summary>
	private bool CanSpareAt(Person person, TimePoint at, int minutes, TimePoint now) => GetNeedsTooUrgentAt(person, at, minutes, now).Count == 0;

	/// <summary>
	/// Gets the needs of someone's that would be at least the agree urgency if they spent some minutes from a time, now or
	/// later, on something else (see <see cref="GetUrgencyAt"/>).
	/// </summary>
	private IReadOnlyList<Need> GetNeedsTooUrgentAt(Person person, TimePoint at, int minutes, TimePoint now)
	{
		person.UpdateNeeds(now);
		return m_settings.Needs.Where(x => GetUrgencyAt(person, x, now, at, minutes) >= m_settings.Talking.AgreeUrgency).ToList();
	}

	/// <summary>
	/// Whether the player would be too shy to say yes to the question someone is asking them: it would make an appointment
	/// they'd be too shy for (see <see cref="IsTooShyFor"/>), at the place and time it would most likely have.
	/// </summary>
	internal bool IsPlayerTooShyToAccept(Person asker, Person player, TimePoint time) =>
		asker.Current is { CurrentStep: { Kind: StepKind.Ask, Appointment: { } settings } step } sequence &&
		FindAppointmentTime(asker, sequence, player, sequence.Need, sequence.Step, settings, 0, GetTalkMinutes(asker, sequence, step), time, isEstimate: true) is { } found &&
		IsTooShyFor(player, asker, sequence.Need, sequence.Step, found.Place, found.Time, isWorker: false);

	/// <summary>
	/// Gets the needs of the player's that saying yes to the question someone is asking them would keep them from: those
	/// that would be at least the agree urgency by the end of the time it takes them, now, or at the appointment it would
	/// make, as anyone else's yes is checked. The player can still say yes.
	/// </summary>
	internal IReadOnlyList<Need> GetPlayerAskConflicts(Person asker, Person player, TimePoint time)
	{
		var conflicts = new List<Need>();
		if (asker.Current is { CurrentStep: { Kind: StepKind.Ask } step } sequence)
		{
			var goal = sequence.Need;
			var forMinutes = step.ForMinutes is { } amount ? (int) amount.GetAmount(goal.SharedByFamily ? asker.Family.Members.Count : 0) : 0;
			if (step.Appointment is { } settings)
			{
				if (FindAppointmentTime(asker, sequence, player, goal, sequence.Step, settings, forMinutes, GetTalkMinutes(asker, sequence, step), time, isEstimate: true) is { } found)
					conflicts.AddRange(GetNeedsTooUrgentAt(player, found.Time, found.ClientMinutes, time));
			}
			else if (forMinutes > 0)
			{
				conflicts.AddRange(GetNeedsTooUrgentAt(player, time, forMinutes, time));
			}
		}

		return conflicts;
	}

	/// <summary>
	/// Gets how urgent a need would be for someone after they spent some minutes from a time (now or later) on something
	/// else. A level need rises for that long, from where it is now if the time is within the hour, or otherwise from
	/// nothing, since they'd see to it in the meantime. A quota need has that much less of its hours left from that time,
	/// with as much still to do as now (as if they did none of it in the meantime), or all of it in a later day or week.
	/// It's not urgent if it's closed both when they start and when they're done.
	/// </summary>
	private double GetUrgencyAt(Person person, Need need, TimePoint now, TimePoint at, int busyMinutes)
	{
		if (!HasNeed(person, need))
			return 0;

		var end = m_calendar.AddMinutes(at, busyMinutes);
		double urgency;
		if (need.Kind == NeedKind.Level)
		{
			var level = Math.Min((m_calendar.GetTotalMinutes(at - now) < 60 ? person.GetLevel(need) : 0) + (need.PerHour!.Value * busyMinutes / 60.0), Need.MaximumLevel);
			urgency = Math.Max(need.IsWithinHours(m_calendar.GetParts(end).Hour) ? need.GetLevelUrgency(level) : 0, need == m_sleepNeed ? GetBedtimeUrgency(person, end) : 0);
		}
		else
		{
			var isSamePeriod = need.Period == NeedPeriod.Day ? m_calendar.GetStartOfDay(at) == m_calendar.GetStartOfDay(now) : m_calendar.GetStartOfWeek(at) == m_calendar.GetStartOfWeek(now);
			var remaining = isSamePeriod ? GetRemainingMinutes(person, need, now) : need.GetQuotaMinutes(need.SharedByFamily ? person.Family.Members.Count : 1);
			var available = Math.Max(GetAvailableMinutes(person, need, at) - busyMinutes, 0);
			urgency = remaining <= 0 || !(IsQuotaOpen(person, need, at) || IsQuotaOpen(person, need, end)) ? 0 : GetQuotaUrgency(person, need, remaining, available, now);
		}

		return urgency * GetUrgencyFactor(person, need);
	}

	/// <summary>
	/// Makes an appointment for a worker and a client who said yes, in the same conversation: first to meet right away,
	/// as soon as they're done talking and both could set off and get to the place, and failing that, at the earliest
	/// later time on the hour or half hour. Either way, it must be within the appointment's hours (all of it) and how far
	/// ahead it looks, neither can have another appointment within its gap, and the client must be able to spare the time
	/// it takes them (the walk there, and the appointment's steps, or the "ask" step's minutes for, if it has them). The
	/// worker can be the player. Returns null if there's no such time, or either of them would be too shy for it (see
	/// <see cref="IsTooShyFor"/>), when who would be is given.
	/// </summary>
	private Appointment? MakeAppointment(Person worker, StepSequence sequence, Person client, Need goal, int askStep, StepAppointment settings, int forMinutes, int askMinutes, TimePoint time, out Person? shy)
	{
		var found = FindAppointmentTime(worker, sequence, client, goal, askStep, settings, forMinutes, askMinutes, time);
		shy = found is not { } at ? null :
			IsTooShyFor(worker, client, goal, askStep, at.Place, at.Time, isWorker: true) ? worker :
			IsTooShyFor(client, worker, goal, askStep, at.Place, at.Time, isWorker: false) ? client :
			null;
		return found is { } place && shy is null ? m_world.AddAppointment(worker, client, goal, askStep, place.Place, place.Time) : null;
	}

	/// <summary>
	/// Whether someone would be too shy for the appointment an "ask" step would make: at its place and time, with only the
	/// other there, they wouldn't be comfortable in the outfit of a "dress" step of its steps that they change for (every one
	/// for the worker, and those with the partner changing too for the client), on that step.
	/// </summary>
	internal bool IsTooShyFor(Person person, Person other, Need goal, int askStep, Location place, TimePoint time, bool isWorker)
	{
		var (start, _, end) = goal.GetAppointmentSteps(askStep)!.Value;
		var gameData = m_world.GameData;
		return Enumerable.Range(start, end - start).Select(x => goal.Steps[x])
			.Where(x => x is { Kind: StepKind.Dress, Outfit: not null } && (isWorker || x.WithPartner))
			.Any(x => person.GetOutfits(x.Outfit!.Value).FirstOrDefault() is { } outfit &&
				!ClothingRules.IsComfortableIn(gameData, person, outfit, ModestyComfort.Get(gameData, person, place, [(other, false)], x, time)));
	}

	/// <summary>
	/// Finds where and when the appointment an "ask" step would make would be (see <see cref="MakeAppointment"/>), with how
	/// long it takes the client, or null if there's no such time. For an estimate, a place among quarters is the nearest
	/// rather than one chosen at random.
	/// </summary>
	private (Location Place, TimePoint Time, int ClientMinutes)? FindAppointmentTime(Person worker, StepSequence sequence, Person client, Need goal, int askStep, StepAppointment settings, int forMinutes,
		int askMinutes, TimePoint time, bool isEstimate = false)
	{
		var place = ResolvePlace(worker, sequence, settings.Place!, GetDistances(worker.Location), isEstimate);
		var workerTravel = place is null ? int.MaxValue : GetTravelMinutesTo(worker, place);
		var clientTravel = place is null ? int.MaxValue : GetTravelMinutesTo(client, place);
		(Location, TimePoint, int)? found = null;
		if (place is not null && workerTravel != int.MaxValue && clientTravel != int.MaxValue)
		{
			var minutes = GetAppointmentMinutes(worker, goal, askStep);
			var gap = settings.Gap?.Minutes ?? 0;
			var clientMinutes = (forMinutes > 0 ? forMinutes : minutes) + clientTravel;
			var setOff = Math.Max(EstimateSetOffMinutes(worker, goal, askStep, isWorker: true, place, time), EstimateSetOffMinutes(client, goal, askStep, isWorker: false, place, time));
			var soonest = m_calendar.AddMinutes(time, askMinutes + setOff);
			var latest = m_calendar.AddMinutes(time, settings.WithinHours * 60);
			IEnumerable<TimePoint> GetTimes()
			{
				yield return soonest;
				var parts = m_calendar.GetParts(soonest);
				for (var later = m_calendar.AddMinutes(soonest, 30 - (parts.Minute % 30)); later <= latest; later = m_calendar.AddMinutes(later, 30))
					yield return later;
			}

			bool IsFit(TimePoint at) =>
				at <= latest &&
				(settings.Hours is not { } hours || (hours.Contains(m_calendar.GetParts(at).Hour) && hours.Contains(m_calendar.GetParts(m_calendar.AddMinutes(at, minutes - 1)).Hour))) &&
				!m_world.Appointments.Any(x => (x.Worker == worker || x.Client == worker || x.Worker == client || x.Client == client) &&
					Math.Abs(m_calendar.GetTotalMinutes(x.Time - at)) < minutes + gap) &&
				(!m_world.IsAutomatic(client) || CanSpareAt(client, at, clientMinutes, time));

			if (GetTimes().Cast<TimePoint?>().FirstOrDefault(x => IsFit(x!)) is { } chosen)
				found = (place, chosen, clientMinutes);
		}

		return found;
	}

	/// <summary>
	/// Gets how long the steps of the appointment an "ask" step makes take, once both are there: the minutes of the "do"
	/// and "wait" steps after its "meet" step.
	/// </summary>
	private static int GetAppointmentMinutes(Person worker, Need goal, int askStep)
	{
		var (_, meet, end) = goal.GetAppointmentSteps(askStep)!.Value;
		var members = goal.SharedByFamily ? worker.Family.Members.Count : 0;
		return Math.Max(Enumerable.Range(meet + 1, end - meet - 1).Select(x => goal.Steps[x]).Where(x => x.Kind is StepKind.Do or StepKind.Wait && x.Minutes is not null)
			.Sum(x => (int) x.Minutes!.GetAmount(members)), 1);
	}

	/// <summary>
	/// Estimates how many minutes before an appointment someone sets off for it, from where they are, with conditions as
	/// they are now: changing out of clothes they won't need, for the worker, the steps before its "meet" step (the walking
	/// they do and changing clothes, the longest of the ways they could go through them), then the walk there, and how early
	/// its "meet" step arrives.
	/// </summary>
	private int EstimateSetOffMinutes(Person person, Need goal, int askStep, bool isWorker, Location place, TimePoint time)
	{
		var (start, meet, _) = goal.GetAppointmentSteps(askStep)!.Value;
		var changeMinutes = m_settings.Clothing.ChangeMinutes;
		var changing = GetChangingOut(person, isWorker ? goal : null, time).Count * changeMinutes + EstimateFinishingUpMinutes(person, isWorker ? goal : null, time);

		// The worker carries out the steps before its "meet" step first, which could go more than one way, so they set off in
		// time for the longest.
		int GetTravel(Location from) => GetDistances(from).TryGetValue(place, out var route) ? Math.Min(route.Minutes, MinutesPerDay) : MinutesPerDay;
		var longest = isWorker ? WalkSteps(person, goal, start, meet, isCountingChanges: true, time).Max(x => x.Total + GetTravel(x.End)) : GetTravel(person.Location);
		return changing + longest + (goal.Steps[meet].Early?.Minutes ?? 0);
	}

	/// <summary>
	/// Estimates how long someone would take finishing up what they're doing when they set off for an appointment, if
	/// setting off gives it up (it's for another need than the one given): the walking and changing of the "finally" steps
	/// of the lists of steps they're in.
	/// </summary>
	private int EstimateFinishingUpMinutes(Person person, Need? goal, TimePoint time)
	{
		if (person.Current is not { Role: SequenceRole.Steps, IsDone: false, IsUnwinding: false } current || current.Need == goal)
			return 0;

		var need = current.Need;
		return need.GetBlocksLeft(current.Step, need.Steps.Count)
			.Sum(block => WalkSteps(person, need, block.FinallyStart, block.End, isCountingChanges: true, time).Select(x => x.Total).DefaultIfEmpty(0).Max());
	}

	/// <summary>
	/// Decides whether someone sets off for an appointment when it's time to: someone the game decides for always does,
	/// since they agreed to it. For the player, their sequence for it is due, and they're asked whether to set off or call
	/// it off.
	/// </summary>
	private void DecideSetOff(StepSequence sequence, Person person, TimePoint time)
	{
		if (m_world.IsAutomatic(person))
			SetOff(sequence, person, time);
		else
			person.AskToSetOff(sequence);
	}

	/// <summary>
	/// Finds the next time someone should set off for an appointment whose sequence is waiting for them to set off.
	/// </summary>
	private (StepSequence Sequence, Person Person, TimePoint Time)? GetNextSetOff(TimePoint time)
	{
		(StepSequence Sequence, Person Person, TimePoint Time)? next = null;
		foreach (var appointment in m_world.Appointments)
		{
			foreach (var person in new[] { appointment.Worker, appointment.Client })
			{
				if (appointment.GetSequence(person) is { SetOff: SetOffState.Waiting } sequence)
				{
					var setOff = GetSetOffTime(person, appointment, time);
					if (next is null || setOff < next.Value.Time)
						next = (sequence, person, setOff);
				}
			}
		}

		return next;
	}

	/// <summary>
	/// Gets when someone should set off for an appointment: as long before it as they'd take to set off and get there from
	/// where they are (see <see cref="EstimateSetOffMinutes"/>).
	/// </summary>
	internal TimePoint GetSetOffTime(Person person, Appointment appointment, TimePoint time) =>
		appointment.Time - m_calendar.CreateTimeOffset(0, 0, EstimateSetOffMinutes(person, appointment.Need, appointment.Step, person == appointment.Worker, appointment.Place, time));

	/// <summary>
	/// Has someone set off for an appointment their sequence is for: what they were doing is put on hold if it's for the
	/// same need (as for a worker whose appointment is part of their work), or otherwise fails, as interrupted, and the
	/// client lets go of anyone following them. The sequence becomes their current one, carried on from where they are.
	/// </summary>
	internal void TakeUpAppointment(StepSequence sequence, Person person, TimePoint time)
	{
		var appointment = sequence.Appointment!;
		person.UpdateNeeds(time);
		var isWorker = person == appointment.Worker;
		person.IsAtWork = false;
		person.SetTasks([]);
		// Steps put on hold carry on from the step they were on, which counts as carried out if it was. A worker's own steps
		// put on hold for the appointment carry on from wherever they are once it's done (see EndSequence), so going to it
		// isn't leaving them.
		var interrupted = person.Current;
		if (interrupted is not null)
		{
			if (!isWorker || interrupted.Need != appointment.Need)
			{
				interrupted.IsCarriedOut = false;
				FailStep(person, interrupted, FailCause.Interrupted, time);
			}
			else
			{
				interrupted.Place = null;
			}
		}

		if (!isWorker)
			Release(person, time);
		person.SetOff(sequence);
		sequence.Place = person.Location;

		// Steps that were given up and are finishing up do that first, on the way.
		if (interrupted is { IsFinishingUp: true } && person.Sequences.Contains(interrupted))
			person.MakeCurrent(interrupted);
	}

	/// <summary>
	/// Has someone the game decides for set off for an appointment (see <see cref="TakeUpAppointment"/>): they change out of
	/// clothes they won't need, as for starting on a goal, and carry on with its steps.
	/// </summary>
	private void SetOff(StepSequence sequence, Person person, TimePoint time)
	{
		var appointment = sequence.Appointment!;
		TakeUpAppointment(sequence, person, time);
		person.SetTasks(GetChangingOut(person, person == appointment.Worker ? appointment.Need : null, time));
		Continue(person, time);
	}

	/// <summary>
	/// Gets when someone at an appointment stops waiting for the other: its "meet" step's minutes past its time.
	/// </summary>
	private TimePoint GetMeetEnd(Appointment appointment)
	{
		var (_, meet, _) = appointment.Need.GetAppointmentSteps(appointment.Step)!.Value;
		var members = appointment.Need.SharedByFamily ? appointment.Worker.Family.Members.Count : 0;
		return m_calendar.AddMinutes(appointment.Time, (int) (appointment.Need.Steps[meet].Minutes?.GetAmount(members) ?? 0));
	}

	/// <summary>
	/// Starts an appointment if one of them asks the other and they say yes. A client the game decides for who said no to
	/// starting it, or was told no, tells the worker once they're ready, and a worker the game decides for asks the client
	/// once they're ready themselves, even before its time. Neither asks someone who said no until they'd ask again (see
	/// <see cref="GetAskAgainTime"/>), nor someone who can't be asked now (see <see cref="CanBeAskedToStart"/>). The player
	/// asks from conversation instead. Anyone the game decides for answers as they are, and the player is asked. Someone
	/// whose "meet" step is being checked carries on with their own steps. Returns whether it started.
	/// </summary>
	internal bool TryStartAppointment(Appointment appointment, TimePoint time, Person? checking = null)
	{
		var (worker, client) = (appointment.Worker, appointment.Client);
		var isStarted = appointment.IsStarted;
		if (!isStarted && appointment.DeclinedBy is not null && IsReadyToAsk(client, worker, appointment, time))
			isStarted = AskToStart(appointment, client, time, checking);
		else if (!isStarted && IsReadyToAsk(worker, client, appointment, time))
			isStarted = AskToStart(appointment, worker, time, checking);
		return isStarted;
	}

	/// <summary>
	/// Whether someone the game decides for would ask the other about starting an appointment now: they're ready to start
	/// it, the other can be asked, and it's time to ask them.
	/// </summary>
	private bool IsReadyToAsk(Person asker, Person asked, Appointment appointment, TimePoint time) =>
		IsDecidedByGame(asker, appointment) && IsReadyToMeet(asker, appointment) && CanBeAskedToStart(asked, appointment) &&
		(appointment.DeclinedBy != asked || GetAskAgainTime(appointment) is not { } again || time >= again);

	/// <summary>
	/// Whether the game decides about starting an appointment for someone: anyone it decides for, and the player while
	/// their steps for it are being done for them.
	/// </summary>
	private bool IsDecidedByGame(Person person, Appointment appointment) =>
		m_world.IsAutomatic(person) || appointment.GetSequence(person) is { Takeover: not StepTakeover.None };

	/// <summary>
	/// Whether someone is ready to start an appointment: they're at its place, having set off for it, on its "meet" step,
	/// which is what they're doing now, and aren't walking on, talking, or asleep.
	/// </summary>
	internal static bool IsReadyToMeet(Person person, Appointment appointment) =>
		person.Location == appointment.Place && appointment.GetSequence(person) is { HasSetOff: true, CurrentStep.Kind: StepKind.Meet } sequence &&
		person.Current == sequence && !person.IsAsleep && person.CurrentTask is not (TravelTask or TalkTask);

	/// <summary>
	/// Whether someone can be asked about starting an appointment now: they're at its place, and aren't walking on, talking,
	/// or asleep, and for the player, they aren't doing anything, or being asked something else.
	/// </summary>
	internal bool CanBeAskedToStart(Person person, Appointment appointment) =>
		person.Location == appointment.Place && !person.IsAsleep && person.CurrentTask is not (TravelTask or TalkTask) &&
		(IsDecidedByGame(person, appointment) || IsPlayerFree(person));

	/// <summary>
	/// Gets when whoever said no to starting an appointment is asked again, while they stay: once the schedules'
	/// "askToStartAgain" has passed, or at its time, if they said no before it and that comes first. Returns null if no one
	/// has said no.
	/// </summary>
	internal TimePoint? GetAskAgainTime(Appointment appointment)
	{
		if (appointment.DeclinedAt is not { } at)
			return null;
		var again = m_calendar.AddMinutes(at, m_settings.AskToStartAgain.Minutes);
		return at < appointment.Time && appointment.Time < again ? appointment.Time : again;
	}

	/// <summary>
	/// Has one of an appointment's two ask the other whether they're ready to start it, or tell them they're ready now. Anyone
	/// the game decides for says yes if they're ready to start it (see <see cref="IsReadyToMeet"/>). The player is asked,
	/// and it waits for their answer (see <see cref="AnswerPlayerStart"/>). Returns whether it started.
	/// </summary>
	private bool AskToStart(Appointment appointment, Person asker, TimePoint time, Person? checking)
	{
		var asked = appointment.GetOther(asker);
		var isDecided = IsDecidedByGame(asked, appointment);
		if (!isDecided)
			asked.StartRequest = appointment;
		return isDecided && AnswerStart(appointment, asked, IsReadyToMeet(asked, appointment), time, checking);
	}

	/// <summary>
	/// Has someone answer whether to start an appointment. A yes starts it (see <see cref="StartAppointment"/>).
	/// A no is remembered, so they aren't asked again for a while, and the other, if the game has them waiting for it, waits
	/// until they'd ask again. Returns whether it started.
	/// </summary>
	private bool AnswerStart(Appointment appointment, Person answering, bool isYes, TimePoint time, Person? checking)
	{
		if (isYes)
		{
			StartAppointment(appointment, time, checking);
		}
		else
		{
			appointment.Decline(answering, time);
			var other = appointment.GetOther(answering);
			if (other != checking && other.CurrentTask is MeetTask meet && meet.Appointment == appointment && IsDecidedByGame(other, appointment))
			{
				other.UpdateNeeds(time);
				other.SetTasks([]);
				Continue(other, time);
			}
		}

		return isYes;
	}

	/// <summary>
	/// Has the player answer whether to start the appointment the other asked them about, or told them they're ready for,
	/// as anyone else would answer (see <see cref="AnswerStart"/>), except that the player decides.
	/// </summary>
	internal void AnswerPlayerStart(Person player, bool isYes, TimePoint time)
	{
		var appointment = player.StartRequest ?? throw new InvalidOperationException("No one is asking the player to start an appointment.");
		player.StartRequest = null;
		if (m_world.Appointments.Contains(appointment) && !appointment.IsStarted)
			AnswerStart(appointment, player, isYes, time, checking: player);
	}

	/// <summary>
	/// Whether the player can ask the other of an appointment about starting it now: it hasn't started, both are at its
	/// place, and the other can be asked. As its client, the player can only tell the worker they're ready once someone has
	/// said no to starting it.
	/// </summary>
	internal bool CanPlayerAskToStart(Person player, Appointment appointment) =>
		!appointment.IsStarted && m_world.Appointments.Contains(appointment) && player.Location == appointment.Place && player.StartRequest is null &&
		(player == appointment.Worker || appointment.DeclinedBy is not null) && CanBeAskedToStart(appointment.GetOther(player), appointment);

	/// <summary>
	/// Has the player ask the other of an appointment whether they're ready to start it, as its worker, or tell them they're
	/// ready now, as its client, from conversation (see <see cref="CanPlayerAskToStart"/>). The other answers as anyone else
	/// would (see <see cref="AskToStart"/>). Returns whether it started.
	/// </summary>
	internal bool PlayerAskToStart(Person player, Appointment appointment, TimePoint time) =>
		CanPlayerAskToStart(player, appointment) && AskToStart(appointment, player, time, checking: player);

	/// <summary>
	/// Starts an appointment, once both are there: the client follows the worker (staying with them, wherever its steps
	/// take them) until the worker is done with its steps, and the worker carries them out, with the client as their
	/// partner. Either of them who hadn't set off for it yet, such as the player starting it early, sets off first. A
	/// worker the game decides for who was waiting carries on; the one whose "meet" step is being checked carries on with
	/// their own steps.
	/// </summary>
	private void StartAppointment(Appointment appointment, TimePoint time, Person? checking)
	{
		var worker = appointment.Worker;
		var client = appointment.Client;
		foreach (var person in new[] { worker, client })
		{
			if (appointment.GetSequence(person) is { HasSetOff: false } waiting)
				TakeUpAppointment(waiting, person, time);
		}

		appointment.IsStarted = true;
		foreach (var person in new[] { worker, client })
		{
			person.UpdateNeeds(time);
			if (person.CurrentTask is MeetTask meet && meet.Appointment == appointment)
				person.FinishTask();
		}

		// The client stays with them for at most as long as the steps take, and the appointment's patience more, in case they
		// never finish, such as a player who goes off to do something else. The player stays with them themselves.
		if (appointment.GetSequence(client) is { } following)
			following.Leader = worker;
		if (m_world.IsAutomatic(client))
		{
			var patience = appointment.Need.Steps[appointment.Step].Appointment?.Patience?.Minutes ?? 0;
			var followMinutes = GetAppointmentMinutes(worker, appointment.Need, appointment.Step) + patience;
			var follow = new FollowTask(worker, followMinutes);
			client.SetTasks([follow]);
			client.StartTask(m_calendar.AddMinutes(time, followMinutes));
			m_world.OnTaskStarted(client, follow, time);
		}

		if (appointment.GetSequence(worker) is { } sequence)
		{
			sequence.Partner = client;
			sequence.Place = appointment.Place;
			if (sequence.Takeover == StepTakeover.Step)
				sequence.Takeover = StepTakeover.None;
		}

		if (m_world.IsAutomatic(worker) && worker != checking)
			Continue(worker, time);

		// The player's "meet" step is done, so as the worker, the step they take next is the one after it.
		if (!m_world.IsAutomatic(worker) && worker != checking && appointment.GetSequence(worker) is { CurrentStep.Kind: StepKind.Meet } meeting)
			AdvanceStep(worker, meeting, time, isPassive: true);
	}

	/// <summary>
	/// Calls off an appointment: each of them gives up their sequence for it, and anyone on the way to it or waiting for it
	/// stops and chooses what to do next. Someone who hadn't set off yet carries on with what they're doing.
	/// </summary>
	internal void CallOff(Appointment appointment, TimePoint time)
	{
		m_world.RemoveAppointment(appointment);
		foreach (var person in new[] { appointment.Worker, appointment.Client })
		{
			if (appointment.GetSequence(person) is { } sequence)
			{
				var isCurrent = person.Current == sequence;
				if (isCurrent)
				{
					person.UpdateNeeds(time);
					person.IsAtWork = false;
					person.SetTasks([]);
				}

				EndSequence(person, sequence, time);
				if (isCurrent && m_world.IsAutomatic(person))
					Continue(person, time);
			}
		}
	}

	/// <summary>
	/// Gets the product of the urgency factors for a need that apply to a person.
	/// </summary>
	private double GetUrgencyFactor(Person person, Need need) =>
		m_settings.UrgencyFactors
			.Where(x => x.Needs.Contains(need.Id, StringComparer.Ordinal) && CharacterCondition.IsMet(x.When, person.GetConditionValueId))
			.Aggregate(1.0, (total, x) => total * x.Factor);

	/// <summary>
	/// Whether a person has a need: they meet its condition, and for a need met at a workplace, they have a business.
	/// </summary>
	public static bool HasNeed(Person person, Need need) =>
		CharacterCondition.IsMet(need.When, person.GetConditionValueId) && (!need.IsWork || person.Business is not null);

	private double GetBaseUrgency(Person person, Need need, TimePoint time)
	{
		if (!HasNeed(person, need))
			return 0;
		if (need.Kind == NeedKind.Level)
			return Math.Max(need.IsWithinHours(m_calendar.GetParts(time).Hour) ? need.GetLevelUrgency(person.GetLevel(need)) : 0, need == m_sleepNeed ? GetBedtimeUrgency(person, time) : 0);

		var remaining = GetRemainingMinutes(person, need, time);
		return remaining <= 0 || !IsQuotaOpen(person, need, time) ? 0 : GetQuotaUrgency(person, need, remaining, GetAvailableMinutes(person, need, time), time);
	}

	/// <summary>
	/// Gets how urgent a quota need is, with so many minutes of it left to do and so many minutes of its hours left to do
	/// them in. The walking each shift or run takes counts too, so people set out in time.
	/// </summary>
	private double GetQuotaUrgency(Person person, Need need, long remaining, long available, TimePoint time)
	{
		var shifts = (remaining + Math.Max(need.GetShiftMinutes(person.Family.Members.Count), 1) - 1) / Math.Max(need.GetShiftMinutes(person.Family.Members.Count), 1);
		// It counts the most walking the steps could take, so people set out in time whichever way they go.
		var spare = available - remaining - (shifts * EstimateTravel(person, need, time).Total.Max);
		return need.OverdueUrgency is { } overdue && available == 0 ? overdue : Need.MaximumLevel * Math.Clamp(1 - ((double) spare / need.Lead!.Value), 0, 1);
	}

	/// <summary>
	/// Finds where a need would be met for a person, from where they are.
	/// </summary>
	internal Location? FindPlace(Person person, Need need) => need.MainPlace is { } place ? FindPlace(person, place.Kind, place.QuarterKinds, GetDistances(person.Location), place.Nearest) : null;

	/// <summary>
	/// Gets how many minutes the quickest way from where a person is to a location takes, at the usual walking speed.
	/// </summary>
	internal int GetTravelMinutesTo(Person person, Location location) =>
		GetDistances(person.Location).TryGetValue(location, out var route) ? route.Minutes : int.MaxValue;

	/// <summary>
	/// Gets how many minutes the quickest way between two locations takes, at the usual walking speed, or
	/// <see cref="int.MaxValue"/> if there's no way.
	/// </summary>
	internal int GetRouteMinutes(Location from, Location to) => GetDistances(from).TryGetValue(to, out var route) ? route.Minutes : int.MaxValue;

	/// <summary>
	/// Gets the location next to the first that the quickest way to the second goes through first, or null if there's no
	/// way, or they're the same.
	/// </summary>
	internal Location? GetNextOnRoute(Location from, Location to) => GetPath(from, to, GetDistances(from)) is [var next, ..] ? next : null;

	/// <summary>
	/// Gets how long it takes a person to walk a path between two locations next to each other. Some people walk more
	/// slowly than others.
	/// </summary>
	public int GetTravelMinutes(Person person, Location from, Location to)
	{
		var factor = m_settings.TravelTimeFactors.Where(x => CharacterCondition.IsMet(x.When, person.GetConditionValueId)).Aggregate(1.0, (total, x) => total * x.Factor);
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
		FollowTask follow => Math.Max(follow.Minutes, 1),
		MeetTask meet => Math.Max(meet.Minutes, 1),
		WaitTask wait => Math.Max(wait.Minutes, 1),
		PrivacyTask privacy => Math.Max(privacy.Minutes, 1),
		TravelTask travel => GetTravelMinutes(person, person.Location, travel.Destination),
		BusyTask { Minutes: { } minutes } => Math.Max(minutes, 1),
		ChangeClothesTask => m_settings.Clothing.ChangeMinutes,
		// A level that's met in a whole number of minutes mustn't take a minute more because of rounding.
		BusyTask when person.Goal is { Kind: NeedKind.Level, SatisfyPerHour: not null } goal =>
			Math.Max((int) Math.Ceiling((person.GetLevel(goal) / person.GetSatisfyPerHour(goal) * 60) - LevelMinutesTolerance), 1),
		_ => 1,
	};

	/// <summary>
	/// How far over a whole number of minutes the time to meet a level can be and still count as that number of minutes.
	/// </summary>
	private const double LevelMinutesTolerance = 1e-6;

	/// <summary>
	/// Gets the change of clothes a "dress" or "undress" step makes for someone, or null if there's none to make: into the
	/// first of their outfits of its kind with every piece at hand, unless they already have one on, or out of the outfit
	/// of its kind they changed into.
	/// </summary>
	private static ChangeClothesTask? GetStepChange(Person person, Step step, TimePoint time)
	{
		var kind = step.Outfit!.Value;
		return step.Kind == StepKind.Dress
			? !ClothingRules.IsWearingKind(person, kind) && ClothingRules.FindAtHand(person, kind, time) is { } outfit ? new ChangeClothesTask(outfit, IsTakingOff: false) : null
			: person.GetOutfits(kind).FirstOrDefault(x => ClothingRules.IsChangedInto(person, x)) is { } worn ? new ChangeClothesTask(worn, IsTakingOff: true) : null;
	}

	/// <summary>
	/// Whether someone would be too shy to make a change of clothes for a step: changing into an outfit they wouldn't be
	/// comfortable in where they are, with the people there, on the step. Changing out of an outfit puts back what it
	/// replaced, so it never is. With only the household, it's whether they'd be too shy with only their own household and
	/// the partner changing with them there, which waiting for others to leave can't change.
	/// </summary>
	private static bool IsTooShyToChange(GameData gameData, Person person, ChangeClothesTask change, Step step, TimePoint time, bool isHouseholdOnly = false, Person? partner = null) =>
		!change.IsTakingOff && !ClothingRules.IsComfortableIn(gameData, person, change.Outfit, ModestyComfort.Get(gameData, person, person.Location,
			person.Location.People.Where(x => x != person && (!isHouseholdOnly || x.Family == person.Family || x == partner)).Select(x => (x, x.IsAsleep)), step, time));

	/// <summary>
	/// Gets how long to spend on a quota need this time: a shift, unless less is left to do or less time is left in today's
	/// hours once the person has walked to its first "do" step, or a run, if its "do" steps fit in today's hours with the
	/// walking up to the last of them, or 0 otherwise. Where the steps could go more than one way, it's whether the quickest
	/// way fits, so people don't turn down what they could do.
	/// </summary>
	private int GetShiftMinutes(Person person, Need need, TimePoint time)
	{
		var members = person.Family.Members.Count;
		var untilEnd = GetMinutesUntilEnd(person, need, time);
		if (need.IsRuns)
			return untilEnd - EstimateTravel(person, need, time).BeforeLastDo.Min >= need.GetRunMinuteRange(members).Min ? need.GetRunMinutes(members) : 0;

		var remaining = GetRemainingMinutes(person, need, time);
		var available = untilEnd - EstimateTravel(person, need, time).BeforeFirstDo.Min;
		return (int) Math.Min(Math.Min(need.GetShiftMinutes(members), remaining), available);
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
		return need.GetQuotaMinutes(people.Count) - people.Sum(x => x.GetProgressMinutes(need)) - people.Where(x => x != person).Sum(x => x.GetPlannedMinutes(need, time));
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
		need.IsWork && person.Business?.Profession.WorkHours is { } hours ? (hours.Start, hours.End) : (need.Start!.Value, need.End!.Value);

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
	internal bool IsQuotaDayOpen(Person person, Need need, TimePoint time) =>
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
		if (place == NeedPlace.OtherHome)
		{
			var homes = m_world.Families.Select(x => x.Home).Where(x => x != person.Home && distances.ContainsKey(x)).Distinct().ToList();
			return homes.Count == 0 ? null : isNearest ? homes.MinBy(x => distances[x].Minutes) : m_world.Random.NextWeighted(homes, x => 1.0 / (1 + distances[x].Minutes));
		}

		m_quarters ??= m_world.Locations.Where(x => x.Template is QuarterTemplate).ToLookup(x => x.QuarterKind ?? "", StringComparer.Ordinal);
		var quarters = m_quarters.SelectMany(x => x.Where(distances.ContainsKey)).ToLookup(x => x.QuarterKind ?? "", StringComparer.Ordinal);
		var choices = (quarterKinds ?? []).Where(x => quarters[x.QuarterKind].Any()).ToList();
		if (choices.Count == 0)
			return null;

		var kind = isNearest ? null : m_world.GameData.FrequencyChances.Choose(m_world.Random, choices, quarterKinds).QuarterKind;
		return kind is null
			? choices.SelectMany(x => quarters[x.QuarterKind]).MinBy(x => distances[x].Minutes)
			: m_world.Random.NextWeighted(quarters[kind].ToList(), x => 1.0 / (1 + distances[x].Minutes));
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
	/// Finds the quickest way to every location from the given one, using the travel time of each path. The map doesn't
	/// change, so the ways from each location are only worked out once.
	/// </summary>
	private Dictionary<Location, Route> GetDistances(Location start)
	{
		if (!m_distances.TryGetValue(start, out var routes))
			m_distances[start] = routes = FindDistances(start);
		return routes;
	}

	private Dictionary<Location, Route> FindDistances(Location start)
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
	/// The most steps someone carries out without any of them taking time, before giving up on them.
	/// </summary>
	private const int MaxStepsWithoutTime = 1000;

	/// <summary>
	/// The most ways through a need's steps an estimate follows, past which it only follows the first option of each
	/// "choose" step.
	/// </summary>
	private const int MaxEstimateWays = 64;

	/// <summary>
	/// How likely a "seek" step is to look in a home where nobody who meets its condition works or was lately seen, compared
	/// to each such person.
	/// </summary>
	private const double HomeLookWeight = 0.25;

	/// <summary>
	/// How long, in minutes, someone who asked the player something waits for an answer. Time stops until the player
	/// answers, so it's only how long they'd wait if it didn't.
	/// </summary>
	private const int AskWaitMinutes = 5;

	/// <summary>
	/// How often, in minutes, waking is checked for while working out how long someone sleeps.
	/// </summary>
	private const int WakeCheckMinutes = 5;

	private readonly GameWorld m_world;
	private readonly GameCalendar m_calendar;
	private readonly ScheduleSettings m_settings;
	private readonly Need? m_socializeNeed;
	private readonly Need? m_sleepNeed;
	private readonly Dictionary<Location, Dictionary<Location, Route>> m_distances = [];

	// The quarters of each kind, which don't change, and the last estimate of each person's walking for each need.
	private ILookup<string, Location>? m_quarters;
	private readonly Dictionary<(Person Person, Need Need), (Location Location, TimePoint Time, TravelEstimate Estimate)> m_estimates = [];
}
