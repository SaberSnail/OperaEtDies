using System.Collections.ObjectModel;
using System.Collections.Specialized;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Time;
using OperaEtDies.Town;

namespace OperaEtDies.GameState;

/// <summary>
/// The state of the game world while a game is being played. All of the game's mutable state is in the world objects
/// it owns, so saving them saves the game.
/// </summary>
public sealed class GameWorld : NotifyPropertyChangedBase
{
	/// <summary>
	/// Creates the world for a new game from the game data and the player's chosen traits, generating the town around
	/// the player's household.
	/// </summary>
	public static GameWorld CreateNewGame(GameData gameData, CharacterTraits playerTraits, Random random) =>
		CreateFromTown(gameData, TownGenerator.Generate(gameData, playerTraits, random), random);

	/// <summary>
	/// Creates the world for a new game from a generated town. The town isn't kept: everything the game needs from it is
	/// copied into world objects. Everyone starts asleep at home at midnight on the first day of a random week of the
	/// calendar's start year, and time runs until the player wakes up.
	/// The world's locations and families are in the same order as the town's locations and households.
	/// </summary>
	public static GameWorld CreateFromTown(GameData gameData, GeneratedTown town, Random random)
	{
		var objects = new WorldObjects();

		var featuresByTownFeature = town.Features.ToDictionary(x => x, x => objects.Create(id => new Feature(id, x.Feature, x.Bounds)));
		var featuresByMapFeature = featuresByTownFeature.GroupBy(x => x.Key.Feature).ToDictionary(x => x.Key, x => x.First().Value);

		var locationsByTownLocation = town.Locations.ToDictionary(x => x, x => objects.Create(id => new Location(id, gameData, x.Template, x.Bounds,
			x.Surroundings.Select(surrounding => new LocationSurrounding(featuresByMapFeature[surrounding.Feature], surrounding.Direction, surrounding.Text)).ToList())));
		foreach (var (townLocation, location) in locationsByTownLocation)
			location.SetExits(townLocation.Exits.Select(x => new LocationExit(locationsByTownLocation[x.Location], x.Direction, x.Path, x.IsCrossing)).ToList());

		// The start time is chosen first, so everyone's needs start from it.
		var calendar = gameData.Calendar;
		var startTime = ChooseStartTime(calendar, random);

		// Everyone starts at home and not at work, until their family and business are known.
		var families = gameData.Families;
		var atWorkProperty = gameData.CharacterProperties.Get(families.Roles.Properties.AtWork);
		var notAtWork = atWorkProperty.Values!.First(x => x.Id == families.Roles.NotAtWork);
		var peopleByTownPerson = new Dictionary<TownPerson, Person>();
		foreach (var household in town.Households)
		{
			var home = locationsByTownLocation[household.House!];
			var members = household.Members
				.Select(x => peopleByTownPerson[x] = objects.Create(id => new Person(id, gameData, x.Traits.With(atWorkProperty.Id, notAtWork), x.Role, x.IsPlayer, home, startTime)))
				.ToList();
			objects.Create(id => new Family(id, household.LastName, members, home, household.WealthLevel));
		}

		foreach (var business in town.Businesses)
		{
			objects.Create(id => new Business(id, business.Profession, peopleByTownPerson[business.Owner], business.Helpers.Select(x => peopleByTownPerson[x]).ToList(),
				business.Location is { } location ? locationsByTownLocation[location] : null));
		}

		// Everyone has a relationship with everyone else, closer for family (depending on how they're related) and for neighbors.
		var people = peopleByTownPerson.Values.ToList();
		var relationships = gameData.Relationships;
		var headRelation = families.Roles.Properties.HeadRelation;
		for (var first = 0; first < people.Count; first++)
		{
			for (var second = first + 1; second < people.Count; second++)
			{
				var (a, b) = (people[first], people[second]);
				var score = relationships.ChooseScore(random, a.Family == b.Family ? families.Roles.HeadRelations.GetFamilyTie(a.Traits.GetValueId(headRelation), b.Traits.GetValueId(headRelation)) : null,
					isNeighbor: a.Home.Quarter is { } quarter && quarter == b.Home.Quarter);
				objects.Create(id => new Relationship(id, a, b, score));
			}
		}

		// Everyone has their own clothing, kept at home until they dress for the start of the game.
		foreach (var person in people)
			ClothingGenerator.GiveOutfits(gameData, objects, random, person);

		// What the player sees happen is logged from the start.
		objects.Create(id => new EventLog(id, []));

		var world = new GameWorld(gameData, objects, random, startTime);
		world.StartAsleep();
		return world;
	}

	/// <summary>
	/// Chooses when a new game starts: midnight on the first day of a random week of the start year, each equally likely.
	/// </summary>
	private static TimePoint ChooseStartTime(GameCalendar calendar, Random random) =>
		calendar.CreateTimePoint(calendar.StartYear, random.Next(calendar.SeasonsInYear) + 1, random.Next(calendar.WeeksInSeason) + 1, 1);

	/// <summary>
	/// Creates the world for a loaded game from the saved world objects.
	/// </summary>
	internal static GameWorld Load(GameData gameData, int nextId, TimePoint time, IReadOnlyList<WorldObjectRecord> records, Random random)
	{
		var loader = new WorldLoader(gameData, random, time, records);
		var objects = WorldObjects.CreateLoaded(nextId, loader.CreateAll());

		var people = objects.OfType<Person>().ToList();
		var familyMembers = objects.OfType<Family>().SelectMany(x => x.Members).ToList();
		if (familyMembers.Count != familyMembers.Distinct().Count() || familyMembers.Count != people.Count)
			throw new SaveLoadException(OurResources.SaveFamiliesInvalid);
		var workers = objects.OfType<Business>().SelectMany(x => x.Helpers.Prepend(x.Owner)).ToList();
		if (workers.Count != workers.Distinct().Count())
			throw new SaveLoadException(OurResources.SaveBusinessesInvalid);
		if (people.Count(x => x.IsPlayer) != 1)
			throw new SaveLoadException(OurResources.SavePlayerInvalid);
		if (people.Any(x => x.Relationships.Count != people.Count - 1))
			throw new SaveLoadException(OurResources.SaveRelationshipsMissing);

		// Everyone has an outfit for wearing nothing, even in a game saved before there were any.
		foreach (var person in people)
			ClothingGenerator.GiveNothing(objects, person);

		// A game saved before there was an event log starts with an empty one.
		var logs = objects.OfType<EventLog>().Count();
		if (logs > 1)
			throw new SaveLoadException(OurResources.SaveEventLogInvalid);
		if (logs == 0)
			objects.Create(id => new EventLog(id, []));

		return new GameWorld(gameData, objects, random, time);
	}

	private GameWorld(GameData gameData, WorldObjects objects, Random random, TimePoint time)
	{
		GameData = gameData;
		Objects = objects;
		Random = random;
		m_time = time;

		// The lists are built from the registry, which keeps the order the objects were created in. The player's family
		// is always created first.
		Locations = objects.OfType<Location>().ToList();
		Features = objects.OfType<Feature>().ToList();
		Businesses = objects.OfType<Business>().ToList();
		Relationships = objects.OfType<Relationship>().ToList();
		m_appointments = [.. objects.OfType<Appointment>()];
		m_jobs = [.. objects.OfType<Job>()];
		foreach (var job in m_jobs)
			job.Worker.AddJob(job);
		var families = objects.OfType<Family>().ToList();
		Families = [.. families.Where(x => x.Members.Any(member => member.IsPlayer)), .. families.Where(x => !x.Members.Any(member => member.IsPlayer))];
		People = Families.SelectMany(x => x.Members).ToList();
		Player = People.Single(x => x.IsPlayer);
		Npcs = People.Where(x => !x.IsPlayer).ToList();
		EventLog = objects.OfType<EventLog>().Single();
		m_scheduler = new Scheduler(this);
		m_stepsDestination = GetPlayerStepsDestination();
	}

	/// <summary>
	/// The game data the world was created with.
	/// </summary>
	public GameData GameData { get; }

	/// <summary>
	/// Every object in the world.
	/// </summary>
	public WorldObjects Objects { get; }

	/// <summary>
	/// What the player has seen happen.
	/// </summary>
	public EventLog EventLog { get; }

	/// <summary>
	/// The appointments people have made and not yet kept, in the order they were made.
	/// </summary>
	public IReadOnlyList<Appointment> Appointments => m_appointments;

	/// <summary>
	/// Makes an appointment, with a sequence for each of them, waiting until it's time to set off: the worker's, from the
	/// first of its steps, and the client's part in them, from its "meet" step.
	/// </summary>
	internal Appointment AddAppointment(Person worker, Person client, Need need, int step, Location place, TimePoint time)
	{
		var appointment = Objects.Create(id => new Appointment(id, worker, client, need, step, place, time));
		m_appointments.Add(appointment);
		var (start, meet, _) = need.GetAppointmentSteps(step)!.Value;
		worker.AddSequence(new StepSequence(need, start, setOff: SetOffState.Waiting) { Appointment = appointment });
		client.AddSequence(new StepSequence(need, meet, role: SequenceRole.Client, setOff: SetOffState.Waiting) { Appointment = appointment });
		return appointment;
	}

	/// <summary>
	/// Takes an appointment out of the world, once it's kept or called off. A sequence of its steps that has started
	/// carries on without it.
	/// </summary>
	internal void RemoveAppointment(Appointment appointment)
	{
		Objects.Remove(appointment);
		m_appointments.Remove(appointment);
		foreach (var person in new[] { appointment.Worker, appointment.Client })
		{
			foreach (var sequence in person.Sequences.Where(x => x.Appointment == appointment && appointment.IsStarted))
				sequence.Appointment = null;
			if (person.StartRequest == appointment)
				person.StartRequest = null;
		}
	}

	/// <summary>
	/// The jobs people have taken on and not yet done, in the order they were taken on.
	/// </summary>
	public IReadOnlyList<Job> Jobs => m_jobs;

	/// <summary>
	/// Has someone take on a job for someone else, for one of their needs, at a place, taking so many minutes in all. It
	/// replaces any job they had for the same need.
	/// </summary>
	internal Job AddJob(Person worker, Person client, Need need, Location place, int minutes)
	{
		if (worker.GetJob(need) is { } existing)
			RemoveJob(existing);
		var job = Objects.Create(id => new Job(id, worker, client, need, place, minutes));
		m_jobs.Add(job);
		worker.AddJob(job);
		return job;
	}

	/// <summary>
	/// Takes a job out of the world, once it's done.
	/// </summary>
	internal void RemoveJob(Job job)
	{
		Objects.Remove(job);
		m_jobs.Remove(job);
		job.Worker.RemoveJob(job);
	}

	/// <summary>
	/// Gets the appointments the player has made with people and not yet kept, soonest first.
	/// </summary>
	public IReadOnlyList<Appointment> GetPlayerAppointments() => m_appointments.Where(x => (x.Worker == Player || x.Client == Player) && !x.IsStarted).OrderBy(x => x.Time).ToList();

	/// <summary>
	/// Raised when the player misses an appointment, so it's called off: the one they were to meet gave up waiting for
	/// them, or never came.
	/// </summary>
	public event EventHandler<AppointmentMissedEventArgs>? AppointmentMissed;

	internal void OnAppointmentMissed(Appointment appointment, bool isOtherWaiting, TimePoint time)
	{
		EventLog.Add(new LogEntry(time, isOtherWaiting ? LogEntryKind.GaveUpWaiting : LogEntryKind.NeverCame, appointment.GetOther(Player), Player.Location));
		AppointmentMissed?.Invoke(this, new AppointmentMissedEventArgs(appointment, isOtherWaiting));
	}

	/// <summary>
	/// The random numbers used while playing. Their state isn't saved.
	/// </summary>
	public Random Random { get; }

	/// <summary>
	/// Whether the world runs with no one playing, as for tests and simulating days: the player then decides what to do
	/// like everyone else. It's a setting for this session rather than game state, so it isn't saved. Turning it off has the
	/// player stop whatever they were doing, so they can be played.
	/// </summary>
	public bool IsHeadless
	{
		get => m_isHeadless;
		set
		{
			if (m_isHeadless && !value)
			{
				DropPlan(Player);
			}
			m_isHeadless = value;
		}
	}

	/// <summary>
	/// Whether the game decides what someone does: everyone but the player, and the player too when the world is headless.
	/// </summary>
	public bool IsAutomatic(Person person) => !person.IsPlayer || m_isHeadless;

	/// <summary>
	/// Raised when someone starts a task, as time passes.
	/// </summary>
	public event EventHandler<PersonTaskEventArgs>? TaskStarted;

	/// <summary>
	/// The current time in the game, from <see cref="GameData.Calendar"/>. It only moves forward, through
	/// <see cref="AdvanceTimeAsync"/>.
	/// </summary>
	public TimePoint Time
	{
		get => m_time;
		private set => SetPropertyField(value, ref m_time);
	}

	/// <summary>
	/// Gets how long it takes to go from one location to a location next to it. A path between two quarters takes the
	/// longer of their kinds' travel times. Going into or out of a house or business takes the building travel time.
	/// </summary>
	public TimeOffset GetTravelTime(Location from, Location to) => GameData.Calendar.CreateTimeOffset(0, 0, GetTravelMinutes(from, to));

