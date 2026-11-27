using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.Town;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.GameState;

/// <summary>
/// How each kind of step is carried out: when it's done, how the game carries it out, and what the player does toward it
/// themselves (see Docs/Steps.md).
/// </summary>
internal sealed partial class Scheduler
{
	/// <summary>
	/// Gets the handler for a step's kind.
	/// </summary>
	private static StepHandler GetHandler(Step step) => s_handlers[step.Kind];

	/// <summary>
	/// Whether the game carries out a step for the player as they go, rather than leaving it to them: steps that only keep
	/// track of things, and decisions the data makes for everyone.
	/// </summary>
	internal bool IsForPlayer(Person person, StepSequence sequence) =>
		sequence.CurrentStep is { } step && GetHandler(step).IsForPlayer(this, new StepContext(person, sequence, step, sequence.Step, m_world.Time));

	/// <summary>
	/// Whether a step of the player's is waiting on them to decide, such as which option of a "choose" step to take, or
	/// whether to keep going.
	/// </summary>
	internal bool IsAskingPlayer(Person person, StepSequence sequence) =>
		sequence.CurrentStep is { } step && GetHandler(step).IsAskingPlayer(this, new StepContext(person, sequence, step, sequence.Step, m_world.Time));

	/// <summary>
	/// Whether a step of a sequence is done now, as its handler checks it, before it's carried out.
	/// </summary>
	internal bool IsStepDone(Person person, StepSequence sequence, int index) =>
		GetHandler(sequence.Need.Steps[index]).Check(this, new StepContext(person, sequence, sequence.Need.Steps[index], index, m_world.Time)).State == StepState.Done;

	/// <summary>
	/// Whether a location is a place for a person's steps: their home or workplace, a quarter of one of the place's kinds,
	/// or where their sequence remembered or its partner's home.
	/// </summary>
	public static bool IsPlaceFor(Person person, StepSequence? sequence, SchedulePlace place, Location location) => place.Kind switch
	{
		NeedPlace.Home => location == person.Home,
		NeedPlace.Workplace => person.Business is not null && location == person.Workplace,
		NeedPlace.Remembered => sequence?.RememberedPlaces.GetValueOrDefault(place.Remembered ?? "") == location,
		NeedPlace.PartnerHome => sequence?.Partner?.Home == location,
		NeedPlace.JobSite => sequence is not null && person.GetJob(sequence.Need) is { IsDone: false } job && job.Place == location,
		NeedPlace.OtherHome => location.IsHouse && location != person.Home,
		_ => location.Template is QuarterTemplate && (place.QuarterKinds ?? []).Any(x => x.QuarterKind == location.QuarterKind),
	};

	/// <summary>
	/// Gets how many minutes a step takes from the time chosen for its sequence, or its own minutes: its minutes, at least
	/// 1, or without them, what's left of the time chosen, and for a step that takes from the time chosen, at most that,
	/// which it uses up. It's cut short at the end of the step's hours, if it has them.
	/// </summary>
	private int? TakeMinutes(StepContext context, FamilyAmount? amount)
	{
		var (person, sequence, step) = (context.Person, context.Sequence, context.Step);
		var goal = sequence.Need;
		var minutes = amount is not null ? Math.Max((int) amount.GetAmount(goal.SharedByFamily ? person.Family.Members.Count : 0), 1) : sequence.MinutesLeft;
		if (goal.TakesChosenTime(step) && sequence.MinutesLeft is { } left)
		{
			minutes = Math.Min(minutes ?? left, left);
			sequence.UseMinutes(minutes.Value);
		}

		if (minutes is { } total && GetMinutesLeftInHours(step, context.Time) is { } inHours && inHours < total)
		{
			sequence.IsCut = true;
			minutes = inHours;
		}

		return minutes;
	}

	/// <summary>
	/// Gets the tasks for time spent on a step, with the person at work through it if the step says.
	/// </summary>
	private static IReadOnlyList<PersonTask> GetBusyTasks(Step step, int? minutes, int index)
	{
		var tasks = new List<PersonTask>();
		if (step.AtWork)
			tasks.Add(new AtWorkTask(true));
		tasks.Add(new BusyTask(minutes, index));
		if (step.AtWork)
			tasks.Add(new AtWorkTask(false));
		return tasks;
	}

