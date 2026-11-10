using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Test;

/// <summary>
/// Small, predictable game data for tests.
/// </summary>
internal static class TestData
{
	public static GameInfo CreateGameInfo() => new() { Title = "Title", Description = "Description" };

	public static GameData CreateGameData() => new(CreateCharacterData(), CreateLocationList());

	/// <summary>
	/// Creates the templates the engine requires.
	/// </summary>
	public static IReadOnlyList<CharacterTemplate> CreateTemplates(string fullName, string description) =>
	[
		new CharacterTemplate { Id = CharacterData.FullNameTemplateId, Template = fullName },
		new CharacterTemplate { Id = CharacterData.DescriptionTemplateId, Template = description },
	];

	public static CharacterData CreateCharacterData() => new()
	{
		Templates = CreateTemplates(
			"{firstName} {lastName}",
			"{fullName} is a {gender.noun} of {age}. {gender.pronoun:first_upper} is {height.description} and {weight.description}, and works as {profession.description}."),
		Properties =
		[
			new CharacterProperty
			{
				Id = "gender",
				Title = "Gender",
				Type = CharacterPropertyType.ValueList,
				Values =
				[
					CreateOption("male", Frequency.Common, ("noun", "man"), ("pronoun", "he")),
					CreateOption("female", Frequency.Common, ("noun", "woman"), ("pronoun", "she")),
				],
				Player = new CharacterRoleSettings { Values = ["male"] },
			},
			new CharacterProperty
			{
				Id = "profession",
				Title = "Profession",
				Type = CharacterPropertyType.ValueList,
				Values =
				[
					CreateOption("smith", Frequency.Common, ("description", "a blacksmith")),
					CreateOption("miller", Frequency.Uncommon, ("description", "a miller")),
				],
			},
			new CharacterProperty
			{
				Id = "firstName",
				Title = "First Name",
				Type = CharacterPropertyType.String,
				Defaults = new StringDefaults
				{
					KeyedBy = "gender",
					ValuesByKey = new Dictionary<string, IReadOnlyList<WeightedString>>
					{
						["male"] = [CreateValue("John", Frequency.Common), CreateValue("Osric", Frequency.Rare)],
						["female"] = [CreateValue("Alice", Frequency.Common)],
					},
				},
			},
			new CharacterProperty
			{
				Id = "lastName",
				Title = "Last Name",
				Type = CharacterPropertyType.String,
				Defaults = new StringDefaults
				{
					KeyedBy = "profession",
					ValuesByKey = new Dictionary<string, IReadOnlyList<WeightedString>>
					{
						["smith"] = [CreateValue("Smith", Frequency.Common)],
						["miller"] = [CreateValue("Miller", Frequency.Common)],
					},
				},
			},
			new CharacterProperty
			{
				Id = "height",
				Title = "Height",
				Type = CharacterPropertyType.ValueList,
				Values =
				[
					CreateOption("tall", Frequency.Uncommon, ("description", "tall")),
					CreateOption("medium", Frequency.Common, ("description", "medium")),
					CreateOption("short", Frequency.Uncommon, ("description", "short")),
				],
			},
			new CharacterProperty
			{
				Id = "weight",
				Title = "Weight",
				Type = CharacterPropertyType.ValueList,
				Values =
				[
					CreateOption("slim", Frequency.Uncommon, ("description", "slim")),
					CreateOption("medium", Frequency.Common, ("description", "medium")),
					CreateOption("heavy", Frequency.Uncommon, ("description", "heavy")),
				],
			},
			new CharacterProperty
			{
				Id = "age",
				Title = "Age",
				Type = CharacterPropertyType.Integer,
				Range = new IntegerRange { Minimum = 16, Maximum = 70, Shape = new WeightShape { Peak = ShapePeak.Low, MostWithin = 25 } },
				Player = new CharacterRoleSettings { Range = new IntegerRange { Minimum = 36, Maximum = 60, Shape = new WeightShape { Peak = ShapePeak.Low, MostWithin = 15 } } },
			},
		],
	};