	/// <summary>
	/// Gets how many minutes it takes to go from one location to a location next to it, as for <see cref="GetTravelTime"/>.
	/// </summary>
	public int GetTravelMinutes(Location from, Location to) =>
		from.Template is QuarterTemplate fromQuarter && to.Template is QuarterTemplate toQuarter
			? Math.Max(GameData.QuarterKinds.Get(fromQuarter.Kind).TravelTime, GameData.QuarterKinds.Get(toQuarter.Kind).TravelTime)
			: GameData.BuildingTravelTime;

	/// <summary>
	/// How time is paced as it passes while the player plays, so they can see it go by, or null for it to pass at once, as
	/// for tests and reports. It's a setting for this session rather than game state, so it isn't saved.
	/// </summary>
	public ITimePacer? Pacer { get; set; }

	/// <summary>
	/// Whether the player asked to stop time passing, so none passes until what they were doing is over.
	/// </summary>
	private bool IsStopRequested => Pacer?.IsStopRequested == true;

	/// <summary>
	/// Moves the time forward. All time passes through here, so anything that happens as time passes can be handled in
	/// one place: everyone works through their tasks in the order they end, choosing new goals as they finish them. If
	/// the stop check is met after a task ends, someone asks the player to chat, or it's time for the player to set off for
	/// an appointment, time stops there instead, and the player stops whatever they were doing so they can answer. No
	/// time passes at all while they're being asked whether to set off. With a <see cref="Pacer"/>, time passes a slice at a
	/// time, pausing after each, except while the player walks somewhere, or if it isn't to be paced: then it passes at
	/// once, without the pacing counting it, since it isn't clear where the player is waiting while they walk. If the
	/// player asks it to stop, it stops at the end of a slice (or once they arrive, if they're walking somewhere), and they
	/// stop whatever they were doing, unless it can't be stopped. Returns how much time passed.
	/// </summary>
	/// <exception cref="ArgumentException">The offset is negative.</exception>
	public async Task<TimeOffset> AdvanceTimeAsync(TimeOffset offset, Func<bool>? shouldStop = null, bool canStop = true, bool isPaced = true)
	{
		if (offset < GameData.Calendar.CreateTimeOffset(0, 0, 0))
			throw new ArgumentException("Time can't move backward.", nameof(offset));

		var start = Time;
		var end = start + offset;
		var player = Player;
		bool IsPlayerStopping() => canStop && IsStopRequested && player.CurrentTask is not TravelTask;
		if (!IsPlayerStopping())
		{
			bool isDone;
			var isContinuing = false;
			do
			{
				var pacer = Pacer;
				var sliceStart = Time;
				var walkEnd = isPaced && player.CurrentTask is TravelTask && player.TaskEnd is { } taskEnd && taskEnd > Time ? taskEnd : null;
				var sliceEnd = pacer is null || !isPaced ? end : walkEnd is not null ? (walkEnd < end ? walkEnd : end) : pacer.GetSliceEnd(Time, end);
				isDone = !RunTimeUntil(sliceEnd, shouldStop, isContinuing) || Time >= end;
				isContinuing = true;
				if (pacer is not null)
				{
					if (isPaced && walkEnd is null)
						await pacer.WaitUntilAsync(Time).ConfigureAwait(true);
					else
						pacer.Skip(Time - sliceStart);
				}
			}
			while (!isDone && !IsPlayerStopping());

			// The player asking time to stop stops whatever they were doing.
			if (IsPlayerStopping() && player.Tasks.Count != 0 && player.CurrentTask is not TalkTask)
				ClearPlayerTasks();
		}

		return Time - start;
	}

	/// <summary>
	/// Moves the time forward at once, as <see cref="AdvanceTimeAsync"/> does without a <see cref="Pacer"/>, for the village
	/// report and tests. Returns how much time passed.
	/// </summary>
	/// <exception cref="ArgumentException">The offset is negative.</exception>
	/// <exception cref="InvalidOperationException">Time is paced.</exception>
	internal TimeOffset AdvanceTime(TimeOffset offset, Func<bool>? shouldStop = null)
	{
		if (offset < GameData.Calendar.CreateTimeOffset(0, 0, 0))
			throw new ArgumentException("Time can't move backward.", nameof(offset));
		if (Pacer is not null)
			throw new InvalidOperationException("Paced time can only pass through AdvanceTimeAsync.");

		var start = Time;
		RunTimeUntil(start + offset, shouldStop);
		return Time - start;
	}

	/// <summary>
	/// Runs time up to the given end, as <see cref="AdvanceTimeAsync"/> does, without pausing, carrying on from the slice
	/// before if it's continuing. Returns whether it got there, rather than stopping first.
	/// </summary>
	private bool RunTimeUntil(TimePoint end, Func<bool>? shouldStop, bool isContinuing = false)
	{
		// No time passes while the player is being asked whether to set off for an appointment, a question someone asked
		// them, or whether to start an appointment, until they answer.
		if (GetPlayerSetOffSequence() is not null || Player.AskRequest is not null || Player.StartRequest is not null)
			return false;

		// Whether to stop is only checked as tasks end, so time passes the same way however it's sliced.
		var player = Player;
		var isStopped = false;
		Time = m_scheduler.RunUntil(Time, end, () =>
		{
			var isStopping = IsPlayerAsked || shouldStop?.Invoke() == true;
			isStopped |= isStopping;
			return isStopping;
		}, isContinuing);

		// Someone asking the player to chat, or an appointment to set off for, stops time, and whatever the player was doing,
		// so they can answer.
		if (IsPlayerAsked && player.Tasks.Count != 0 && player.CurrentTask is not TalkTask)
		{
			ClearPlayerTasks();
		}

		// Sightings are only remembered for so long.
		var forgetBefore = Time - GameData.Calendar.CreateTimeOffset(0, GameData.Schedules.SightingHours, 0);
		foreach (var person in People)
			person.ForgetSightingsBefore(forgetBefore);
		UpdatePlayerDestination();
		return !isStopped;
	}

	/// <summary>
	/// Passes time until someone arrives at or leaves the location, until an appointment of the player's starts, or until
	/// the next part of the day starts, whichever comes first. While following someone, the wait stops instead when they
	/// start spending time on a different goal (or on being idle), or when they leave, and if they leave, the player goes
	/// with them, arriving when they do, unless they'd be leaving home without being dressed enough to go out. Starting
	/// another stretch of the same goal, such as resting again, doesn't stop it. Returns whether the wait was stopped by
	/// someone. Given a time to wait until, it waits until then instead, and before then, only the one followed leaving
	/// stops it (besides an appointment starting).
	/// </summary>
	public async Task<bool> WaitForPeopleAsync(Location location, Person? following = null, TimePoint? until = null)
	{
		var isChanged = false;
		var appointments = Appointments.Where(x => !x.IsStarted && (x.Worker == Player || x.Client == Player)).ToList();
		var goal = following?.Goal;
		void OnPeopleChanged(object? sender, NotifyCollectionChangedEventArgs e) => isChanged |= following is null ? until is null : following.Location != location;
		void OnTaskStarted(object? sender, PersonTaskEventArgs e)
		{
			if (e.Person == following && e.Task is BusyTask && until is null)
			{
				isChanged |= following.Goal != goal;
				goal = following.Goal;
			}
		}

		var people = (INotifyCollectionChanged) location.People;
		people.CollectionChanged += OnPeopleChanged;
		TaskStarted += OnTaskStarted;
		try
		{
			var end = until ?? GameData.Calendar.GetNextDayPartStart(Time);
			await AdvanceTimeAsync(end > Time ? end - Time : GameData.Calendar.CreateTimeOffset(0, 0, 0), () => isChanged || appointments.Any(x => x.IsStarted)).ConfigureAwait(true);
		}
		finally
		{
			people.CollectionChanged -= OnPeopleChanged;
			TaskStarted -= OnTaskStarted;
		}

		// The player can only follow someone where they would be dressed enough, with the people there.
		if (following is not null && following.Location != location && GetPlayerUncoveredSlotsAt(following.Location).Count == 0)
			await MovePlayerAsync(following.Location).ConfigureAwait(true);
		else
			await UpdatePlayerStepsAsync().ConfigureAwait(true);

		return isChanged;
	}

	/// <summary>
	/// Gets how many minutes of a quota need the player (or their family, for a need it shares) still has to spend on it
	/// this day or week, less the time the rest of the family already plans to spend on it. It's 0 or less once the need is
	/// done.
	/// </summary>
	public long GetPlayerRemainingMinutes(Need need) => Scheduler.GetRemainingMinutes(Player, need, Time);

	/// <summary>
	/// Gets how far along the player is with each quota need they have this day or week, in the order of the needs: the
	/// minutes (or runs, for a quota measured in runs) done of how many it takes, counting the whole family's for a need it
	/// shares, and whether it can be worked on today.
	/// </summary>
	public IReadOnlyList<PlayerQuotaProgress> GetPlayerQuotaProgress()
	{
		var player = Player;
		var progress = new List<PlayerQuotaProgress>();
		foreach (var need in GameData.Schedules.Needs.Where(x => x.Kind == NeedKind.Quota && Scheduler.HasNeed(player, x)))
		{
			var people = need.SharedByFamily ? player.Family.Members : [player];
			foreach (var person in people)
				person.UpdateNeeds(Time);
			var done = people.Sum(x => x.GetProgress(need));
			var total = need.IsRuns ? need.GetQuotaRuns(people.Count) : need.GetQuotaMinutes(people.Count);
			progress.Add(new PlayerQuotaProgress(need, Math.Min(done, total), total, m_scheduler.IsQuotaDayOpen(player, need, Time)));
		}

		return progress;
	}

	/// <summary>
	/// Gets the other times the player can wait until, besides until something changes, soonest first: those the schedules
	/// offer (see <see cref="PlayerTimeSettings.Waits"/>), when it's time to set off for their next appointment, and when the
	/// step they're on opens, if it's closed now. Times that have passed, or that another choice already waits until, are
	/// left out.
	/// </summary>
	public IReadOnlyList<PlayerWaitChoice> GetPlayerWaitChoices()
	{
		var calendar = GameData.Calendar;
		var choices = new List<PlayerWaitChoice>();
		foreach (var wait in GameData.Schedules.Player.Waits)
		{
			if (wait.Minutes is { } minutes)
				choices.Add(new PlayerWaitChoice(Time + calendar.CreateTimeOffset(0, 0, minutes.Minutes), PlayerWaitReason.Minutes));
			else if (wait.Until == PlayerTimeUntil.NextDayPart)
				choices.Add(new PlayerWaitChoice(calendar.GetNextDayPartStart(Time), PlayerWaitReason.DayPart, DayPart: calendar.GetDayPart(calendar.GetNextDayPartStart(Time))));
		}

		var setOff = Player.Sequences.Where(x => x is { HasSetOff: false, Appointment: not null }).Select(x => x.Appointment!)
			.Select(x => (Appointment: x, Time: m_scheduler.GetSetOffTime(Player, x, Time))).Where(x => x.Time > Time).OrderBy(x => x.Time).ToList();
		if (setOff.Count != 0)
			choices.Add(new PlayerWaitChoice(setOff[0].Time, PlayerWaitReason.SetOff, Appointment: setOff[0].Appointment));

		if (Player.Current is { CurrentStep: { IsPlayerAction: true, Hours: { } hours } step } && !hours.Contains(calendar.GetParts(Time).Hour))
		{
			var opens = calendar.GetStartOfDay(Time) + calendar.CreateTimeOffset(0, hours.Start, 0);
			if (opens <= Time)
				opens += calendar.CreateTimeOffset(1, 0, 0);
			choices.Add(new PlayerWaitChoice(opens, PlayerWaitReason.Opens, Title: step.Player!.Title));
		}

		return choices.Where(x => x.Until > Time).GroupBy(x => x.Until).Select(x => x.First()).OrderBy(x => x.Until).ToList();
	}

	/// <summary>
	/// Gets the minutes the player would spend on a quota need measured in time if they started it now, as anyone else
	/// would choose (a shift, less if less is left), or null for any other need.
	/// </summary>
	public int? GetPlayerShiftMinutes(Need need) => m_scheduler.GetSequenceMinutes(Player, need, Time);

	/// <summary>
	/// Gets when the player would finish spending so many minutes on a need if they started it now, walking to its first
	/// "do" step included.
	/// </summary>
	public TimePoint GetPlayerShiftEnd(Need need, int minutes) =>
		Time + GameData.Calendar.CreateTimeOffset(0, 0, m_scheduler.GetMinutesBeforeFirstDo(Player, need, Time) + minutes);

	/// <summary>
	/// Gets the shorter shifts the player can choose for a quota need measured in time, in the order the schedules offer
	/// them (see <see cref="PlayerTimeSettings.Shifts"/>), with when each would end: so many minutes, or until the next
	/// part of the day starts, less the walk to its first "do" step. Only those shorter than the shift anyone else would
	/// choose are offered, and none for a need the player is already in the middle of.
	/// </summary>
	public IReadOnlyList<PlayerShiftChoice> GetPlayerShiftChoices(Need need)
	{
		ArgumentNullException.ThrowIfNull(need);
		var choices = new List<PlayerShiftChoice>();
		if (Player.GetSequence(need) is null && GetPlayerShiftMinutes(need) is { } shift)
		{
			var calendar = GameData.Calendar;
			var walk = m_scheduler.GetMinutesBeforeFirstDo(Player, need, Time);
			var nextDayPart = calendar.GetNextDayPartStart(Time);
			foreach (var choice in GameData.Schedules.Player.Shifts)
			{
				var minutes = choice.Minutes is { } amount ? amount.Minutes : (int) calendar.GetTotalMinutes(nextDayPart - Time) - walk;
				if (minutes > 0 && minutes < shift && choices.All(x => x.Minutes != minutes))
				{
					choices.Add(new PlayerShiftChoice(minutes, Time + calendar.CreateTimeOffset(0, 0, walk + minutes),
						choice.Until == PlayerTimeUntil.NextDayPart ? calendar.GetDayPart(nextDayPart) : null));
				}
			}
		}

		return choices;
	}