	private static readonly IReadOnlyDictionary<StepKind, StepHandler> s_handlers = new Dictionary<StepKind, StepHandler>
	{
		[StepKind.GoTo] = new GoToHandler(),
		[StepKind.Do] = new DoHandler(),
		[StepKind.Take] = new TakeHandler(),
		[StepKind.Put] = new PutHandler(),
		[StepKind.Make] = new MakeHandler(),
		[StepKind.Dress] = new ChangeHandler(),
		[StepKind.Undress] = new ChangeHandler(),
		[StepKind.CountRun] = new CountRunHandler(),
		[StepKind.Repeat] = new RepeatHandler(),
		[StepKind.Choose] = new ChooseHandler(),
		[StepKind.Call] = new PassHandler(),
		[StepKind.Group] = new PassHandler(),
		[StepKind.Stop] = new StopHandler(),
		[StepKind.Remember] = new RememberHandler(),
		[StepKind.Wait] = new WaitHandler(),
		[StepKind.Wander] = new WanderHandler(),
		[StepKind.Seek] = new SeekHandler(),
		[StepKind.Talk] = new TalkHandler(),
		[StepKind.Ask] = new TalkHandler(),
		[StepKind.Lead] = new LeadHandler(),
		[StepKind.Release] = new ReleaseHandler(),
		[StepKind.Meet] = new MeetHandler(),
	};

	/// <summary>
	/// How one kind of step is carried out, the same way for everyone (see Docs/Steps.md).
	/// </summary>
	private abstract class StepHandler
	{
		/// <summary>
		/// Whether the step goes somewhere, so leaving where the steps before it were done doesn't fail it.
		/// </summary>
		public virtual bool IsMoving => false;

		/// <summary>
		/// Checks whether the step is done already, before it's carried out, such as being where a "goTo" step goes.
		/// </summary>
		public virtual StepCheck Check(Scheduler scheduler, StepContext context) => StepCheck.NotDone;

		/// <summary>
		/// Carries the step out, as the game does for anyone it decides for.
		/// </summary>
		public abstract StepWork CarryOut(Scheduler scheduler, StepContext context);

		/// <summary>
		/// Checks the step once the tasks the game gave for it are done: done, failed, or not done yet, to be carried out
		/// again. By default, a step that checks something is done if it now holds, or fails; any other is done.
		/// </summary>
		public virtual StepCheck AfterTasks(Scheduler scheduler, StepContext context) => StepCheck.Done;

		/// <summary>
		/// Whether the game carries the step out for the player as they go, since it's one the player doesn't take
		/// themselves: a step that only keeps track of things, or a decision the data makes.
		/// </summary>
		public virtual bool IsForPlayer(Scheduler scheduler, StepContext context) => false;

		/// <summary>
		/// Whether the step waits on the player to decide something, even while the rest of their steps are done for them.
		/// </summary>
		public virtual bool IsAskingPlayer(Scheduler scheduler, StepContext context) => false;
	}

	/// <summary>
	/// A step that has nothing to do, such as a "call" step, which isn't laid out among the steps.
	/// </summary>
	private sealed class PassHandler : StepHandler
	{
		public override StepWork CarryOut(Scheduler scheduler, StepContext context) => StepWork.Done;

		public override bool IsForPlayer(Scheduler scheduler, StepContext context) => true;
	}

	/// <summary>
	/// "goTo": done once the person is at a place like it. The game walks there the quickest way. The player walks there
	/// themselves.
	/// </summary>
	private sealed class GoToHandler : StepHandler
	{
		public override bool IsMoving => true;

