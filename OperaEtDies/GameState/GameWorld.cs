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
	/// the stop check is met after a task ends, time stops there instead. Returns how much time passed.
	/// </summary>
	/// <exception cref="ArgumentException">The offset is negative.</exception>
	public TimeOffset AdvanceTime(TimeOffset offset, Func<bool>? shouldStop = null)
	{
		if (offset < GameData.Calendar.CreateTimeOffset(0, 0, 0))
			throw new ArgumentException("Time can't move backward.", nameof(offset));
		var start = Time;
		Time = m_scheduler.RunUntil(start, start + offset, shouldStop);
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
			Player.Location = following.Location;

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
	public bool IsPlayerActionOpen(Need need) => m_scheduler.IsPlayerActionOpen(need, Time);

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
	/// same time. Time runs until the player wakes, when the game begins.
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

			var minutes = Math.Max((settings.Start.WakeHour * 60) + Random.NextInRange(settings.Start.WakeMinutes), 1);
			if (sleep is not null)
				person.SetLevel(sleep, sleep.SatisfyPerHour!.Value * minutes / 60);
			person.SetPlan(sleep, [new BusyTask(minutes)]);
			person.StartTask(calendar.AddMinutes(Time, minutes));
		}

		AdvanceTime(Player.TaskEnd! - Time);
	}

	internal void OnTaskStarted(Person person, PersonTask task, TimePoint time) => TaskStarted?.Invoke(this, new PersonTaskEventArgs(person, task, time));

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
	private TimePoint m_time;
}

/// <summary>
/// Something the player can spend time on: a need, and for a trip, whether it's the part at home that finishes it.
/// </summary>
public sealed record PlayerActionChoice(Need Need, bool IsHome);