	/// <summary>
	/// Gets what the player can start: each need they have with steps, in the order of the needs, with whether they can
	/// start it now (and if not, why), and whether they're already in the middle of it.
	/// </summary>
	public IReadOnlyList<PlayerStartChoice> GetPlayerStartChoices()
	{
		var player = Player;
		var choices = new List<PlayerStartChoice>();
		foreach (var need in GameData.Schedules.Needs.Where(x => x.Steps.Count != 0 && Scheduler.HasNeed(player, x)))
		{
			var block = player.GetSequence(need) is not null ? PlayerStartBlock.None :
				need.Kind == NeedKind.Quota && !m_scheduler.IsQuotaDayOpen(player, need, Time) ? PlayerStartBlock.Closed :
				need.Kind == NeedKind.Quota && GetPlayerRemainingMinutes(need) <= 0 ? PlayerStartBlock.Done :
				!m_scheduler.CanReachPlaces(player, need, Time) || (need.Seeks && !m_scheduler.GetFriends(player).Any()) ? PlayerStartBlock.NoPlace :
				PlayerStartBlock.None;
			choices.Add(new PlayerStartChoice(need, block, player.GetSequence(need) is not null));
		}

		return choices;
	}

	/// <summary>
	/// Has the player start a need's steps, as anyone else choosing it as their goal would, with the time anyone else would
	/// choose for it, or carry on with them, if they're already in the middle of them. They become the player's current
	/// steps, and any others they're in the middle of are put on hold. Either they're carried out for them, or the player
	/// takes them themselves, starting with any they've already done. Returns how much time passed.
	/// </summary>
	public async Task<TimeOffset> StartPlayerStepsAsync(Need need, bool isAutomatic, int? minutes = null)
	{
		var sequence = StartPlayerSteps(need, minutes);
		return isAutomatic ? await DoPlayerStepsAutomaticallyAsync(sequence).ConfigureAwait(true) : await UpdatePlayerStepsAsync().ConfigureAwait(true);
	}

	/// <summary>
	/// Makes a need's steps the player's current steps, as anyone else choosing it as their goal would, with the time anyone
	/// else would choose for it, or carries on with them, if they're already in the middle of them. Any others they're in
	/// the middle of are put on hold. No time passes, and the player takes none of the steps until their steps are next
	/// brought up to date (see <see cref="UpdatePlayerStepsAsync"/>). Given minutes for a need measured in time, it spends
	/// that long on it (one of <see cref="GetPlayerShiftChoices"/>) rather than the shift anyone else would choose, if
	/// that's less. Returns the steps.
	/// </summary>
	public StepSequence StartPlayerSteps(Need need, int? minutes = null)
	{
		ArgumentNullException.ThrowIfNull(need);
		var player = Player;
		player.UpdateNeeds(Time);
		var sequence = player.GetSequence(need);
		if (sequence is null)
		{
			ClearPlayerTasks();
			var shift = m_scheduler.GetSequenceMinutes(player, need, Time);
			sequence = Scheduler.StartSequence(player, need, 0, shift is { } most && minutes is { } chosen ? Math.Min(most, chosen) : shift, [], null);
		}
		else
		{
			FocusPlayerSteps(sequence);
		}

		return sequence;
	}

	/// <summary>
	/// Makes one of the sequences of steps the player is in the middle of their current one, putting the one that was on
	/// hold. Anything being done for them stops.
	/// </summary>
	public void FocusPlayerSteps(StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		var player = Player;
		if (player.Current != sequence && sequence.HasSetOff)
		{
			ClearPlayerTasks();
			player.MakeCurrent(sequence);
		}
	}

	/// <summary>
	/// Has the step a sequence of the player's is on carried out for them, as anyone else would carry it out, while time
	/// passes for everyone, such as walking where it goes, looking for someone, asking them, changing clothes, or with a
	/// step's button, spending time on it. Before an appointment's steps, it's going to it or meeting the one they're
	/// meeting. Returns how much time passed.
	/// </summary>
	public Task<TimeOffset> DoPlayerStepAsync(StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		FocusPlayerSteps(sequence);
		sequence.Takeover = StepTakeover.Step;
		return RunPlayerStepsAsync(sequence);
	}

	/// <summary>
	/// Has a sequence of the player's carried out for them, as for anyone else, walking included, until it's done, while
	/// time passes for everyone. After a pass of a "repeat" step that reconsiders, the player is asked whether to keep going
	/// (see <see cref="AnswerKeepGoingAsync"/>), and it carries on for them once they answer. Anything that stops them partway,
	/// such as someone asking to chat, leaves the rest for them to take themselves. Returns how much time passed.
	/// </summary>
	public Task<TimeOffset> DoPlayerStepsAutomaticallyAsync(StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		FocusPlayerSteps(sequence);
		sequence.Takeover = StepTakeover.All;
		return RunPlayerStepsAsync(sequence);
	}

	/// <summary>
	/// Passes time while the game carries out the player's current steps, until it leaves the rest to them, or something
	/// stops it, such as the player asking time to stop. Returns how much time passed.
	/// </summary>
	private async Task<TimeOffset> RunPlayerStepsAsync(StepSequence sequence)
	{
		var player = Player;
		var start = Time;
		m_scheduler.Continue(player, Time);
		for (var count = 0; count < MaxPlayerRuns && player.Tasks.Count != 0 && player.Sequences.Contains(sequence) && !IsPlayerAsked && !IsStopRequested; count++)
			await AdvanceTimeAsync(GameData.Calendar.CreateTimeOffset(1, 0, 0), () => player.Tasks.Count == 0 || player.Current != sequence).ConfigureAwait(true);
		// Someone asking the player to chat, or an appointment to set off for, stops what's being done for them, so they can answer.
		if (IsPlayerAsked && player.CurrentTask is not TalkTask)
			ClearPlayerTasks();
		if (player.Sequences.Contains(sequence) && sequence.Takeover == StepTakeover.Step && player.Tasks.Count == 0)
			sequence.Takeover = StepTakeover.None;
		return Time - start;
	}

	/// <summary>
	/// Carries the player on through every sequence of steps they're in the middle of, past the steps they've already done
	/// (being where a step goes, or having on what it puts on) and the steps that only keep track of things, up to the next
	/// one they take themselves. Steps fail if the player left where they were being done. First, the other of an
	/// appointment where the player is may ask them to start it. Returns how much time passed.
	/// </summary>
	public async Task<TimeOffset> UpdatePlayerStepsAsync()
	{
		var player = Player;
		var start = Time;
		if (!IsAutomatic(player))
		{
			// The other of an appointment where the player is can ask them to start it, even before they set off for it.
			foreach (var appointment in m_appointments.Where(x => !x.IsStarted && (x.Worker == player || x.Client == player) && x.Place == player.Location).ToList())
				m_scheduler.TryStartAppointment(appointment, Time, player);

			// Steps put on hold for an appointment (without a place) wait for it to be done, as they do for everyone.
			foreach (var sequence in player.Sequences.Where(x => x != player.Current && x.Place is not null).ToList())
			{
				for (var count = 0; count < MaxPlayerStepsAdvanced && player.Sequences.Contains(sequence) && m_scheduler.AdvanceStep(player, sequence, Time, isPassive: true); count++)
				{
				}
			}

			if (player.Current is { } current && player.TaskEnd is null)
				await RunPlayerStepsAsync(current).ConfigureAwait(true);
		}

		UpdatePlayerDestination();
		return Time - start;
	}

	/// <summary>
	/// Gets the options of the "choose" step a sequence of the player's is on that they can choose now, or none if it isn't
	/// on one they choose themselves.
	/// </summary>
	public IReadOnlyList<StepOption> GetPlayerOptions(StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		return sequence.CurrentStep is { Kind: StepKind.Choose, First: false } && m_scheduler.IsAskingPlayer(Player, sequence)
			? m_scheduler.GetAvailableOptions(Player, sequence, sequence.Need, sequence.Step, Time).Select(x => x.Option).ToList()
			: [];
	}

	/// <summary>
	/// Has the player choose one of the options of the "choose" step a sequence of theirs is on, and carry on with its
	/// steps. Returns how much time passed.
	/// </summary>
	/// <exception cref="InvalidOperationException">The option can't be chosen now.</exception>
	public Task<TimeOffset> ChoosePlayerOptionAsync(StepSequence sequence, StepOption option)
	{
		if (!GetPlayerOptions(sequence).Contains(option))
			throw new InvalidOperationException("The player can't choose that option now.");

		var need = sequence.Need;
		var chosen = need.GetOptions(sequence.Step).First(x => x.Option == option);
		sequence.GoTo(Scheduler.GetOptionStart(need, sequence.Step, chosen));
		FocusPlayerSteps(sequence);
		return ContinuePlayerStepsAsync(sequence);
	}

	/// <summary>
	/// Whether a sequence of the player's is waiting on them to say whether to keep going, after a pass of a "repeat" step
	/// that reconsiders.
	/// </summary>
	public bool IsAskingPlayerToKeepGoing(StepSequence sequence) =>
		sequence.CurrentStep is { Kind: StepKind.Repeat } && m_scheduler.IsAskingPlayer(Player, sequence);

	/// <summary>
	/// Gets the need more urgent than a sequence's need that the player is being asked about, while they're asked whether
	/// to keep going, or null if nothing is more urgent now.
	/// </summary>
	public Need? GetPlayerMoreUrgentNeed(StepSequence sequence) =>
		IsAskingPlayerToKeepGoing(sequence) ? m_scheduler.GetMoreUrgentNeed(Player, sequence.Need, Time) : null;

	/// <summary>
	/// Answers whether the player keeps going with a sequence that's asking them, while time passes for everyone: they go
	/// back to the first step repeated, or carry on with the steps after the "repeat" step. Returns how much time passed.
	/// </summary>
	/// <exception cref="InvalidOperationException">The player isn't being asked.</exception>
	public Task<TimeOffset> AnswerKeepGoingAsync(StepSequence sequence, bool keepGoing)
	{
		if (!IsAskingPlayerToKeepGoing(sequence))
			throw new InvalidOperationException("The player isn't being asked whether to keep going.");

		var need = sequence.Need;
		sequence.GoTo(keepGoing ? need.GetLoopStart(sequence.Step) : need.GetNextStep(sequence.Step));
		FocusPlayerSteps(sequence);
		return ContinuePlayerStepsAsync(sequence);
	}

	/// <summary>
	/// Carries on with a sequence of the player's after they decided something: for them, if all of it is being done for
	/// them, or else up to the next step they take themselves.
	/// </summary>
	private Task<TimeOffset> ContinuePlayerStepsAsync(StepSequence sequence) => sequence.Takeover == StepTakeover.All ? RunPlayerStepsAsync(sequence) : UpdatePlayerStepsAsync();

	/// <summary>
	/// Gets what the player can do instead of just stopping a sequence of steps they're in the middle of: finish up, if any
	/// of its "finally" steps would still need doing, and keep the bundle its steps made, to finish them later, if they
	/// carry one a later step puts down (which is given).
	/// </summary>
	public PlayerStopOptions GetPlayerStopOptions(StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		var player = Player;
		var canFinishUp = player.Sequences.Contains(sequence) && m_scheduler.HasStepsToFinishUp(player, sequence, Time);
		var kept = player.Sequences.Contains(sequence) && !sequence.IsUnwinding && Scheduler.FindKeptStep(sequence) is { } keptStep && sequence.Need.Steps.Skip(keptStep)
			.FirstOrDefault(x => x is { Kind: StepKind.Put, ItemKind: null, Bundle: { } bundle } && sequence.Bundles.ContainsKey(bundle.Id))?.Bundle is { } keptBundle
			? sequence.Bundles[keptBundle.Id] : null;
		return new PlayerStopOptions(canFinishUp, kept);
	}

	/// <summary>
	/// Has the player give up a sequence of steps they're in the middle of, finishing up first: carrying out the "finally"
	/// steps of the lists of steps they're in, which become the next steps they take.
	/// </summary>
	public void FinishUpPlayerSteps(StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		var player = Player;
		if (player.Sequences.Contains(sequence))
		{
			if (player.Current == sequence)
				ClearPlayerTasks();
			m_scheduler.GiveUp(player, sequence, Time);
		}
	}

	/// <summary>
	/// Has the player keep the bundle a sequence of their steps made, rather than give the steps up, to finish them later:
	/// they go on from the step that takes it home (see <see cref="GetPlayerStopOptions"/>).
	/// </summary>
	public void KeepPlayerSteps(StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		var player = Player;
		if (player.Sequences.Contains(sequence))
		{
			if (player.Current == sequence)
				ClearPlayerTasks();
			m_scheduler.Keep(player, sequence, Time);
		}
	}

