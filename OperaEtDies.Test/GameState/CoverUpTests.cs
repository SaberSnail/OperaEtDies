using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class CoverUpTests
{
	[Test]
	public void SomeoneWaitsForAGuestToLeaveBeforeUndressing()
	{
		// Someone at home who wants to undress waits while a guest from another household is there, and undresses once
		// they've gone.
		var (world, person) = CreateWorld(new Step { Kind = StepKind.Dress, Outfit = OutfitKind.Nothing }, new Step { Kind = StepKind.Do, Minutes = TestData.Amount(30) });
		var home = person.Home;
		foreach (var someone in world.People.Where(x => x.Location == home && x != person))
			someone.Location = home.Quarter!;
		person.Location = home;
		var guest = world.Npcs.First(x => x.Family != person.Family);
		guest.Location = home;
		guest.SetPlan(null, [new BusyTask(20), new TravelTask(home.Quarter!), new BusyTask(600)]);
		guest.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 20));

		var isWaiting = false;
		world.TaskStarted += (_, e) => isWaiting |= e.Person == person && e.Task is PrivacyTask;
		person.SetPlan(person.GetSequenceNeed(), [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 0, 50), () => guest.Location != home && person.Clothing.Pieces.Count == 0);

		Assert.That((isWaiting, guest.Location != home, person.Clothing.Pieces.Count), Is.EqualTo((true, true, 0)));
	}

	[Test]
	public void SomeoneCoversUpWhenAStrangerArrives()
	{
		var (world, person, stranger, place) = CreateSwimmer();
		var house = place.Connections.First(x => x.IsHouse);
		stranger.Location = house;
		stranger.SetPlan(null, [new BusyTask(20), new TravelTask(place), new BusyTask(600)]);
		stranger.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 20));

		var coverUps = new List<ChangeClothesTask>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is ChangeClothesTask { IsCoverUp: true } change)
				coverUps.Add(change);
		};
		person.SetPlan(person.GetSequenceNeed(), [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 0, 40));

		// They change back out of nothing into the clothes they were carrying.
		Assert.That(coverUps.Select(x => (x.Outfit.Kind, x.IsTakingOff)), Is.EqualTo(new[] { (OutfitKind.Nothing, true) }));
		Assert.That(person.Clothing.Pieces, Is.Not.Empty);
	}

	[Test]
	public void CoveringUpIsCalledOffWhenTheStrangerLeaves()
	{
		var (world, person, stranger, place) = CreateSwimmer();
		var house = place.Connections.First(x => x.IsHouse);
		stranger.Location = house;
		stranger.SetPlan(null, [new BusyTask(20), new TravelTask(place), new BusyTask(1), new TravelTask(house), new BusyTask(600)]);
		stranger.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 20));

		var isCoveringUp = false;
		world.TaskStarted += (_, e) => isCoveringUp |= e.Person == person && e.Task is ChangeClothesTask { IsCoverUp: true };
		person.SetPlan(person.GetSequenceNeed(), [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 0, 40));

		Assert.That((isCoveringUp, person.Clothing.Pieces.Count), Is.EqualTo((true, 0)));
	}

	[Test]
	public void SomeoneOnlyPassingThroughDoesntMakeAnyoneCoverUp()
	{
		// The stranger walks through the lane on their way somewhere else, so the swimmer only looks embarrassed.
		var (world, person, stranger, place) = CreateSwimmer();
		var house = place.Connections.First(x => x.IsHouse);
		var onward = place.Connections.First(x => x != house);
		stranger.Location = house;
		stranger.SetPlan(null, [new BusyTask(20), new TravelTask(place), new TravelTask(onward), new BusyTask(600)]);
		stranger.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 20));
		var isCoveringUp = false;
		world.TaskStarted += (_, e) => isCoveringUp |= e.Person == person && e.Task is ChangeClothesTask { IsCoverUp: true };
		person.SetPlan(person.GetSequenceNeed(), [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 0, 40));

		Assert.That((isCoveringUp, person.Clothing.Pieces.Count, stranger.Location), Is.EqualTo((false, 0, onward)));
	}

	[Test]
	public void SomeoneWithNothingToCoverUpWithHeadsHome()
	{
		var (world, person, stranger, place) = CreateSwimmer();
		var house = place.Connections.First(x => x.IsHouse);
		stranger.Location = house;
		stranger.SetPlan(null, [new BusyTask(20), new TravelTask(place), new BusyTask(600)]);
		stranger.StartTask(world.GameData.Calendar.AddMinutes(world.Time, 20));
		FailCause? cause = null;
		world.StepFailed += (_, e) => cause ??= e.Person == person ? e.Cause : null;
		var isRetreating = false;
		world.TaskStarted += (_, e) => isRetreating |= e.Person == person && e.Task is TravelTask { IsRetreat: true };

		// Once they've undressed, the clothes they were carrying are left where they are.
		person.SetPlan(person.GetSequenceNeed(), [], startStep: 0);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 0, 10));
		Assume.That(person.Clothing.Pieces, Is.Empty);
		foreach (var piece in person.Inventory.Clothing.ToList())
		{
			person.Inventory.Remove(piece);
			place.Items.Add(piece);
		}

		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 0, 20));

		Assert.That((isRetreating, cause), Is.EqualTo((true, (FailCause?) FailCause.Shy)));
	}

	/// <summary>
	/// Creates a world with a need for swimming, which is comfortable enough for a man of ordinary boldness to undress
	/// alone in a lane, but not with a woman from another household there. He's alone in the lane, and she's elsewhere.
	/// </summary>
	private static (GameWorld World, Person Person, Person Stranger, Location Place) CreateSwimmer()
	{
		var (world, person) = CreateWorld(
			new Step { Kind = StepKind.Dress, Outfit = OutfitKind.Nothing, ModestyComfort = 35 },
			new Step { Kind = StepKind.Do, Minutes = TestData.Amount(120), ModestyComfort = 35 });
		var genderId = world.GameData.Families.Roles.Properties.Gender;
		var place = world.Locations.First(x => x.QuarterKind == "lane" && x.Template is QuarterTemplate && x.Connections.Any(y => y.IsHouse));
		foreach (var someone in world.People.Where(x => x.Location == place))
			someone.Location = someone.Home;
		person.Location = place;
		var stranger = world.Npcs.First(x => x.Family != person.Family && x.Traits.GetValueId(genderId) != person.Traits.GetValueId(genderId));
		return (world, person, stranger, place);
	}

	/// <summary>
	/// Creates a world with a need made of the given steps, and a man of ordinary boldness who isn't the player, with
	/// nothing to do.
	/// </summary>
	private static (GameWorld World, Person Person) CreateWorld(params Step[] steps)
	{
		var need = new Need { Id = "test", Title = "Test", Icon = "icon", Category = NeedCategory.Personal, Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 }, StepSettings = steps };
		need.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		gameData.Schedules.Needs = [.. gameData.Schedules.Needs, need];
		var random = new Random(0);
		var world = GameWorld.CreateNewGame(gameData, CharacterRandomizer.CreateRandomPlayer(gameData, random), random);
		world.IsHeadless = true;
		var genderId = gameData.Families.Roles.Properties.Gender;
		var person = world.Npcs.First(x => x.Traits.GetValueId(genderId) == gameData.Families.Roles.Male);
		world.SetBoldness(person, 50);
		person.SetPlan(null, []);
		return (world, person);
	}
}

/// <summary>
/// Finds the need the cover-up tests add.
/// </summary>
internal static class CoverUpTestExtensions
{
	public static Need GetSequenceNeed(this Person person) => person.GameData.Schedules.Needs.Single(x => x.Id == "test");
}
