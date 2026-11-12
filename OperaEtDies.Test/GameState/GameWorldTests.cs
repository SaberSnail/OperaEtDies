using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Town;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class GameWorldTests
{
	[Test]
	public void PlayerStartsAtHome()
	{
		var (town, world) = CreateWorld();

		Assert.That(world.Player.IsPlayer, Is.True);
		Assert.That(world.Player.FullName, Is.EqualTo("John Smith"));
		Assert.That(world.Player.Location, Is.SameAs(world.Player.Home));
		Assert.That(world.Player.Home.Template, Is.SameAs(town.PlayerHousehold.House!.Template));
		Assert.That(world.Player.Location.People, Does.Contain(world.Player));
	}

	[Test]
	public void GameStartsOnARandomDayOfTheStartYearAtTheFirstPartOfTheDay()
	{
		var days = new HashSet<(int Season, int Week, int Day)>();
		for (var seed = 0; seed < 40; seed++)
		{
			var (_, world) = CreateWorld(seed);
			var calendar = world.GameData.Calendar;
			var parts = calendar.GetParts(world.Time);

			Assert.That(parts.Year, Is.EqualTo(calendar.StartYear));
			Assert.That((parts.Hour, parts.Minute), Is.EqualTo((calendar.DayParts[0].Start, 0)));
			days.Add((parts.Season, parts.Week, parts.Day));
		}

		Assert.That(days, Has.Count.GreaterThan(10));
	}

	[Test]
	public void AdvancingTimeMovesItForward()
	{
		var (_, world) = CreateWorld();
		var calendar = world.GameData.Calendar;
		var start = world.Time;
		var changes = new List<string?>();
		world.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

		world.AdvanceTime(calendar.CreateTimeOffset(0, 0, 15));
		world.AdvanceTime(calendar.CreateTimeOffset(0, 0, 0));

		Assert.That(world.Time, Is.EqualTo(start + calendar.CreateTimeOffset(0, 0, 15)));
		Assert.That(changes, Is.EqualTo(new[] { nameof(GameWorld.Time) }));
		Assert.That(() => world.AdvanceTime(calendar.CreateTimeOffset(0, 0, -15)), Throws.ArgumentException);
	}

	[Test]
	public void TravelTimeIsTheLongerOfTheQuarterKindsOrTheBuildingTime()
	{
		var (_, world) = CreateWorld(withFeatures: true);
		var calendar = world.GameData.Calendar;
		var quarters = world.Locations.Where(x => x.Template is QuarterTemplate).ToList();
		var village = quarters.Where(x => x.Area == MapArea.Village).ToList();
		var outskirts = quarters.First(x => x.Area == MapArea.Outskirts);
		var house = world.Player.Home;

		Assert.Multiple(() =>
		{
			Assert.That(world.GetTravelTime(village[0], village[1]), Is.EqualTo(calendar.CreateTimeOffset(0, 0, 2)));
			Assert.That(world.GetTravelTime(village[0], outskirts), Is.EqualTo(calendar.CreateTimeOffset(0, 0, 5)));
			Assert.That(world.GetTravelTime(outskirts, village[0]), Is.EqualTo(calendar.CreateTimeOffset(0, 0, 5)));
			Assert.That(world.GetTravelTime(house, house.Quarter!), Is.EqualTo(calendar.CreateTimeOffset(0, 0, 1)));
			Assert.That(world.GetTravelTime(house.Quarter!, house), Is.EqualTo(calendar.CreateTimeOffset(0, 0, 1)));
		});
	}

	[Test]
	public void LocationsAreCreatedFromTown()
	{
		var (town, world) = CreateWorld();

		Assert.That(world.Locations.Select(x => x.Template), Is.EqualTo(town.Locations.Select(x => x.Template)));
		var townLocations = world.Locations.Zip(town.Locations).ToDictionary(x => x.First, x => x.Second);
		foreach (var (location, townLocation) in townLocations)
		{
			// Houses are named after the head of the household that lives there.
			var household = town.Households.FirstOrDefault(x => x.House == townLocation);
			Assert.That(location.Title, Is.EqualTo(household is not null ? $"The {household.Head.Traits.GetString("lastName")} Home" : townLocation.Template.Title));
			Assert.That(location.ShortDescription, Is.EqualTo(townLocation.Template.ShortDescription));
			Assert.That(location.MediumDescription, Is.EqualTo(townLocation.Template.MediumDescription));
			Assert.That(location.LongDescription, Is.EqualTo(townLocation.Template.LongDescription));
			Assert.That(location.Bounds, Is.EqualTo(townLocation.Bounds));
			Assert.That(location.Connections.Select(x => townLocations[x]), Is.EqualTo(townLocation.Connections));
			Assert.That(location.QuarterKind, Is.EqualTo((townLocation.Quarter?.Template as QuarterTemplate)?.Kind));
			Assert.That(world.Objects.Get<Location>(location.Id), Is.SameAs(location));
		}
	}

	[Test]
	public void EveryObjectHasAUniqueId()
	{
		var (_, world) = CreateWorld(withFeatures: true);
		var objects = world.Objects.All.ToList();

		Assert.That(objects.Select(x => x.Id).Distinct().Count(), Is.EqualTo(objects.Count));
		Assert.That(objects, Is.SupersetOf(world.Locations).And.SupersetOf(world.People).And.SupersetOf(world.Families).And.SupersetOf(world.Businesses).And.SupersetOf(world.Features));
		Assert.That(world.Features, Is.Not.Empty);
	}

	[Test]
	public void EveryoneStartsAtHomeAtWorkOrInAQuarter()
	{
		var places = new HashSet<string>();
		var homeWork = new HashSet<bool>();
		for (var seed = 0; seed < 20; seed++)
		{
			var (town, world) = CreateWorld(seed);
			var quarters = world.Locations.Where(x => x.Bounds is not null).ToList();
			var townLocations = world.Locations.Zip(town.Locations).ToDictionary(x => x.First, x => x.Second);
			var people = town.Households.SelectMany(x => x.Members).ToList();

			Assert.That(world.Npcs, Has.Count.EqualTo(people.Count - 1));
			foreach (var (npc, person) in world.Npcs.Zip(people.Where(x => !x.IsPlayer)))
			{
				Assert.That(npc.IsPlayer, Is.False);
				Assert.That(npc.Role, Is.EqualTo(person.Role));
				Assert.That(npc.Traits.GetValueId("profession"), Is.EqualTo(person.Traits.GetValueId("profession")));
				Assert.That(npc.Location == npc.Home || npc.Location == npc.Workplace || quarters.Contains(npc.Location), Is.True);
				// People who work from home are working half the time they're there.
				if (npc.Location == npc.Workplace && npc.Workplace == npc.Home && npc.Profession is not null)
					homeWork.Add(npc.IsAtWork);
				else
					Assert.That(npc.IsAtWork, Is.EqualTo(npc.Location == npc.Workplace && npc.Workplace != npc.Home));
				Assert.That(townLocations[npc.Home], Is.SameAs(person.Household.House));
				Assert.That(townLocations[npc.Workplace], Is.SameAs(person.Business?.Location ?? person.Household.House));
				Assert.That(npc.Business?.Profession, Is.SameAs(person.Business?.Profession));
				Assert.That(npc.IsBusinessOwner, Is.EqualTo(person.IsBusinessOwner));
				Assert.That(npc.Location.People, Does.Contain(npc));
				places.Add(npc.Location == npc.Home ? "home" : npc.Location == npc.Workplace ? "work" : "quarter");
			}
		}

		Assert.That(places, Is.EquivalentTo(new[] { "home", "work", "quarter" }));
		Assert.That(homeWork, Is.EquivalentTo(new[] { true, false }));
	}

	[Test]
	public void PeopleAreGroupedByFamily()
	{
		var (town, world) = CreateWorld();

		Assert.That(world.Families.Select(x => x.LastName), Is.EqualTo(town.Households.Select(x => x.LastName)));
		Assert.That(world.Families.Select(x => x.WealthLevel), Is.EqualTo(town.Households.Select(x => x.WealthLevel)));
		Assert.That(world.Families[0].Members[0], Is.SameAs(world.Player));
		Assert.That(world.People.Where(x => !x.IsPlayer), Is.EqualTo(world.Npcs));
		foreach (var (family, household) in world.Families.Zip(town.Households))
		{
			// The game sets the at work property from where people are.
			Assert.That(family.Members.Select(x => x.Traits.Values.Where(value => value.Key != "atWork")),
				Is.EqualTo(household.Members.Select(x => x.Traits.Values.Where(value => value.Key != "atWork"))));
			Assert.That(family.Members.Select(x => x.Home).Distinct(), Is.EqualTo(new[] { family.Home }));
			Assert.That(family.Members.Select(x => x.Family).Distinct(), Is.EqualTo(new[] { family }));
			Assert.That(family.Home.Family, Is.SameAs(family));
		}
	}

	[Test]
	public void MovingPersonUpdatesLocations()
	{
		var (_, world) = CreateWorld();
		var player = world.Player;
		var home = player.Home;
		var quarter = home.Connections[0];
		player.Location = home;
		var changed = new List<string?>();
		player.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		player.Location = quarter;

		Assert.That(player.Location, Is.SameAs(quarter));
		Assert.That(home.People, Does.Not.Contain(player));
		Assert.That(quarter.People, Does.Contain(player));
		Assert.That(changed, Is.SupersetOf(new[] { nameof(Person.Location), nameof(Person.PortraitPlace) }));
		Assert.That(player.PortraitPlace, Is.EqualTo(PortraitPlace.InQuarter(quarter.QuarterKind!)));
	}

	[Test]
	public void PersonDescribesThemselves()
	{
		var gameData = TestData.CreateGameData();
		var world = new TestWorld(gameData);
		var traits = TestData.CreateTraits(gameData, "female", "Alice", "Miller", "miller", "tall", "heavy", 34);

		var person = world.AddPerson(traits, world.AddLocation(gameData.HouseTemplates[0]));

		Assert.That(person.FullName, Is.EqualTo("Alice Miller"));
		Assert.That(person.Traits.GetOption("profession").Id, Is.EqualTo("miller"));
		Assert.That(person.Traits.GetOption("height").Id, Is.EqualTo("tall"));
		Assert.That(person.Description, Is.EqualTo("Alice Miller is a woman of 34. She is tall and heavy, and works as a miller."));
		Assert.That(person.Workplace, Is.SameAs(person.Home));
	}

	[Test]
	public void WorkAndPortraitPlaceFollowTheLocation()
	{
		var gameData = TestData.CreateGameData();
		var world = new TestWorld(gameData);
		var traits = TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40);
		var home = world.AddLocation(gameData.HouseTemplates[0]);
		var otherHome = world.AddLocation(gameData.HouseTemplates[0]);
		var forge = world.AddLocation(gameData.BusinessTemplates.Get("forge"));
		var smithy = world.AddLocation(gameData.BusinessTemplates.Get("smithy"));
		var lane = world.AddLocation(gameData.QuarterTemplates[0]);
		var person = world.AddPerson(traits, home, forge);

		Assert.That(person.Workplace, Is.SameAs(forge));
		Assert.That(person.IsAtWork, Is.False);
		Assert.That(person.Traits.GetValueId("atWork"), Is.EqualTo("notAtWork"));
		Assert.That(person.PortraitPlace, Is.EqualTo(PortraitPlace.Home));

		person.Location = forge;
		Assert.That(person.IsAtWork, Is.True);
		Assert.That(person.Traits.GetValueId("atWork"), Is.EqualTo("atWork"));
		Assert.That(person.PortraitPlace, Is.EqualTo(PortraitPlace.InWorkplace("smith")));

		// Someone else's business of the same profession looks the same, but the person isn't working there.
		person.Location = smithy;
		Assert.That(person.IsAtWork, Is.False);
		Assert.That(person.Traits.GetValueId("atWork"), Is.EqualTo("notAtWork"));
		Assert.That(person.PortraitPlace, Is.EqualTo(PortraitPlace.InWorkplace("smith")));

		person.Location = otherHome;
		Assert.That(person.PortraitPlace, Is.EqualTo(PortraitPlace.Home));

		person.Location = lane;
		Assert.That(person.PortraitPlace, Is.EqualTo(PortraitPlace.InQuarter(lane.QuarterKind!)));
	}

	[Test]
	public void WorkingFromHomeMakesHomeTheWorkplace()
	{
		var gameData = TestData.CreateGameData();
		var world = new TestWorld(gameData);
		var traits = TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40);
		var person = world.AddPerson(traits, world.AddLocation(gameData.HouseTemplates[0]));
		person.IsAtWork = false;
		var changed = new List<string?>();
		person.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		person.IsAtWork = true;

		Assert.That(person.Traits.GetValueId("atWork"), Is.EqualTo("atWork"));
		Assert.That(person.PortraitPlace, Is.EqualTo(PortraitPlace.InWorkplace("smith")));
		Assert.That(changed, Is.EquivalentTo(new[] { nameof(Person.IsAtWork), nameof(Person.Traits), nameof(Person.Description), nameof(Person.PortraitPlace) }));
	}

	[Test]
	public void WorkingFromHomeIsDecidedOnArrivingHome()
	{
		var gameData = TestData.CreateGameData();
		var world = new TestWorld(gameData);
		var smith = TestData.CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40);
		var child = new CharacterTraits(smith.Values.Where(x => x.Key != "profession").ToDictionary(x => x.Key, x => x.Value));
		var home = world.AddLocation(gameData.HouseTemplates[0]);
		var lane = world.AddLocation(gameData.QuarterTemplates[0]);
		var person = world.AddPerson(smith, home);
		var results = new HashSet<bool> { person.IsAtWork };

		for (var visit = 0; visit < 40; visit++)
		{
			person.Location = lane;
			Assert.That(person.IsAtWork, Is.False);

			person.Location = home;
			results.Add(person.IsAtWork);
			Assert.That(person.PortraitPlace, Is.EqualTo(person.IsAtWork ? PortraitPlace.InWorkplace("smith") : PortraitPlace.Home));
		}

		// Someone without a profession is never working.
		var idle = world.AddPerson(child, home);

		Assert.That(results, Is.EquivalentTo(new[] { true, false }));
		Assert.That(idle.IsAtWork, Is.False);
	}

	[Test]
	public void ShippedDataDescribesPeople()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;

		for (var seed = 0; seed < 10; seed++)
		{
			var player = new CharacterRandomizer(gameData, new Random(seed), CharacterRole.Player).GetTraits();
			var world = GameWorld.CreateNewGame(gameData, player, new Random(seed));

			Assert.That(world.Npcs, Is.Not.Empty);
			foreach (var npc in world.Npcs)
			{
				Assert.That(npc.FullName, Is.Not.Empty);
				Assert.That(npc.Description, Is.Not.Empty.And.Not.Contain("  ").And.Not.Contain(" .").And.Not.Contain(" ,"));

				// A character has a value for exactly the properties whose conditions its other values meet.
				foreach (var property in gameData.CharacterProperties)
					Assert.That(npc.Traits.HasValue(property.Id), Is.EqualTo(property.HasValue(CharacterRole.Npc, npc.Traits.GetValueId)), $"{property.Id}: {npc.Description}");
			}
		}
	}

	internal static (GeneratedTown Town, GameWorld World) CreateWorld(int seed = 0, bool withFeatures = false)
	{
		var gameData = TestData.CreateGameData(withFeatures: withFeatures);
		var random = new Random(seed);
		var town = TownGenerator.Generate(gameData, TestData.CreatePlayerTraits(gameData), random);
		return (town, GameWorld.CreateFromTown(gameData, town, random));
	}
}
