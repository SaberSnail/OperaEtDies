using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class ClothingGeneratorTests
{
	[Test]
	public void EveryoneGetsOutfitsTheyCanHave()
	{
		for (var seed = 0; seed < 5; seed++)
		{
			var world = CreateWorld(seed);
			foreach (var person in world.People)
			{
				Assert.That(person.GetOutfits(OutfitKind.Everyday).Count(), Is.EqualTo(2), $"Seed {seed}, {person.FullName}");
				Assert.That(person.GetOutfits(OutfitKind.Night).Count(), Is.EqualTo(1), $"Seed {seed}, {person.FullName}");
				Assert.That(person.GetOutfits(OutfitKind.Nothing).Count(), Is.EqualTo(1), $"Seed {seed}, {person.FullName}");

				var canWork = world.GameData.Outfits.Any(x => x.Kind == OutfitKind.Work && CharacterCondition.IsMet(x.When, person.Traits.GetValueId));
				Assert.That(person.GetOutfits(OutfitKind.Work).Count(), Is.EqualTo(canWork ? 1 : 0), $"Seed {seed}, {person.FullName}");

				foreach (var outfit in person.Outfits)
				{
					Assert.That(outfit.Template!.Kind, Is.EqualTo(outfit.Kind));
					Assert.That(CharacterCondition.IsMet(outfit.Template.When, person.Traits.GetValueId), Is.True);
					Assert.That(outfit.Pieces.Select(x => x.Clothing.Id), Is.EqualTo(outfit.Template.Clothing));
					Assert.That(outfit.Pieces.Select(x => x.Owner), Is.All.SameAs(person));
				}
			}
		}
	}

	[Test]
	public void EveryoneStartsAsleepInTheirNightClothes()
	{
		var world = CreateWorld(0);

		// The game starts once the player wakes, so check those still asleep.
		foreach (var person in world.People.Where(x => x.IsAsleep))
		{
			var night = person.GetOutfits(OutfitKind.Night).Single();
			Assert.That(person.Clothing.Pieces, Is.EquivalentTo(night.Pieces), person.FullName);
			Assert.That(person.Inventory.Items, Is.Empty, person.FullName);
			var others = person.Outfits.Where(x => x != night).SelectMany(x => x.Pieces);
			Assert.That(others.Select(x => x.Container), Is.All.SameAs(person.Home.Items), person.FullName);
		}
	}

	[Test]
	public void SomeoneStartingAtWorkWearsTheirWorkClothesOverEverydayClothes()
	{
		var world = CreateWorld(0);
		var person = world.People.First(x => x.GetOutfits(OutfitKind.Work).Any() && x.GetOutfits(OutfitKind.Night).Any());
		person.Clothing.TakeOffAll(person.Home.Items);
		foreach (var item in person.Inventory.Items.ToList())
		{
			person.Inventory.Remove(item);
			person.Home.Items.Add(item);
		}

		ClothingGenerator.DressAtStart(person, isAtWork: true);

		var everyday = person.GetOutfits(OutfitKind.Everyday).First();
		var work = person.GetOutfits(OutfitKind.Work).Single();
		var replaced = everyday.Pieces.Where(x => work.Pieces.Any(y => y.Clothing.Overlaps(x.Clothing))).ToList();
		Assert.That(person.Clothing.Pieces, Is.EquivalentTo(work.Pieces.Concat(everyday.Pieces.Except(replaced))));
		Assert.That(person.Inventory.Items, Is.EquivalentTo(replaced));
		Assert.That(person.GetOutfits(OutfitKind.Night).Single().Pieces.Select(x => x.Container), Is.All.SameAs(person.Home.Items));
	}

	[Test]
	public void DescriptionsFollowWhatIsWorn()
	{
		var world = CreateWorld(0);
		var templates = world.GameData.CharacterTemplates;
		bool HidesSomething(Person person, OwnedOutfit outfit) =>
			person.Traits.GetDescription(templates, world.GameData.GetObscuring(outfit.Pieces.Select(x => x.Clothing))) != person.Traits.GetDescription(templates);
		string Describe(Person person) => person.Clothing.Describe(world.GameData.ClothingSlots, world.GameData.ClothingLayers, world.GameData.ClothingSettings.HidesClothingAt);
		var person = world.People.First(x => x.GetOutfits(OutfitKind.Everyday).Any(o => HidesSomething(x, o)));
		Assert.That(person.Description, Does.Contain(" is wearing "));

		var changes = new List<string?>();
		person.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
		person.Clothing.TakeOffAll(person.Home.Items);
		Assert.That(changes, Does.Contain(nameof(Person.Description)));
		Assert.That(person.Description, Does.Not.Contain(" is wearing "));
		Assert.That(person.Description, Is.EqualTo(person.Traits.GetDescription(templates)));

		person.Clothing.PutOn(person.GetOutfits(OutfitKind.Everyday).First(o => HidesSomething(person, o)), person.Home.Items);

		// Underclothes are covered by the clothes over them, which are described.
		Assert.That(person.Clothing.Pieces.Where(x => x.Clothing.Layer == "under").Select(x => x.Description), Is.Not.Empty.And.None.Matches<string>(x => person.Description.Contains(x!, StringComparison.Ordinal)));
		Assert.That(person.Clothing.Pieces.Where(x => x.Clothing.Layer != "under").Select(x => x.Description), Is.All.Matches<string>(x => person.Description.Contains(x!, StringComparison.Ordinal)));
		Assert.That(person.Description, Is.EqualTo(person.Traits.GetDescription(templates, world.GameData.GetObscuring(person.Clothing.Pieces.Select(x => x.Clothing)), Describe(person))));
		Assert.That(person.Description, Is.Not.EqualTo(person.Traits.GetDescription(templates, null, Describe(person))));
	}

	private static GameWorld CreateWorld(int seed)
	{
		var random = new Random(seed);
		return GameWorld.CreateNewGame(s_gameData, CharacterRandomizer.CreateRandomPlayer(s_gameData, random), random);
	}

	private static readonly GameData s_gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
}