		public override StepCheck Check(Scheduler scheduler, StepContext context) =>
			IsPlaceFor(context.Person, context.Sequence, context.Step.Place!, context.Person.Location) ? StepCheck.Done : StepCheck.NotDone;

		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var person = context.Person;
			var distances = scheduler.GetDistances(person.Location);
			return scheduler.ResolvePlace(person, context.Sequence, context.Step.Place!, distances, isEstimate: false) is { } place && distances.ContainsKey(place)
				? StepWork.Give(GetPath(person.Location, place, distances).Select(PersonTask (x) => new TravelTask(x)).ToList())
				: StepWork.Fail(FailCause.Unreachable);
		}

		public override StepCheck AfterTasks(Scheduler scheduler, StepContext context) =>
			Check(scheduler, context).State == StepState.Done ? StepCheck.Done : StepCheck.Fail(FailCause.Unreachable);
	}

	/// <summary>
	/// "meet": going to an appointment's place and waiting there for the other, done once the appointment starts, which it
	/// does once one asks the other and they say yes (see <see cref="TryStartAppointment"/>), even before its time. The game
	/// walks there and waits, until its time and then for the other, or until it would ask again someone who said no. The
	/// player walks there themselves, and waits by being there, or has it done for them. Once the wait is over, it fails as
	/// "no" if someone said no to starting it and they're still there, or otherwise as "noShow".
	/// </summary>
	private sealed class MeetHandler : StepHandler
	{
		public override bool IsMoving => true;

		public override StepCheck Check(Scheduler scheduler, StepContext context)
		{
			var (person, time) = (context.Person, context.Time);
			return context.Sequence.Appointment is not { } appointment ? StepCheck.Fail(FailCause.NoShow) :
				appointment.IsStarted || (person.Location == appointment.Place && scheduler.TryStartAppointment(appointment, time, person)) ? StepCheck.Done :
				time < scheduler.GetMeetEnd(appointment) ? StepCheck.NotDone :
				appointment.DeclinedBy is { } declined ? StepCheck.Fail(FailCause.No, declined) :
				StepCheck.Fail(FailCause.NoShow);
		}

		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var (person, appointment, time) = (context.Person, context.Sequence.Appointment!, context.Time);
			var distances = scheduler.GetDistances(person.Location);
			var waitEnd = time < appointment.Time ? appointment.Time : scheduler.GetMeetEnd(appointment);
			if (scheduler.GetAskAgainTime(appointment) is { } again && again > time && again < waitEnd)
				waitEnd = again;
			return person.Location == appointment.Place ? StepWork.Give([new MeetTask(appointment, (int) Math.Max(scheduler.m_calendar.GetTotalMinutes(waitEnd - time), 1))]) :
				distances.ContainsKey(appointment.Place) ? StepWork.Give(GetPath(person.Location, appointment.Place, distances).Select(PersonTask (x) => new TravelTask(x)).ToList()) :
				StepWork.Fail(FailCause.Unreachable);
		}

		public override StepCheck AfterTasks(Scheduler scheduler, StepContext context) => Check(scheduler, context);
	}

	/// <summary>
	/// "do": spending time where the person is. The game spends it. The player spends it with the step's button, or has it
	/// done for them.
	/// </summary>
	private sealed class DoHandler : StepHandler
	{
		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var minutes = scheduler.TakeMinutes(context, context.Step.Minutes);
			return minutes is 0 ? StepWork.Done : StepWork.Give(GetBusyTasks(context.Step, minutes, context.Index));
		}
	}

	/// <summary>
	/// "wait": done once its time is up, counted from when it's reached. The game waits. The player can do anything else
	/// meanwhile.
	/// </summary>
	private sealed class WaitHandler : StepHandler
	{
		public override StepCheck Check(Scheduler scheduler, StepContext context)
		{
			var (sequence, step, time) = (context.Sequence, context.Step, context.Time);
			sequence.Until ??= scheduler.m_calendar.AddMinutes(time, step.Minutes is { } amount ?
				(int) amount.GetAmount(sequence.Need.SharedByFamily ? context.Person.Family.Members.Count : 0) : scheduler.GetMinutesUntilHour(time, step.UntilHour!.Value));
			return time >= sequence.Until ? StepCheck.Done : StepCheck.NotDone;
		}

		public override StepWork CarryOut(Scheduler scheduler, StepContext context) =>
			StepWork.Give([new BusyTask((int) Math.Max(scheduler.m_calendar.GetTotalMinutes(context.Sequence.Until! - context.Time), 1), context.Index)]);

		public override StepCheck AfterTasks(Scheduler scheduler, StepContext context) => Check(scheduler, context);
	}

	/// <summary>
	/// "wander": done once its time is up, counted from when it's reached. The game walks to another quarter of its kinds
	/// (chosen by frequency, with nearer quarters more likely) and stays a while, again and again. The player can go
	/// wherever they like meanwhile.
	/// </summary>
	private sealed class WanderHandler : StepHandler
	{
		public override bool IsMoving => true;

		public override StepCheck Check(Scheduler scheduler, StepContext context)
		{
			var (sequence, step, time) = (context.Sequence, context.Step, context.Time);
			if (sequence.Until is null)
			{
				var total = (int) step.Minutes!.GetAmount(sequence.Need.SharedByFamily ? context.Person.Family.Members.Count : 0);
				if (sequence.Need.TakesChosenTime(step) && sequence.MinutesLeft is { } left)
				{
					total = Math.Min(total, left);
					sequence.UseMinutes(total);
				}

				sequence.Until = scheduler.m_calendar.AddMinutes(time, total);
			}

			return time >= sequence.Until ? StepCheck.Done : StepCheck.NotDone;
		}

		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var (person, step, time) = (context.Person, context.Step, context.Time);
			var world = scheduler.m_world;
			var remaining = (int) scheduler.m_calendar.GetTotalMinutes(context.Sequence.Until! - time);
			var distances = scheduler.GetDistances(person.Location);
			var place = step.Place!;
			var quarters = world.Locations.Where(x => x.Template is QuarterTemplate && x != person.Location && distances.ContainsKey(x)).ToLookup(x => x.QuarterKind ?? "", StringComparer.Ordinal);
			var choices = (place.QuarterKinds ?? []).Where(x => quarters[x.QuarterKind].Any()).ToList();
			var destination = choices.Count == 0 ? null :
				world.Random.NextWeighted(quarters[world.GameData.FrequencyChances.Choose(world.Random, choices, place.QuarterKinds).QuarterKind].ToList(), x => 1.0 / (1 + distances[x].Minutes));
			var tasks = new List<PersonTask>();
			if (destination is not null)
				tasks.AddRange(GetPath(person.Location, destination, distances).Select(PersonTask (x) => new TravelTask(x)));
			var stay = Math.Min(world.Random.NextInRange(step.StopMinutes!), remaining - (destination is null ? 0 : distances[destination].Minutes));
			tasks.Add(new BusyTask(Math.Max(stay, 1), context.Index));
			return StepWork.Give(tasks);
		}

		public override StepCheck AfterTasks(Scheduler scheduler, StepContext context) => Check(scheduler, context);
	}

	/// <summary>
	/// "take": done once the person carries its bundle. The game picks up the things from where they're kept, if the person
	/// is there. The player picks them up themselves (for an outfit, it's done once they carry or wear all of it, which
	/// become the bundle), or with the step's button if it has one.
	/// </summary>
	private sealed class TakeHandler : StepHandler
	{
		public override StepCheck Check(Scheduler scheduler, StepContext context)
		{
			var (person, sequence, step) = (context.Person, context.Sequence, context.Step);
			var isTaken = sequence.Bundles.ContainsKey(step.Bundle!.Id);
			if (!isTaken && !scheduler.m_world.IsAutomatic(person) && step.Outfit is { } kind && person.GetOutfits(kind).FirstOrDefault() is { } outfit &&
				outfit.Pieces.All(x => x.Wearer == person || x.Container == person.Inventory))
			{
				sequence.AddBundle(step.Bundle.Id, scheduler.m_world.CreateBundle(person, step.Bundle.Title!, outfit.Pieces.Where(x => x.Container == person.Inventory)));
				isTaken = true;
			}

			return isTaken ? StepCheck.Done : StepCheck.NotDone;
		}

		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var (person, step) = (context.Person, context.Step);
			if (person.Location != person.Home)
				return StepWork.Fail(FailCause.Missing);

			var pieces = ClothingRules.Take(person, step.Owner ?? StepOwner.Self, step.Outfit, step.Accessories, context.Time);
			context.Sequence.AddBundle(step.Bundle!.Id, scheduler.m_world.CreateBundle(person, step.Bundle.Title!, pieces));
			return StepWork.Done;
		}
	}

	/// <summary>
	/// "put": done once the person no longer carries its bundle. The game puts its things where they're kept, if the person
	/// is there, or for a bundle a "make" step made, is done with it. The player has it done as they go, or with the step's
	/// button if it has one.
	/// </summary>
	private sealed class PutHandler : StepHandler
	{
		public override StepCheck Check(Scheduler scheduler, StepContext context) =>
			context.Sequence.Bundles.ContainsKey(context.Step.Bundle!.Id) ? StepCheck.NotDone : StepCheck.Done;

		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var (person, sequence, step) = (context.Person, context.Sequence, context.Step);
			if (step.ItemKind is not null && person.Location != person.Home)
				return StepWork.Fail(FailCause.Missing);

			var bundle = sequence.RemoveBundle(step.Bundle!.Id)!;
			sequence.KeptSince = null;
			var world = scheduler.m_world;
			if (step.ItemKind is not null)
				ClothingRules.Put(person, bundle.Items, step.AfterDelay is { } delay ? scheduler.m_calendar.AddMinutes(context.Time, delay.Minutes) : null, step.DelayActivity);
			world.Unpack(bundle);
			return StepWork.Done;
		}

		public override bool IsForPlayer(Scheduler scheduler, StepContext context) => context.Step.Player?.Title is null;
	}

	/// <summary>
	/// "make": makes a bundle, in place of another one if it says, for everyone as they go.
	/// </summary>
	private sealed class MakeHandler : StepHandler
	{
		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var (person, sequence, step) = (context.Person, context.Sequence, context.Step);
			var world = scheduler.m_world;

			// The bundle made in place of another carries its things, such as the washing once it's washed.
			var items = new List<Item>();
			if (step.Replaces is { } replaced && sequence.RemoveBundle(replaced) is { } old)
			{
				items.AddRange(old.Items);
				world.Unpack(old);
			}

			if (sequence.RemoveBundle(step.Bundle!.Id) is { } existing)
				world.Unpack(existing);
			sequence.AddBundle(step.Bundle.Id, world.CreateBundle(person, step.Bundle.Title!, items));
			return StepWork.Done;
		}

		public override bool IsForPlayer(Scheduler scheduler, StepContext context) => true;
	}

	/// <summary>
	/// "dress" and "undress": done once the person has on an outfit of the kind, or has taken it off. The game changes, and
	/// the partner changes too, if the step says (the player's clothes are changed for them). While either of them wouldn't
	/// be comfortable in the outfit they'd change into, they wait for privacy, a few minutes at a time, and it fails as "shy"
	/// once they've waited the most they will. The player changes themselves.
	/// </summary>
	private sealed class ChangeHandler : StepHandler
	{
		public override StepCheck Check(Scheduler scheduler, StepContext context)
		{
			var (person, step) = (context.Person, context.Step);
			var isDone = step.Kind == StepKind.Dress ? ClothingRules.IsWearingKind(person, step.Outfit!.Value) : !person.GetOutfits(step.Outfit!.Value).Any(x => ClothingRules.IsChangedInto(person, x));
			return isDone ? StepCheck.Done : StepCheck.NotDone;
		}

		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var (person, sequence, step, time) = (context.Person, context.Sequence, context.Step, context.Time);
			if (GetStepChange(person, step, time) is not { } change)
				return StepWork.Fail(FailCause.Missing);

			// The partner changes at the same time, if they're there and can, such as a friend going swimming with them. No one
			// changes into clothes they wouldn't be comfortable in there and then, on the step.
			var world = scheduler.m_world;
			var changing = step.WithPartner && sequence.Partner is { } partner && partner.Location == person.Location && !partner.IsAsleep && partner.CurrentTask is not TalkTask ? partner : null;
			var partnerChange = changing is null ? null : GetStepChange(changing, step, time);
			if (IsTooShyToChange(world.GameData, person, change, step, time) || (partnerChange is not null && IsTooShyToChange(world.GameData, changing!, partnerChange, step, time)))
			{
				// They wait a while for whoever makes them uncomfortable to leave, unless it's their own household, who won't.
				var coverUp = world.GameData.ModestySettings.CoverUp;
				var isWaitingUseless = IsTooShyToChange(world.GameData, person, change, step, time, isHouseholdOnly: true, partner: partnerChange is null ? null : changing) ||
					(partnerChange is not null && IsTooShyToChange(world.GameData, changing!, partnerChange, step, time, isHouseholdOnly: true, partner: person));
				sequence.Until ??= scheduler.m_calendar.AddMinutes(time, coverUp.WaitMinutes);
				return time < sequence.Until && !isWaitingUseless ? StepWork.Give([new PrivacyTask(coverUp.CheckMinutes)]) :
					StepWork.Fail(FailCause.Shy, IsTooShyToChange(world.GameData, person, change, step, time) ? person : changing);
			}

			// The player's clothes are changed for them, at once.
			if (partnerChange is not null)
			{
				if (world.IsAutomatic(changing!))
				{
					changing!.Interrupt(partnerChange, time, scheduler.m_calendar.AddMinutes(time, scheduler.GetMinutes(changing, partnerChange)));
					world.OnTaskStarted(changing, partnerChange, time);
				}
				else
				{
					ClothingRules.Change(changing!, partnerChange.Outfit, partnerChange.IsTakingOff, time);
				}
			}

			return StepWork.Give([change]);
		}

		public override StepCheck AfterTasks(Scheduler scheduler, StepContext context) =>
			Check(scheduler, context).State == StepState.Done ? StepCheck.Done : context.Sequence.Until is not null ? StepCheck.NotDone : StepCheck.Fail(FailCause.Missing);
	}

	/// <summary>
	/// "countRun": counts a run, for everyone as they go.
	/// </summary>
	private sealed class CountRunHandler : StepHandler
	{
		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var (person, goal) = (context.Person, context.Sequence.Need);
			person.SetProgress(goal, person.GetProgress(goal) + 1);
			return StepWork.Done;
		}

		public override bool IsForPlayer(Scheduler scheduler, StepContext context) => true;
	}

	/// <summary>
	/// "remember": remembers where the person is, for everyone as they go.
	/// </summary>
	private sealed class RememberHandler : StepHandler
	{
		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			context.Sequence.RememberPlace(context.Step.Name!, context.Person.Location);
			return StepWork.Done;
		}

		public override bool IsForPlayer(Scheduler scheduler, StepContext context) => true;
	}

	/// <summary>
	/// "stop": goes to the end of the steps, for everyone as they go.
	/// </summary>
	private sealed class StopHandler : StepHandler
	{
		public override StepWork CarryOut(Scheduler scheduler, StepContext context) => StepWork.GoToStep(context.Sequence.Need.Steps.Count);

		public override bool IsForPlayer(Scheduler scheduler, StepContext context) => true;
	}

	/// <summary>
	/// "group": isn't laid out among the steps, only the steps it carries out, as for a "call" step.
	/// </summary>

	/// <summary>
	/// "repeat": after a pass of its steps, goes back to the first of them while there's time left of the time chosen and
	/// the need can still be met. Someone the game decides for stops when something else is more urgent, if it reconsiders.
	/// The player is asked whether to keep going instead.
	/// </summary>
	private sealed class RepeatHandler : StepHandler
	{
		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var (person, sequence, time) = (context.Person, context.Sequence, context.Time);
			var isGoingOn = scheduler.ShouldRepeat(person, sequence, time) &&
				!(context.Step.Reconsider && scheduler.m_world.IsAutomatic(person) && scheduler.GetMoreUrgentNeed(person, sequence.Need, time) is not null);
			return isGoingOn ? StepWork.GoToStep(sequence.Need.GetLoopStart(context.Index)) : StepWork.Done;
		}

		public override bool IsForPlayer(Scheduler scheduler, StepContext context) => !IsAskingPlayer(scheduler, context);

		public override bool IsAskingPlayer(Scheduler scheduler, StepContext context) =>
			context.Step.Reconsider && !scheduler.m_world.IsAutomatic(context.Person) && scheduler.ShouldRepeat(context.Person, context.Sequence, context.Time);
	}

	/// <summary>
	/// "choose": goes on with one of its options. The game chooses by frequency among those that can be chosen. The player
	/// chooses any of them themselves. With "first", it's the first whose condition is met, for everyone, and if there's
	/// none, it's passed.
	/// </summary>
	private sealed class ChooseHandler : StepHandler
	{
		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var (person, sequence, step, index) = (context.Person, context.Sequence, context.Step, context.Index);
			return scheduler.ChooseOption(person, sequence, sequence.Need, index, context.Time) is { } option ? StepWork.GoToStep(GetOptionStart(sequence.Need, index, option)) :
				step.First ? StepWork.Done :
				StepWork.Fail(FailCause.NoOption);
		}

		public override bool IsForPlayer(Scheduler scheduler, StepContext context) => context.Step.First;

		public override bool IsAskingPlayer(Scheduler scheduler, StepContext context) => !context.Step.First && !scheduler.m_world.IsAutomatic(context.Person);
	}

	/// <summary>
	/// "seek": looking for someone. The game chooses where to look, walks there, and looks around a while. The player looks
	/// for them themselves, and it's done once someone it looks for is where they are.
	/// </summary>
	private sealed class SeekHandler : StepHandler
	{
		public override bool IsMoving => true;

		public override StepCheck Check(Scheduler scheduler, StepContext context)
		{
			var (person, sequence, step) = (context.Person, context.Sequence, context.Step);
			var found = scheduler.m_world.IsAutomatic(person) ? null : person.Location.People.FirstOrDefault(x => scheduler.IsSought(person, x, step.Who));
			if (found is not null)
				sequence.Seeking = new SeekChoice(step.Who is { Friends: true } ? found : null, person.Location, SeekClue.People);
			return found is not null ? StepCheck.Done : StepCheck.NotDone;
		}

		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var (person, sequence, step, time) = (context.Person, context.Sequence, context.Step, context.Time);
			var distances = scheduler.GetDistances(person.Location);
			if ((step.Who is { Friends: true } ? scheduler.ChooseWhereToSeek(person, distances, time, who: step.Who) : scheduler.ChooseWhereToSeekPeople(person, step, distances, time)) is not { } seeking)
				return StepWork.Fail(FailCause.Nobody);

			// Looking for clients and the like can count as work, taking from the time chosen for the goal.
			var lookMinutes = scheduler.TakeMinutes(context, step.Minutes) ?? 1;
			sequence.Seeking = seeking;
			var tasks = new List<PersonTask>(GetPath(person.Location, seeking.Place, distances).Select(PersonTask (x) => new TravelTask(x)));
			if (lookMinutes > 0)
				tasks.AddRange(GetBusyTasks(step, lookMinutes, context.Index));
			return StepWork.Give(tasks);
		}
	}

	/// <summary>
	/// "talk" and "ask": talking with or asking someone where the person is, who becomes the partner. The game picks who.
	/// The player talks with them, or asks, in conversation. An "ask" step fails when the answer is no, and a "talk" step
	/// with the player when they won't talk.
	/// </summary>
	private sealed class TalkHandler : StepHandler
	{
		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var (person, sequence, step, time) = (context.Person, context.Sequence, context.Step, context.Time);
			if (scheduler.ChooseWhoToTalkWith(person, sequence, step, time) is not { } other)
				return StepWork.Fail(FailCause.Nobody);

			// The player decides for themselves whether to talk, or what to answer.
			if (!scheduler.m_world.IsAutomatic(other))
				return step.Kind == StepKind.Ask ? AskPlayer(person, sequence, other) : AskPlayerToTalk(person, sequence, other);

			scheduler.TalkOrAskWith(person, sequence, step, context.Index, other, time);
			return StepWork.Give([]);
		}

		public override StepCheck AfterTasks(Scheduler scheduler, StepContext context) =>
			context.Step.Kind == StepKind.Ask && !context.Sequence.Answers.GetValueOrDefault(context.Step.Name!) ? (context.Sequence.ShyAnswers.TryGetValue(context.Step.Name!, out var shy) ? StepCheck.Fail(FailCause.Shy, shy) : StepCheck.Fail(FailCause.No, context.Sequence.Partner)) : StepCheck.Done;
	}

	/// <summary>
	/// "lead": the partner follows the person, for everyone as they go, if the partner is there and can.
	/// </summary>
	private sealed class LeadHandler : StepHandler
	{
		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			var (person, sequence, step, time) = (context.Person, context.Sequence, context.Step, context.Time);
			if (sequence.Partner is not { } partner || partner.Location != person.Location || (partner.CurrentTask is TalkTask talk && talk.With != person) || partner.CurrentTask is FollowTask || partner.IsAsleep ||
				partner.Sequences.Any(x => x.Role != SequenceRole.Steps && x.Leader is not null))
			{
				return StepWork.Fail(FailCause.Nobody);
			}

			// The player follows them as part of their steps, until they're let go, the step's minutes are up, or they stop
			// following.
			var followMinutes = Math.Max((int) step.Minutes!.GetAmount(sequence.Need.SharedByFamily ? person.Family.Members.Count : 0), 1);
			if (!scheduler.m_world.IsAutomatic(partner))
			{
				scheduler.m_world.StartFollowing(partner, person, sequence.Need, scheduler.m_calendar.AddMinutes(time, followMinutes));
				return StepWork.Done;
			}

			partner.Interrupt(new FollowTask(person, followMinutes), time, scheduler.m_calendar.AddMinutes(time, followMinutes));
			return StepWork.Done;
		}

		public override bool IsForPlayer(Scheduler scheduler, StepContext context) => true;
	}

	/// <summary>
	/// "release": lets go of anyone following the person, for everyone as they go.
	/// </summary>
	private sealed class ReleaseHandler : StepHandler
	{
		public override StepWork CarryOut(Scheduler scheduler, StepContext context)
		{
			scheduler.Release(context.Person, context.Time);
			return StepWork.Done;
		}

		public override bool IsForPlayer(Scheduler scheduler, StepContext context) => true;
	}
}