	/// <summary>
	/// Whether the player has kept the bundle a sequence of their steps made, to finish them later, for so long that
	/// they're reminded of it (see <see cref="ScheduleSettings.CarryWarningDelay"/>).
	/// </summary>
	public bool IsPlayerCarryingTooLong(StepSequence sequence) =>
		sequence?.KeptSince is { } since && Time - since >= GameData.Calendar.CreateTimeOffset(0, 0, GameData.Schedules.CarryWarningDelay.Minutes);

	/// <summary>
	/// Has the player give up a sequence of steps they're in the middle of. What they carry for it stays with them, and is
	/// put away when they get home. Giving up an appointment they set off for calls it off, and once it's started, lets
	/// go of the one they met, as it does anyone else following them.
	/// </summary>
	public void StopPlayerSteps(StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		var player = Player;
		if (player.Sequences.Contains(sequence))
		{
			if (player.Current == sequence)
				ClearPlayerTasks();
			m_scheduler.EndSequence(player, sequence, Time);
			UpdatePlayerDestination();
		}
	}

	/// <summary>
	/// Stops whatever tasks the player is doing toward their current steps, such as walking somewhere for them, and
	/// anything being done for them.
	/// </summary>
	private void ClearPlayerTasks()
	{
		var player = Player;
		if (player.Current is { } current)
		{
			current.IsCarriedOut = false;
			current.Takeover = StepTakeover.None;
		}

		if (player.Tasks.Count != 0 && player.CurrentTask is not TalkTask)
			DropPlan(player);
	}

	/// <summary>
	/// Gets the steps the player is shown for a sequence of theirs, from the step it's on: the steps they'd come to that
	/// they take themselves, up to a "choose" step they choose themselves, its options, and if it has any steps after it,
	/// a step standing for the option's steps, followed by the steps after it, up to the next such "choose" step. A step
	/// they'd come to again, as a "repeat" step repeats, isn't shown again. An appointment's "meet" step, before it starts,
	/// is shown as going to it and meeting the one they're meeting. Taking part in someone else's steps, they're shown
	/// following them, after meeting them at an appointment.
	/// </summary>
	public IReadOnlyList<PlayerStep> GetPlayerSteps(StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		var player = Player;
		var need = sequence.Need;
		var steps = new List<PlayerStep>();
		void AddMeeting(int index, bool isOn)
		{
			if (sequence.Appointment is { IsStarted: false } appointment)
			{
				steps.Add(new PlayerStep(sequence, PlayerStepKind.GoToAppointment, index, Place: appointment.Place, IsCurrent: isOn && player.Location != appointment.Place));
				steps.Add(new PlayerStep(sequence, PlayerStepKind.Meet, index, Place: appointment.Place, IsCurrent: isOn && player.Location == appointment.Place));
			}
		}

		// Taking part in someone else's steps, they follow them, once they've met them.
		if (sequence.Role != SequenceRole.Steps)
		{
			AddMeeting(sequence.Step, isOn: true);
			steps.Add(new PlayerStep(sequence, PlayerStepKind.Follow, sequence.Step, IsCurrent: steps.Count == 0));
		}

		var seen = new HashSet<int>();
		var isAfterChoose = false;
		var currentIndex = sequence.Step;

		// Leaving the steps, the "finally" steps being carried out are followed by the others still to carry out, and then
		// where it's going.
		var running = sequence.UnwindRunning;
		var queue = new Queue<int>(sequence.UnwindQueue);
		int GetNext(int index)
		{
			var next = need.GetNextStep(index);
			if (running is { } start && need.GetBlockFinallyAt(start) is { } block && block.IsInFinally(index) && !block.IsInFinally(next))
			{
				running = queue.Count != 0 ? queue.Dequeue() : null;
				next = running ?? sequence.UnwindTo ?? need.Steps.Count;
			}

			return next;
		}

		for (var index = sequence.Step; sequence.Role == SequenceRole.Steps && index < need.Steps.Count && seen.Add(index);)
		{
			var step = need.Steps[index];
			var next = GetNext(index);
			var isCurrent = index == currentIndex && steps.Count == 0;

			// A "meet" step whose appointment has started is done, though the sequence hasn't moved on from it yet, so the step
			// after it is the one they're on.
			if (step.Kind == StepKind.Meet && isCurrent && sequence.Appointment is { IsStarted: true })
				currentIndex = next;
			if (step.Kind == StepKind.Stop && m_scheduler.IsConditionMet(player, need, sequence, step.When, Time))
				break;
			if (step is { Kind: StepKind.Choose, First: false })
			{
				if (isAfterChoose)
					break;
				var options = need.GetOptions(index);
				foreach (var option in options)
					steps.Add(new PlayerStep(sequence, PlayerStepKind.Option, index, Option: option.Option, IsCurrent: isCurrent));
				if (options.Any(x => x.End > x.Start))
					steps.Add(new PlayerStep(sequence, PlayerStepKind.OptionSteps, index));
				isAfterChoose = true;
			}
			else if (step is { Kind: StepKind.Choose, First: true })
			{
				// A choice the data makes goes the way it would now.
				next = m_scheduler.GetAvailableOptions(player, sequence, need, index, Time) is [var first, ..] ? Scheduler.GetOptionStart(need, index, first) : next;
			}
			else if (step.Kind == StepKind.Meet)
			{
				AddMeeting(index, isCurrent);
			}
			else if (IsShownToPlayer(step) && (isCurrent || m_scheduler.IsConditionMet(player, need, sequence, step.When, Time)))
			{
				steps.Add(new PlayerStep(sequence, PlayerStepKind.Step, index,
					Place: step.Kind == StepKind.GoTo ? m_scheduler.FindPlace(player, sequence, step.Place!) : null, IsCurrent: isCurrent));
			}

			index = next;
		}

		return steps;
	}

	/// <summary>
	/// Whether a step is one the player is shown, as one they take themselves or have done for them, rather than one
	/// that only keeps track of things or decides which steps come next.
	/// </summary>
	private static bool IsShownToPlayer(Step step) =>
		step.Kind is StepKind.GoTo or StepKind.Do or StepKind.Take or StepKind.Put or StepKind.Dress or StepKind.Undress or StepKind.Wait or StepKind.Wander or
			StepKind.Seek or StepKind.Talk or StepKind.Ask;

	/// <summary>
	/// Whether the step a sequence of the player's is on can be done for them, or with its button: one they'd take
	/// themselves rather than decide, once they've set off for any appointment it's for. As the client of an appointment,
	/// that's its "meet" step, until it starts.
	/// </summary>
	public bool CanDoPlayerStep(StepSequence sequence) =>
		Player.Sequences.Contains(sequence) && sequence.HasSetOff && sequence.CurrentStep is not null && !m_scheduler.IsAskingPlayer(Player, sequence) &&
		(sequence.Role == SequenceRole.Steps || sequence is { CurrentStep.Kind: StepKind.Meet, Appointment.IsStarted: false });

	/// <summary>
	/// Gets the place the player's current steps need them to go next, such as home to put the water away, or an
	/// appointment's place once they've set off for it, or null if the steps don't need them anywhere they aren't.
	/// </summary>
	public Location? GetPlayerStepsDestination()
	{
		var player = Player;
		return player.Current is { } sequence && GetPlayerSteps(sequence).FirstOrDefault(x => x.IsCurrent) is { Place: { } place } step && place != player.Location &&
			(step.Kind == PlayerStepKind.GoToAppointment || (step.Kind == PlayerStepKind.Step && step.Step.Kind == StepKind.GoTo))
			? place
			: null;
	}

	/// <summary>
	/// Whether the player's destination is the one their current steps chose, so choosing another gives up those steps.
	/// </summary>
	public bool IsPlayerDestinationFromSteps => Player.Destination is { } destination && destination == m_stepsDestination && Player.Current is not null;

	/// <summary>
	/// Whether choosing the given place as the player's destination would give up the steps they're in the middle of,
	/// since the destination they have now is the one those steps chose.
	/// </summary>
	public bool WouldDestinationStopPlayerSteps(Location place) => place != Player.Destination && IsPlayerDestinationFromSteps;

	/// <summary>
	/// Chooses the place the player is heading for, replacing any other, or clears it with null. Choosing a place while the
	/// destination is the one their current steps chose gives up those steps (as <see cref="StopPlayerSteps"/> does), and
	/// returns true, in which case the steps should be brought up to date with <see cref="UpdatePlayerStepsAsync"/>.
	/// Clearing it leaves the steps alone. The place the player is in can't be a destination.
	/// </summary>
	public bool SetPlayerDestination(Location? place)
	{
		var player = Player;
		var isStopping = place is not null && WouldDestinationStopPlayerSteps(place);
		if (isStopping)
			StopPlayerSteps(player.Current!);
		player.Destination = place == player.Location ? null : place;
		m_stepsDestination = GetPlayerStepsDestination();
		return isStopping;
	}

	/// <summary>
	/// Gets the place next to the player's that the quickest way to their destination goes through first, or null if they
	/// have no destination or there's no way there.
	/// </summary>
	public Location? GetPlayerWayToDestination() => Player.Destination is { } destination ? m_scheduler.GetNextOnRoute(Player.Location, destination) : null;

	/// <summary>
	/// Gets how long the quickest way from where the player is to a place takes, or null if there's no way there.
	/// </summary>
	public TimeOffset? GetPlayerTravelTimeTo(Location place)
	{
		ArgumentNullException.ThrowIfNull(place);
		var minutes = m_scheduler.GetRouteMinutes(Player.Location, place);
		return minutes == int.MaxValue ? null : GameData.Calendar.CreateTimeOffset(0, 0, minutes);
	}

	/// <summary>
	/// Keeps the player's destination up to date with their steps: when the place the steps need them to go changes, it
	/// becomes their destination, and the place the steps chose before stops being it, if it still is.
	/// </summary>
	private void UpdatePlayerDestination()
	{
		var player = Player;
		var steps = GetPlayerStepsDestination();
		if (steps != m_stepsDestination)
		{
			if (m_stepsDestination is not null && player.Destination == m_stepsDestination)
				player.Destination = null;
			m_stepsDestination = steps;
			if (steps is not null)
				player.Destination = steps;
		}
	}

	/// <summary>
	/// Gets the player's sequence for an appointment it's time for them to set off for, while they're being asked whether
	/// to, or null if there's none.
	/// </summary>
	public StepSequence? GetPlayerSetOffSequence() => Player.Sequences.FirstOrDefault(x => x.SetOff == SetOffState.Due);

	/// <summary>
	/// Answers whether the player sets off for the appointment they're being asked about. Setting off takes up its steps,
	/// putting what they were doing on hold if it's for the same need, or failing it otherwise, and carries them on up to
	/// the next one they take themselves (going to it and meeting the one they're meeting, after anything they do first).
	/// Not setting off calls it off, and the one they were to meet goes on with their day. Returns how much time passed.
	/// </summary>
	/// <exception cref="InvalidOperationException">The player isn't being asked whether to set off.</exception>
	public async Task<TimeOffset> AnswerSetOffAsync(bool isSettingOff)
	{
		var sequence = GetPlayerSetOffSequence() ?? throw new InvalidOperationException("The player isn't being asked whether to set off for an appointment.");
		var start = Time;
		if (isSettingOff)
		{
			ClearPlayerTasks();
			m_scheduler.TakeUpAppointment(sequence, Player, Time);
			await UpdatePlayerStepsAsync().ConfigureAwait(true);
		}
		else
		{
			m_scheduler.CallOff(sequence.Appointment!, Time);
		}

		return Time - start;
	}

	/// <summary>
	/// Whether the player is being asked something, so time stops until they answer: someone asking to chat, whether to set
	/// off for an appointment, someone asking them the question of an "ask" step, or whether to start an appointment.
	/// </summary>
	/// <summary>
	/// Whether the player is being asked something they need to answer before time can pass: someone asking them to chat,
	/// whether to set off for an appointment, a question someone asked them, or whether to start an appointment.
	/// </summary>
	public bool IsPlayerAsked => Player.TalkRequest is not null || GetPlayerSetOffSequence() is not null || Player.AskRequest is not null || Player.StartRequest is not null;

	/// <summary>
	/// Has the player answer whether to start the appointment the other asked them about, or told them they're ready for
	/// (see <see cref="Person.StartRequest"/>), as anyone else would answer, except that the player decides. A yes starts it,
	/// and the player carries on with their steps. Answering takes no time.
	/// </summary>
	/// <exception cref="InvalidOperationException">No one is asking the player to start an appointment.</exception>
	public async Task AnswerStartAsync(bool isYes)
	{
		m_scheduler.AnswerPlayerStart(Player, isYes, Time);
		await UpdatePlayerStepsAsync().ConfigureAwait(true);
	}

	/// <summary>
	/// Gets the appointment of the player's with someone that the player can ask them to start now, from conversation, as
	/// its worker, or tell them they're ready for, as its client, after someone said no to starting it. Returns null if
	/// there's none.
	/// </summary>
	public Appointment? GetPlayerStartAppointment(Person person) =>
		m_appointments.FirstOrDefault(x => x.GetOther(Player) == person && (x.Worker == Player || x.Client == Player) && m_scheduler.CanPlayerAskToStart(Player, x));

