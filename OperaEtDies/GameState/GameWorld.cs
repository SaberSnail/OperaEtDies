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
	/// copied into world objects. The player starts at home, and everyone else at home, at work, or in a random quarter. The
	/// game starts on a random day of the calendar's start year, at the start of the first part of the day.
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

		// Everyone starts at home and not at work, until their family and business are known.
		var families = gameData.Families;
		var atWorkProperty = gameData.CharacterProperties.Get(families.Properties.AtWork);
		var notAtWork = atWorkProperty.Values!.First(x => x.Id == families.NotAtWork);
		var peopleByTownPerson = new Dictionary<TownPerson, Person>();
		foreach (var household in town.Households)
		{
			var home = locationsByTownLocation[household.House!];
			var members = household.Members
				.Select(x => peopleByTownPerson[x] = objects.Create(id => new Person(id, gameData, x.Traits.With(atWorkProperty.Id, notAtWork), x.Role, x.IsPlayer, home, random)))
				.ToList();
			objects.Create(id => new Family(id, household.LastName, members, home, household.WealthLevel));
		}

		foreach (var business in town.Businesses)
		{
			objects.Create(id => new Business(id, business.Profession, peopleByTownPerson[business.Owner], business.Helpers.Select(x => peopleByTownPerson[x]).ToList(),
				business.Location is { } location ? locationsByTownLocation[location] : null));
		}

		// The start time is chosen last, so it doesn't change the rest of a town generated from the same random numbers.
		var calendar = gameData.Calendar;
		var world = new GameWorld(gameData, objects, random, calendar.CreateTimePoint(calendar.StartYear, 1, 1, 1));
		var quarters = world.Locations.Where(x => x.Template is QuarterTemplate).ToList();
		foreach (var person in world.People)
			person.PlaceAt(person.IsPlayer ? person.Home : ChooseStartingLocation(person.Home, person.Workplace, quarters, random));
		world.m_time = ChooseStartTime(calendar, random);

		return world;
	}

	/// <summary>
	/// Chooses when a new game starts: a random day of the start year, each equally likely, at the start of the first part
	/// of the day.
	/// </summary>
	private static TimePoint ChooseStartTime(GameCalendar calendar, Random random) =>
		calendar.CreateTimePoint(calendar.StartYear, random.Next(calendar.SeasonsInYear) + 1, random.Next(calendar.WeeksInSeason) + 1,
			random.Next(calendar.DaysInWeek) + 1, calendar.DayParts[0].Start);

	/// <summary>
	/// Creates the world for a loaded game from the saved world objects.
	/// </summary>
	internal static GameWorld Load(GameData gameData, int nextId, TimePoint time, IReadOnlyList<WorldObjectRecord> records, Random random)
	{
		var loader = new WorldLoader(gameData, random, records);
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

	/// <summary>
	/// Chooses where a person is when the game starts: at home, at their workplace (which may be their home), or in a
	/// random quarter, each a third of the time.
	/// </summary>
	private static Location ChooseStartingLocation(Location home, Location workplace, IReadOnlyList<Location> quarters, Random random) =>
		random.Next(3) switch
		{
			0 => home,
			1 => workplace,
			_ => quarters.Count == 0 ? home : quarters[random.Next(quarters.Count)],
		};

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
	public TimeOffset GetTravelTime(Location from, Location to)
	{
		var calendar = GameData.Calendar;
		if (from.Template is not QuarterTemplate fromQuarter || to.Template is not QuarterTemplate toQuarter)
			return calendar.CreateTimeOffset(0, 0, GameData.BuildingTravelTime);

		// Quarter kinds are checked against the quarters when the data is loaded.
		var minutes = Math.Max(GameData.QuarterKinds.Get(fromQuarter.Kind).TravelTime, GameData.QuarterKinds.Get(toQuarter.Kind).TravelTime);
		return calendar.CreateTimeOffset(0, 0, minutes);
	}

	/// <summary>
	/// Moves the time forward. All time passes through here, so anything that happens as time passes can be handled in
	/// one place.
	/// </summary>
	/// <exception cref="ArgumentException">The offset is negative.</exception>
	public void AdvanceTime(TimeOffset offset)
	{
		if (offset < GameData.Calendar.CreateTimeOffset(0, 0, 0))
			throw new ArgumentException("Time can't move backward.", nameof(offset));
		Time += offset;
	}

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

	private TimePoint m_time;
}