	/// <summary>
	/// Creates traits for the test character data, using value ids for the value list properties.
	/// </summary>
	public static CharacterTraits CreateTraits(GameData gameData, string gender, string firstName, string lastName, string profession, string height, string weight, int age)
	{
		CharacterPropertyOption GetOption(string propertyId, string valueId) => gameData.CharacterProperties.Get(propertyId).Values!.Single(x => x.Id == valueId);

		return new CharacterTraits(new Dictionary<string, object>
		{
			["gender"] = GetOption("gender", gender),
			["profession"] = GetOption("profession", profession),
			["firstName"] = firstName,
			["lastName"] = lastName,
			["height"] = GetOption("height", height),
			["weight"] = GetOption("weight", weight),
			["age"] = age,
		});
	}

	/// <summary>
	/// Creates game data whose properties, values, and templates depend on gender. Unlike <see cref="CreateGameData"/>,
	/// the player can be either gender.
	/// </summary>
	public static GameData CreateConditionalGameData() => new(
		new CharacterData
		{
			Templates =
			[
				new CharacterTemplate { Id = CharacterData.FullNameTemplateId, Template = "{gender}" },
				new CharacterTemplate
				{
					Id = CharacterData.DescriptionTemplateId,
					Variants =
					[
						new CharacterTemplateVariant { When = Condition("gender", "male"), Template = "{build.description} with {beard.description}" },
						new CharacterTemplateVariant { Template = "{build.description}" },
					],
				},
			],
			Properties =
			[
				new CharacterProperty
				{
					Id = "gender",
					Title = "Gender",
					Type = CharacterPropertyType.ValueList,
					Values = [CreateOption("male", Frequency.Common), CreateOption("female", Frequency.Common)],
				},
				new CharacterProperty
				{
					Id = "build",
					Title = "Build",
					Type = CharacterPropertyType.ValueList,
					Values =
					[
						CreateOption("brawny", Frequency.Common, Condition("gender", "male"), ("description", "brawny")),
						CreateOption("slender", Frequency.Common, Condition("gender", "female"), ("description", "slender")),
						CreateOption("wiry", Frequency.Uncommon, null, ("description", "wiry")),
					],
				},
				new CharacterProperty
				{
					Id = "beard",
					Title = "Beard",
					Type = CharacterPropertyType.ValueList,
					When = Condition("gender", "male"),
					Values = [CreateOption("full", Frequency.Common, ("description", "a full beard")), CreateOption("none", Frequency.Uncommon, ("description", "no beard"))],
				},
			],
		},
		CreateLocationList());

	/// <summary>
	/// Creates a condition that one property has one of the given values.
	/// </summary>
	public static CharacterCondition Condition(string propertyId, params string[] valueIds) =>
		new() { Values = new Dictionary<string, IReadOnlyList<string>> { [propertyId] = valueIds } };

	public static CharacterTraits CreatePlayerTraits(GameData gameData) => CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40);

	public static LocationList CreateLocationList() => new()
	{
		StartingLocation = "home",
		Locations =
		[
			CreateLocation("home", "lane"),
			CreateLocation("lane", "home", "cottage"),
			CreateLocation("cottage", "lane"),
		],
	};

	private static CharacterPropertyOption CreateOption(string id, Frequency frequency, params (string Name, string Text)[] text) =>
		CreateOption(id, frequency, null, text);

	private static CharacterPropertyOption CreateOption(string id, Frequency frequency, CharacterCondition? when, params (string Name, string Text)[] text) => new()
	{
		Id = id,
		Title = $"{id} title",
		Frequency = frequency,
		Text = text.ToDictionary(x => x.Name, x => x.Text, StringComparer.Ordinal),
		When = when,
	};

	private static WeightedString CreateValue(string value, Frequency frequency) => new() { Value = value, Frequency = frequency };

	private static Location CreateLocation(string id, params string[] connections) => new()
	{
		Id = id,
		Title = $"{id} title",
		ShortDescription = $"{id} short",
		MediumDescription = $"{id} medium",
		LongDescription = [$"{id} long 1", $"{id} long 2", $"{id} long 3"],
		Connections = connections,
	};
}