	/// <summary>
	/// Has the player ask someone to start their appointment, or tell them they're ready for it (see
	/// <see cref="GetPlayerStartAppointment"/>). They answer as anyone else would. A yes starts it, and the player carries on
	/// with their steps. Asking takes no time. Returns whether it started.
	/// </summary>
	/// <exception cref="InvalidOperationException">The player can't ask the person to start an appointment now.</exception>
	public async Task<bool> AskToStartAsync(Person person)
	{
		var appointment = GetPlayerStartAppointment(person) ?? throw new InvalidOperationException($"The player can't ask person {person?.Id} to start an appointment now.");
		var isStarted = m_scheduler.PlayerAskToStart(Player, appointment, Time);
		await UpdatePlayerStepsAsync().ConfigureAwait(true);
		return isStarted;
	}

	/// <summary>
	/// Has the player answer the question someone asked them (see <see cref="Person.AskRequest"/>), as anyone else would
	/// answer it, except that the player decides whether to say yes, while time passes for everyone as they talk. A yes can
	/// make an appointment with them, or lead to them following the one asking. Returns the appointment a yes made, if any.
	/// </summary>
	/// <exception cref="InvalidOperationException">No one is asking the player anything.</exception>
	public async Task<Appointment?> AnswerAskAsync(bool isYes)
	{
		var appointment = m_scheduler.AnswerPlayerAsk(Player, isYes, Time);
		if (Player.CurrentTask is TalkTask)
			await FinishPlayerTalkAsync().ConfigureAwait(true);
		return appointment;
	}

	/// <summary>
	/// Gets the player's needs that saying yes to the question someone is asking them would keep them from seeing to in
	/// time, as anyone else's yes is checked, though the player can still say yes.
	/// </summary>
	public IReadOnlyList<Need> GetPlayerAskConflicts(Person asker) => m_scheduler.GetPlayerAskConflicts(asker, Player, Time);

	/// <summary>
	/// Whether the player would be too shy to say yes to the question someone is asking them, since it would make an
	/// appointment where they'd have to undress more than they'd be comfortable with. They can only say no.
	/// </summary>
	public bool IsPlayerTooShyToAccept(Person asker) => m_scheduler.IsPlayerTooShyToAccept(asker, Player, Time);

	/// <summary>
	/// Has the player follow someone as part of their steps, until they're let go or the player stops: it's the player's
	/// current sequence, with the others put on hold.
	/// </summary>
	internal void StartFollowing(Person player, Person leader, Need need, TimePoint until)
	{
		ClearPlayerTasks();
		player.AddSequence(new StepSequence(need, need.Steps.Count, role: SequenceRole.Follower) { Leader = leader, Place = player.Location, Until = until });
	}

	/// <summary>
	/// Gets who the player is following as part of their steps, or null if they aren't.
	/// </summary>
	public Person? GetPlayerLeader() => Player.Current is { Role: not SequenceRole.Steps, Leader: { } leader } ? leader : null;

	/// <summary>
	/// Whether the player can tell someone they're with that they're calling off what they're doing together: an
	/// appointment between them, or the player following them as part of their steps.
	/// </summary>
	public bool CanCallOffWith(Person person) =>
		m_appointments.Any(x => (x.Worker == Player && x.Client == person) || (x.Client == Player && x.Worker == person)) ||
		Player.Sequences.Any(x => x.Role != SequenceRole.Steps && x.Leader == person);

	/// <summary>
	/// Has the player tell someone they're calling off what they're doing together: appointments between them that haven't
	/// started are called off, they stop following them, and the other lets go of them. They carry on without the player.
	/// </summary>
	public void CallOffWith(Person person)
	{
		ArgumentNullException.ThrowIfNull(person);
		var player = Player;
		foreach (var appointment in m_appointments.Where(x => (x.Worker == player && x.Client == person) || (x.Client == player && x.Worker == person)).ToList())
		{
			if (!appointment.IsStarted)
				m_scheduler.CallOff(appointment, Time);
			else if (appointment.Worker == player)
				m_scheduler.Release(player, Time);
		}

		foreach (var sequence in player.Sequences.Where(x => x.Role != SequenceRole.Steps && x.Leader == person).ToList())
			StopPlayerSteps(sequence);
		foreach (var sequence in person.Sequences.Where(x => x.Partner == player))
			sequence.Partner = null;
	}

	/// <summary>
	/// Gets the "ask" step the player can ask someone now, from conversation: the step one of their sequences is on (once
	/// they've met anyone they set off to meet), if the person is someone it asks. That's someone it asks about, and for one
	/// that asks their partner, their partner, or the one they looked for, the one they looked for (anyone it looks for, if
	/// they looked for no one in particular). Returns null if there's none.
	/// </summary>
	public PlayerAsk? GetPlayerAsk(Person person)
	{
		ArgumentNullException.ThrowIfNull(person);
		var player = Player;
		var sequence = CanTalkTo(person) ? player.Sequences.LastOrDefault(x => x is { Appointment: not { IsStarted: false }, CurrentStep.Kind: StepKind.Ask } && IsAskedBy(x, person)) : null;
		return sequence is null ? null : new PlayerAsk(sequence, sequence.Step);
	}

	/// <summary>
	/// Whether someone is someone the "ask" or "talk" step a sequence of the player's is on asks or talks with (see
	/// <see cref="GetPlayerAsk"/>).
	/// </summary>
	private bool IsAskedBy(StepSequence sequence, Person person)
	{
		var step = sequence.CurrentStep!;
		return m_scheduler.IsSought(Player, person, step.Who) && step.With switch
		{
			StepTarget.Partner => person == sequence.Partner,
			StepTarget.Sought => sequence.Seeking?.Friend is not { } friend || person == friend,
			_ => true,
		};
	}

	/// <summary>
	/// Has the player ask someone the question of the "ask" step they can ask them now (see <see cref="GetPlayerAsk"/>), as
	/// anyone else would ask it, while time passes for everyone. A yes carries them on to the next step, and a no fails the
	/// step. Returns the answer.
	/// </summary>
	/// <exception cref="InvalidOperationException">The player has no question to ask the person.</exception>
	public async Task<PlayerAskAnswer> AskPlayerQuestionAsync(Person person)
	{
		var ask = GetPlayerAsk(person) ?? throw new InvalidOperationException($"The player has nothing to ask person {person.Id}.");
		var start = Time;
		FocusPlayerSteps(ask.Sequence);
		var (isYes, appointment) = m_scheduler.TalkOrAskWith(Player, ask.Sequence, ask.Step, ask.StepIndex, person, Time);
		ask.Sequence.IsCarriedOut = true;
		await FinishPlayerTalkAsync().ConfigureAwait(true);
		await UpdatePlayerStepsAsync().ConfigureAwait(true);
		return new PlayerAskAnswer(isYes, appointment, Time - start);
	}

	/// <summary>
	/// Gets the player's sequence that's on a "talk" step that talks with someone, if there is one.
	/// </summary>
	private StepSequence? GetPlayerTalkSequence(Person person) => Player.Sequences.LastOrDefault(x => x.CurrentStep?.Kind == StepKind.Talk && IsAskedBy(x, person));

	/// <summary>
	/// Counts the player talking with someone toward a "talk" step one of their sequences is on, if it talks with them.
	/// </summary>
	private void OnPlayerTalking(Person person)
	{
		var player = Player;
		if (GetPlayerTalkSequence(person) is { } sequence)
		{
			sequence.Partner = person;
			sequence.IsCarriedOut = true;
			player.MakeCurrent(sequence);
		}
	}

	/// <summary>
	/// Raised when someone's step fails, such as when the one they asked said no, or the player left before they were
	/// done.
	/// </summary>
	public event EventHandler<StepFailedEventArgs>? StepFailed;

	internal void OnStepFailed(Person person, StepSequence sequence, int step, FailCause cause, TimePoint time, Person? who = null, bool isEnding = true)
	{
		// Who said no, or didn't come, is the other one, unless the step says.
		who ??= cause switch
		{
			FailCause.No => sequence.Partner,
			FailCause.NoShow => sequence.Appointment?.GetOther(person),
			_ => null,
		};

		// The player missing an appointment is logged as such (see OnAppointmentMissed).
		if (person.Location == Player.Location && !(person == Player && cause == FailCause.NoShow))
			EventLog.Add(new LogEntry(time, LogEntryKind.Failed, person, person.Location, Other: who, Need: sequence.Need, Step: step, Cause: cause));
		StepFailed?.Invoke(this, new StepFailedEventArgs(person, sequence, step, cause, time, who, isEnding));
	}

	/// <summary>
	/// Logs the player finding themselves less covered than they're comfortable with, unless that's the last thing logged for
	/// them.
	/// </summary>
	internal void OnPlayerExposed(TimePoint time)
	{
		if (EventLog.GetLatest(Player) is not { Kind: LogEntryKind.Exposed })
			EventLog.Add(new LogEntry(time, LogEntryKind.Exposed, Player, Player.Location));
	}

	/// <summary>
	/// Logs how someone in the player's location reacted to being less covered than they're comfortable with, such as
	/// heading home to cover up.
	/// </summary>
	internal void OnCoverUpReaction(Person person, LogEntryKind kind, TimePoint time)
	{
		if (person.Location == Player.Location && !(EventLog.GetLatest(person) is { } latest && latest.Kind == kind && latest.Time == time))
			EventLog.Add(new LogEntry(time, kind, person, person.Location));
	}

	/// <summary>
	/// Makes a bundle for someone to carry, standing for the given items.
	/// </summary>
	internal Bundle CreateBundle(Person person, string title, IEnumerable<Item> items)
	{
		var bundle = Objects.Create(id => new Bundle(id, person, title, items));
		person.Inventory.Add(bundle);
		return bundle;
	}

	/// <summary>
	/// Takes a bundle out of the world once its step is done with it. The items it stood for stay where they are.
	/// </summary>
	internal void Unpack(Bundle bundle)
	{
		if (bundle.Container is { } container)
			container.Remove(bundle);
		Objects.Remove(bundle);
	}

	/// <summary>
	/// Whether the player can put on a piece of clothing: it's at hand and they aren't wearing it.
	/// </summary>
	public bool CanPlayerWear(ClothingPiece piece) => piece.Wearer != Player && ClothingRules.IsAtHand(Player, piece, Time);

	/// <summary>
	/// Has the player put on a piece of clothing, taking off whatever they're wearing in its slots in its layer. What
	/// they take off is kept at home if they're there, or carried. It takes no time until they're done changing (see
	/// <see cref="FinishPlayerChangingAsync"/>).
	/// </summary>
	/// <exception cref="InvalidOperationException">The player can't put the piece on.</exception>
	public void PlayerWear(ClothingPiece piece, bool isCarried = false)
	{
		if (!CanPlayerWear(piece))
			throw new InvalidOperationException($"The player can't put on {piece.Id}.");

		var player = Player;
		var container = GetPlayerClothesContainer(isCarried);
		foreach (var worn in player.Clothing.Pieces.Where(x => x.Clothing.Overlaps(piece.Clothing)).ToList())
		{
			player.Clothing.TakeOff(worn);
			container.Add(worn);
		}

		piece.Container?.Remove(piece);
		player.Clothing.Wear(piece);
	}

	/// <summary>
	/// Has the player take off a piece of clothing they're wearing. It's kept at home if they're there, or carried (always, with <paramref name="isCarried"/>). It takes
	/// no time until they're done changing (see <see cref="FinishPlayerChangingAsync"/>).
	/// </summary>
	/// <exception cref="InvalidOperationException">The player isn't wearing the piece.</exception>
	public void PlayerTakeOff(ClothingPiece piece, bool isCarried = false)
	{
		if (piece.Wearer != Player)
			throw new InvalidOperationException($"The player isn't wearing {piece.Id}.");

		Player.Clothing.TakeOff(piece);
		GetPlayerClothesContainer(isCarried).Add(piece);
	}

	/// <summary>
	/// Has the player take off everything they're wearing. It's kept at home if they're there, or carried (always, with <paramref name="isCarried"/>). It takes no time
	/// until they're done changing (see <see cref="FinishPlayerChangingAsync"/>).
	/// </summary>
	/// <exception cref="InvalidOperationException">The player isn't wearing anything.</exception>
	public void PlayerTakeOffAll(bool isCarried = false)
	{
		if (Player.Clothing.Pieces.Count == 0)
			throw new InvalidOperationException("The player isn't wearing anything.");

		Player.Clothing.TakeOffAll(GetPlayerClothesContainer(isCarried));
	}

	/// <summary>
	/// Whether the player can change into one of their outfits: every piece is at hand, and they aren't already wearing
	/// just that (or for work clothes that go over other clothes, all of it).
	/// </summary>
	public bool CanPlayerChangeInto(OwnedOutfit outfit) =>
		outfit.Owner == Player && ClothingRules.IsAtHand(outfit, Time) &&
		!ClothingRules.IsDressedIn(Player, outfit);

	/// <summary>
	/// Has the player change into one of their outfits, taking off everything else.
	/// Work clothes that aren't complete go over what they're wearing instead, taking off only what's in the same slots in
	/// the same layers.
	/// What they take off is kept at home if they're there, or carried (always, with <paramref name="isCarried"/>). It takes no time until they're done changing (see
	/// <see cref="FinishPlayerChangingAsync"/>).
	/// </summary>
	/// <exception cref="InvalidOperationException">The player can't change into the outfit.</exception>
	public void PlayerChangeInto(OwnedOutfit outfit, bool isCarried = false)
	{
		if (!CanPlayerChangeInto(outfit))
			throw new InvalidOperationException($"The player can't change into {outfit.Id}.");

		var player = Player;
		var container = GetPlayerClothesContainer(isCarried);
		foreach (var worn in player.Clothing.Pieces.Where(x => outfit.IsComplete && !outfit.Pieces.Contains(x)).ToList())
		{
			player.Clothing.TakeOff(worn);
			container.Add(worn);
		}

		player.Clothing.PutOn(outfit, container);
	}

