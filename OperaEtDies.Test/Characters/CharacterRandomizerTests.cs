using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Characters;

[TestFixture]
public sealed class CharacterRandomizerTests
{
	[Test]
	public void StartingValuesAreConsistent()
	{
		for (var seed = 0; seed < 50; seed++)
		{
			var traits = CreateRandomizer(seed).GetTraits();

			Assert.That(traits.GetString("firstName"), traits.GetOption("gender").Id == "male" ? Is.AnyOf("John", "Osric") : Is.EqualTo("Alice"));
			Assert.That(traits.GetString("lastName"), Is.EqualTo(traits.GetOption("profession").Id == "smith" ? "Smith" : "Miller"));
			Assert.That(traits.GetInteger("age"), Is.InRange(16, 70));
		}
	}

	[Test]
	public void RandomizeAllCanPickEveryValue()
	{
		var randomizer = CreateRandomizer(0);
		var genders = new HashSet<string>();
		var professions = new HashSet<string>();
		var heights = new HashSet<string>();

		for (var i = 0; i < 500; i++)
		{
			randomizer.RandomizeAll();
			var traits = randomizer.GetTraits();
			genders.Add(traits.GetOption("gender").Id);
			professions.Add(traits.GetOption("profession").Id);
			heights.Add(traits.GetOption("height").Id);
			Assert.That(traits.GetInteger("age"), Is.InRange(16, 70));
		}

		Assert.That(genders, Is.EquivalentTo(new[] { "male", "female" }));
		Assert.That(professions, Is.EquivalentTo(new[] { "smith", "miller" }));
		Assert.That(heights, Is.EquivalentTo(new[] { "tall", "medium", "short" }));
	}

	[Test]
	public void PlayerUsesPlayerSettings()
	{
		var randomizer = new CharacterRandomizer(TestData.CreateGameData(), new Random(0), CharacterRole.Player);

		for (var i = 0; i < 200; i++)
		{
			randomizer.RandomizeAll();
			var traits = randomizer.GetTraits();

			Assert.That(traits.GetOption("gender").Id, Is.EqualTo("male"));
			Assert.That(traits.GetInteger("age"), Is.InRange(36, 60));
		}
	}

	[TestCase(IntegerShape.Low)]
	[TestCase(IntegerShape.Middle)]
	[TestCase(IntegerShape.High)]
	public void IntegerShapesFavorTheirPartOfTheRange(IntegerShape shape)
	{
		var gameData = CreateGameData(new CharacterProperty
		{
			Id = "number",
			Title = "Number",
			Type = CharacterPropertyType.Integer,
			Range = new IntegerRange { Minimum = 1, Maximum = 9, Shape = shape },
		});
		var randomizer = new CharacterRandomizer(gameData, new Random(0), CharacterRole.Npc);

		var counts = new int[10];
		for (var i = 0; i < 5000; i++)
		{
			randomizer.RandomizeAll();
			counts[randomizer.GetTraits().GetInteger("number")]++;
		}

		Assert.That(counts[0], Is.Zero);
		Assert.That(counts.Skip(1), Has.All.GreaterThan(0));
		var (favored, rare) = shape switch
		{
			IntegerShape.Low => (counts[1], counts[9]),
			IntegerShape.High => (counts[9], counts[1]),
			_ => (counts[5], Math.Max(counts[1], counts[9])),
		};
		Assert.That(favored, Is.GreaterThan(rare * 3));
	}

	[Test]
	public void StringDefaultsCanBeUnkeyedOrMissing()
	{
		var gameData = CreateGameData(
			new CharacterProperty
			{
				Id = "nickname",
				Title = "Nickname",
				Type = CharacterPropertyType.String,
				Defaults = new StringDefaults { Values = [new WeightedString { Value = "Red", Frequency = 3 }] },
			},
			new CharacterProperty { Id = "motto", Title = "Motto", Type = CharacterPropertyType.String });

		var traits = new CharacterRandomizer(gameData, new Random(0), CharacterRole.Npc).GetTraits();

		Assert.That(traits.GetString("nickname"), Is.EqualTo("Red"));
		Assert.That(traits.GetString("motto"), Is.Empty);
	}

