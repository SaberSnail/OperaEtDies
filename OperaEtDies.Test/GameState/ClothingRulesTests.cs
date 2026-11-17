using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class ClothingRulesTests
{
	[Test]
	public void NightClothesGoOnForSleepAtHomeAndComeOffAfter()
	{
		var world = CreateWorld(0);
		var sleep = world.GameData.Schedules.Needs.Single(x => x.Id == world.GameData.Schedules.SleepNeed);
		var person = world.Npcs.First(x => !x.IsAsleep && x.Location == x.Home && x.GetOutfits(OutfitKind.Night).Any());
		Assert.That(ClothingRules.IsWearingAny(person, OutfitKind.Night), Is.False);

		person.SetPlan(sleep, [new BusyTask(60)]);
		ClothingRules.DressForSleep(person, sleep, world.Time);
		Assert.That(person.Clothing.Pieces, Is.EquivalentTo(person.GetOutfits(OutfitKind.Night).First().Pieces));
		Assert.That(person.IsInDayClothes, Is.False);

		person.SetPlan(null, []);
		ClothingRules.DressAfterSleep(person, world.Time);
		Assert.That(person.Clothing.Pieces, Is.EquivalentTo(person.GetOutfits(OutfitKind.Everyday).First().Pieces));
		Assert.That(person.Inventory.Items, Is.Empty);
	}

	[Test]
	public void SleepingInDayClothesIsSlower()
	{
		var world = CreateWorld(0);
		var schedules = world.GameData.Schedules;
		var sleep = schedules.Needs.Single(x => x.Id == schedules.SleepNeed);
		var person = world.Npcs.First(x => x.IsInDayClothes);

		Assert.That(person.GetSatisfyPerHour(sleep), Is.EqualTo(sleep.SatisfyPerHour!.Value * schedules.Sleep.DayClothesFactor));
		person.Clothing.TakeOffAll(person.Home.Items);
		Assert.That(person.GetSatisfyPerHour(sleep), Is.EqualTo(sleep.SatisfyPerHour!.Value));
	}

	[Test]
	public void WorkClothesAreFetchedFromHomeAndChangedIntoAndOutOf()
	{
		var world = CreateWorld(0);
		var person = world.Npcs.First(x => x.Workplace != x.Home && x.GetOutfits(OutfitKind.Work).Any() && !x.IsAsleep);
		var work = person.GetOutfits(OutfitKind.Work).Single();
		var everyday = person.GetOutfits(OutfitKind.Everyday).First(x => ClothingRules.IsWearing(person, x));
		person.Location = person.Home;
		Assert.That(ClothingRules.GetWorkChange(person, person.Workplace, world.Time), Is.EqualTo((work, true)));

		ClothingRules.Fetch(person, work, world.Time);
		Assert.That(person.Inventory.Items, Is.EquivalentTo(work.Pieces));
		person.Location = person.Workplace;
		Assert.That(ClothingRules.GetWorkChange(person, person.Workplace, world.Time), Is.EqualTo((work, false)));

		ClothingRules.Change(person, work, isTakingOff: false, world.Time);
		var replaced = everyday.Pieces.Where(x => work.Pieces.Any(y => y.Clothing.Overlaps(x.Clothing))).ToList();
		Assert.That(person.Clothing.Pieces, Is.EquivalentTo(work.Pieces.Concat(everyday.Pieces.Except(replaced))));
		Assert.That(person.Inventory.Items, Is.EquivalentTo(replaced));

		ClothingRules.Change(person, work, isTakingOff: true, world.Time);
		Assert.That(person.Clothing.Pieces, Is.EquivalentTo(everyday.Pieces));
		Assert.That(person.Inventory.Items, Is.EquivalentTo(work.Pieces));

		// Getting home, the work clothes are put away.
		world.MovePerson(person, person.Home, world.Time);
		Assert.That(person.Inventory.Items, Is.Empty);
		Assert.That(work.Pieces.Select(x => x.Container), Is.All.SameAs(person.Home.Items));
	}

	[Test]
	public void WorkClothesInTheWashArentWorn()
	{
		var world = CreateWorld(0);
		var person = world.Npcs.First(x => x.Workplace != x.Home && x.GetOutfits(OutfitKind.Work).Any(y => y.Pieces.Any(z => !z.Clothing.Accessory)) && !x.IsAsleep);
		person.Location = person.Home;

		ClothingRules.TakeWashing(person.Home);
		Assert.That(ClothingRules.GetWorkChange(person, person.Workplace, world.Time), Is.Null);

		var dryAt = world.GameData.Calendar.AddMinutes(world.Time, 60);
		ClothingRules.HangOutWashing(person.Home, dryAt);
		Assert.That(ClothingRules.GetWorkChange(person, person.Workplace, world.Time), Is.Null);
		Assert.That(ClothingRules.GetWorkChange(person, person.Workplace, dryAt), Is.Not.Null);
	}

	[Test]
	public void PeopleChangeForWorkAtTheirWorkplace()
	{
		var world = CreateWorld(0);
		world.IsHeadless = true;
		// Whether each person with work clothes was wearing them each time they started a stretch of work.
		var working = new List<(Person Person, bool IsInWorkClothes)>();
		world.TaskStarted += (_, e) =>
		{
			if (e.Task is BusyTask && e.Person.IsAtWork && e.Person.GetOutfits(OutfitKind.Work).FirstOrDefault() is { } work)
				working.Add((e.Person, ClothingRules.IsWearing(e.Person, work)));
		};
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(2, 0, 0));

		Assert.That(working, Is.Not.Empty);
		Assert.That(working.Count(x => x.IsInWorkClothes), Is.GreaterThan(working.Count / 2));
	}

	[Test]
	public void ThePlayerChangesForWorkToo()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		var work = player.GetOutfits(OutfitKind.Work).FirstOrDefault();
		var need = world.GameData.Schedules.Needs.FirstOrDefault(x => x.AtWork && Scheduler.HasNeed(player, x));
		Assume.That(work is not null && need is not null && player.Workplace != player.Home && world.IsPlayerActionOpen(need) && world.GetPlayerRemainingMinutes(need) > 60,
			"The player has no work clothes, works at home, or has no work to do today.");

		player.Location = player.Home;
		world.MovePlayer(player.Workplace);
		Assert.That(player.Inventory.Items, Is.SupersetOf(work!.Pieces));

		var passed = world.DoPlayerAction(need!, 60);
		Assert.That(world.GameData.Calendar.GetTotalMinutes(passed), Is.EqualTo(60));
		Assert.That(ClothingRules.IsWearing(player, work), Is.True);
	}

	[Test]
	public void OnlyCoveringClothingHidesWhatsUnderIt()
	{
		var world = CreateWorld(0);
		var gameData = world.GameData;
		var person = world.Npcs[0];
		ClothingPiece Wear(string clothingId)
		{
			var clothing = gameData.Clothing.Get(clothingId);
			var piece = world.Objects.Create(id => new ClothingPiece(id, clothing, clothing.Colors.ToDictionary(x => x, _ => gameData.ClothingColors[0]), person));
			person.Clothing.Wear(piece);
			return piece;
		}
		string Describe() => person.Clothing.Describe(gameData.ClothingSlots, gameData.ClothingLayers, gameData.ClothingSettings.HidesClothingAt);

		person.Clothing.TakeOffAll(person.Home.Items);
		var tunic = Wear("tunic");
		var apron = Wear("apron");
		Assert.That(Describe(), Does.Contain(tunic.Description).And.Contain(apron.Description));

		// Breeches and the tunic hanging over them are in the same layer, so neither hides the other.
		var breeches = Wear("breeches");
		Assert.That(Describe(), Does.Contain(tunic.Description).And.Contain(breeches.Description));

		// The cloak covers the torso enough to hide the tunic and the apron, but not the breeches.
		var cloak = Wear("cloak");
		Assert.That(Describe(), Does.Not.Contain(tunic.Description).And.Not.Contain(apron.Description).And.Contain(cloak.Description).And.Contain(breeches.Description));
	}

	[Test]
	public void ThePlayerCanChangeAndMoveTheirClothesAtHome()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		var calendar = world.GameData.Calendar;
		var changeMinutes = world.GameData.Schedules.Clothing.ChangeMinutes;
		world.MovePlayer(player.Home);
		var other = player.GetOutfits(OutfitKind.Everyday).First(x => !ClothingRules.IsWearing(player, x) && ClothingRules.IsAtHand(x, world.Time));
		var wornBefore = player.Clothing.Pieces.ToList();
		var start = world.Time;

		Assert.That(world.CanPlayerChangeInto(other), Is.True);
		world.PlayerChangeInto(other);
		Assert.That(world.Time, Is.EqualTo(start));
		Assert.That(player.Clothing.Pieces, Is.EquivalentTo(other.Pieces));
		Assert.That(world.CanPlayerChangeInto(other), Is.False);

		var piece = other.Pieces[0];
		world.PlayerTakeOff(piece);
		Assert.That(piece.Container, Is.SameAs(player.Home.Items));

		// Changing takes its time once, when the player is done, however many things they tried.
		Assert.That(world.FinishPlayerChanging(wornBefore) is { } passed ? calendar.GetTotalMinutes(passed) : 0, Is.EqualTo(changeMinutes));
		Assert.That(world.FinishPlayerChanging(player.Clothing.Pieces.ToList()), Is.Null);

		Assert.That(world.CanPlayerTake(piece), Is.True);
		world.PlayerTake(piece);
		Assert.That(piece.Container, Is.SameAs(player.Inventory));
		Assert.That(world.CanPlayerPutAway(piece), Is.True);
		world.PlayerPutAway(piece);
		Assert.That(piece.Container, Is.SameAs(player.Home.Items));

		// Clothes in the wash can't be worn or taken.
		ClothingRules.TakeWashing(player.Home);
		Assert.That(world.CanPlayerWear(piece), Is.False);
		Assert.That(world.CanPlayerTake(piece), Is.False);
	}

	[Test]
	public void NightClothesPutOnWhileAwakeStayOn()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		world.MovePlayer(player.Home);
		var nightwear = player.GetOutfits(OutfitKind.Night).First().Pieces.First(x => x.Clothing.Slots.Contains("torso"));

		world.PlayerWear(nightwear);
		Assert.That(nightwear.Wearer, Is.SameAs(player));
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 0, 30));
		Assert.That(nightwear.Wearer, Is.SameAs(player));
	}

	[Test]
	public void AccessoriesAreSharedAndArentWashed()
	{
		var world = CreateWorld(0);
		foreach (var person in world.People)
		{
			var pieces = person.Outfits.SelectMany(x => x.Pieces).ToList();
			Assert.That(pieces.Where(x => x.Clothing.Accessory).GroupBy(x => x.Clothing).Select(x => x.Distinct().Count()), Is.All.EqualTo(1), person.FullName);
		}

		var home = world.Player.Home;
		ClothingRules.TakeWashing(home);
		Assert.That(home.Items.Clothing.Where(x => x.Clothing.Accessory), Is.Not.Empty);
		Assert.That(home.Items.Clothing.Select(x => x.IsInWash == !x.Clothing.Accessory), Is.All.True);
	}

	[Test]
	public void ThePlayersWorkClothesGoOverTheirOtherClothes()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		world.MovePlayer(player.Home);
		var work = player.GetOutfits(OutfitKind.Work).FirstOrDefault();
		Assume.That(work, Is.Not.Null, "The player has no work clothes.");
		var before = player.Clothing.Pieces.ToList();

		world.PlayerChangeInto(work!);
		var replaced = before.Where(x => work!.Pieces.Any(y => y.Clothing.Overlaps(x.Clothing))).ToList();
		Assert.That(player.Clothing.Pieces, Is.EquivalentTo(work!.Pieces.Union(before.Except(replaced))));
		Assert.That(world.CanPlayerChangeInto(work), Is.False);
	}

	[Test]
	public void EveryonesOutfitsAreModestEnough()
	{
		for (var seed = 0; seed < 5; seed++)
		{
			var world = CreateWorld(seed);
			var gameData = world.GameData;
			foreach (var person in world.People)
			{
				IReadOnlyList<ClothingSlot> Check(IEnumerable<OwnedOutfit> outfits, ModestyLevel level, bool isAtWork) =>
					ClothingRules.GetUncoveredSlots(gameData, person, outfits.SelectMany(x => x.Pieces).Select(x => x.Clothing), level, isAtWork);

				foreach (var everyday in person.GetOutfits(OutfitKind.Everyday))
				{
					Assert.That(Check([everyday], ModestyLevel.Outside, isAtWork: false), Is.Empty, $"Seed {seed}, {person.FullName}, {everyday.Template?.Id}");
					Assert.That(Check(person.GetOutfits(OutfitKind.Work).Any(x => x.IsComplete) ? person.GetOutfits(OutfitKind.Work) : [everyday, .. person.GetOutfits(OutfitKind.Work)], ModestyLevel.Outside, isAtWork: true), Is.Empty, $"Seed {seed}, {person.FullName} at work");
				}

				Assert.That(Check(person.GetOutfits(OutfitKind.Night), ModestyLevel.Home, isAtWork: false), Is.Empty, $"Seed {seed}, {person.FullName} at night");
			}
		}
	}

	[Test]
	public void ThePlayerMustBeDressedEnough()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		world.MovePlayer(player.Home);
		var everyday = player.GetOutfits(OutfitKind.Everyday).First(x => ClothingRules.IsAtHand(x, world.Time));
		if (world.CanPlayerChangeInto(everyday))
			world.PlayerChangeInto(everyday);
		Assert.That(world.GetPlayerUncoveredSlots(), Is.Empty);
		Assert.That(world.GetPlayerUncoveredSlotsOutside(), Is.Empty);

		world.PlayerTakeOffAll();
		Assert.That(world.GetPlayerUncoveredSlots().Select(x => x.Id), Does.Contain("hips"));
		Assert.That(world.GetPlayerUncoveredSlotsOutside().Select(x => x.Id), Is.EquivalentTo(new[] { "torso", "hips" }));

		// Going to sleep puts on night clothes, which are fine at home.
		var sleep = world.GameData.Schedules.Needs.Single(x => x.Id == world.GameData.Schedules.SleepNeed);
		Assert.That(world.GetPlayerActionUncoveredSlots(sleep), Is.Empty);
	}

	[Test]
	public void CompleteWorkClothesAreWornOnTheirOwn()
	{
		var world = CreateWorld(0);
		var gameData = world.GameData;
		var person = world.Npcs.First(x => !x.IsAsleep && x.GetOutfits(OutfitKind.Everyday).Any(y => ClothingRules.IsWearing(x, y)));
		var everyday = person.GetOutfits(OutfitKind.Everyday).First(x => ClothingRules.IsWearing(person, x));
		person.Location = person.Workplace;
		ClothingPiece Create(string clothingId)
		{
			var clothing = gameData.Clothing.Get(clothingId);
			var piece = world.Objects.Create(id => new ClothingPiece(id, clothing, clothing.Colors.ToDictionary(x => x, _ => gameData.ClothingColors[0]), person));
			person.Inventory.Add(piece);
			return piece;
		}
		var template = new Outfit { Id = "uniform", Kind = OutfitKind.Work, Complete = true, Clothing = ["shirt", "tunic", "breeches", "boots"] };
		var pieces = template.Clothing.Select(Create).ToList();
		var uniform = world.Objects.Create(id => new OwnedOutfit(id, person, OutfitKind.Work, template, pieces));
		Assert.That(uniform.IsComplete, Is.True);

		// Changing into it takes off everything else, which is carried.
		ClothingRules.Change(person, uniform, isTakingOff: false, world.Time);
		Assert.That(person.Clothing.Pieces, Is.EquivalentTo(uniform.Pieces));
		Assert.That(person.Inventory.Items, Is.EquivalentTo(everyday.Pieces));

		// Changing out of it puts the everyday clothes back on.
		ClothingRules.Change(person, uniform, isTakingOff: true, world.Time);
		Assert.That(person.Clothing.Pieces, Is.EquivalentTo(everyday.Pieces));
		Assert.That(person.Inventory.Items, Is.EquivalentTo(uniform.Pieces));
	}

	[Test]
	public void CompleteWorkClothesOfWornAccessoriesAreStillChangedInto()
	{
		var world = CreateWorld(0);
		var person = world.Npcs.First(x => !x.IsAsleep && !x.GetOutfits(OutfitKind.Work).Any() &&
			x.GetOutfits(OutfitKind.Everyday).Any(y => ClothingRules.IsWearing(x, y) && y.Pieces.Any(z => z.Clothing.Accessory) && y.Pieces.Any(z => !z.Clothing.Accessory)));
		var everyday = person.GetOutfits(OutfitKind.Everyday).First(x => ClothingRules.IsWearing(person, x));
		var accessories = everyday.Pieces.Where(x => x.Clothing.Accessory).ToList();
		var template = new Outfit { Id = "accessoriesOnly", Kind = OutfitKind.Work, Complete = true, Clothing = [.. accessories.Select(x => x.Clothing.Id)] };
		var work = world.Objects.Create(id => new OwnedOutfit(id, person, OutfitKind.Work, template, accessories));
		person.AddOutfit(work);
		person.Location = person.Workplace;

		// The accessories are already worn with the everyday clothes, but the rest of those still has to come off.
		Assert.That(ClothingRules.IsWearing(person, work), Is.True);
		Assert.That(ClothingRules.IsDressedIn(person, work), Is.False);
		Assert.That(ClothingRules.GetWorkChange(person, person.Workplace, world.Time)?.Outfit, Is.EqualTo(work));

		ClothingRules.Change(person, work, isTakingOff: false, world.Time);
		Assert.That(person.Clothing.Pieces, Is.EquivalentTo(accessories));
		Assert.That(ClothingRules.IsDressedIn(person, work), Is.True);
	}

	private static GameWorld CreateWorld(int seed)
	{
		var random = new Random(seed);
		var world = GameWorld.CreateNewGame(s_gameData, CharacterRandomizer.CreateRandomPlayer(s_gameData, random), random);
		return world;
	}

	private static readonly GameData s_gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
}
