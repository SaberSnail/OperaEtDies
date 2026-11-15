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
		var atWorkProperty = gameData.CharacterProperties.Get(families.Properties.AtWork);
		var notAtWork = atWorkProperty.Values!.First(x => x.Id == families.NotAtWork);
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
		var relationships = families.Relationships;
		var headRelation = families.Properties.HeadRelation;
		for (var first = 0; first < people.Count; first++)
		{
			for (var second = first + 1; second < people.Count; second++)
			{
				var (a, b) = (people[first], people[second]);
				var score = relationships.ChooseScore(random, a.Family == b.Family ? families.HeadRelations.GetFamilyTie(a.Traits.GetValueId(headRelation), b.Traits.GetValueId(headRelation)) : null,
					isNeighbor: a.Home.Quarter is { } quarter && quarter == b.Home.Quarter);
				objects.Create(id => new Relationship(id, a, b, score));
			}
		}

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
				var player = Player;
				player.UpdateNeeds(Time);
				player.IsAtWork = false;
				player.SetPlan(null, []);
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
	/// the stop check is met after a task ends, or someone asks the player to chat, time stops there instead, and the player
	/// stops whatever they were doing so they can answer. Returns how much time passed.
	/// </summary>
	/// <exception cref="ArgumentException">The offset is negative.</exception>
	public TimeOffset AdvanceTime(TimeOffset offset, Func<bool>? shouldStop = null)
	{
		if (offset < GameData.Calendar.CreateTimeOffset(0, 0, 0))
			throw new ArgumentException("Time can't move backward.", nameof(offset));
		var start = Time;
		var player = Player;
		Time = m_scheduler.RunUntil(start, start + offset, () => player.TalkRequest is not null || shouldStop?.Invoke() == true);

		// Someone asking the player to chat stops time, and whatever the player was doing, so they can answer.
		if (player.TalkRequest is not null && player.Tasks.Count != 0 && player.CurrentTask is not TalkTask)
		{
			player.UpdateNeeds(Time);
			player.IsAtWork = false;
			player.SetPlan(null, []);
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
	/// on being idle), or when they leave, and if they leave, the player goes with them, arriving when they do. Starting
	/// another stretch of the same goal, such as resting again, doesn't stop it. Returns whether the wait was stopped by
	/// someone.
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

		if (following is not null && following.Location != location)
			MovePlayer(following.Location);

		return isChanged;
	}

	/// <summary>
	/// Gets what the player can spend time on where they are, through the needs' player actions: a need met here, or
	/// for a trip, the part at the place here, or the part at home that finishes a trip they've been to the place for.
	/// Whether each can be done right now is given by <see cref="GetPlayerActionMinutes"/> and
	/// <see cref="GetPlayerTripMinutes"/>.
	/// </summary>
	public IReadOnlyList<PlayerActionChoice> GetPlayerActions()
	{
		var player = Player;
		var choices = new List<PlayerActionChoice>();
		foreach (var need in GameData.Schedules.Needs.Where(x => x.PlayerAction is not null && Scheduler.HasNeed(player, x)))
		{
			if (Scheduler.IsPlaceFor(player, need, player.Location))
				choices.Add(new PlayerActionChoice(need, IsHome: false));
			if (need.IsTrips && player.PendingTrip == need && player.Location == player.Home)
				choices.Add(new PlayerActionChoice(need, IsHome: true));
		}

		return choices;
	}

	/// <summary>
	/// Gets how many minutes of a quota need the player (or their family, for a need it shares) still has to spend on it
	/// this day or week, less the time the rest of the family already plans to spend on it. It's 0 or less once the need is
	/// done.
	/// </summary>
	public long GetPlayerRemainingMinutes(Need need) => Scheduler.GetRemainingMinutes(Player, need, Time);

	/// <summary>
	/// Gets how many minutes the player would spend on one part of a trip, starting now, or null if they can't do it now.
	/// </summary>
	public int? GetPlayerTripMinutes(Need need, bool isHome) => m_scheduler.GetTripMinutes(Player, need, isHome, Time);

	/// <summary>
	/// Whether a quota need's player action can be done now: on a day the need can be met, and within its hours too if the
	/// action is only open then.
	/// </summary>
	public bool IsPlayerActionOpen(Need need) => m_scheduler.IsPlayerActionOpen(Player, need, Time);

	/// <summary>
	/// Has the player do one part of a trip, while time passes for everyone: one trip's time at the place, after which
	/// they still have to finish it at home, if it returns home, or the time at home that finishes it. Returns how much
	/// time passed.
	/// </summary>
	/// <exception cref="InvalidOperationException">The player can't do that part of the trip now.</exception>
	public TimeOffset DoPlayerTrip(Need need, bool isHome)
	{
		var minutes = GetPlayerTripMinutes(need, isHome) ?? throw new InvalidOperationException($"The player can't do the trip {need.Id} now.");
		var player = Player;
		player.PendingTrip = isHome || !need.ReturnHome ? null : need;
		return minutes == 0 ? GameData.Calendar.CreateTimeOffset(0, 0, 0) : DoPlayerAction(need, minutes);
	}

	/// <summary>
	/// Gets how many minutes the player would spend on a need with one of its player action options, starting now. It's 0
	/// for an option that runs until the need is met, if it already is.
	/// </summary>
	public int GetPlayerActionMinutes(Need need, PlayerActionOption option) => m_scheduler.GetActionMinutes(Player, need, option, Time);

	/// <summary>
	/// Has the player spend the given number of minutes on a need, starting and stopping work around it for a need met at
	/// work, while time passes for everyone. Returns how much time passed.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The minutes aren't more than 0.</exception>
	public TimeOffset DoPlayerAction(Need need, int minutes)
	{
		ArgumentNullException.ThrowIfNull(need);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minutes);

		var tasks = new List<PersonTask>();
		if (need.AtWork)
			tasks.Add(new AtWorkTask(true));
		tasks.Add(new BusyTask(minutes));
		if (need.AtWork)
			tasks.Add(new AtWorkTask(false));

		var player = Player;
		player.SetPlan(need, tasks);
		m_scheduler.Continue(player, Time);
		return AdvanceTime(GameData.Calendar.CreateTimeOffset(0, 0, minutes), () => player.Tasks.Count == 0);
	}

	/// <summary>
	/// Starts a new game: everyone is asleep at home, with their needs at their starting levels, and wakes up around the
	/// same time, except anyone whose work hours include midnight, who starts awake at work. Time runs until the player
	/// wakes, when the game begins.
	/// </summary>
	private void StartAsleep()
	{
		var settings = GameData.Schedules;
		var calendar = GameData.Calendar;
		var sleep = settings.Needs.FirstOrDefault(x => x.Id == settings.Start.Need && x.Kind == NeedKind.Level);
		foreach (var person in People)
		{
			foreach (var need in settings.Needs.Where(x => x.StartLevel is not null))
				person.SetLevel(need, Random.NextInRange(need.StartLevel!));

			// Someone whose work hours include midnight starts awake at work. Everyone else is asleep at home.
			if (!person.IsPlayer && m_scheduler.IsInWorkHours(person, Time))
			{
				person.Location = person.Workplace;
			}
			else
			{
				var minutes = Math.Max((settings.Start.WakeHour * 60) + Random.NextInRange(settings.Start.WakeMinutes), 1);
				if (sleep is not null)
					person.SetLevel(sleep, sleep.SatisfyPerHour!.Value * minutes / 60);
				person.SetPlan(sleep, [new BusyTask(minutes)]);
				person.StartTask(calendar.AddMinutes(Time, minutes));
			}
		}

		AdvanceTime(Player.TaskEnd! - Time);
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
		return AdvanceTime(GameData.Calendar.CreateTimeOffset(0, 0, minutes), () => Player.Tasks.Count == 0);
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
		StartTalking(Player, person, GameData.Schedules.Talking.SocializeMinutes, Time, isSocializing: true);
		return FinishPlayerTalk();
	}

	/// <summary>
	/// Agrees to chat with whoever asked the player, if they're still there and free, while time passes for everyone.
	/// Returns how much time passed, or null if they couldn't chat after all.
	/// </summary>
	public TimeOffset? AcceptTalk()
	{
		var player = Player;
		var asker = player.TalkRequest;
		player.TalkRequest = null;
		var canChat = asker is not null && asker.Location == player.Location && asker.CurrentTask is not TalkTask && player.CurrentTask is not TalkTask;
		if (canChat)
			m_scheduler.ChatWith(asker!, player, Time);
		return canChat ? FinishPlayerTalk() : null;
	}

	/// <summary>
	/// Turns down whoever asked the player to chat. They don't ask again for a while.
	/// </summary>
	public void DeclineTalk()
	{
		if (Player.TalkRequest is { } asker)
		{
			Player.TalkRequest = null;
			asker.GetRelationship(Player).LastDeclined = Time;
		}
	}

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
	/// Passes time until the player's talk is over.
	/// </summary>
	private TimeOffset FinishPlayerTalk() => AdvanceTime(Player.TaskEnd! - Time, () => Player.Tasks.Count == 0);

	/// <summary>
	/// Gets what someone would say they're doing: their goal, and where their walk ends if they're walking. Once they have
	/// nothing left to do for their goal, such as on the way home from fetching water, they don't mention it.
	/// </summary>
	internal static SightingPlan GetPlan(Person person) =>
		new(person.Tasks.Any(x => x is BusyTask) ? person.Goal : null, person.CurrentTask is TravelTask ? person.Tasks.TakeWhile(x => x is TravelTask).OfType<TravelTask>().Last().Destination : null);

	/// <summary>
	/// Has two people start talking at the given time for the given number of minutes, which interrupts what they were
	/// doing until it's over. Anyone woken up to talk has their sleep need rise. When socializing, it meets the socializing
	/// need of each of them who counts the other as a friend. They see each other talking, and it's when they last talked.
	/// </summary>
	internal void StartTalking(Person first, Person second, int minutes, TimePoint time, bool isSocializing = false)
	{
		var settings = GameData.Schedules;
		var sleep = settings.Needs.FirstOrDefault(x => x.Id == settings.Start.Need);
		var social = isSocializing ? settings.Needs.FirstOrDefault(x => x.Id == settings.Talking.SocializeNeed) : null;
		var isFriends = first.Relationships.TryGetValue(second, out var friendship) && GameData.Families.Relationships.IsFriend(friendship.Score);
		var end = GameData.Calendar.AddMinutes(time, minutes);
		var talks = new List<(Person Person, TalkTask Task)>();
		foreach (var (person, other) in new[] { (first, second), (second, first) })
		{
			var isWoken = person.IsAsleep;
			var task = new TalkTask(other, minutes, isFriends ? social : null);
			person.Interrupt(task, time, end);
			if (isWoken && sleep is not null)
				person.SetLevel(sleep, person.GetLevel(sleep) + settings.Talking.WakeLevel);
			talks.Add((person, task));
		}

		if (first.Relationships.TryGetValue(second, out var relationship))
			relationship.LastTalked = time;
		foreach (var (person, task) in talks)
			OnTaskStarted(person, task, time);
	}

	/// <summary>
	/// Has someone the game decides for drop their goal now and choose a new one, such as after a developer changed their
	/// needs. Time already spent on a quota need counts, but the time spent on a trip that isn't finished doesn't, since
	/// trips are counted whole. They stop working if they were.
	/// </summary>
	public void CancelGoal(Person person)
	{
		if (IsAutomatic(person))
		{
			person.UpdateNeeds(Time);
			if (person.Goal is { IsTrips: true } trip)
			{
				var people = trip.SharedByFamily ? person.Family.Members : [person];
				var tripMinutes = trip.GetShiftMinutes(people.Count);
				var unfinished = tripMinutes > 0 ? people.Sum(x => x.GetProgress(trip)) % tripMinutes : 0;
				person.SetProgress(trip, Math.Max(person.GetProgress(trip) - unfinished, 0));
			}

			person.IsAtWork = false;
			person.SetPlan(null, []);
			m_scheduler.Continue(person, Time);
		}
	}

	/// <summary>
	/// Moves the player to another location now, such as along a path, and has them see the people there and be seen.
	/// </summary>
	public void MovePlayer(Location location)
	{
		MovePerson(Player, location, Time);
		SeeEachOther(Player, Time);
	}

	/// <summary>
	/// Moves someone to another location at the given time. Everyone awake where they were sees them leave for it, and
	/// so does anyone who left there at the same moment, and the person sees those people where they last saw them.
	/// </summary>
	internal void MovePerson(Person person, Location location, TimePoint time)
	{
		var from = person.Location;
		person.Location = location;

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
		BusyTask => person.Goal is { } goal ? new Sighting(person.Location, time, SightingActivity.Busy, goal) : new Sighting(person.Location, time, SightingActivity.Idle),
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

	private readonly Scheduler m_scheduler;
	private bool m_isHeadless;

	// Who left where at the moment of the last departure, so people who leave together see each other go. It's only
	// needed until time moves on, so it isn't saved.
	private readonly Collection<(Person Person, Location From, Location To)> m_departures = [];
	private TimePoint? m_departureTime;
	private TimePoint m_time;
}

/// <summary>
/// Something the player can spend time on: a need, and for a trip, whether it's the part at home that finishes it.
/// </summary>
public sealed record PlayerActionChoice(Need Need, bool IsHome);