	[Test]
	public void RoleDefaultsReplacePropertyDefaults()
	{
		var gameData = CreateGameData(new CharacterProperty
		{
			Id = "nickname",
			Title = "Nickname",
			Type = CharacterPropertyType.String,
			Defaults = new StringDefaults { Values = [new WeightedString { Value = "Red", Frequency = 3 }] },
			Player = new CharacterRoleSettings { Defaults = new StringDefaults { Values = [new WeightedString { Value = "Boss", Frequency = 3 }] } },
		});

		Assert.That(new CharacterRandomizer(gameData, new Random(0), CharacterRole.Player).GetTraits().GetString("nickname"), Is.EqualTo("Boss"));
		Assert.That(new CharacterRandomizer(gameData, new Random(0), CharacterRole.Npc).GetTraits().GetString("nickname"), Is.EqualTo("Red"));
	}

	[Test]
	public void LockedValuesAreKept()
	{
		var randomizer = CreateRandomizer(0);
		var female = GetOption("gender", "female");
		randomizer["gender"].Lock(female);
		randomizer["age"].Lock(25);
		randomizer["lastName"].Lock("Atwood");

		for (var i = 0; i < 50; i++)
		{
			randomizer.RandomizeAll();
			randomizer["age"].Randomize();

			var traits = randomizer.GetTraits();
			Assert.That(traits.GetOption("gender"), Is.SameAs(female));
			Assert.That(traits.GetString("firstName"), Is.EqualTo("Alice"));
			Assert.That(traits.GetInteger("age"), Is.EqualTo(25));
			Assert.That(traits.GetString("lastName"), Is.EqualTo("Atwood"));
		}
	}

	[Test]
	public void PredefinedValuesCanBeRandomized()
	{
		var randomizer = CreateRandomizer(0);
		randomizer["age"].Value = 25;

		Assert.That(randomizer["age"].Value, Is.EqualTo(25));
		Assert.That(randomizer["age"].IsLocked, Is.False);

		var ages = new HashSet<int>();
		for (var i = 0; i < 50; i++)
		{
			randomizer["age"].Randomize();
			ages.Add((int) randomizer["age"].Value);
		}

		Assert.That(ages, Has.Count.GreaterThan(1));
	}

	[Test]
	public void FirstNameFollowsGenderUnlessLocked()
	{
		var randomizer = CreateRandomizer(0);

		randomizer["gender"].Value = GetOption("gender", "female");
		Assert.That(randomizer["firstName"].Value, Is.EqualTo("Alice"));

		randomizer["firstName"].Lock("Alice");
		randomizer["gender"].Value = GetOption("gender", "male");
		Assert.That(randomizer["firstName"].Value, Is.EqualTo("Alice"));
	}

	[Test]
	public void LastNameFollowsProfessionUnlessLocked()
	{
		var randomizer = CreateRandomizer(0);

		randomizer["profession"].Value = GetOption("profession", "miller");
		Assert.That(randomizer["lastName"].Value, Is.EqualTo("Miller"));

		randomizer["lastName"].Lock("Cooper");
		randomizer["profession"].Value = GetOption("profession", "smith");
		Assert.That(randomizer["lastName"].Value, Is.EqualTo("Cooper"));
	}

	[Test]
	public void TraitsFormatTemplates()
	{
		var gameData = TestData.CreateGameData();
		var traits = TestData.CreateTraits(gameData, "female", "Alice", "Miller", "miller", "tall", "heavy", 34);

		Assert.That(traits.Format(TextTemplate.Parse("{firstName} ({age}) {{{profession}}} {gender.pronoun} {gender.pronoun:first_upper}")), Is.EqualTo("Alice (34) {miller title} she She"));
	}

	private static CharacterRandomizer CreateRandomizer(int seed) => new(s_gameData, new Random(seed), CharacterRole.Npc);

	private static CharacterPropertyOption GetOption(string propertyId, string valueId) =>
		s_gameData.CharacterProperties.Get(propertyId).Values!.Single(x => x.Id == valueId);

	private static GameData CreateGameData(params CharacterProperty[] properties) => new(
		new CharacterData { Templates = TestData.CreateTemplates("name", "description"), Properties = properties },
		TestData.CreateLocationList());

	private static readonly GameData s_gameData = TestData.CreateGameData();
}