/// <summary>
/// A step of a person's sequence, being checked or carried out.
/// </summary>
internal sealed record StepContext(Person Person, StepSequence Sequence, Step Step, int Index, TimePoint Time);

/// <summary>
/// What checking a step found.
/// </summary>
internal enum StepState
{
	NotDone,
	Done,

	/// <summary>
	/// Its condition isn't met, so it's passed by.
	/// </summary>
	Passed,
	Failed,
}

/// <summary>
/// What checking a step found, with why it failed, if it did, and who it was down to, if anyone in particular, such as
/// who was too shy.
/// </summary>
internal readonly record struct StepCheck(StepState State, FailCause? Cause = null, Person? Who = null)
{
	public static StepCheck NotDone => new(StepState.NotDone);

	public static StepCheck Done => new(StepState.Done);

	public static StepCheck Passed => new(StepState.Passed);

	public static StepCheck Fail(FailCause cause, Person? who = null) => new(StepState.Failed, cause, who);
}

/// <summary>
/// What carrying out a step did: gave tasks to do, finished it straight away, failed, or went on to another step.
/// </summary>
internal abstract record StepWork
{
	public static StepWork Done { get; } = new Finished();

	public static StepWork Give(IReadOnlyList<PersonTask> tasks) => new WithTasks(tasks);

	public static StepWork Fail(FailCause cause, Person? who = null) => new Failed(cause, who);

	public static StepWork GoToStep(int step) => new GoTo(step);

	public sealed record Finished : StepWork;

	public sealed record WithTasks(IReadOnlyList<PersonTask> Tasks) : StepWork
	{
	}

	public sealed record Failed(FailCause Cause, Person? Who = null) : StepWork;

	public sealed record GoTo(int Step) : StepWork;
}
