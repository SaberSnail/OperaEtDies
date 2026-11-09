using OperaEtDies.Characters;
using OperaEtDies.Data;

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
					CreateOption("male", 3, ("noun", "man"), ("pronoun", "he")),
					CreateOption("female", 3, ("noun", "woman"), ("pronoun", "she")),
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
					CreateOption("smith", 3, ("description", "a blacksmith")),
					CreateOption("miller", 2, ("description", "a miller")),
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
						["male"] = [CreateValue("John", 3), CreateValue("Osric", 1)],
						["female"] = [CreateValue("Alice", 3)],
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
						["smith"] = [CreateValue("Smith", 3)],
						["miller"] = [CreateValue("Miller", 3)],
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
					CreateOption("tall", 2, ("description", "tall")),
					CreateOption("medium", 3, ("description", "medium")),
					CreateOption("short", 2, ("description", "short")),
				],
			},
			new CharacterProperty
			{
				Id = "weight",
				Title = "Weight",
				Type = CharacterPropertyType.ValueList,
				Values =
				[
					CreateOption("slim", 2, ("description", "slim")),
					CreateOption("medium", 3, ("description", "medium")),
					CreateOption("heavy", 2, ("description", "heavy")),
				],
			},
			new CharacterProperty
			{
				Id = "age",
				Title = "Age",
				Type = CharacterPropertyType.Integer,
				Range = new IntegerRange { Minimum = 16, Maximum = 70, Shape = IntegerShape.Low },
				Player = new CharacterRoleSettings { Range = new IntegerRange { Minimum = 36, Maximum = 60, Shape = IntegerShape.Low } },
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

	private static CharacterPropertyOption CreateOption(string id, int frequency, params (string Name, string Text)[] text) => new()
	{
		Id = id,
		Title = $"{id} title",
		Frequency = frequency,
		Text = text.ToDictionary(x => x.Name, x => x.Text, StringComparer.Ordinal),
	};

	private static WeightedString CreateValue(string value, int frequency) => new() { Value = value, Frequency = frequency };

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
