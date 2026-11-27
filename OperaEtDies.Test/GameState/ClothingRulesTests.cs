using System.IO;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class ClothingRulesTests
{
	[Test]
	public async Task ClothesTakenOffToCarryStayCarriedAtHomeAsync()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		await world.MovePlayerAsync(player.Home);
		Assume.That(player.Clothing.Pieces, Is.Not.Empty, "The player has nothing on.");

		// In the wardrobe, what comes off is put away at home. In the inventory, it's kept carried, so it can be put back on.
		var kept = player.Clothing.Pieces[0];
		world.PlayerTakeOff(kept);
		Assert.That(kept.Container, Is.SameAs(player.Home.Items));
		world.PlayerWear(kept);
		world.PlayerTakeOff(kept, isCarried: true);
		Assert.That(kept.Container, Is.SameAs(player.Inventory));
		Assert.That(world.CanPlayerWear(kept), Is.True);
	}

	[Test]
	public void ComfortDependsOnCompanyPlaceAndStep()
	{
		var world = CreateWorld(0);
		var gameData = world.GameData;
		var player = world.Player;
		var home = player.Home;
		var household = player.Family.Members.FirstOrDefault(x => x != player);
		var stranger = world.People.First(x => x.Family != player.Family && !player.Relationships.Any(y => y.Key == x && gameData.Relationships.IsFriend(y.Value.Score)));
		Assume.That(household, Is.Not.Null, "The player lives alone.");
		double Comfort(Location location, Step? step = null, params (Person Person, bool IsAsleep)[] company) => ModestyComfort.Get(gameData, player, location, company, step, world.Time);

		// No one there is the most comfortable, then a member of the household asleep, then awake, then anyone else.
		var alone = Comfort(home);
		var asleep = Comfort(home, null, (household!, true));
		var awake = Comfort(home, null, (household!, false));
		var withStranger = Comfort(home, null, (household!, false), (stranger, false));
		Assert.That(alone, Is.GreaterThanOrEqualTo(asleep));
		Assert.That(asleep, Is.GreaterThan(awake));
		Assert.That(awake, Is.GreaterThan(withStranger));

		// A step can make people more comfortable, and a busy quarter alone is less comfortable than being home alone.
		var lesson = new Step { Kind = StepKind.Do, ModestyComfort = 20 };
		Assert.That(Comfort(home, lesson, (stranger, false)), Is.EqualTo(Math.Min(Comfort(home, null, (stranger, false)) + 20, ModestySettings.MaxComfort)));
		var square = world.Locations.First(x => x.QuarterKind == "square" && x.Template is QuarterTemplate);
		Assert.That(ModestyComfort.GetAloneComfort(gameData, square, world.Time), Is.LessThan(ModestyComfort.GetAloneComfort(gameData, home, world.Time)));
	}

	[Test]
	public void PartnersChangeClothesTogether()
	{
		// Like going swimming with a friend: both change into nothing, swim, and dress again. While swimming (until they start
		// changing back), both have nothing on. Swimming makes them comfortable enough to.
		static Step Do(int minutes) => new() { Kind = StepKind.Do, Minutes = TestData.Amount(minutes) };
		var swimming = new Need
		{
			Id = "swimming",
			Title = "Swimming", Icon = "icon",
			Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 },
			StepSettings =
			[
				new Step { Kind = StepKind.Talk, With = StepTarget.Anyone, Minutes = TestData.Amount(5) },
				Do(1),
				new Step { Kind = StepKind.Lead, Minutes = TestData.Amount(120) },
				new Step { Kind = StepKind.Dress, Outfit = OutfitKind.Nothing, WithPartner = true, ModestyComfort = ModestySettings.MaxComfort },
				Do(30),
				new Step { Kind = StepKind.Undress, Outfit = OutfitKind.Nothing, WithPartner = true },
				new Step { Kind = StepKind.Release },
				Do(10),
			],
		};
		swimming.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		gameData.Schedules.Needs = [.. gameData.Schedules.Needs, swimming];
		var random = new Random(0);
		var world = GameWorld.CreateNewGame(gameData, CharacterRandomizer.CreateRandomPlayer(gameData, random), random);
		world.IsHeadless = true;

		// Two people from different households, alone in a quarter, with the clothes they slept in on.
		var person = world.Npcs[0];
		var other = world.Npcs.First(x => x.Family != person.Family);
		var place = world.Locations.First(x => x.Template is QuarterTemplate);
		foreach (var someone in world.People.Where(x => x.Location == place))
			someone.Location = someone.Home;
		person.Location = place;
		other.Location = place;
		person.SetPlan(null, []);
		other.SetPlan(null, [new BusyTask(600)]);
		other.StartTask(gameData.Calendar.AddMinutes(world.Time, 600));
		var dressed = (Person: person.Clothing.Pieces.ToList(), Other: other.Clothing.Pieces.ToList());
		Assert.That(dressed.Person.Count != 0 && dressed.Other.Count != 0, Is.True);

		(int Person, int Other)? swimmingWorn = null;
		var isDone = false;
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is ChangeClothesTask { IsTakingOff: true })
				swimmingWorn = (person.Clothing.Pieces.Count, other.Clothing.Pieces.Count);
			isDone |= e.Person == person && e.Task is BusyTask { Minutes: 10, ResumeMinutes: null };
		};
		person.SetPlan(swimming, [], startStep: 0);
		world.AdvanceTime(gameData.Calendar.CreateTimeOffset(0, 2, 0), () => isDone);

		// Once they're done (and everything ending then has ended), both are back in what they had on, and the other is let go.
		Assert.That(swimmingWorn, Is.EqualTo(((int, int)?) (0, 0)));
		Assert.That(isDone, Is.True);
		Assert.That(person.Clothing.Pieces, Is.EquivalentTo(dressed.Person));
		Assert.That(other.Clothing.Pieces, Is.EquivalentTo(dressed.Other));
		Assert.That(other.Tasks.OfType<FollowTask>(), Is.Empty);
	}

	[Test]
	public async Task ThePlayersClothesAreChangedWithTheirPartnersAsync()
	{
		// Someone who talked with the player changes into nothing with them, comfortable enough to: the player's clothes are
		// changed at once.
		static Step Do(int minutes) => new() { Kind = StepKind.Do, Minutes = TestData.Amount(minutes) };
		var swimming = new Need
		{
			Id = "swimming",
			Title = "Swimming", Icon = "icon",
			Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 },
			StepSettings =
			[
				new Step { Kind = StepKind.Talk, With = StepTarget.Anyone, Minutes = TestData.Amount(5) },
				Do(1),
				new Step { Kind = StepKind.Dress, Outfit = OutfitKind.Nothing, WithPartner = true, ModestyComfort = ModestySettings.MaxComfort },

				// Swimming keeps them comfortable, so no one coming by makes them cover up.
				new Step { Kind = StepKind.Do, Minutes = TestData.Amount(30), ModestyComfort = ModestySettings.MaxComfort },
			],
		};
		swimming.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		gameData.Schedules.Needs = [.. gameData.Schedules.Needs, swimming];
		var random = new Random(0);
		var world = GameWorld.CreateNewGame(gameData, CharacterRandomizer.CreateRandomPlayer(gameData, random), random);
		var player = world.Player;
		var person = world.Npcs.First(x => x.Family != player.Family);
		var place = world.Locations.First(x => x.Template is QuarterTemplate);
		foreach (var someone in world.People.Where(x => x.Location == place))
			someone.Location = someone.Home;
		person.Location = place;
		player.Location = place;
		Assume.That(player.Clothing.Pieces, Is.Not.Empty);

		person.SetPlan(swimming, [], startStep: 0);
		await world.AdvanceTimeAsync(gameData.Calendar.CreateTimeOffset(0, 0, 30));
		Assume.That(player.TalkRequest, Is.EqualTo(person));
		await world.AcceptTalkAsync();
		await world.AdvanceTimeAsync(gameData.Calendar.CreateTimeOffset(0, 0, 15));

		Assert.That((person.Clothing.Pieces.Count, player.Clothing.Pieces.Count), Is.EqualTo((0, 0)));
	}

	[Test]
	public void StrangersAreTooShyToUndressTogetherInTheVillage()
	{
		// Without anything to make them comfortable, two people from different households in a quarter won't undress
		// together: the "dress" step fails as shy at once, without waiting for privacy, since the stranger is the partner who
		// would undress with them.
		var swimming = new Need
		{
			Id = "swimming",
			Title = "Swimming", Icon = "icon",
			Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 },
			StepSettings =
			[
				new Step { Kind = StepKind.Talk, With = StepTarget.Anyone, Minutes = TestData.Amount(5) },
				new Step { Kind = StepKind.Dress, Outfit = OutfitKind.Nothing, WithPartner = true },
				new Step { Kind = StepKind.Do, Minutes = TestData.Amount(30) },
			],
		};
		swimming.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		gameData.Schedules.Needs = [.. gameData.Schedules.Needs, swimming];
		var random = new Random(0);
		var world = GameWorld.CreateNewGame(gameData, CharacterRandomizer.CreateRandomPlayer(gameData, random), random);
		world.IsHeadless = true;
		var person = world.Npcs[0];
		var other = world.Npcs.First(x => x.Family != person.Family);
		var place = world.Locations.First(x => x.Template is QuarterTemplate);
		foreach (var someone in world.People.Where(x => x.Location == place))
			someone.Location = someone.Home;
		person.Location = place;
		other.Location = place;
		other.SetPlan(null, [new BusyTask(600)]);
		other.StartTask(gameData.Calendar.AddMinutes(world.Time, 600));
		var dressed = person.Clothing.Pieces.ToList();

		FailCause? cause = null;
		world.StepFailed += (_, e) => cause ??= e.Person == person ? e.Cause : null;
		var isWaiting = false;
		world.TaskStarted += (_, e) => isWaiting |= e.Person == person && e.Task is PrivacyTask;
		person.SetPlan(swimming, [], startStep: 0);
		world.AdvanceTime(gameData.Calendar.CreateTimeOffset(0, 2, 0), () => cause is not null);

		Assert.That((isWaiting, cause), Is.EqualTo((false, (FailCause?) FailCause.Shy)));
		Assert.That(person.Clothing.Pieces, Is.EquivalentTo(dressed));
	}

	[Test]
	public void NightClothesGoOnForSleepAtHomeAndComeOffAfter()
	{
		var world = CreateWorld(0);
		world.IsHeadless = true;
		var calendar = world.GameData.Calendar;
		var sleep = world.GameData.Schedules.Needs.Single(x => x.Id == world.GameData.Schedules.SleepNeed);
		var person = world.Npcs.First(x => !x.IsAsleep && x.Goal != sleep && x.Location == x.Home && x.GetOutfits(OutfitKind.Night).Any() && x.CurrentTask is not TalkTask);
		Assert.That(ClothingRules.IsWearingAny(person, OutfitKind.Night), Is.False);

		// Sleep's steps change into night clothes, sleep, and change back. Someone may stop to chat along the way, so what
		// they're wearing is checked as they start sleeping and as they start on what's next.
		var whileAsleep = (IReadOnlyList<ClothingPiece>?) null;
		var afterwards = (IReadOnlyList<ClothingPiece>?) null;
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is BusyTask && person.Goal == sleep)
				whileAsleep ??= [.. person.Clothing.Pieces];
			else if (e.Person == person && e.Task is not TalkTask && person.Goal != sleep && whileAsleep is not null)
				afterwards ??= [.. person.Clothing.Pieces];
		};
		person.SetPlan(sleep, [], startStep: 0, goalMinutes: 60);
		world.AdvanceTime(calendar.CreateTimeOffset(0, 4, 0));

		Assert.That(whileAsleep, Is.EquivalentTo(person.GetOutfits(OutfitKind.Night).First().Pieces));
		Assert.That(afterwards, Is.Not.Null);
		Assert.That(afterwards, Is.EquivalentTo(person.GetOutfits(OutfitKind.Everyday).First(x => x.Pieces.All(afterwards!.Contains)).Pieces));
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
	public void CarriedWorkClothesAreChangedIntoAndOutOf()
	{
		var world = CreateWorld(0);
		var person = world.Npcs.First(x => x.Workplace != x.Home && x.GetOutfits(OutfitKind.Work).Any() && !x.IsAsleep &&
			x.GetOutfits(OutfitKind.Everyday).Any(y => ClothingRules.IsWearing(x, y)) && ClothingRules.IsOutfitAtHome(x, OutfitKind.Work, world.Time));
		var work = person.GetOutfits(OutfitKind.Work).Single();
		var everyday = person.GetOutfits(OutfitKind.Everyday).First(x => ClothingRules.IsWearing(person, x));
		person.Location = person.Home;
		Assert.That(ClothingRules.IsOutfitAtHome(person, OutfitKind.Work, world.Time), Is.True);

		foreach (var piece in work.Pieces)
		{
			person.Home.Items.Remove(piece);
			person.Inventory.Add(piece);
		}

		person.Location = person.Workplace;
		Assert.That(ClothingRules.FindAtHand(person, OutfitKind.Work, world.Time), Is.EqualTo(work));

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
	public void UndressingPutsBackWhatTheOutfitReplaced()
	{
		var world = CreateWorld(0);
		var person = world.Npcs.First(x => !x.IsAsleep && x.GetOutfits(OutfitKind.Night).Any() && x.GetOutfits(OutfitKind.Everyday).Count() > 1);
		person.Location = person.Home;

		// They're in the everyday outfit that changing back would otherwise not choose.
		var fallback = person.GetOutfits(OutfitKind.Everyday).MaxBy(x => x.Pieces.Count(y => ClothingRules.IsAtHand(person, y, world.Time)));
		var everyday = person.GetOutfits(OutfitKind.Everyday).First(x => x != fallback);
		ClothingRules.Change(person, everyday, isTakingOff: false, world.Time);
		var before = person.Clothing.Pieces.ToList();

		var night = person.GetOutfits(OutfitKind.Night).First();
		ClothingRules.Change(person, night, isTakingOff: false, world.Time);
		Assert.That(person.ReplacedClothes[night], Is.EquivalentTo(before.Except(night.Pieces)));

		ClothingRules.Change(person, night, isTakingOff: true, world.Time);
		Assert.That(person.Clothing.Pieces, Is.EquivalentTo(before));
		Assert.That(person.ReplacedClothes.ContainsKey(night), Is.False);
	}

	[Test]
	public void WorkClothesTakenToBeWashedArentWorn()
	{
		var world = CreateWorld(0);
		var person = world.Npcs.First(x => x.Workplace != x.Home && x.Family.Members.Count > 1 && x.GetOutfits(OutfitKind.Work).Any(y => y.Pieces.Any(z => !z.Clothing.Accessory)) && !x.IsAsleep &&
			ClothingRules.IsOutfitAtHome(x, OutfitKind.Work, world.Time));
		var washer = person.Family.Members.First(x => x != person);
		person.Location = person.Home;
		washer.Location = washer.Home;

		var washing = ClothingRules.Take(washer, StepOwner.Household, outfit: null, accessories: false, world.Time);
		Assert.That(washing, Is.Not.Empty);
		Assert.That(washing.Select(x => x.Container), Is.All.SameAs(washer.Inventory));
		Assert.That(ClothingRules.FindAtHand(person, OutfitKind.Work, world.Time), Is.Null);
		Assert.That(ClothingRules.IsOutfitAtHome(person, OutfitKind.Work, world.Time), Is.False);

		var readyAt = world.GameData.Calendar.AddMinutes(world.Time, 60);
		ClothingRules.Put(washer, washing, readyAt);
		Assert.That(washing.Select(x => x.Container), Is.All.SameAs(washer.Home.Items));
		Assert.That(ClothingRules.IsOutfitAtHome(person, OutfitKind.Work, world.Time), Is.False);
		Assert.That(ClothingRules.IsOutfitAtHome(person, OutfitKind.Work, readyAt), Is.True);
	}

	[Test]
	public void BundledClothesArentPutAwayOnTheWayHome()
	{
		var world = CreateWorld(0);
		var person = world.Npcs.First(x => !x.IsAsleep && x.Home.Items.Clothing.Any(y => y.Owner == x && !y.Clothing.Accessory));
		person.Location = person.Home;
		var washing = ClothingRules.Take(person, StepOwner.Self, outfit: null, accessories: false, world.Time);
		var bundle = world.CreateBundle(person, "Washing", washing);

		world.MovePerson(person, person.Home.Quarter!, world.Time);
		world.MovePerson(person, person.Home, world.Time);
		Assert.That(washing.Select(x => x.Container), Is.All.SameAs(person.Inventory));

		// Once the bundle is gone, they're put away like anything else carried home.
		world.Unpack(bundle);
		world.MovePerson(person, person.Home.Quarter!, world.Time);
		world.MovePerson(person, person.Home, world.Time);
		Assert.That(washing.Select(x => x.Container), Is.All.SameAs(person.Home.Items));
	}

	[Test]
	public void LaundryTakesTheHouseholdsWashingAndHangsItOutAtHome()
	{
		var world = CreateWorld(0);
		world.IsHeadless = true;
		var calendar = world.GameData.Calendar;
		var laundry = world.GameData.Schedules.Needs.Single(x => x.Id == "need_laundry");

		// Wash day, when the laundry can be carried on with after a chat.
		while (calendar.GetDayOfWeek(world.Time).Id != "day_threeday" || calendar.GetParts(world.Time).Hour < 8)
			world.AdvanceTime(calendar.CreateTimeOffset(0, 1, 0));
		var person = world.Npcs.First(x => !x.IsAsleep && x.CurrentTask is not TalkTask && x.Family.Members.All(y => y.Goal != laundry && y.GetProgress(laundry) == 0) &&
			x.Home.Items.Clothing.Any(y => !y.Clothing.Accessory && y.IsReady(world.Time)));
		var washing = person.Home.Items.Clothing.Where(x => !x.Clothing.Accessory && x.IsReady(world.Time)).ToList();

		// The washing is carried to be washed, and hung out to dry once it's back home.
		var whileWashing = (IReadOnlyList<Item>?) null;
		var afterwards = (IReadOnlyList<ItemContainer?>?) null;
		world.TaskStarted += (_, e) =>
		{
			if (e.Person == person && e.Task is BusyTask && person.Goal == laundry)
				whileWashing ??= [.. person.Current?.Bundles.GetValueOrDefault("washing")?.Items ?? []];
			else if (e.Person == person && e.Task is not TalkTask && person.Goal != laundry && whileWashing is not null)
				afterwards ??= [.. washing.Select(x => x.Container)];
		};
		person.SetPlan(laundry, [], startStep: 0);
		world.AdvanceTime(calendar.CreateTimeOffset(0, 8, 0));

		Assert.That(whileWashing, Is.EquivalentTo(washing));
		Assert.That(afterwards, Is.Not.Null);
		Assert.That(afterwards, Is.All.SameAs(person.Home.Items));
		Assert.That(person.GetProgress(laundry), Is.EqualTo(1));
		Assert.That(washing.Select(x => x.ReadyAt), Is.All.Not.Null);
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
	public void ClothesTakenOffAreCarriedUntilTheStepsThatNeedThemAreDone()
	{
		var world = CreateWorld(0);
		var sleep = world.GameData.Schedules.Needs.Single(x => x.Id == world.GameData.Schedules.SleepNeed);
		var person = world.People.First(x => !x.IsPlayer && !x.IsAsleep && x.GetOutfits(OutfitKind.Night).Any(y => y.IsComplete) && x.GetOutfits(OutfitKind.Everyday).Any(y => ClothingRules.IsDressedIn(x, y)));
		var night = person.GetOutfits(OutfitKind.Night).First(x => x.IsComplete);
		person.Location = person.Home;
		foreach (var piece in night.Pieces.Where(x => x.Container is not null && x.Wearer is null))
		{
			piece.Container!.Remove(piece);
			person.Home.Items.Add(piece);
		}

		// Changing at home leaves what came off carried, and putting things away keeps it while a later step needs it.
		var dayClothes = person.Clothing.Pieces.Where(x => !night.Pieces.Contains(x)).ToList();
		Assert.That(dayClothes, Is.Not.Empty);
		ClothingRules.Change(person, night, isTakingOff: false, world.Time);
		Assert.That(dayClothes.Select(x => x.Container), Is.All.SameAs(person.Inventory));
		var sequence = new StepSequence(sleep, sleep.Steps.ToList().FindIndex(x => x.Kind == StepKind.Do));
		person.AddSequence(sequence);
		Assert.That(ClothingRules.GetNeededClothes(person, sequence), Is.EquivalentTo(dayClothes));
		ClothingRules.PutAway(person);
		Assert.That(dayClothes.Select(x => x.Container), Is.All.SameAs(person.Inventory));

		// Once they're put away, they're only at hand at home.
		foreach (var piece in dayClothes)
		{
			person.Inventory.Remove(piece);
			person.Home.Items.Add(piece);
		}
		Assert.That(ClothingRules.IsMissingNeededClothes(person, sequence, world.Time), Is.False);
		person.Location = world.Locations.First(x => x != person.Home);
		Assert.That(ClothingRules.IsMissingNeededClothes(person, sequence, world.Time), Is.True);
	}

	[Test]
	public async Task StartingWorkAtHomeTakesTheWorkClothesAlongAsync()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		var work = player.GetOutfits(OutfitKind.Work).FirstOrDefault();
		var choice = world.GetPlayerStartChoices().FirstOrDefault(x => x.Need.IsWork);
		Assume.That(work is not null && choice is { CanStart: true } && player.Workplace != player.Home, "The player has no work clothes, works at home, or can't start work now.");

		// At home, taking the work clothes along is next, which the player does themselves.
		player.Location = player.Home;
		await world.StartPlayerStepsAsync(choice!.Need, isAutomatic: false);
		var sequence = player.Current!;
		Assert.That(player.Inventory.Items, Is.Empty);
		Assert.That(sequence.CurrentStep?.Kind, Is.EqualTo(StepKind.Take));
		foreach (var piece in work!.Pieces.Where(world.CanPlayerTake))
			await world.PlayerTakeAsync(piece);
		Assert.That(world.GetPlayerSteps(sequence)[0].Place, Is.EqualTo(player.Workplace));

		// Getting there, changing into them is next, which they do themselves too, and then work.
		await world.MovePlayerAsync(player.Workplace);
		Assert.That(sequence.CurrentStep?.Kind, Is.EqualTo(StepKind.Dress));
		world.PlayerChangeInto(work);
		await world.UpdatePlayerStepsAsync();
		Assert.That(sequence.CurrentStep?.Player?.Title, Is.EqualTo("Work"));

		// Once they've worked and said they're done, changing out of the work clothes is the last thing to do. They're only
		// asked whether to keep going if there's time left for more.
		sequence.MinutesLeft = 600;
		await world.DoPlayerStepAsync(sequence);
		await world.AnswerKeepGoingAsync(sequence, keepGoing: false);
		Assert.That(world.GetPlayerSteps(sequence).Select(x => x.Step.Kind), Is.EqualTo(new[] { StepKind.Undress }));
		world.PlayerTakeOff(work.Pieces.First(x => x.Wearer == player));
		await world.UpdatePlayerStepsAsync();
		Assert.That(player.Sequences, Is.Empty);
	}

	[Test]
	public async Task ThePlayerChangesForWorkThemselvesAsync()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		var work = player.GetOutfits(OutfitKind.Work).FirstOrDefault();
		var need = world.GameData.Schedules.Needs.FirstOrDefault(x => x.IsWork && Scheduler.HasNeed(player, x));
		Assume.That(work is not null && need is not null && player.Workplace != player.Home && world.GetPlayerRemainingMinutes(need) > 60,
			"The player has no work clothes, works at home, or has no work to do today.");

		// Leaving home doesn't take the work clothes along. The player has to bring them.
		player.Location = player.Home;
		await world.MovePlayerAsync(player.Workplace);
		Assert.That(player.Inventory.Items, Is.Empty);
		await world.MovePlayerAsync(player.Home);
		foreach (var piece in work!.Pieces)
			await world.PlayerTakeAsync(piece);
		await world.MovePlayerAsync(player.Workplace);
		Assert.That(player.Inventory.Items, Is.SupersetOf(work.Pieces));

		// Starting work, changing into the work clothes is next, which the player does themselves.
		await world.StartPlayerStepsAsync(need!, isAutomatic: false);
		Assert.That(player.Current?.CurrentStep?.Kind, Is.EqualTo(StepKind.Dress));
		Assert.That(ClothingRules.IsWearing(player, work), Is.False);
		Assert.That(world.CanPlayerChangeInto(work), Is.True);
	}

	[Test]
	public void NightClothesStayOnWhenThePlayersSleepIsInterrupted()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		bool IsSleepingInNightClothes() => player.IsAsleep && ClothingRules.IsWearingAny(player, OutfitKind.Night);
		world.IsHeadless = true;
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(2, 0, 0), IsSleepingInNightClothes);
		Assume.That(IsSleepingInNightClothes(), Is.True, "The player never slept in night clothes.");

		// Taking over from the game stops the player's sleep, leaving its steps to them, and they're still in their night clothes.
		world.IsHeadless = false;
		Assert.That((player.Tasks.Count, player.IsAsleep, ClothingRules.IsWearingAny(player, OutfitKind.Night)), Is.EqualTo((0, false, true)));
	}

	[Test]
	public void NightClothesComeOffOnlyWhenChoosingSomethingOtherThanSleep()
	{
		var world = CreateWorld(0);
		var schedules = world.GameData.Schedules;
		var sleep = schedules.Needs.Single(x => x.Id == schedules.SleepNeed);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(2, 0, 0), () => world.Npcs.Any(x => x.IsAsleep && ClothingRules.IsWearingAny(x, OutfitKind.Night)));
		var sleeper = world.Npcs.FirstOrDefault(x => x.IsAsleep && ClothingRules.IsWearingAny(x, OutfitKind.Night));
		Assume.That(sleeper, Is.Not.Null, "No one slept in night clothes.");

		// Rested, they choose something else, and changing out of their night clothes is the first thing they do.
		sleeper!.SetLevel(sleep, 0);
		world.CancelGoal(sleeper);
		Assert.That(sleeper.Goal, Is.Not.EqualTo(sleep));
		Assert.That(ClothingRules.IsWearingAny(sleeper, OutfitKind.Night), Is.True);
		Assert.That(sleeper.CurrentTask, Is.EqualTo(new ChangeClothesTask(sleeper.GetOutfits(OutfitKind.Night).First(x => ClothingRules.IsWearing(sleeper, x)), IsTakingOff: true)));
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
	public async Task ThePlayerCanChangeAndMoveTheirClothesAtHomeAsync()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		var calendar = world.GameData.Calendar;
		var changeMinutes = world.GameData.Schedules.Clothing.ChangeMinutes;
		await world.MovePlayerAsync(player.Home);
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
		Assert.That(await world.FinishPlayerChangingAsync(wornBefore) is { } passed ? calendar.GetTotalMinutes(passed) : 0, Is.EqualTo(changeMinutes.Minutes));
		Assert.That(await world.FinishPlayerChangingAsync(player.Clothing.Pieces.ToList()), Is.Null);

		Assert.That(world.CanPlayerTake(piece), Is.True);
		await world.PlayerTakeAsync(piece);
		Assert.That(piece.Container, Is.SameAs(player.Inventory));
		Assert.That(world.CanPlayerPutAway(piece), Is.True);
		await world.PlayerPutAwayAsync(piece);
		Assert.That(piece.Container, Is.SameAs(player.Home.Items));

		// Clothes still drying can't be worn or taken.
		piece.ReadyAt = calendar.AddMinutes(world.Time, 60);
		Assert.That(world.CanPlayerWear(piece), Is.False);
		Assert.That(world.CanPlayerTake(piece), Is.False);
	}

	[Test]
	public async Task NightClothesPutOnWhileAwakeStayOnAsync()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		await world.MovePlayerAsync(player.Home);
		var nightwear = player.GetOutfits(OutfitKind.Night).First().Pieces.First(x => x.Clothing.Slots.Contains("torso"));

		world.PlayerWear(nightwear);
		Assert.That(nightwear.Wearer, Is.SameAs(player));
		await world.AdvanceTimeAsync(world.GameData.Calendar.CreateTimeOffset(0, 0, 30));
		Assert.That(nightwear.Wearer, Is.SameAs(player));
	}

	[Test]
	public async Task AccessoriesAreSharedAndArentWashedAsync()
	{
		var world = CreateWorld(0);
		foreach (var person in world.People)
		{
			var pieces = person.Outfits.SelectMany(x => x.Pieces).ToList();
			Assert.That(pieces.Where(x => x.Clothing.Accessory).GroupBy(x => x.Clothing).Select(x => x.Distinct().Count()), Is.All.EqualTo(1), person.FullName);
		}

		var player = world.Player;
		await world.MovePlayerAsync(player.Home);
		var washing = ClothingRules.Take(player, StepOwner.Household, outfit: null, accessories: false, world.Time);
		Assert.That(player.Home.Items.Clothing.Where(x => x.Clothing.Accessory), Is.Not.Empty);
		Assert.That(player.Home.Items.Clothing.Select(x => x.Clothing.Accessory), Is.All.True);
		Assert.That(washing.Select(x => x.Clothing.Accessory), Is.All.False);
	}

	[Test]
	public async Task ThePlayersWorkClothesGoOverTheirOtherClothesAsync()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		await world.MovePlayerAsync(player.Home);
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
		// Everyday clothes, and work clothes over each of them (in place of what they overlap), go out among anyone, and
		// night clothes are worn at home with the household, at each person's own boldness.
		for (var seed = 0; seed < 5; seed++)
		{
			var world = CreateWorld(seed);
			var gameData = world.GameData;
			foreach (var person in world.People)
			{
				var businessProfession = person.Business?.Profession.Id;
				IReadOnlyList<ClothingSlot> Check(OutfitKind kind, IEnumerable<ClothingItem> clothing) =>
					ClothingRules.GetUncoveredSlots(gameData, person, clothing, OutfitModesty.GetComfort(gameData, kind, person.Traits, businessProfession)!.Value);

				var everydays = person.GetOutfits(OutfitKind.Everyday).ToList();
				foreach (var everyday in everydays)
					Assert.That(Check(OutfitKind.Everyday, everyday.Pieces.Select(x => x.Clothing)), Is.Empty, $"Seed {seed}, {person.FullName}, {everyday.Template?.Id}");
				foreach (var work in person.GetOutfits(OutfitKind.Work))
				{
					foreach (var everyday in everydays)
					{
						var clothing = OutfitModesty.GetClothesIn(work.Template!, work.Pieces.Select(x => x.Clothing).ToList(), everyday.Pieces.Select(x => x.Clothing));
						Assert.That(Check(OutfitKind.Work, clothing), Is.Empty, $"Seed {seed}, {person.FullName}, {work.Template?.Id} over {everyday.Template?.Id}");
					}
				}

				foreach (var night in person.GetOutfits(OutfitKind.Night))
					Assert.That(Check(OutfitKind.Night, night.Pieces.Select(x => x.Clothing)), Is.Empty, $"Seed {seed}, {person.FullName}, {night.Template?.Id}");
			}
		}
	}

	[Test]
	public void EveryoneHasOutfitsModestEnoughEvenAtTheLeastBoldness()
	{
		// However shy someone is, there's an outfit of each kind they can have that they're comfortable in, with work clothes
		// that do over a variety of everyday clothes they could have.
		var boldness = s_gameData.ModestySettings.Boldness!.Property;
		for (var seed = 0; seed < 5; seed++)
		{
			var world = CreateWorld(seed);
			var gameData = world.GameData;
			var random = new Random(seed);
			foreach (var person in world.People)
			{
				var traits = person.Traits.With(boldness, 0);
				var businessProfession = person.Business?.Profession.Id;
				(Outfit Outfit, IReadOnlyList<ClothingItem> Clothing, bool IsModestEnough)? Choose(OutfitKind kind, IReadOnlyList<IReadOnlyList<ClothingItem>> wornUnder) =>
					OutfitModesty.Choose(gameData, random, kind, traits, businessProfession, wornUnder);

				var everydays = Enumerable.Range(0, 5).Select(_ => Choose(OutfitKind.Everyday, [])).ToList();
				Assert.That(everydays, Has.All.Matches<(Outfit, IReadOnlyList<ClothingItem>, bool IsModestEnough)?>(x => x?.IsModestEnough == true), $"Seed {seed}, {person.FullName}'s everyday clothes");
				if (Choose(OutfitKind.Work, everydays.Select(x => x!.Value.Clothing).ToList()) is { } work)
					Assert.That(work.IsModestEnough, Is.True, $"Seed {seed}, {person.FullName}'s work clothes");
				Assert.That(Choose(OutfitKind.Night, [])?.IsModestEnough, Is.True, $"Seed {seed}, {person.FullName}'s night clothes");
			}
		}
	}

	[Test]
	public void WorkStepsThatTakeAwayComfortCallForMoreModestWorkClothes()
	{
		// The data is loaded just for this test, since its steps are changed.
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		var random = new Random(0);
		var world = GameWorld.CreateNewGame(gameData, CharacterRandomizer.CreateRandomPlayer(gameData, random), random);
		var person = world.People.First(x => x.Business is not null);
		var profession = person.Business!.Profession.Id;
		var everyday = OutfitModesty.GetComfort(gameData, OutfitKind.Everyday, person.Traits, profession)!.Value;
		Assume.That(OutfitModesty.GetComfort(gameData, OutfitKind.Work, person.Traits, profession), Is.EqualTo(everyday), "The work steps already change comfort.");
		var steps = gameData.Schedules.Needs.Where(x => x.IsWork && Scheduler.HasNeed(person, x)).SelectMany(x => x.GetSteps(profession)).Where(x => x.Kind == StepKind.Do).ToList();
		Assume.That(steps, Is.Not.Empty);

		// Steps that add comfort don't make work clothes less modest, since walking to work can't add any.
		foreach (var step in steps)
			SetModestyComfort(step, 20);
		Assert.That(OutfitModesty.GetComfort(gameData, OutfitKind.Work, person.Traits, profession), Is.EqualTo(everyday));

		SetModestyComfort(steps[0], -20);
		Assert.That(OutfitModesty.GetComfort(gameData, OutfitKind.Work, person.Traits, profession), Is.EqualTo(Math.Max(0, everyday - 20)).Within(0.001));
		Assert.That(OutfitModesty.GetComfort(gameData, OutfitKind.Everyday, person.Traits, profession), Is.EqualTo(everyday));

		static void SetModestyComfort(Step step, int comfort) => typeof(Step).GetProperty(nameof(Step.ModestyComfort))!.SetValue(step, comfort);
	}

	[Test]
	public async Task ThePlayerMustBeDressedEnoughAsync()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		await world.MovePlayerAsync(player.Home);
		var quarter = player.Home.Quarter!;
		var everyday = player.GetOutfits(OutfitKind.Everyday).First(x => ClothingRules.IsAtHand(x, world.Time));
		if (world.CanPlayerChangeInto(everyday))
			world.PlayerChangeInto(everyday);
		Assert.That(world.GetPlayerUncoveredSlots(), Is.Empty);
		Assert.That(world.GetPlayerUncoveredSlotsAt(quarter), Is.Empty);

		// With nothing on, the player can't go out, though a step that makes them comfortable enough lets them do it as they are.
		world.PlayerTakeOffAll();
		Assert.That(world.GetPlayerUncoveredSlotsAt(quarter).Select(x => x.Id), Does.Contain("hips"));
		Assert.That(world.GetPlayerStepUncoveredSlots(new Step { Kind = StepKind.Do, ModestyComfort = ModestySettings.MaxComfort }), Is.Empty);
		Assert.That(world.GetPlayerStepUncoveredSlots(new Step { Kind = StepKind.Do, ModestyComfort = -ModestySettings.MaxComfort }).Select(x => x.Id), Is.EquivalentTo(new[] { "torso", "hips" }));
	}

	[Test]
	public async Task ChangingByHandCountsAsChangingIntoAndOutOfOutfitsAsync()
	{
		// Undressing by hand changes into the outfit of nothing at all, as a "dress" step would, so an "undress" step that
		// puts the clothes back on comes next. Dressing again by hand changes out of it, as that step would.
		var world = CreateWorld(0);
		var player = world.Player;
		var nothing = player.GetOutfits(OutfitKind.Nothing).Single(x => x.Pieces.Count == 0);
		var before = player.Clothing.Pieces.ToList();
		Assume.That(before, Is.Not.Empty);

		// The player is bold enough not to feel exposed with nothing on, which would be logged instead.
		world.SetBoldness(player, 100);
		world.PlayerTakeOffAll(isCarried: true);
		await world.FinishPlayerChangingAsync(before);
		Assert.That((ClothingRules.IsChangedInto(player, nothing), player.ReplacedClothes.GetValueOrDefault(nothing)), Is.EqualTo((true, before)));
		Assert.That(world.EventLog.GetLatest(player)?.Kind, Is.EqualTo(LogEntryKind.ChangingClothes));

		foreach (var piece in before)
			world.PlayerWear(piece, isCarried: true);
		await world.FinishPlayerChangingAsync([]);
		Assert.That((ClothingRules.IsChangedInto(player, nothing), player.ReplacedClothes.ContainsKey(nothing)), Is.EqualTo((false, false)));
	}

	[Test]
	public void ThePlayerCanAlwaysCoverUpMore()
	{
		var world = CreateWorld(0);
		var player = world.Player;
		var before = player.Clothing.Pieces.ToList();
		Assume.That(before.Any(x => x.Clothing.GetCoverage("hips") > 0), Is.True, "Nothing the player has on covers their hips.");

		world.PlayerTakeOffAll(isCarried: true);
		Assert.That(world.IsPlayerNoLessCovered(before), Is.False);
		Assert.That(world.IsPlayerNoLessCovered([]), Is.True);
		foreach (var piece in before)
			world.PlayerWear(piece, isCarried: true);
		Assert.That(world.IsPlayerNoLessCovered(before), Is.True);
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
		var pieces = template.Clothing.Select(x => Create(x.Clothing!)).ToList();
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
		var (world, person) = Enumerable.Range(0, 10).Select(CreateWorld)
			.Select(x => (World: x, Person: x.Npcs.FirstOrDefault(y => !y.IsAsleep && !y.GetOutfits(OutfitKind.Work).Any() &&
				y.GetOutfits(OutfitKind.Everyday).Any(z => ClothingRules.IsWearing(y, z) && z.Pieces.Any(piece => piece.Clothing.Accessory) && z.Pieces.Any(piece => !piece.Clothing.Accessory)))))
			.First(x => x.Person is not null);
		var everyday = person!.GetOutfits(OutfitKind.Everyday).First(x => ClothingRules.IsWearing(person, x));
		var accessories = everyday.Pieces.Where(x => x.Clothing.Accessory).ToList();
		var template = new Outfit { Id = "accessoriesOnly", Kind = OutfitKind.Work, Complete = true, Clothing = [.. accessories.Select(x => x.Clothing.Id)] };
		var work = world.Objects.Create(id => new OwnedOutfit(id, person, OutfitKind.Work, template, accessories));
		person.AddOutfit(work);
		person.Location = person.Workplace;

		// The accessories are already worn with the everyday clothes, but the rest of those still has to come off.
		Assert.That(ClothingRules.IsWearing(person, work), Is.True);
		Assert.That(ClothingRules.IsDressedIn(person, work), Is.False);
		Assert.That(ClothingRules.FindAtHand(person, OutfitKind.Work, world.Time), Is.EqualTo(work));

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
