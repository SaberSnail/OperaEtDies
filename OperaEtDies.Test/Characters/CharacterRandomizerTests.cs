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

	[TestCase(ShapePeak.Low, 3)]
	[TestCase(ShapePeak.Middle, 2)]
	[TestCase(ShapePeak.High, 3)]
	public void IntegerShapesFavorTheirPartOfTheRange(ShapePeak shape, double mostWithin)
	{
		var gameData = CreateGameData(new CharacterProperty
		{
			Id = "number",
			Title = "Number",
			Type = CharacterPropertyType.Integer,
			Range = new IntegerRange { Minimum = 1, Maximum = 9, Shape = new WeightShape { Peak = shape, MostWithin = mostWithin } },
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
			ShapePeak.Low => (counts[1], counts[9]),
			ShapePeak.High => (counts[9], counts[1]),
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
				Defaults = new StringDefaults { Values = [new WeightedString { Value = "Red" }] },
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
			Defaults = new StringDefaults { Values = [new WeightedString { Value = "Red" }] },
			Player = new CharacterRoleSettings { Defaults = new StringDefaults { Values = [new WeightedString { Value = "Boss" }] } },
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

	[Test]
	public void ConditionalPropertiesAndValuesFollowConditions()
	{
		var gameData = TestData.CreateConditionalGameData();
		var randomizer = new CharacterRandomizer(gameData, new Random(0), CharacterRole.Npc);
		var builds = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal) { ["male"] = [], ["female"] = [] };

		for (var i = 0; i < 500; i++)
		{
			randomizer.RandomizeAll();
			var traits = randomizer.GetTraits();
			var gender = traits.GetOption("gender").Id;

			Assert.That(traits.HasValue("beard"), Is.EqualTo(gender == "male"));
			builds[gender].Add(traits.GetOption("build").Id);
		}

		Assert.That(builds["male"], Is.EquivalentTo(new[] { "brawny", "wiry" }));
		Assert.That(builds["female"], Is.EquivalentTo(new[] { "slender", "wiry" }));
	}

	[Test]
	public void ChangingAConditionUpdatesOnlyValuesThatNoLongerFit()
	{
		var gameData = TestData.CreateConditionalGameData();
		var randomizer = new CharacterRandomizer(gameData, new Random(0), CharacterRole.Npc);
		CharacterPropertyOption Option(string propertyId, string valueId) => gameData.CharacterProperties.Get(propertyId).Values!.Single(x => x.Id == valueId);

		randomizer["gender"].Value = Option("gender", "male");
		randomizer["build"].Value = Option("build", "brawny");
		Assert.That(randomizer["beard"].Value, Is.Not.Null);

		randomizer["gender"].Value = Option("gender", "female");
		Assert.That(randomizer["build"].Value, Is.AnyOf(Option("build", "slender"), Option("build", "wiry")));
		Assert.That(randomizer["beard"].Value, Is.Null);
		Assert.That(randomizer.HasValue(gameData.CharacterProperties.Get("beard")), Is.False);

		randomizer["build"].Value = Option("build", "wiry");
		randomizer["gender"].Value = Option("gender", "male");
		Assert.That(randomizer["build"].Value, Is.SameAs(Option("build", "wiry")));
		Assert.That(randomizer["beard"].Value, Is.Not.Null);
	}

	[Test]
	public void TraitsFormatTemplateVariants()
	{
		var gameData = TestData.CreateConditionalGameData();
		var randomizer = new CharacterRandomizer(gameData, new Random(0), CharacterRole.Npc);
		CharacterPropertyOption Option(string propertyId, string valueId) => gameData.CharacterProperties.Get(propertyId).Values!.Single(x => x.Id == valueId);

		randomizer["gender"].Value = Option("gender", "male");
		randomizer["build"].Value = Option("build", "brawny");
		randomizer["beard"].Value = Option("beard", "full");
		Assert.That(randomizer.GetTraits().GetDescription(gameData.CharacterTemplates), Is.EqualTo("brawny with a full beard"));

		randomizer["gender"].Value = Option("gender", "female");
		randomizer["build"].Value = Option("build", "slender");
		Assert.That(randomizer.GetTraits().GetDescription(gameData.CharacterTemplates), Is.EqualTo("slender"));
	}

	[Test]
	public void ObscurersHideValuesUpToTheirStrength()
	{
		CharacterPropertyOption CreateOption(string id, int prominence) =>
			new() { Id = id, Title = id, Prominence = prominence, Text = new Dictionary<string, string> { ["description"] = $"a {id} beard" } };
		var gameData = new GameData(
			new CharacterData
			{
				Templates =
				[
					new CharacterTemplate { Id = CharacterData.FullNameTemplateId, Template = "Hugh" },
					new CharacterTemplate
					{
						Id = CharacterData.DescriptionTemplateId,
						Variants =
						[
							new CharacterTemplateVariant { When = new CharacterCondition { Hidden = ["beard"] }, Template = "Hugh stands here." },
							new CharacterTemplateVariant { Template = "Hugh has {beard.description}." },
						],
					},
				],
				Properties =
				[
					new CharacterProperty { Id = "beard", Title = "Beard", Type = CharacterPropertyType.ValueList, Values = [CreateOption("short", 1), CreateOption("long", 3)] },
				],
				Obscurers =
				[
					new CharacterObscurer { Id = "mask", Hides = new Dictionary<string, int> { ["beard"] = 2 } },
					new CharacterObscurer { Id = "darkness", Hides = new Dictionary<string, int> { [CharacterObscurer.AllProperties] = 3 } },
				],
			},
			TestData.CreateLocationList());
		var randomizer = new CharacterRandomizer(gameData, new Random(0), CharacterRole.Npc);
		string Describe(params string[] obscurerIds) => randomizer.GetTraits().GetDescription(gameData.CharacterTemplates, gameData.GetObscuring(obscurerIds));

		randomizer["beard"].Value = gameData.CharacterProperties.Get("beard").Values![0];
		Assert.That(Describe(), Is.EqualTo("Hugh has a short beard."));
		Assert.That(Describe("mask"), Is.EqualTo("Hugh stands here."));

		randomizer["beard"].Value = gameData.CharacterProperties.Get("beard").Values![1];
		Assert.That(Describe("mask"), Is.EqualTo("Hugh has a long beard."));
		Assert.That(Describe("mask", "darkness"), Is.EqualTo("Hugh stands here."));
	}

	private static CharacterRandomizer CreateRandomizer(int seed) => new(s_gameData, new Random(seed), CharacterRole.Npc);

	private static CharacterPropertyOption GetOption(string propertyId, string valueId) =>
		s_gameData.CharacterProperties.Get(propertyId).Values!.Single(x => x.Id == valueId);

	private static GameData CreateGameData(params CharacterProperty[] properties) => new(
		new CharacterData { Templates = TestData.CreateTemplates("name", "description"), Properties = properties },
		TestData.CreateLocationList());

	private static readonly GameData s_gameData = TestData.CreateGameData();
}
