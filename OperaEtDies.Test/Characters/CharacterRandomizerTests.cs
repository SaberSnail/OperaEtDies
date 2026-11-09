using OperaEtDies.Characters;

namespace OperaEtDies.Test.Characters;

[TestFixture]
public sealed class CharacterRandomizerTests
{
	[Test]
	public void StartingValuesAreConsistent()
	{
		for (var seed = 0; seed < 50; seed++)
		{
			var randomizer = CreateRandomizer(seed);
			var traits = randomizer.GetTraits();

			Assert.That(traits.FirstName, traits.Gender == Gender.Male ? Is.AnyOf("John", "Osric") : Is.EqualTo("Alice"));
			Assert.That(traits.LastName, Is.EqualTo(traits.Profession.DefaultLastName));
			Assert.That(traits.Age, Is.InRange(20, 30));
		}
	}

	[Test]
	public void RandomizeAllCanPickEveryValue()
	{
		var randomizer = CreateRandomizer(0);
		var genders = new HashSet<Gender>();
		var professions = new HashSet<string>();
		var ages = new HashSet<int>();

		for (var i = 0; i < 500; i++)
		{
			randomizer.RandomizeAll();
			genders.Add(randomizer.Gender.Value);
			professions.Add(randomizer.Profession.Value.Id);
			ages.Add(randomizer.Age.Value);
		}

		Assert.That(genders, Is.EquivalentTo(new[] { Gender.Male, Gender.Female }));
		Assert.That(professions, Is.EquivalentTo(new[] { "smith", "miller" }));
		Assert.That(ages, Is.EquivalentTo(Enumerable.Range(20, 11)));
	}

	[Test]
	public void LockedValuesAreKept()
	{
		var randomizer = CreateRandomizer(0);
		randomizer.Gender.Lock(Gender.Female);
		randomizer.Age.Lock(25);
		randomizer.LastName.Lock("Atwood");

		for (var i = 0; i < 50; i++)
		{
			randomizer.RandomizeAll();
			randomizer.Age.Randomize();

			Assert.That(randomizer.Gender.Value, Is.EqualTo(Gender.Female));
			Assert.That(randomizer.FirstName.Value, Is.EqualTo("Alice"));
			Assert.That(randomizer.Age.Value, Is.EqualTo(25));
			Assert.That(randomizer.LastName.Value, Is.EqualTo("Atwood"));
		}
	}

	[Test]
	public void PredefinedValuesCanBeRandomized()
	{
		var randomizer = CreateRandomizer(0);
		randomizer.Age.Value = 25;

		Assert.That(randomizer.Age.Value, Is.EqualTo(25));
		Assert.That(randomizer.Age.IsLocked, Is.False);

		var ages = new HashSet<int>();
		for (var i = 0; i < 50; i++)
		{
			randomizer.Age.Randomize();
			ages.Add(randomizer.Age.Value);
		}

		Assert.That(ages, Has.Count.GreaterThan(1));
	}

	[Test]
	public void FirstNameFollowsGenderUnlessLocked()
	{
		var randomizer = CreateRandomizer(0);

		randomizer.Gender.Value = Gender.Female;
		Assert.That(randomizer.FirstName.Value, Is.EqualTo("Alice"));

		randomizer.FirstName.Lock("Alice");
		randomizer.Gender.Value = Gender.Male;
		Assert.That(randomizer.FirstName.Value, Is.EqualTo("Alice"));
	}

	[Test]
	public void LastNameFollowsProfessionUnlessLocked()
	{
		var randomizer = CreateRandomizer(0);
		var professions = TestData.CreateGameData().Professions;

		randomizer.Profession.Value = professions[1];
		Assert.That(randomizer.LastName.Value, Is.EqualTo("Miller"));

		randomizer.LastName.Lock("Cooper");
		randomizer.Profession.Value = professions[0];
		Assert.That(randomizer.LastName.Value, Is.EqualTo("Cooper"));
	}

	private static CharacterRandomizer CreateRandomizer(int seed) => new(TestData.CreateGameData(), new Random(seed), 20, 30);
}