	/// <summary>
	/// Finishes the player's changing of clothes, such as when they close their wardrobe: if they're wearing something
	/// different from what they were wearing before, however many things they tried, it takes the time to change clothes,
	/// while time passes for everyone. It counts as changing into and out of outfits, as for a step (see
	/// <see cref="ClothingRules.NoteChangedByHand"/>). Returns how much time passed, or null if nothing changed.
	/// </summary>
	public async Task<TimeOffset?> FinishPlayerChangingAsync(IReadOnlyCollection<ClothingPiece> wornBefore)
	{
		ArgumentNullException.ThrowIfNull(wornBefore);
		ClothingRules.NoteChangedByHand(Player, wornBefore);
		var spent =Player.Clothing.Pieces.ToHashSet().SetEquals(wornBefore) ? (TimeOffset?) null : await PassPlayerChangingTimeAsync().ConfigureAwait(true);
		await UpdatePlayerStepsAsync().ConfigureAwait(true);
		return spent;
	}

	/// <summary>
	/// Whether the player can pick up a piece of clothing kept at home to carry it: they're at home, and it's there and
	/// isn't in the wash or drying.
	/// </summary>
	public bool CanPlayerTake(ClothingPiece piece) => Player.Location == Player.Home && piece.Container == Player.Home.Items && piece.IsReady(Time);

	/// <summary>
	/// Has the player pick up a piece of clothing kept at home to carry it, which takes no time, though it can carry them on through their steps.
	/// </summary>
	/// <exception cref="InvalidOperationException">The player can't take the piece.</exception>
	public async Task PlayerTakeAsync(ClothingPiece piece)
	{
		if (!CanPlayerTake(piece))
			throw new InvalidOperationException($"The player can't take {piece.Id}.");

		Player.Home.Items.Remove(piece);
		Player.Inventory.Add(piece);
		await UpdatePlayerStepsAsync().ConfigureAwait(true);
	}

	/// <summary>
	/// Whether the player can put away a piece of clothing they're carrying: they're at home, and it isn't in a bundle,
	/// which only its step puts down.
	/// </summary>
	public bool CanPlayerPutAway(ClothingPiece piece) => Player.Location == Player.Home && piece.Container == Player.Inventory && !Player.IsInBundle(piece);

	/// <summary>
	/// Has the player put away a piece of clothing they're carrying at home, which takes no time, though it can carry them on through their steps.
	/// </summary>
	/// <exception cref="InvalidOperationException">The player can't put the piece away.</exception>
	public async Task PlayerPutAwayAsync(ClothingPiece piece)
	{
		if (!CanPlayerPutAway(piece))
			throw new InvalidOperationException($"The player can't put away {piece.Id}.");

		Player.Inventory.Remove(piece);
		Player.Home.Items.Add(piece);
		await UpdatePlayerStepsAsync().ConfigureAwait(true);
	}

	/// <summary>
	/// The integer character property that makes people bolder or shyer about how covered they are, or null if there's none
	/// (see <see cref="ModestySettings.Boldness"/>).
	/// </summary>
	public CharacterProperty? BoldnessProperty => GameData.ModestySettings.Boldness is { } boldness ? GameData.CharacterProperties.Find(boldness.Property) : null;

	/// <summary>
	/// Gets someone's boldness, or null if they have none.
	/// </summary>
	public int? GetBoldness(Person person) => BoldnessProperty is { } property && person.Traits.Values.GetValueOrDefault(property.Id) is int value ? value : null;

	/// <summary>
	/// Changes someone's boldness, kept within the range of the property for them, for developers.
	/// </summary>
	public void SetBoldness(Person person, int value)
	{
		ArgumentNullException.ThrowIfNull(person);
		if (BoldnessProperty is { } property)
		{
			var range = GameData.GetRange(property, person.IsPlayer ? CharacterRole.Player : CharacterRole.Npc);
			person.SetInteger(property.Id, Math.Clamp(value, range.Minimum, range.Maximum));
		}
	}

	/// <summary>
	/// Gets how comfortable the player is with how covered they are where they are now (see <see cref="ModestyComfort"/>).
	/// </summary>
	public double GetPlayerComfort() => ModestyComfort.Get(GameData, Player, Time);

	/// <summary>
	/// Gets the clothing slots in which the player isn't covered enough for where they are, with the people there and the
	/// step they're on.
	/// </summary>
	public IReadOnlyList<ClothingSlot> GetPlayerUncoveredSlots() => ClothingRules.GetUncoveredSlots(GameData, Player, PlayerWorn, GetPlayerComfort());

	/// <summary>
	/// Gets how modest the player is in each clothing slot their modesty rule checks, and the least it calls for where they
	/// are, such as for developers to see.
	/// </summary>
	public IReadOnlyList<(ClothingSlot Slot, int Modesty, int Minimum)> GetPlayerModesty() => ClothingRules.GetModesty(GameData, Player, PlayerWorn, GetPlayerComfort());

	/// <summary>
	/// Gets the clothing slots in which the player wouldn't be covered enough at a location they're going to, with the
	/// people there now and the step they're on.
	/// </summary>
	public IReadOnlyList<ClothingSlot> GetPlayerUncoveredSlotsAt(Location location)
	{
		ArgumentNullException.ThrowIfNull(location);
		var comfort = ModestyComfort.Get(GameData, Player, location, location.People.Where(x => x != Player).Select(x => (x, x.IsAsleep)), ModestyComfort.GetCurrentStep(Player), Time);
		return ClothingRules.GetUncoveredSlots(GameData, Player, PlayerWorn, comfort);
	}

	/// <summary>
	/// Whether the player is heading home to cover up by going to a location: they aren't covered enough where they are, and
	/// it's on the way home, so they can go there however little they have on.
	/// </summary>
	public bool IsPlayerRetreatingTo(Location location) =>
		GetPlayerUncoveredSlots().Count != 0 && m_scheduler.GetRouteMinutes(location, Player.Home) < m_scheduler.GetRouteMinutes(Player.Location, Player.Home);

	/// <summary>
	/// Gets the clothing slots in which the player wouldn't be covered enough for a step, where they are: in what they're
	/// wearing, for a "dress" step, in the outfit it would change them into, and for an "undress" step, in what they'd have
	/// on after it, though that's always allowed when it leaves them no less covered than now.
	/// </summary>
	public IReadOnlyList<ClothingSlot> GetPlayerStepUncoveredSlots(Step step)
	{
		ArgumentNullException.ThrowIfNull(step);
		var player = Player;
		var comfort = ModestyComfort.Get(GameData, player, player.Location, player.Location.People.Where(x => x != player).Select(x => (x, x.IsAsleep)), step, Time);
		var worn = step switch
		{
			{ Kind: StepKind.Dress, Outfit: { } kind } when ClothingRules.FindAtHand(player, kind, Time) is { } outfit => ClothingRules.GetClothesIn(player, outfit),
			{ Kind: StepKind.Undress, Outfit: { } kind } when player.GetOutfits(kind).FirstOrDefault(x => ClothingRules.IsChangedInto(player, x)) is { } outfit => ClothingRules.GetClothesOutOf(player, outfit, Time),
			_ => PlayerWorn,
		};
		var uncovered = ClothingRules.GetUncoveredSlots(GameData, player, worn, comfort);
		var isNoLessCovered = step.Kind == StepKind.Undress && ClothingRules.GetModesty(GameData, player, worn, 0).Zip(ClothingRules.GetModesty(GameData, player, PlayerWorn, 0)).All(x => x.First.Modesty >= x.Second.Modesty);
		return isNoLessCovered ? [] : uncovered;
	}

	/// <summary>
	/// Whether the player is covered at least as much in every slot their modesty rule checks as they were in the given
	/// clothes, so changing hasn't left them any less covered.
	/// </summary>
	public bool IsPlayerNoLessCovered(IEnumerable<ClothingPiece> before)
	{
		var then = ClothingRules.GetModesty(GameData, Player, before.Select(x => x.Clothing), 0);
		var now = ClothingRules.GetModesty(GameData, Player, PlayerWorn, 0);
		return now.Zip(then).All(x => x.First.Modesty >= x.Second.Modesty);
	}

	/// <summary>
	/// The clothing the player is wearing.
	/// </summary>
	private IReadOnlyList<ClothingItem> PlayerWorn => Player.Clothing.Pieces.Select(x => x.Clothing).ToList();

	/// <summary>
	/// Where the player's clothes go when they take them off: their home, if they're there (in their wardrobe), or else, or
	/// when they're to be carried (as in their inventory), what they're carrying.
	/// </summary>
	private ItemContainer GetPlayerClothesContainer(bool isCarried) => !isCarried && Player.Location == Player.Home ? Player.Home.Items : Player.Inventory;

	/// <summary>
	/// Passes the time it takes the player to change clothes, while time passes for everyone.
	/// </summary>
	private Task<TimeOffset> PassPlayerChangingTimeAsync()
	{
		var player = Player;
		var minutes = GameData.Schedules.Clothing.ChangeMinutes;
		player.SetTasks([new BusyTask(minutes, IsAside: true)]);
		m_scheduler.Continue(player, Time);
		return AdvanceTimeAsync(GameData.Calendar.CreateTimeOffset(0, 0, minutes), () => player.Tasks.Count == 0);
	}

	/// <summary>
	/// Starts a new game: everyone is asleep at home, with their needs at their starting levels, and wakes up around the
	/// same time, except anyone whose work hours include midnight, who starts awake at work. Time runs until the player
	/// has woken and finished getting up, when the game begins.
	/// </summary>
	private void StartAsleep()
	{
		var settings = GameData.Schedules;
		var calendar = GameData.Calendar;
		var sleep = settings.Needs.FirstOrDefault(x => x.Id == settings.SleepNeed && x.Kind == NeedKind.Level);
		foreach (var person in People)
		{
			foreach (var need in settings.Needs.Where(x => x.StartLevel is not null))
				person.SetLevel(need, Random.NextInRange(need.StartLevel!));

			// Someone whose work hours include midnight starts awake at work. Everyone else is asleep at home.
			if (!person.IsPlayer && m_scheduler.IsInWorkHours(person, Time))
			{
				person.Location = person.Workplace;
				ClothingGenerator.DressAtStart(person, isAtWork: true);
			}
			else
			{
				var minutes = Math.Max((settings.Start.WakeHour * 60) + Random.NextInRange(settings.Start.WakeMinutes), 1);
				if (sleep is not null)
					person.SetLevel(sleep, sleep.SatisfyPerHour!.Value * minutes / 60);
				// They're partway through sleeping, with the rest of sleep's steps still to come.
				var sleepStep = sleep is null ? -1 : sleep.Steps.ToList().FindIndex(x => x.Kind == StepKind.Do);
				if (sleepStep >= 0)
				{
					// The player finishes waking up as anyone else would, such as changing out of night clothes.
					var sequence = Scheduler.StartSequence(person, sleep!, sleepStep, null, [new BusyTask(minutes, sleepStep)], null);
					sequence.IsCarriedOut = true;
					sequence.Takeover = StepTakeover.All;
				}
				else
					person.SetTasks([new BusyTask(minutes)]);
				person.StartTask(calendar.AddMinutes(Time, minutes));
				ClothingGenerator.DressAtStart(person, isAtWork: false);
			}
		}

		// The game begins once the player has woken and finished their sleep's steps, such as changing out of night clothes,
		// and put away what they took off, as anyone else does before choosing what to do.
		while (Player.TaskEnd is { } end)
			RunTimeUntil(end, null);
		if (Player.Location == Player.Home)
			ClothingRules.PutAway(Player);
	}

	/// <summary>
	/// Whether someone refuses to stop and talk with the player right now, because they're following someone, or their goal
	/// is too urgent. Someone waiting to meet someone isn't too busy.
	/// </summary>
	public bool IsTooBusyToTalk(Person person) => m_scheduler.IsTooBusyToTalk(person, Time, isPlayerAsking: true);

