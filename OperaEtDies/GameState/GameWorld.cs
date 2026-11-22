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
		m_scheduler = new Scheduler(this);
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

	internal void OnAppointmentMissed(Appointment appointment, bool isOtherWaiting) =>
		AppointmentMissed?.Invoke(this, new AppointmentMissedEventArgs(appointment, isOtherWaiting));

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
	/// <see cref="AdvanceTime"/>.
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
	/// Moves the time forward. All time passes through here, so anything that happens as time passes can be handled in
	/// one place: everyone works through their tasks in the order they end, choosing new goals as they finish them. If
	/// the stop check is met after a task ends, someone asks the player to chat, or it's time for the player to set off for
	/// an appointment, time stops there instead, and the player stops whatever they were doing so they can answer. No
	/// time passes at all while they're being asked whether to set off. Returns how much time passed.
	/// </summary>
	/// <exception cref="ArgumentException">The offset is negative.</exception>
	public TimeOffset AdvanceTime(TimeOffset offset, Func<bool>? shouldStop = null)
	{
		if (offset < GameData.Calendar.CreateTimeOffset(0, 0, 0))
			throw new ArgumentException("Time can't move backward.", nameof(offset));

		// No time passes while the player is being asked whether to set off for an appointment, until they answer.
		if (GetPlayerSetOffSequence() is not null || Player.AskRequest is not null)
			return GameData.Calendar.CreateTimeOffset(0, 0, 0);

		var start = Time;
		var player = Player;
		Time = m_scheduler.RunUntil(start, start + offset, () => IsPlayerAsked || shouldStop?.Invoke() == true);

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
		return Time - start;
	}

	/// <summary>
	/// Passes time until someone arrives at or leaves the location, or until the next part of the day starts, whichever
	/// comes first. While following someone, the wait stops instead when they start spending time on a different goal (or
	/// on being idle), or when they leave, and if they leave, the player goes with them, arriving when they do, unless
	/// they'd be leaving home without being dressed enough to go out. Starting another stretch of the same goal, such as
	/// resting again, doesn't stop it. Returns whether the wait was stopped by someone.
	/// </summary>
	public bool WaitForPeople(Location location, Person? following = null)
	{
		var isChanged = false;
		var goal = following?.Goal;
		void OnPeopleChanged(object? sender, NotifyCollectionChangedEventArgs e) => isChanged |= following is null || following.Location != location;
		void OnTaskStarted(object? sender, PersonTaskEventArgs e)
		{
			if (e.Person == following && e.Task is BusyTask)
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
			AdvanceTime(GameData.Calendar.GetNextDayPartStart(Time) - Time, () => isChanged);
		}
		finally
		{
			people.CollectionChanged -= OnPeopleChanged;
			TaskStarted -= OnTaskStarted;
		}

		// The player can't follow someone out of their home unless they're dressed enough to go out.
		if (following is not null && following.Location != location && (location != Player.Home || GetPlayerUncoveredSlotsOutside().Count == 0))
			MovePlayer(following.Location);
		else
			UpdatePlayerSteps();

		return isChanged;
	}

	/// <summary>
	/// Gets how many minutes of a quota need the player (or their family, for a need it shares) still has to spend on it
	/// this day or week, less the time the rest of the family already plans to spend on it. It's 0 or less once the need is
	/// done.
	/// </summary>
	public long GetPlayerRemainingMinutes(Need need) => Scheduler.GetRemainingMinutes(Player, need, Time);

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
	public TimeOffset StartPlayerSteps(Need need, bool isAutomatic)
	{
		ArgumentNullException.ThrowIfNull(need);
		var player = Player;
		player.UpdateNeeds(Time);
		var sequence = player.GetSequence(need);
		if (sequence is null)
		{
			ClearPlayerTasks();
			sequence = Scheduler.StartSequence(player, need, 0, m_scheduler.GetSequenceMinutes(player, need, Time), [], null);
		}
		else
		{
			FocusPlayerSteps(sequence);
		}

		return isAutomatic ? DoPlayerStepsAutomatically(sequence) : UpdatePlayerSteps();
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
	public TimeOffset DoPlayerStep(StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		FocusPlayerSteps(sequence);
		sequence.Takeover = StepTakeover.Step;
		return RunPlayerSteps(sequence);
	}

	/// <summary>
	/// Has a sequence of the player's carried out for them, as for anyone else, walking included, until it's done, while
	/// time passes for everyone. After a pass of a "repeat" step that reconsiders, the player is asked whether to keep going
	/// (see <see cref="AnswerKeepGoing"/>), and it carries on for them once they answer. Anything that stops them partway,
	/// such as someone asking to chat, leaves the rest for them to take themselves. Returns how much time passed.
	/// </summary>
	public TimeOffset DoPlayerStepsAutomatically(StepSequence sequence)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		FocusPlayerSteps(sequence);
		sequence.Takeover = StepTakeover.All;
		return RunPlayerSteps(sequence);
	}

	/// <summary>
	/// Passes time while the game carries out the player's current steps, until it leaves the rest to them, or something
	/// stops it. Returns how much time passed.
	/// </summary>
	private TimeOffset RunPlayerSteps(StepSequence sequence)
	{
		var player = Player;
		var start = Time;
		m_scheduler.Continue(player, Time);
		for (var count = 0; count < MaxPlayerRuns && player.Tasks.Count != 0 && player.Sequences.Contains(sequence) && !IsPlayerAsked; count++)
			AdvanceTime(GameData.Calendar.CreateTimeOffset(1, 0, 0), () => player.Tasks.Count == 0 || player.Current != sequence);
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
	/// one they take themselves. Steps fail if the player left where they were being done. Returns how much time passed.
	/// </summary>
	public TimeOffset UpdatePlayerSteps()
	{
		var player = Player;
		var start = Time;
		if (!IsAutomatic(player))
		{
			foreach (var sequence in player.Sequences.Where(x => x != player.Current).ToList())
			{
				for (var count = 0; count < MaxPlayerStepsAdvanced && player.Sequences.Contains(sequence) && m_scheduler.AdvanceStep(player, sequence, Time, isPassive: true); count++)
				{
				}
			}

			if (player.Current is { } current && player.TaskEnd is null)
				RunPlayerSteps(current);
		}

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
	public TimeOffset ChoosePlayerOption(StepSequence sequence, StepOption option)
	{
		if (!GetPlayerOptions(sequence).Contains(option))
			throw new InvalidOperationException("The player can't choose that option now.");

		var need = sequence.Need;
		var chosen = need.GetOptions(sequence.Step).First(x => x.Option == option);
		sequence.GoTo(Scheduler.GetOptionStart(need, sequence.Step, chosen));
		FocusPlayerSteps(sequence);
		return ContinuePlayerSteps(sequence);
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
	public TimeOffset AnswerKeepGoing(StepSequence sequence, bool keepGoing)
	{
		if (!IsAskingPlayerToKeepGoing(sequence))
			throw new InvalidOperationException("The player isn't being asked whether to keep going.");

		var need = sequence.Need;
		sequence.GoTo(keepGoing ? need.GetLoopStart(sequence.Step) : need.GetNextStep(sequence.Step));
		FocusPlayerSteps(sequence);
		return ContinuePlayerSteps(sequence);
	}

	/// <summary>
	/// Carries on with a sequence of the player's after they decided something: for them, if all of it is being done for
	/// them, or else up to the next step they take themselves.
	/// </summary>
	private TimeOffset ContinuePlayerSteps(StepSequence sequence) => sequence.Takeover == StepTakeover.All ? RunPlayerSteps(sequence) : UpdatePlayerSteps();

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
		for (var index = sequence.Step; sequence.Role == SequenceRole.Steps && index < need.Steps.Count && seen.Add(index);)
		{
			var step = need.Steps[index];
			var next = need.GetNextStep(index);
			var isCurrent = index == sequence.Step && steps.Count == 0;
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
	public TimeOffset AnswerSetOff(bool isSettingOff)
	{
		var sequence = GetPlayerSetOffSequence() ?? throw new InvalidOperationException("The player isn't being asked whether to set off for an appointment.");
		var start = Time;
		if (isSettingOff)
		{
			ClearPlayerTasks();
			m_scheduler.TakeUpAppointment(sequence, Player, Time);
			UpdatePlayerSteps();
		}
		else
		{
			m_scheduler.CallOff(sequence.Appointment!, Time);
		}

		return Time - start;
	}

	/// <summary>
	/// Whether the player is being asked something, so time stops until they answer: someone asking to chat, whether to set
	/// off for an appointment, or someone asking them the question of an "ask" step.
	/// </summary>
	private bool IsPlayerAsked => Player.TalkRequest is not null || GetPlayerSetOffSequence() is not null || Player.AskRequest is not null;

	/// <summary>
	/// Has the player answer the question someone asked them (see <see cref="Person.AskRequest"/>), as anyone else would
	/// answer it, except that the player decides whether to say yes, while time passes for everyone as they talk. A yes can
	/// make an appointment with them, or lead to them following the one asking. Returns the appointment a yes made, if any.
	/// </summary>
	/// <exception cref="InvalidOperationException">No one is asking the player anything.</exception>
	public Appointment? AnswerAsk(bool isYes)
	{
		var appointment = m_scheduler.AnswerPlayerAsk(Player, isYes, Time);
		if (Player.CurrentTask is TalkTask)
			FinishPlayerTalk();
		return appointment;
	}

	/// <summary>
	/// Gets the player's needs that saying yes to the question someone is asking them would keep them from seeing to in
	/// time, as anyone else's yes is checked, though the player can still say yes.
	/// </summary>
	public IReadOnlyList<Need> GetPlayerAskConflicts(Person asker) => m_scheduler.GetPlayerAskConflicts(asker, Player, Time);

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
	public PlayerAskAnswer AskPlayerQuestion(Person person)
	{
		var ask = GetPlayerAsk(person) ?? throw new InvalidOperationException($"The player has nothing to ask person {person.Id}.");
		var start = Time;
		FocusPlayerSteps(ask.Sequence);
		var (isYes, appointment) = m_scheduler.TalkOrAskWith(Player, ask.Sequence, ask.Step, ask.StepIndex, person, Time);
		ask.Sequence.IsCarriedOut = true;
		FinishPlayerTalk();
		UpdatePlayerSteps();
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

	internal void OnStepFailed(Person person, StepSequence sequence, int step, FailCause cause) =>
		StepFailed?.Invoke(this, new StepFailedEventArgs(person, sequence, step, cause));

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
	/// <see cref="FinishPlayerChanging"/>).
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
	/// no time until they're done changing (see <see cref="FinishPlayerChanging"/>).
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
	/// until they're done changing (see <see cref="FinishPlayerChanging"/>).
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
	/// <see cref="FinishPlayerChanging"/>).
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
	/// while time passes for everyone. Returns how much time passed, or null if nothing changed.
	/// </summary>
	public TimeOffset? FinishPlayerChanging(IReadOnlyCollection<ClothingPiece> wornBefore)
	{
		var spent = Player.Clothing.Pieces.ToHashSet().SetEquals(wornBefore) ? (TimeOffset?) null : PassPlayerChangingTime();
		UpdatePlayerSteps();
		return spent;
	}

	/// <summary>
	/// Whether the player can pick up a piece of clothing kept at home to carry it: they're at home, and it's there and
	/// isn't in the wash or drying.
	/// </summary>
	public bool CanPlayerTake(ClothingPiece piece) => Player.Location == Player.Home && piece.Container == Player.Home.Items && piece.IsReady(Time);

	/// <summary>
	/// Has the player pick up a piece of clothing kept at home to carry it, which takes no time.
	/// </summary>
	/// <exception cref="InvalidOperationException">The player can't take the piece.</exception>
	public void PlayerTake(ClothingPiece piece)
	{
		if (!CanPlayerTake(piece))
			throw new InvalidOperationException($"The player can't take {piece.Id}.");

		Player.Home.Items.Remove(piece);
		Player.Inventory.Add(piece);
		UpdatePlayerSteps();
	}

	/// <summary>
	/// Whether the player can put away a piece of clothing they're carrying: they're at home, and it isn't in a bundle,
	/// which only its step puts down.
	/// </summary>
	public bool CanPlayerPutAway(ClothingPiece piece) => Player.Location == Player.Home && piece.Container == Player.Inventory && !Player.IsInBundle(piece);

	/// <summary>
	/// Has the player put away a piece of clothing they're carrying at home, which takes no time.
	/// </summary>
	/// <exception cref="InvalidOperationException">The player can't put the piece away.</exception>
	public void PlayerPutAway(ClothingPiece piece)
	{
		if (!CanPlayerPutAway(piece))
			throw new InvalidOperationException($"The player can't put away {piece.Id}.");

		Player.Inventory.Remove(piece);
		Player.Home.Items.Add(piece);
		UpdatePlayerSteps();
	}

	/// <summary>
	/// Gets the clothing slots in which the player isn't covered enough for where they are: at home, or out of the house,
	/// and for their work while they're working.
	/// </summary>
	public IReadOnlyList<ClothingSlot> GetPlayerUncoveredSlots() =>
		GetPlayerUncoveredSlots(Player.Clothing.Pieces.Select(x => x.Clothing), Player.Location == Player.Home ? ModestyLevel.Home : ModestyLevel.Outside, IsPlayerWorking);

	/// <summary>
	/// Gets how modest the player is in each clothing slot their modesty rule checks, and the least it calls for where they
	/// are, such as for developers to see.
	/// </summary>
	public IReadOnlyList<(ClothingSlot Slot, int Modesty, int Minimum)> GetPlayerModesty() =>
		ClothingRules.GetModesty(GameData, Player, Player.Clothing.Pieces.Select(x => x.Clothing), Player.Location == Player.Home ? ModestyLevel.Home : ModestyLevel.Outside, IsPlayerWorking);

	/// <summary>
	/// Gets the clothing slots in which the player isn't covered enough to go out of the house.
	/// </summary>
	public IReadOnlyList<ClothingSlot> GetPlayerUncoveredSlotsOutside() => GetPlayerUncoveredSlots(Player.Clothing.Pieces.Select(x => x.Clothing), ModestyLevel.Outside, IsPlayerWorking);

	/// <summary>
	/// Gets the clothing slots in which the player wouldn't be covered enough for a step, where they are, in what they're
	/// wearing, such as for working. The player changes clothes themselves, so taking a step doesn't.
	/// </summary>
	public IReadOnlyList<ClothingSlot> GetPlayerStepUncoveredSlots(Step step)
	{
		ArgumentNullException.ThrowIfNull(step);
		var player = Player;
		return GetPlayerUncoveredSlots(player.Clothing.Pieces.Select(x => x.Clothing), player.Location == player.Home ? ModestyLevel.Home : ModestyLevel.Outside, step.AtWork || IsPlayerWorking);
	}

	private IReadOnlyList<ClothingSlot> GetPlayerUncoveredSlots(IEnumerable<ClothingItem> worn, ModestyLevel level, bool isAtWork) =>
		ClothingRules.GetUncoveredSlots(GameData, Player, worn, level, isAtWork);

	/// <summary>
	/// Where the player's clothes go when they take them off: their home, if they're there (in their wardrobe), or else, or
	/// when they're to be carried (as in their inventory), what they're carrying.
	/// </summary>
	private ItemContainer GetPlayerClothesContainer(bool isCarried) => !isCarried && Player.Location == Player.Home ? Player.Home.Items : Player.Inventory;

	/// <summary>
	/// Whether the player is working, for how covered they must be: while at work, and from when they start work's steps
	/// until they're done with them, so they can change into work clothes and go to work in them.
	/// </summary>
	private bool IsPlayerWorking => Player.IsAtWork || Player.Sequences.Any(x => x.Need.IsWork && x.HasSetOff);

	/// <summary>
	/// Passes the time it takes the player to change clothes, while time passes for everyone.
	/// </summary>
	private TimeOffset PassPlayerChangingTime()
	{
		var player = Player;
		var minutes = GameData.Schedules.Clothing.ChangeMinutes;
		player.SetTasks([new BusyTask(minutes, IsAside: true)]);
		m_scheduler.Continue(player, Time);
		return AdvanceTime(GameData.Calendar.CreateTimeOffset(0, 0, minutes), () => player.Tasks.Count == 0);
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
			AdvanceTime(end - Time);
		if (Player.Location == Player.Home)
			ClothingRules.PutAway(Player);
	}

	/// <summary>
	/// Whether someone refuses to stop and talk right now, because their goal is too urgent.
	/// </summary>
	public bool IsTooBusyToTalk(Person person) => m_scheduler.IsTooBusyToTalk(person, Time);

	/// <summary>
	/// Has the player talk with someone for the given number of minutes, while time passes for everyone. Talking pauses
	/// what the other person was doing until it's over, waking them if they were asleep. When socializing, it meets the
	/// socializing need of each of them who counts the other as a friend. Returns how much time passed, or null if they
	/// refused because their goal is too urgent.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The minutes aren't more than 0.</exception>
	public TimeOffset? TalkTo(Person person, int minutes, bool isSocializing = false)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minutes);
		if (person.IsPlayer || IsTooBusyToTalk(person))
			return null;

		StartTalking(Player, person, minutes, Time, isSocializing);
		var spent = AdvanceTime(GameData.Calendar.CreateTimeOffset(0, 0, minutes), () => Player.Tasks.Count == 0);
		UpdatePlayerSteps();
		return spent;
	}

	/// <summary>
	/// Has the player ask someone where they're going and why, which takes a few minutes while time passes for everyone.
	/// The player remembers the answer with what they saw the person doing. Returns how much time passed and what the
	/// player saw and was told.
	/// </summary>
	/// <exception cref="InvalidOperationException">The person isn't with the player, or won't talk.</exception>
	public TalkAnswer AskWhatDoing(Person person)
	{
		CheckCanTalk(person);
		var time = Time;
		var seen = Observe(person, time) with { Plan = GetPlan(person) };
		StartTalking(Player, person, GameData.Schedules.Talking.AskDoingMinutes, time);
		Player.See(person, seen);
		return new TalkAnswer(FinishPlayerTalk(), seen);
	}

	/// <summary>
	/// Has the player ask someone whether they've seen another person, which takes a minute while time passes for everyone.
	/// If they have, the player learns what they know, as a secondhand sighting. Returns how much time passed and the
	/// sighting, or no sighting if they haven't seen the person.
	/// </summary>
	/// <exception cref="InvalidOperationException">The person isn't with the player, or won't talk.</exception>
	public TalkAnswer AskAbout(Person person, Person subject)
	{
		CheckCanTalk(person);
		var known = person.Sightings.GetValueOrDefault(subject);
		StartTalking(Player, person, GameData.Schedules.Talking.AskAboutMinutes, Time);
		if (known is not null)
			Player.LearnSighting(subject, known, person);
		return new TalkAnswer(FinishPlayerTalk(), known);
	}

	/// <summary>
	/// Has the player socialize with someone, while time passes for everyone. It meets the socializing need of each of
	/// them who counts the other as a friend. Returns how much time passed.
	/// </summary>
	/// <exception cref="InvalidOperationException">The person isn't with the player, or won't talk.</exception>
	public TimeOffset Socialize(Person person)
	{
		CheckCanTalk(person);
		StartTalking(Player, person, GameData.Schedules.Socializing.ChatMinutes, Time, isSocializing: true);
		return FinishPlayerTalk();
	}

	/// <summary>
	/// Agrees to talk with whoever asked the player, to chat or for a "talk" step of theirs, if they're still there and
	/// free, while time passes for everyone. Returns how much time passed, or null if they couldn't talk after all.
	/// </summary>
	public TimeOffset? AcceptTalk()
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

		return canTalk ? FinishPlayerTalk() : null;
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
	private TimeOffset FinishPlayerTalk()
	{
		var start = Time;
		AdvanceTime(Player.TaskEnd! - Time, () => Player.Tasks.Count == 0);
		UpdatePlayerSteps();
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
		return goal.GetActivity(step, profession);
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
	public void MovePlayer(Location location)
	{
		MovePerson(Player, location, Time);
		SeeEachOther(Player, Time);
		UpdatePlayerSteps();
	}

	/// <summary>
	/// Moves someone to another location at the given time. They put away the clothes they're carrying when they get home. Everyone awake where they were sees them leave for it, and
	/// so does anyone who left there at the same moment, and the person sees those people where they last saw them.
	/// </summary>
	internal void MovePerson(Person person, Location location, TimePoint time)
	{
		var from = person.Location;
		person.Location = location;

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
		SeeEachOther(person, time);
		TaskStarted?.Invoke(this, new PersonTaskEventArgs(person, task, time));
		m_scheduler.OnTaskStarted(person, task, time);
	}

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
		TalkTask => new Sighting(person.Location, time, SightingActivity.Talking),
		WaitTask => new Sighting(person.Location, time, SightingActivity.Idle),
		BusyTask busy => person.Goal is { } goal
			? new Sighting(person.Location, time, SightingActivity.Busy, goal, Doing: goal.GetActivity(busy.Step, person.Business?.Profession.Id))
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
	private readonly System.Collections.ObjectModel.Collection<Appointment> m_appointments;
	private readonly System.Collections.ObjectModel.Collection<Job> m_jobs;
	private bool m_isHeadless;

	// Who left where at the moment of the last departure, so people who leave together see each other go. It's only
	// needed until time moves on, so it isn't saved.
	private readonly Collection<(Person Person, Location From, Location To)> m_departures = [];
	private TimePoint? m_departureTime;
	private TimePoint m_time;
}

/// <summary>
/// Something the player can start: a need, whether they can start it now (and if not, why), and whether they're already
/// in the middle of its steps, which starting it carries on with.
/// </summary>
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
public sealed class StepFailedEventArgs(Person person, StepSequence sequence, int step, FailCause cause) : EventArgs
{
	public Person Person { get; } = person;

	public StepSequence Sequence { get; } = sequence;

	/// <summary>
	/// The index of the step that failed.
	/// </summary>
	public int Step { get; } = step;

	public FailCause Cause { get; } = cause;
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