	/// <summary>
	/// Has the player talk with someone for the given number of minutes, while time passes for everyone. Talking pauses
	/// what the other person was doing until it's over, waking them if they were asleep. When socializing, it meets the
	/// socializing need of each of them who counts the other as a friend. Returns how much time passed, or null if they
	/// refused because their goal is too urgent.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The minutes aren't more than 0.</exception>
	public async Task<TimeOffset?> TalkToAsync(Person person, int minutes, bool isSocializing = false)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minutes);
		if (person.IsPlayer || IsTooBusyToTalk(person))
			return null;

		StartTalking(Player, person, minutes, Time, isSocializing);
		var spent = await AdvanceTimeAsync(GameData.Calendar.CreateTimeOffset(0, 0, minutes), () => Player.Tasks.Count == 0).ConfigureAwait(true);
		await UpdatePlayerStepsAsync().ConfigureAwait(true);
		return spent;
	}

	/// <summary>
	/// Has the player ask someone where they're going and why, which takes a few minutes while time passes for everyone.
	/// The player remembers the answer with what they saw the person doing. Returns how much time passed and what the
	/// player saw and was told.
	/// </summary>
	/// <exception cref="InvalidOperationException">The person isn't with the player, or won't talk.</exception>
	public async Task<TalkAnswer> AskWhatDoingAsync(Person person)
	{
		CheckCanTalk(person);
		var time = Time;
		var seen = Observe(person, time) with { Plan = GetPlan(person) };
		StartTalking(Player, person, GameData.Schedules.Talking.AskDoingMinutes, time);
		Player.See(person, seen);
		return new TalkAnswer(await FinishPlayerTalkAsync().ConfigureAwait(true), seen);
	}

	/// <summary>
	/// Has the player ask someone whether they've seen another person, which takes a minute while time passes for everyone.
	/// If they have, the player learns what they know, as a secondhand sighting. Returns how much time passed and the
	/// sighting, or no sighting if they haven't seen the person.
	/// </summary>
	/// <exception cref="InvalidOperationException">The person isn't with the player, or won't talk.</exception>
	public async Task<TalkAnswer> AskAboutAsync(Person person, Person subject)
	{
		CheckCanTalk(person);
		var known = person.Sightings.GetValueOrDefault(subject);
		StartTalking(Player, person, GameData.Schedules.Talking.AskAboutMinutes, Time);
		if (known is not null)
			Player.LearnSighting(subject, known, person);
		return new TalkAnswer(await FinishPlayerTalkAsync().ConfigureAwait(true), known);
	}

	/// <summary>
	/// Has the player socialize with someone, while time passes for everyone. It meets the socializing need of each of
	/// them who counts the other as a friend. Returns how much time passed.
	/// </summary>
	/// <exception cref="InvalidOperationException">The person isn't with the player, or won't talk.</exception>
	public Task<TimeOffset> SocializeAsync(Person person)
	{
		CheckCanTalk(person);
		StartTalking(Player, person, GameData.Schedules.Socializing.ChatMinutes, Time, isSocializing: true);
		return FinishPlayerTalkAsync();
	}

	/// <summary>
	/// Agrees to talk with whoever asked the player, to chat or for a "talk" step of theirs, if they're still there and
	/// free, while time passes for everyone. Returns how much time passed, or null if they couldn't talk after all.
	/// </summary>
	public async Task<TimeOffset?> AcceptTalkAsync()
	{
		var player = Player;
		var asker = player.TalkRequest;
		player.TalkRequest = null;
		bool canTalk;
		if (asker is not null && Scheduler.IsTalkStepRequest(asker, player))
		{
			canTalk = m_scheduler.AnswerPlayerTalk(asker, player, isYes: true, Time);
		}
		else
		{
			canTalk = asker is not null && asker.Location == player.Location && asker.CurrentTask is not TalkTask && player.CurrentTask is not TalkTask;
			if (canTalk)
				m_scheduler.ChatWith(asker!, player, Time);
		}

		return canTalk ? await FinishPlayerTalkAsync().ConfigureAwait(true) : null;
	}

	/// <summary>
	/// Turns down whoever asked the player to talk. They don't ask again for a while, and a "talk" step of theirs fails.
	/// </summary>
	public void DeclineTalk()
	{
		if (Player.TalkRequest is { } asker)
		{
			Player.TalkRequest = null;
			if (Scheduler.IsTalkStepRequest(asker, Player))
				m_scheduler.AnswerPlayerTalk(asker, Player, isYes: false, Time);
			else
				asker.GetRelationship(Player).LastDeclined = Time;
		}
	}

	/// <summary>
	/// Whether someone asking the player to talk is asking for a "talk" step of theirs, rather than to chat.
	/// </summary>
	public bool IsTalkStepRequest(Person asker) => Scheduler.IsTalkStepRequest(asker, Player);

	/// <summary>
	/// Gets how long talking with whoever is asking the player to talk would take.
	/// </summary>
	public int GetTalkRequestMinutes(Person asker) => Scheduler.GetTalkStepMinutes(asker, Player) ?? GameData.Schedules.Socializing.ChatMinutes;

	/// <summary>
	/// Whether the player can talk with someone now: they're someone else, in the player's location, and not too busy.
	/// </summary>
	public bool CanTalkTo(Person person) => !person.IsPlayer && person.Location == Player.Location && !IsTooBusyToTalk(person);

	private void CheckCanTalk(Person person)
	{
		if (!CanTalkTo(person))
			throw new InvalidOperationException($"The player can't talk with person {person.Id} now.");
	}

	/// <summary>
	/// Passes time until the player's talk is over, then carries the player on through their steps.
	/// </summary>
	private async Task<TimeOffset> FinishPlayerTalkAsync()
	{
		var start = Time;
		await AdvanceTimeAsync(Player.TaskEnd! - Time, () => Player.Tasks.Count == 0).ConfigureAwait(true);
		await UpdatePlayerStepsAsync().ConfigureAwait(true);
		return Time - start;
	}

	/// <summary>
	/// Gets what someone would say they're doing: their goal, what they're doing for it (the activity of the step they're
	/// on, or of the next one that spends time), and where their walk ends if they're walking. Once they have nothing left to
	/// do for their goal, such as on the way home from fetching water, they don't mention it.
	/// </summary>
	internal static SightingPlan GetPlan(Person person)
	{
		var goal = person.Tasks.Any(x => x is BusyTask) || (person.Current is { } current && current.Need.Steps.Skip(current.Step).Any(x => x.Kind == StepKind.Do)) ? person.Goal : null;
		var destination = person.CurrentTask is TravelTask ? person.Tasks.TakeWhile(x => x is TravelTask).OfType<TravelTask>().Last().Destination : null;
		return new SightingPlan(goal, destination, goal is null ? null : GetDoing(person, goal));
	}

	/// <summary>
	/// Gets what someone is doing for their goal, or about to: the activity of the step they're spending time on, or of the
	/// next "do" step of their current sequence, or the goal's activity.
	/// </summary>
	private static string GetDoing(Person person, Need goal)
	{
		var profession = person.Business?.Profession.Id;
		var step = person.Tasks.OfType<BusyTask>().FirstOrDefault()?.Step ??
			(person.Current is { } current && current.Need == goal ? Enumerable.Range(current.Step, Math.Max(goal.Steps.Count - current.Step, 0)).Cast<int?>().FirstOrDefault(x => goal.Steps[x!.Value].Kind == StepKind.Do) : null);
		return ActivityText.Fill(goal.GetActivity(step, profession), person.Current);
	}

	/// <summary>
	/// Has two people start talking at the given time for the given number of minutes, which interrupts what they were
	/// doing until it's over. Anyone woken up to talk has their sleep need rise. When socializing, it meets the socializing
	/// need of each of them who counts the other as a friend. A need given for either of them is met for them instead, such
	/// as a "talk" step that meets its goal's quota. They see each other talking, and it's when they last talked.
	/// </summary>
	internal void StartTalking(Person first, Person second, int minutes, TimePoint time, bool isSocializing = false, Need? firstNeed = null, Need? secondNeed = null)
	{
		var settings = GameData.Schedules;

		// The player chatting with someone for a "talk" step of theirs meets what the step meets, as for anyone else.
		if (isSocializing && first == Player && !IsAutomatic(first) && firstNeed is null && secondNeed is null && GetPlayerTalkSequence(second) is { CurrentStep: { } talkStep } talkSequence)
		{
			firstNeed = talkStep.MeetsQuota && talkSequence.Need.Quota?.Time is not null ? talkSequence.Need : null;
			secondNeed = talkStep.PartnerMeets is { } partnerMeets ? settings.Needs.FirstOrDefault(x => x.Id == partnerMeets && Scheduler.HasNeed(second, x)) : null;
			isSocializing = !talkStep.MeetsQuota;
		}

		var sleep = settings.Needs.FirstOrDefault(x => x.Id == settings.SleepNeed);
		var social = isSocializing ? settings.Needs.FirstOrDefault(x => x.Id == settings.SocializeNeed) : null;
		var isFriends = first.Relationships.TryGetValue(second, out var friendship) && GameData.Relationships.IsFriend(friendship.Score);
		var end = GameData.Calendar.AddMinutes(time, minutes);
		var talks = new List<(Person Person, TalkTask Task)>();
		foreach (var (person, other, need) in new[] { (first, second, firstNeed), (second, first, secondNeed) })
		{
			var isWoken = person.IsAsleep;
			var task = new TalkTask(other, minutes, need ?? (isFriends ? social : null));
			person.Interrupt(task, time, end);
			if (isWoken && sleep is not null)
				person.SetLevel(sleep, person.GetLevel(sleep) + settings.Talking.WakeLevel);
			talks.Add((person, task));
		}

		if (first.Relationships.TryGetValue(second, out var relationship))
			relationship.LastTalked = time;
		foreach (var (person, task) in talks)
			OnTaskStarted(person, task, time);

		// Talking with someone counts toward the player's "talk" step, if it talks with them.
		if (first == Player && !IsAutomatic(first))
			OnPlayerTalking(second);
		else if (second == Player && !IsAutomatic(second))
			OnPlayerTalking(first);
	}

	/// <summary>
	/// Has someone the game decides for drop their goal now and choose a new one, such as after a developer changed their
	/// needs. Time already spent on a quota need counts, but a run that isn't finished doesn't. They stop working if they
	/// were, and give up every sequence of steps they were in the middle of, though not appointments they haven't set off
	/// for yet.
	/// </summary>
	public void CancelGoal(Person person)
	{
		if (IsAutomatic(person))
		{
			DropPlan(person);
			foreach (var sequence in person.Sequences.Where(x => x.HasSetOff).Reverse().ToList())
				m_scheduler.EndSequence(person, sequence, Time);
			m_scheduler.Continue(person, Time);
		}
	}

	/// <summary>
	/// Has someone stop the tasks they were doing, bringing their needs up to now. They stop working, and the step they
	/// were carrying out is left to be carried out again. Someone who was sleeping keeps their night clothes on, until they
	/// choose to do something other than sleep (or for the player, change out of them).
	/// </summary>
	private void DropPlan(Person person)
	{
		person.UpdateNeeds(Time);
		person.IsAtWork = false;
		person.SetTasks([]);
		if (person.Current is { } current)
			current.IsCarriedOut = false;
	}

	/// <summary>
	/// Moves the player to another location now, such as along a path, and has them see the people there and be seen.
	/// Leaving where a step of theirs was being done fails it.
	/// </summary>
	public async Task MovePlayerAsync(Location location)
	{
		MovePerson(Player, location, Time);
		SeeEachOther(Player, Time);
		m_scheduler.ReactToCompany(Time);
		await UpdatePlayerStepsAsync().ConfigureAwait(true);
	}

	/// <summary>
	/// Moves someone to another location at the given time. They put away the clothes they're carrying when they get home. Everyone awake where they were sees them leave for it, and
	/// so does anyone who left there at the same moment, and the person sees those people where they last saw them.
	/// </summary>
	internal void MovePerson(Person person, Location location, TimePoint time)
	{
		var from = person.Location;
		var playerLocation = PlayerSeenLocation;
		person.Location = location;

		// Reaching their destination clears it.
		if (location == person.Destination)
			person.Destination = null;
		m_scheduler.OnCompanyChanged(from);

		// Someone who leaves an appointment's place can be asked to start it again once they're back.
		foreach (var appointment in m_appointments.Where(x => x.Place == from && from != location && (x.Worker == person || x.Client == person)))
			appointment.ClearDeclined();
		m_scheduler.OnCompanyChanged(location);

		// The player sees themselves arrive, and anyone else come or go where they are.
		var seen = from == location ? null :
			person == Player || location == playerLocation ? new LogEntry(time, LogEntryKind.Arrived, person, location, Place: from) :
			from == playerLocation ? new LogEntry(time, LogEntryKind.Left, person, from, Place: location) :
			null;
		if (seen is not null)
			EventLog.Add(seen);

		// Clothes carried home are put away.
		if (location == person.Home)
			ClothingRules.PutAway(person);

		var left = new Sighting(from, time, SightingActivity.Left, Destination: location);
		foreach (var other in from.People.Where(x => x != person))
		{
			if (!other.IsAsleep)
				other.See(person, left);
			person.See(other, Observe(other, time));
		}

		// People who leave together still see each other go.
		if (m_departureTime != time)
		{
			m_departures.Clear();
			m_departureTime = time;
		}
		foreach (var departure in m_departures.Where(x => x.From == from && x.Person != person))
		{
			departure.Person.See(person, left);
			person.See(departure.Person, new Sighting(from, time, SightingActivity.Left, Destination: departure.To));
		}
		m_departures.Add((person, from, location));
	}

	internal void OnTaskStarted(Person person, PersonTask task, TimePoint time)
	{
		if (person.Location == PlayerSeenLocation || person == Player)
			LogTaskStarted(person, task, time);
		SeeEachOther(person, time);
		TaskStarted?.Invoke(this, new PersonTaskEventArgs(person, task, time));
		m_scheduler.OnTaskStarted(person, task, time);
	}

	/// <summary>
	/// Where the player sees what happens, for the event log: where they are, or nowhere while they're walking away from it.
	/// </summary>
	private Location? PlayerSeenLocation => m_isPlayerWalking || Player.CurrentTask is TravelTask ? null : Player.Location;

	/// <summary>
	/// Has the player walk to a location next to theirs, which takes the travel time between them, and can't be stopped
	/// partway, while time passes for everyone. Someone asking them to chat on the way stops them where they are. Returns
	/// whether they got there.
	/// </summary>
	public async Task<bool> WalkPlayerToAsync(Location location)
	{
		ArgumentNullException.ThrowIfNull(location);
		m_isPlayerWalking = true;
		try
		{
			await AdvanceTimeAsync(GetTravelTime(Player.Location, location), canStop: false, isPaced: false).ConfigureAwait(true);
		}
		finally
		{
			m_isPlayerWalking = false;
		}

		var isThere = Player.TalkRequest is null;
		if (isThere)
			await MovePlayerAsync(location).ConfigureAwait(true);
		return isThere;
	}

	/// <summary>
	/// Logs someone in the player's location starting a task, unless it only carries on with what was last logged for
	/// them (other than changing clothes in between), such as resting again, or following someone again after changing with
	/// them, or it's the other half of a talk just logged. Walking isn't logged, since leaving is.
	/// </summary>
	private void LogTaskStarted(Person person, PersonTask task, TimePoint time)
	{
		var location = person.Location;
		LogEntry? entry = task switch
		{
			BusyTask { IsAside: true } => new LogEntry(time, LogEntryKind.ChangingClothes, person, location),
			BusyTask busy => person.Goal is { } goal
				? new LogEntry(time, LogEntryKind.Busy, person, location, Need: goal, Step: busy.Step is { } step && step < goal.Steps.Count ? step : null,
					Other: ActivityText.HasPartner(goal.GetActivity(busy.Step, person.Business?.Profession.Id)) ? person.Current?.Partner : null)
				: new LogEntry(time, LogEntryKind.Idle, person, location),
			ChangeClothesTask { IsCoverUp: true } => new LogEntry(time, LogEntryKind.CoveringUp, person, location),
			ChangeClothesTask => new LogEntry(time, LogEntryKind.ChangingClothes, person, location),
			PrivacyTask => new LogEntry(time, LogEntryKind.WaitingForPrivacy, person, location),
			TalkTask talk => new LogEntry(time, LogEntryKind.Talking, person, location, Other: talk.With),
			WaitTask wait => new LogEntry(time, LogEntryKind.Waiting, person, location, Other: wait.For),
			MeetTask meet => new LogEntry(time, LogEntryKind.Meeting, person, location, Other: meet.Appointment.GetOther(person)),
			FollowTask follow => new LogEntry(time, LogEntryKind.Following, person, location, Other: follow.Leader),
			_ => null,
		};

		var latest = entry is null ? null : IsChangingClothes(entry) ? EventLog.GetLatest(person) : EventLog.GetLatest(person, x => !IsChangingClothes(x));
		var isRepeat = entry is not null && latest is not null && latest with { Time = time } == entry;
		var isOtherHalf = entry is { Kind: LogEntryKind.Talking } && EventLog.Entries.Count != 0 && EventLog.Entries[^1] is { Kind: LogEntryKind.Talking } last &&
			last.Time == time && last.Person == entry.Other && last.Other == person;
		if (entry is not null && !isRepeat && !isOtherHalf)
			EventLog.Add(entry);
	}

	/// <summary>
	/// Whether an entry in the event log is someone changing clothes, which can come in the middle of what they're doing.
	/// </summary>
	private static bool IsChangingClothes(LogEntry entry) => entry.Kind is LogEntryKind.ChangingClothes or LogEntryKind.CoveringUp or LogEntryKind.WaitingForPrivacy;

	/// <summary>
	/// Has someone and everyone else where they are see each other doing what they're doing now. Anyone asleep sees no
	/// one, except that the person sees everyone as they fall asleep.
	/// </summary>
	internal static void SeeEachOther(Person person, TimePoint time)
	{
		var seen = Observe(person, time);
		foreach (var other in person.Location.People.Where(x => x != person))
		{
			if (!other.IsAsleep)
				other.See(person, seen);
			person.See(other, Observe(other, time));
		}
	}

	/// <summary>
	/// Gets what someone looks like they're doing right now, where they are.
	/// </summary>
	internal static Sighting Observe(Person person, TimePoint time) => person.CurrentTask switch
	{
		TravelTask travel => new Sighting(person.Location, time, SightingActivity.Walking, Destination: travel.Destination),
		TalkTask talk => new Sighting(person.Location, time, SightingActivity.Talking, With: talk.With),
		WaitTask or PrivacyTask => new Sighting(person.Location, time, SightingActivity.Idle),
		BusyTask busy => person.Goal is { } goal
			? new Sighting(person.Location, time, SightingActivity.Busy, goal, Doing: ActivityText.Fill(goal.GetActivity(busy.Step, person.Business?.Profession.Id), person.Current))
			: new Sighting(person.Location, time, SightingActivity.Idle),
		_ => new Sighting(person.Location, time, SightingActivity.None),
	};

	/// <summary>
	/// Every place in the town: the quarters, and the houses and businesses in them.
	/// </summary>
	public IReadOnlyList<Location> Locations { get; }

	/// <summary>
	/// The map features in the town, such as a river or a forest.
	/// </summary>
	public IReadOnlyList<Feature> Features { get; }

	public IReadOnlyList<Business> Businesses { get; }

	/// <summary>
	/// The relationships between every two people.
	/// </summary>
	public IReadOnlyList<Relationship> Relationships { get; }

	/// <summary>
	/// The families in the town, starting with the player's.
	/// </summary>
	public IReadOnlyList<Family> Families { get; }

	/// <summary>
	/// Everyone in the game world, family by family, starting with the player's family.
	/// </summary>
	public IReadOnlyList<Person> People { get; }

	public Person Player { get; }

	/// <summary>
	/// The people in the game world, other than the player.
	/// </summary>
	public IReadOnlyList<Person> Npcs { get; }

	/// <summary>
	/// The most times the player's steps are carried on through steps they've already got to at once, so steps that keep
	/// leading back to each other can't go on forever.
	/// </summary>
	private const int MaxPlayerStepsAdvanced = 100;

	/// <summary>
	/// The most days at a time the game carries out the player's steps for them.
	/// </summary>
	private const int MaxPlayerRuns = 7;

	private readonly Scheduler m_scheduler;

	// The place the player's steps needed them to go when it was last checked, which became their destination.
	private Location? m_stepsDestination;
	private readonly System.Collections.ObjectModel.Collection<Appointment> m_appointments;
	private readonly System.Collections.ObjectModel.Collection<Job> m_jobs;
	private bool m_isHeadless;

	// Who left where at the moment of the last departure, so people who leave together see each other go. It's only
	// needed until time moves on, so it isn't saved.
	private readonly Collection<(Person Person, Location From, Location To)> m_departures = [];

	// Whether the player is walking to the next location themselves (see WalkPlayerToAsync), which is only while time passes.
	private bool m_isPlayerWalking;
	private TimePoint? m_departureTime;
	private TimePoint m_time;
}

/// <summary>
/// How far along the player (or their family, for a need it shares) is with a quota need this day or week: how much is
/// done of how much it takes, in minutes, or in runs for a quota measured in runs, and whether it can be worked on today.
/// </summary>
public sealed record PlayerQuotaProgress(Need Need, long Done, long Total, bool IsOpenToday)
{
	/// <summary>
	/// How much of it is done, from 0 to 1. A quota of nothing is done.
	/// </summary>
	public double Fraction => Total <= 0 ? 1 : (double) Done / Total;
}

/// <summary>
/// Something the player can start: a need, whether they can start it now (and if not, why), and whether they're already
/// in the middle of its steps, which starting it carries on with.
/// </summary>
/// <summary>
/// A time the player can wait until (see <see cref="GameWorld.GetPlayerWaitChoices"/>) and why, with the part of the day
/// it starts, for the next part of the day, the appointment, for setting off, and the step's title, for a step that opens.
/// </summary>
public sealed record PlayerWaitChoice(TimePoint Until, PlayerWaitReason Reason, CalendarDayPart? DayPart = null, Appointment? Appointment = null, string? Title = null);

/// <summary>
/// Why the player might wait until a time.
/// </summary>
public enum PlayerWaitReason
{
	/// <summary>
	/// For so many minutes, offered by the schedules.
	/// </summary>
	Minutes,

	/// <summary>
	/// Until the next part of the day, offered by the schedules.
	/// </summary>
	DayPart,

	/// <summary>
	/// Until it's time to set off for the next appointment.
	/// </summary>
	SetOff,

	/// <summary>
	/// Until the step they're on opens.
	/// </summary>
	Opens,
}

/// <summary>
/// A shorter shift the player can choose for a quota need (see <see cref="GameWorld.GetPlayerShiftChoices"/>): how many
/// minutes they'd spend on it, when that would end, walking there included, and for one that ends with the next part of
/// the day, which part it is.
/// </summary>
public sealed record PlayerShiftChoice(int Minutes, TimePoint Until, CalendarDayPart? DayPart);

public sealed record PlayerStartChoice(Need Need, PlayerStartBlock Block, bool IsInProgress)
{
	public bool CanStart => Block == PlayerStartBlock.None;
}

/// <summary>
/// Why the player can't start something now.
/// </summary>
public enum PlayerStartBlock
{
	/// <summary>
	/// They can start it.
	/// </summary>
	None,

	/// <summary>
	/// It can't be met today.
	/// </summary>
	Closed,

	/// <summary>
	/// It's done for the day or week.
	/// </summary>
	Done,

	/// <summary>
	/// There's somewhere its steps go that can't be reached, or no one to look for.
	/// </summary>
	NoPlace,
}

/// <summary>
/// One of the steps the player is shown for a sequence of theirs (see <see cref="GameWorld.GetPlayerSteps"/>): a step,
/// with the place it would take them for going somewhere (the nearest, for quarters), an option of a "choose" step, the
/// steps of whichever option is chosen, or before an appointment starts, going to it or meeting the one they're meeting.
/// The current one is the one the sequence is on.
/// </summary>
public sealed record PlayerStep(StepSequence Sequence, PlayerStepKind Kind, int StepIndex, Location? Place = null, StepOption? Option = null, bool IsCurrent = false)
{
	/// <summary>
	/// The step itself (for an option, the "choose" step, and for going to or meeting at an appointment, its "meet" step).
	/// </summary>
	public Step Step => Sequence.Need.Steps[StepIndex];
}

/// <summary>
/// What one of the steps the player is shown stands for.
/// </summary>
public enum PlayerStepKind
{
	/// <summary>
	/// A step.
	/// </summary>
	Step,

	/// <summary>
	/// One of the options of a "choose" step, for the player to choose.
	/// </summary>
	Option,

	/// <summary>
	/// The steps of whichever option of a "choose" step is chosen.
	/// </summary>
	OptionSteps,

	/// <summary>
	/// Going to an appointment.
	/// </summary>
	GoToAppointment,

	/// <summary>
	/// Meeting the one they're meeting at an appointment, once both are there.
	/// </summary>
	Meet,

	/// <summary>
	/// Following someone, as part of their steps, until they're let go.
	/// </summary>
	Follow,
}

/// <summary>
/// An "ask" step the player can ask someone in conversation: the step a sequence of theirs is on.
/// </summary>
public sealed record PlayerAsk(StepSequence Sequence, int StepIndex)
{
	/// <summary>
	/// The step itself.
	/// </summary>
	public Step Step => Sequence.Need.Steps[StepIndex];
}

/// <summary>
/// Tells that a step of someone's failed, and why.
/// </summary>
public sealed class StepFailedEventArgs(Person person, StepSequence sequence, int step, FailCause cause, TimePoint time, Person? who = null, bool isEnding = true) : EventArgs
{
	public Person Person { get; } = person;

	public StepSequence Sequence { get; } = sequence;

	/// <summary>
	/// The index of the step that failed.
	/// </summary>
	public int Step { get; } = step;

	public FailCause Cause { get; } = cause;

	/// <summary>
	/// When it failed, which can be before the world's time while time is passing.
	/// </summary>
	public TimePoint Time { get; } = time;

	/// <summary>
	/// Who the failure was down to, if anyone in particular: who said no, didn't come, or was too shy.
	/// </summary>
	public Person? Who { get; } = who;

	/// <summary>
	/// Whether it gives up the steps (finishing up first, if they have "finally" steps), rather than carrying on with them
	/// some other way.
	/// </summary>
	public bool IsEnding { get; } = isEnding;
}

/// <summary>
/// The answer to a question the player asked: whether it was yes, the appointment a yes made, if any, and how much time
/// passed.
/// </summary>
public sealed record PlayerAskAnswer(bool IsYes, Appointment? Appointment, TimeOffset Spent);

/// <summary>
/// Tells that the player missed an appointment: the one they were to meet gave up waiting for them, or never came.
/// </summary>
public sealed class AppointmentMissedEventArgs(Appointment appointment, bool isOtherWaiting) : EventArgs
{
	public Appointment Appointment { get; } = appointment;

	/// <summary>
	/// Whether the one they were to meet was waiting there and gave up, rather than never coming.
	/// </summary>
	public bool IsOtherWaiting { get; } = isOtherWaiting;
}

/// <summary>
/// What the player can do instead of just stopping a sequence of steps: finish up, carrying out the "finally" steps still
/// to do, and keep the bundle its steps made, to finish them later, if they carry one (or null).
/// </summary>
public sealed record PlayerStopOptions(bool CanFinishUp, Bundle? Kept)
{
	/// <summary>
	/// Whether there's anything to do other than just stopping, so the player should be asked.
	/// </summary>
	public bool HasChoice => CanFinishUp || Kept is not null;
}
