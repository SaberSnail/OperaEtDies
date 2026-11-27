using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Test;

/// <summary>
/// Small, predictable game data for tests.
/// </summary>
internal static class TestData
{
	public static GameInfo CreateGameInfo() => new() { Title = "Title", Description = Text("Description") };

	/// <summary>
	/// Creates game data for tests. Without schedules, people have no needs, so they stay at home.
	/// </summary>
	public static GameData CreateGameData(LayoutShape shape = LayoutShape.Radial, bool withFeatures = false, bool withSchedules = false, double napBelow = 0) =>
		new(CreateCharacterData(), CreateLocationList(shape, withFeatures), CreateProfessionList(), CreateFamilySettings(), CreateRelationshipSettings(), CreateCalendarSettings(),
			schedules: withSchedules ? CreateScheduleSettings(napBelow) : null);

	/// <summary>
	/// Creates schedules for the test data: fatigue met at home at night, hunger met at home, restlessness met in the
	/// square or a landmark, and eight hours of work on the first six days of the week, and shared chores: upkeep at home,
	/// a trip to the square or a landmark for water for each family member each day, weekly mending, a weekly laundry trip on the first
	/// day with the household's washing and time at home afterward (overdue if it's missed), and a trip to the square on the sixth day.
	/// Sleep is in night clothes, and work in work clothes fetched from home, an hour at a time. Everyone wakes within a few minutes of six, the infirm walk at half speed, and
	/// homemakers find chores three times as urgent. Water comes from the nearest square or landmark.
	/// </summary>
	public static ScheduleSettings CreateScheduleSettings(double napBelow = 0)
	{
		var settings = new ScheduleSettings
		{
			MinimumUrgency = 40,
			SightingHours = 24,
			Sleep = new SleepSettings { NapBelow = napBelow, NapMinutes = Range(30, 30), BedtimeLead = 60, DayClothesFactor = 1 },
			Clothing = new ClothingSchedule { ChangeMinutes = 5 },
			Talking = new TalkingSettings { RefuseUrgency = 95, AgreeUrgency = 60, WakeLevel = 6, AskAboutMinutes = 1, AskDoingMinutes = 5 },
			Socializing = new SocializingSettings { ChatMinutes = 30, ChatUrgency = 25, ChatGoalWeight = 0.5, WakeUrgency = 90, NearMinutes = 15, SeekFactor = 0.6, RecentHours = 12, AskPlayerAgainHours = 4, Clues = new SeekClues { Workplace = 3, Home = 2, Sighting = 4, Gathering = 2 } },
			UrgencyPower = 2,
			Idle = new IdleSettings { Place = Home, Minutes = Range(10, 30), Activity = "Resting" },
			Start = new ScheduleStart { WakeHour = 6, WakeMinutes = Range(-10, 10) },
			Player = new PlayerTimeSettings
			{
				Waits = [new PlayerTimeChoice { Minutes = new Duration(60) }, new PlayerTimeChoice { Until = PlayerTimeUntil.NextDayPart }],
				Shifts = [new PlayerTimeChoice { Minutes = new Duration(60) }, new PlayerTimeChoice { Until = PlayerTimeUntil.NextDayPart }],
			},
		};

		settings.Needs =
		[
			new Need { Id = "fatigue", Title = "Fatigue", Icon = "icon", Role = ScheduleSettings.SleepRole, Activity = "Sleeping", Hours = Hours(21, 4),
				StepSettings = [GoTo(Home), new Step { Kind = StepKind.Dress, Outfit = OutfitKind.Night, OnFail = new StepFailure { Any = new StepFailureResult(StepFailureKind.Next) } }, new Step { Kind = StepKind.Do, Player = new StepPlayer { Title = "Sleep", Result = "You slept" } }, Undress(OutfitKind.Night)],
				Level = new LevelNeedSettings { PerHour = 6, SatisfyPerHour = 12, UrgentFrom = 85, Levels = [new NeedLevel { Minimum = 0, Title = "Rested" }, new NeedLevel { Minimum = 50, Title = "Tired" }] } },
			new Need { Id = "hunger", Title = "Hunger", Icon = "icon", PlaceSetting = Home, Hours = Hours(5, 22),
				Level = new LevelNeedSettings { PerHour = 10, SatisfyPerHour = 200, WakeUrgency = 90, StartLevel = Range(10, 40) } },
			new Need { Id = "restlessness", Title = "Restlessness", Icon = "icon", PlaceSetting = Quarters(false, "square", "landmark"), Hours = Hours(9, 22),
				Level = new LevelNeedSettings { PerHour = 5, SatisfyPerHour = 60, PausedBy = ["fatigue"], StartLevel = Range(0, 50) } },
			new Need { Id = "socializing", Title = "Socializing", Icon = "icon", Role = ScheduleSettings.SocializingRole, Activity = "Chatting", StepSettings = [new Step { Kind = StepKind.Seek, Who = new StepWho { Friends = true }, Minutes = Amount(15) }], Hours = Hours(7, 21),
				Level = new LevelNeedSettings { PerHour = 2, SatisfyPerHour = 60, PausedBy = ["fatigue"] } },
			new Need { Id = "work", Title = "Work", Icon = "icon",
				StepSettings = [GoTo(Home, new StepCondition { OutfitAtHome = OutfitKind.Work }), new Step { Kind = StepKind.Take, ItemKind = StepItemKind.Clothing, From = StepItemPlace.Wardrobe, Outfit = OutfitKind.Work, Bundle = Bundle("workClothes", "Work clothes"), When = new StepCondition { OutfitAtHome = OutfitKind.Work } },
					GoTo(new SchedulePlace { Kind = NeedPlace.Workplace }), new Step { Kind = StepKind.Dress, Outfit = OutfitKind.Work, OnFail = new StepFailure { Any = new StepFailureResult(StepFailureKind.Next) } },
					new Step { Kind = StepKind.Repeat, Reconsider = true, Steps = [new Step { Kind = StepKind.Do, Minutes = Amount(60), MeetsQuota = true, AtWork = true, Player = new StepPlayer { Title = "Work", Result = "You worked" } }] },
					Undress(OutfitKind.Work, new StepCondition { DoneForToday = true })],
				Hours = new Hours { Start = 6, End = 19, Days = ["day1", "day2", "day3", "day4", "day5", "day6"], OnSpecialDays = false },
				Quota = new QuotaNeedSettings { Lead = 300, Time = new QuotaTime { Minutes = Amount(480), Shift = 480 } } },
			new Need { Id = "upkeep", Title = "Upkeep", Icon = "icon", Hours = Hours(6, 21),
				StepSettings = [GoTo(Home), new Step { Kind = StepKind.Do, MeetsQuota = true, Player = new StepPlayer { Title = "Tidy up", Result = "You tidied up" } }],
				Quota = new QuotaNeedSettings { Lead = 480, SharedByFamily = true, Time = new QuotaTime { Minutes = Amount(30, 15), Shift = 30 } } },
			new Need { Id = "water", Title = "Water", Icon = "icon", Hours = Hours(5, 20),
				StepSettings = [GoTo(Quarters(true, "square", "landmark")), Do(Amount(5), new StepPlayer { Title = "Fetch water", Result = "You fetched water" }),
					new Step { Kind = StepKind.Make, Bundle = Bundle("water", "Bucket of water") },
					GoTo(Home, player: new StepPlayer { Text = Text("Take it home to finish.") }),
					new Step { Kind = StepKind.Put, Bundle = Bundle("water"), Player = new StepPlayer { Title = "Put away the water", Result = "You put away the water" } }, new Step { Kind = StepKind.CountRun }],
				Quota = new QuotaNeedSettings { Lead = 600, SharedByFamily = true, Runs = Amount(0, 1) } },
			new Need { Id = "mending", Title = "Mending", Icon = "icon", PlaceSetting = Home, Hours = Hours(6, 21),
				Quota = new QuotaNeedSettings { Period = NeedPeriod.Week, Lead = 3000, SharedByFamily = true, Time = new QuotaTime { Minutes = Amount(60, 30), Shift = 60 } } },
			new Need { Id = "laundry", Title = "Laundry", Icon = "icon", Activity = "Washing",
				Hours = new Hours { Start = 7, End = 17, Days = ["day1"] },
				StepSettings = [GoTo(Home), new Step { Kind = StepKind.Take, ItemKind = StepItemKind.Clothing, From = StepItemPlace.Wardrobe, Owner = StepOwner.Household, Accessories = false, Bundle = Bundle("washing", "Washing"), Player = new StepPlayer { Title = "Gather the washing", Result = "You gathered the washing" } },
					GoTo(Quarters(false, "square", "landmark")), Do(Amount(0, 20), new StepPlayer { Title = "Wash the clothes", Result = "You washed the clothes" }),
					GoTo(Home, player: new StepPlayer { Text = Text("Take the washing home to hang it out.") }),
					new Step { Kind = StepKind.Do, Minutes = Amount(0, 10), Activity = "Hanging out the washing", Player = new StepPlayer { Title = "Hang out the washing", Result = "You hung out the washing" } },
					new Step { Kind = StepKind.Put, ItemKind = StepItemKind.Clothing, To = StepItemPlace.Wardrobe, Bundle = Bundle("washing"), AfterDelay = new Duration(60), DelayActivity = "Drying" }, new Step { Kind = StepKind.CountRun }],
				Quota = new QuotaNeedSettings { Period = NeedPeriod.Week, OverdueUrgency = 80, Lead = 300, SharedByFamily = true, Runs = Amount(1) } },
			new Need { Id = "market", Title = "Market", Icon = "icon",
				Hours = new Hours { Start = 7, End = 14, Days = ["day6"] },
				StepSettings = [GoTo(Quarters(false, "square")), new Step { Kind = StepKind.Do, Minutes = Amount(30), Hours = new Hours { Start = 7, End = 14 }, Player = new StepPlayer { Title = "Shop", Result = "You shopped" } },
					new Step { Kind = StepKind.Make, Bundle = Bundle("shopping", "Shopping") },
					GoTo(Home, player: new StepPlayer { Text = Text("Go home.") }), new Step { Kind = StepKind.Put, Bundle = Bundle("shopping"), Player = new StepPlayer { Title = "Put it away", Result = "You put it away" } }, new Step { Kind = StepKind.CountRun }],
				Quota = new QuotaNeedSettings { Lead = 300, SharedByFamily = true, Runs = Amount(1) } },
		];
		settings.TravelTimeFactors = [new TravelTimeFactor { Id = "infirm", When = Condition("profession", "infirm"), Factor = 2 }];
		settings.UrgencyFactors = [new UrgencyFactor { Id = "homemaker", When = Condition("profession", "homemaker"), Needs = ["upkeep", "water", "mending", "laundry", "market"], Factor = 3 }];
		return settings;
	}

	/// <summary>
	/// The place for a need met at home.
	/// </summary>
	public static SchedulePlace Home { get; } = new() { Kind = NeedPlace.Home };

	/// <summary>
	/// Creates the place for a need met in a quarter of one of the given kinds.
	/// </summary>
	public static SchedulePlace Quarters(bool nearest, params string[] kinds) =>
		new() { Kind = NeedPlace.Quarter, Nearest = nearest, QuarterKinds = kinds.Select(x => new QuarterChoice { QuarterKind = x }).ToList() };

	/// <summary>
	/// Creates hours from the start hour to the end hour, every day.
	/// </summary>
	public static Hours Hours(int start, int end) => new() { Start = start, End = end };

	/// <summary>
	/// Creates a step going to a place.
	/// </summary>
	public static Step GoTo(SchedulePlace place, StepCondition? when = null, StepPlayer? player = null) => new() { Kind = StepKind.GoTo, Place = place, When = when, Player = player };

	/// <summary>
	/// Creates a step spending a set time.
	/// </summary>
	public static Step Do(FamilyDuration minutes, StepPlayer? player = null) => new() { Kind = StepKind.Do, Minutes = minutes, Player = player };

	/// <summary>
	/// Creates an appointment's "meet" step, arriving 5 minutes early and waiting up to 30 minutes past its time.
	/// </summary>
	public static Step Meet() => new() { Kind = StepKind.Meet, Early = 5, Minutes = Amount(30) };

	/// <summary>
	/// Creates an appointment at the home of the one asked, with two hours between appointments, meeting there and then
	/// carrying out the given steps, after any given steps to do first.
	/// </summary>
	public static StepAppointment AtPartnerHome(IReadOnlyList<Step> steps, IReadOnlyList<Step>? before = null) =>
		new() { Place = new SchedulePlace { Kind = NeedPlace.PartnerHome }, Gap = 120, Patience = 60, Steps = [.. before ?? [], Meet(), .. steps] };

	/// <summary>
	/// Creates a step changing into an outfit of a kind.
	/// </summary>
	public static Step Dress(OutfitKind outfit) => new() { Kind = StepKind.Dress, Outfit = outfit };

	/// <summary>
	/// Creates a bundle for a step: with a title, for a step that takes or makes it, or just its id, for a step that puts it down.
	/// </summary>
	public static StepBundle Bundle(string id, string? title = null) => new() { Id = id, Title = title };

	/// <summary>
	/// Creates a step changing out of an outfit of a kind.
	/// </summary>
	public static Step Undress(OutfitKind outfit, StepCondition? when = null) => new() { Kind = StepKind.Undress, Outfit = outfit, When = when };

	/// <summary>
	/// Creates an amount, with an amount for each family member if given.
	/// </summary>
	public static FamilyDuration Amount(double baseAmount, double? perMember = null) => new() { Base = baseAmount, PerMember = perMember };

	private static IntegerRange Range(int minimum, int maximum) =>
		new() { Minimum = minimum, Maximum = maximum, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = Math.Max((maximum - minimum) / 4.0, 1) } };

	/// <summary>
	/// Creates the templates the engine requires.
	/// </summary>
	public static IReadOnlyList<CharacterTemplate> CreateTemplates(string fullName, string description) =>
		CreateTemplateList(fullName, description);

	/// <summary>
	/// Reads character data (properties, templates, and what each item in a "clothing" list obscures) from JSON and checks it on its own, the way the game
	/// does before checking it against the rest of the data.
	/// </summary>
	public static DataLoadResult<CharacterData> LoadCharacterData(string json)
	{
		var root = System.Text.Json.Nodes.JsonNode.Parse(json, documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true })!.AsObject();
		CharacterData data;
		try
		{
			IReadOnlyList<T> List<T>(string name) => root.TryGetPropertyValue(name, out var node) ? System.Text.Json.JsonSerializer.Deserialize<List<T>>(node, JsonDataLoader.Options)! : [];
			// Only what the clothing obscures matters to the character data.
			var obscurers = root.TryGetPropertyValue("clothing", out var clothing) ? clothing!.AsArray().Select(x => (IReadOnlyDictionary<string, int>) System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(x!["obscures"], JsonDataLoader.Options)!).ToList() : [];
			data = new CharacterData { Properties = List<CharacterProperty>("properties"), Templates = List<CharacterTemplate>("templates"), Obscurers = obscurers };
		}
		catch (System.Text.Json.JsonException e)
		{
			return DataLoadResult<CharacterData>.Failure(new DataLoadError("", e.Message));
		}

		var errors = data.Validate().Select(x => new DataLoadError("", x)).ToList();
		return errors.Count != 0 ? DataLoadResult<CharacterData>.Failure(errors) : DataLoadResult<CharacterData>.Success(data);
	}

	private static IReadOnlyList<CharacterTemplate> CreateTemplateList(string fullName, string description) =>
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
				Role = "gender",
				Title = "Gender",
				Type = CharacterPropertyType.ValueList,
				Values =
				[
					WithRole(CreateOption("male", Frequency.Common, ("noun", "man"), ("pronoun", "he"))),
					WithRole(CreateOption("female", Frequency.Common, ("noun", "woman"), ("pronoun", "she"))),
				],
				Player = new CharacterRoleSettings { Values = ["male"] },
			},
			new CharacterProperty
			{
				Id = "profession",
				Role = "profession",
				Title = "Profession",
				Type = CharacterPropertyType.ValueList,
				Values = CreateProfessionList().Professions.Select(x => x.ToOption()).ToList(),
				Player = new CharacterRoleSettings { Values = ["smith", "miller"] },
			},
			new CharacterProperty
			{
				Id = "workRole",
				Role = "workRole",
				Title = "Work Role",
				Type = CharacterPropertyType.ValueList,
				Values = [WithRole(CreateOption("independent", Frequency.Common)), WithRole(CreateOption("helper", Frequency.Common))],
				SetByGame = true,
			},
			new CharacterProperty
			{
				Id = "householdRole",
				Role = "householdRole",
				Title = "Household Role",
				Type = CharacterPropertyType.ValueList,
				Values = [WithRole(CreateOption("head", Frequency.Common)), WithRole(CreateOption("member", Frequency.Common))],
				SetByGame = true,
			},
			new CharacterProperty
			{
				Id = "headRelation",
				Role = "headRelation",
				Title = "Head Relation",
				Type = CharacterPropertyType.ValueList,
				Values = new[] { "self", "husband", "father", "mother", "son", "daughter", "sonInLaw", "daughterInLaw" }.Select(x => WithRole(CreateOption(x, Frequency.Common))).ToList(),
				SetByGame = true,
			},
			new CharacterProperty
			{
				Id = "headName",
				Role = "headName",
				Title = "Head Name",
				Type = CharacterPropertyType.String,
				SetByGame = true,
			},
			new CharacterProperty
			{
				Id = "atWork",
				Role = "atWork",
				Title = "At Work",
				Type = CharacterPropertyType.ValueList,
				Values = [WithRole(CreateOption("atWork", Frequency.Common)), WithRole(CreateOption("notAtWork", Frequency.Common))],
				SetByGame = true,
			},
			new CharacterProperty
			{
				Id = "married",
				Role = "married",
				Title = "Married",
				Type = CharacterPropertyType.ValueList,
				Values = [WithRole(CreateOption("married", Frequency.Common)), WithRole(CreateOption("unmarried", Frequency.Common))],
				SetByGame = true,
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
				Role = "lastName",
				Title = "Last Name",
				Type = CharacterPropertyType.String,
				Defaults = new StringDefaults
				{
					KeyedBy = "profession",
					ValuesByKey = new Dictionary<string, IReadOnlyList<WeightedString>>
					{
						["smith"] = [CreateValue("Smith", Frequency.Common)],
						["miller"] = [CreateValue("Miller", Frequency.Common)],
						["homemaker"] = [CreateValue("Hall", Frequency.Common)],
						["infirm"] = [CreateValue("Hall", Frequency.Common)],
					},
					OtherValues = [CreateValue("Green", Frequency.Common), CreateValue("Hill", Frequency.Common), CreateValue("Wood", Frequency.Uncommon), CreateValue("Brook", Frequency.Common), CreateValue("Field", Frequency.Rare), CreateValue("Stone", Frequency.Common), CreateValue("Marsh", Frequency.Common), CreateValue("Reed", Frequency.Common), CreateValue("Ford", Frequency.Uncommon), CreateValue("Lane", Frequency.Common)],
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
				Role = "age",
				Title = "Age",
				Type = CharacterPropertyType.Integer,
				Range = new IntegerRange { Minimum = 16, Maximum = 80, Shape = new WeightShape { Peak = ShapePeak.Low, MostWithin = 25 } },
				Groups = [new IntegerGroup { Id = "young", Maximum = 29 }, new IntegerGroup { Id = "adult", Minimum = 30, Maximum = 49 }, new IntegerGroup { Id = "old", Minimum = 50, Maximum = 69 }, new IntegerGroup { Id = "veryOld", Minimum = 70 }],
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
		CreateLocationList(),
		CreateProfessionList(),
		CreateFamilySettings(),
		CreateRelationshipSettings(),
		CreateCalendarSettings());

	/// <summary>
	/// Creates a condition that one property has one of the given values.
	/// </summary>
	public static CharacterCondition Condition(string propertyId, params string[] valueIds) =>
		new() { Values = new Dictionary<string, IReadOnlyList<string>> { [propertyId] = valueIds } };

	/// <summary>
	/// Creates who a step talks with or looks for: people with one of the given values of one property.
	/// </summary>
	public static StepWho Who(string propertyId, params string[] valueIds) =>
		new() { Values = new Dictionary<string, IReadOnlyList<string>> { [propertyId] = valueIds } };

	private static CharacterCondition Both(string propertyId, string[] valueIds, string otherPropertyId, string otherValueId) =>
		new() { Values = new Dictionary<string, IReadOnlyList<string>> { [propertyId] = valueIds, [otherPropertyId] = [otherValueId] } };

	public static CharacterTraits CreatePlayerTraits(GameData gameData) => CreateTraits(gameData, "male", "John", "Smith", "smith", "tall", "slim", 40);

	/// <summary>
	/// Creates a quarter template for each wealth level with room for a few houses, a square with room for
	/// businesses, landmarks in the village and outside it, and two houses for each wealth level. Towns are laid out in
	/// the given shape, and have a river and woods if asked. The bank and the clearing are only used with them.
	/// </summary>
	public static LocationList CreateLocationList(LayoutShape shape = LayoutShape.Radial, bool withFeatures = false) => new()
	{
		Features = withFeatures
			?
			[
				new MapFeature { Id = "river", Title = "River", Type = MapFeatureType.River, Crossings = ["bridge"], Text = [Text("The river is {direction}.")] },
				new MapFeature { Id = "woods", Title = "Woods", Type = MapFeatureType.Area, Size = new QuarterSize { Length = 2, Width = 2 }, Text = [Text("The woods are {direction}.")] },
			]
			: [],
		QuarterKinds =
		[
			new QuarterKind { Id = "square", Area = MapArea.Village, Count = new QuarterCount { Min = 1, Max = 1 }, Path = "street", TravelTime = 2 },
			new QuarterKind { Id = "lane", Area = MapArea.Village, Path = "lane", TravelTime = 2 },
			new QuarterKind { Id = "landmark", Area = MapArea.Village, Path = "alley", TravelTime = 2 },
			new QuarterKind { Id = "fields", Area = MapArea.Outskirts, Path = "track", TravelTime = 5 },
			new QuarterKind { Id = "riverbank", Area = MapArea.Outskirts, Path = "towpath", Borders = "river", TravelTime = 5 },
		],
		Village = new VillageSettings
		{
			FamilyCount = new IntegerRange { Minimum = 3, Maximum = 5, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 1 } },
			LandmarkCount = new IntegerRange { Minimum = 1, Maximum = 3, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 1 } },
			FeatureCount = withFeatures ? new IntegerRange { Minimum = 2, Maximum = 2, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 1 } } : null,
			BuildingTravelTime = 1,
			HouseTitle = "The {lastName} Home",
		},
		Layouts = [new TownLayoutChoice { Id = "layout", Shape = shape, Center = "square" }],
		Quarters =
		[
			CreateQuarter("lane-low", "lane", WealthLevel.Low, 2, 4, 0),
			CreateQuarter("lane-medium", "lane", WealthLevel.Medium, 2, 3, 1),
			CreateQuarter("lane-high", "lane", WealthLevel.High, 1, 2, 0),
			CreateQuarter("square", "square", null, 0, 0, 3),
			CreateQuarter("pond", "landmark", null, 0, 0, 0),
			CreateQuarter("shrine", "landmark", null, 0, 0, 0),
			CreateQuarter("common", "fields", null, 0, 0, 0),
			CreateQuarter("bank", "riverbank", null, 0, 0, 0),
			new QuarterTemplate { Id = "clearing", Title = "Clearing", Description = Describe("Short.", "Medium.", "Long."), Kind = "fields", Borders = "woods", FeatureText = new Dictionary<string, LongText> { ["woods"] = Text("The trees stand {direction}.") } },
		],
		Houses =
		[
			CreateHouse("cottage", WealthLevel.Low),
			CreateHouse("hovel", WealthLevel.Low),
			CreateHouse("house", WealthLevel.Medium),
			CreateHouse("townhouse", WealthLevel.Medium),
			CreateHouse("hall", WealthLevel.High),
			CreateHouse("manor", WealthLevel.High),
		],
		Businesses = [CreateBusiness("forge", "smith", "square"), CreateBusiness("smithy", "smith", "square", "lane")],
	};

	/// <summary>
	/// Creates professions for the test character data. Only one smithy is allowed, with one helper, and millers work
	/// from home with two helpers.
	/// </summary>
	public static ProfessionList CreateProfessionList() => new()
	{
		Professions =
		[
			new Profession
			{
				Id = "smith",
				Title = "Smith",
				Text = new Dictionary<string, string> { ["description"] = "a blacksmith" },
				Business = new ProfessionBusiness { Wealth = WealthLevel.High, MaxBusinesses = 1, Helpers = 1 },
			},
			new Profession
			{
				Id = "miller",
				Title = "Miller",
				Frequency = new FrequencyLevel(Frequency.Uncommon),
				Text = new Dictionary<string, string> { ["description"] = "a miller" },
				Business = new ProfessionBusiness { Wealth = WealthLevel.Low, Helpers = 2, ForHouseholdElders = true },
			},
			new Profession
			{
				Id = "homemaker",
				Role = "homemaker",
				Player = false,
				Title = "Homemaker",
				Frequency = new FrequencyLevel(Frequency.VeryRare),
				When = Condition("gender", "female"),
				Text = new Dictionary<string, string> { ["description"] = "a homemaker" },
			},
			new Profession
			{
				Id = "infirm",
				Role = "infirm",
				Player = false,
				Title = "Infirm",
				Frequency = new FrequencyLevel(Frequency.VeryRare),
				Text = new Dictionary<string, string> { ["description"] = "infirm" },
			},
		],
	};

	/// <summary>
	/// Creates a calendar of four seasons of three weeks of seven days, with four parts of the day (the last lasting past midnight) and one special day.
	/// </summary>
	public static CalendarSettings CreateCalendarSettings() => new()
	{
		Days = [.. Enumerable.Range(1, 7).Select(x => new CalendarDay { Id = $"day{x}", Title = $"Day {x}" })],
		Weeks =
		[
			new CalendarWeek { Id = "early", Title = "Early" },
			new CalendarWeek { Id = "mid", Title = "Mid" },
			new CalendarWeek { Id = "late", Title = "Late" },
		],
		Seasons =
		[
			new CalendarSeason { Id = "spring", Title = "Spring" },
			new CalendarSeason { Id = "summer", Title = "Summer" },
			new CalendarSeason { Id = "autumn", Title = "Autumn" },
			new CalendarSeason { Id = "winter", Title = "Winter" },
		],
		DayParts =
		[
			new CalendarDayPart { Id = "morning", Title = "Morning", Start = 6 },
			new CalendarDayPart { Id = "afternoon", Title = "Afternoon", Start = 12, Announcement = Text("The bell rings for noon.") },
			new CalendarDayPart { Id = "evening", Title = "Evening", Start = 18 },
			new CalendarDayPart { Id = "night", Title = "Night", Start = 21 },
		],
		SpecialDays = [new CalendarSpecialDay { Id = "midsummer", Title = "Midsummer", Season = "summer", Week = "mid", Day = "day4" }],
		StartYear = 1,
		YearFormat = "year {year}",
	};

	public static FamilySettings CreateFamilySettings() => new()
	{
		Head = new FamilyHeadSettings
		{
			FemaleChance = 10,
			FemaleMinWealth = WealthLevel.Low,
			Age = new IntegerRange { Minimum = 25, Maximum = 60, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 6 } },
		},
		Wife = new FamilyWifeSettings
		{
			Chance = 80,
			AgeDifference = new IntegerRange { Minimum = -10, Maximum = 4, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 3 } },
		},
		Children = new FamilyChildSettings
		{
			Count = new IntegerRange { Minimum = 0, Maximum = 2, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 1 } },
			Age = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 5 },
			SiblingAgeSpread = 2,
			SonChance = 5,
			HomemakerChance = 50,
		},
		Elder = new FamilyElderSettings
		{
			Chance = 40,
			FemaleChance = 70,
			AgeGap = new WeightShape { Peak = ShapePeak.Low, MostWithin = 8 },
			MinAge = new GenderAges { Male = 65, Female = 45 },
			InfirmChance = new Dictionary<string, double> { ["old"] = 20, ["veryOld"] = 60 },
			HomemakerChance = 50,
		},
		ParentAge = new ParentAgeSettings { Minimum = 16, MaxMother = 42, MaxFather = 55 },
		HelperChance = 80,
		Wealth = new FamilyWealthSettings
		{
			Helper = 0.5,
			HelperFalloff = 0.7,
			Homemaker = 0.5,
			Infirm = -0.5,
			Fortune = new IntegerRange { Minimum = 80, Maximum = 120, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 10 } },
			HeadAge = new Dictionary<string, double> { ["young"] = 90, ["adult"] = 110 },
			Thresholds = new WealthThresholds { Medium = 5, High = 10 },
		},
	};

	public static RelationshipSettings CreateRelationshipSettings() => new()
	{
		Score = new IntegerRange { Minimum = 0, Maximum = 100, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 15 } },
		FamilyBonus = 20,
		SpouseBonus = 30,
		ElderBonus = 15,
		ElderInLawBonus = 10,
		NeighborBonus = 10,
		FriendScore = 70,
		Levels = [new RelationshipLevel { Minimum = 0, Title = "Cold" }, new RelationshipLevel { Minimum = 40, Title = "Friendly" }, new RelationshipLevel { Minimum = 70, Title = "Friend" }],
		PlayerRelations =
		[
			new PlayerRelation { When = Condition("headRelation", "husband"), Title = "wife" },
			new PlayerRelation { When = Both("headRelation", ["father", "mother"], "gender", "male"), Title = "son" },
			new PlayerRelation { When = Both("headRelation", ["father", "mother"], "gender", "female"), Title = "daughter" },
			new PlayerRelation { When = Both("headRelation", ["son", "daughter", "sonInLaw", "daughterInLaw"], "gender", "male"), Title = "father" },
			new PlayerRelation { When = Both("headRelation", ["son", "daughter", "sonInLaw", "daughterInLaw"], "gender", "female"), Title = "mother" },
		],
	};

	/// <summary>
	/// Creates long text, marked as written by AI.
	/// </summary>
	public static LongText Text(string text) => new() { Text = text, Ai = true };

	/// <summary>
	/// Creates the descriptions of a location.
	/// </summary>
	public static LocationDescription Describe(string short1, string medium, params string[] paragraphs) =>
		new() { Short = Text(short1), Medium = Text(medium), Long = paragraphs.Select(Text).ToList() };

	/// <summary>
	/// Creates a copy of a value whose role is its id, for the properties whose values the engine finds by role.
	/// </summary>
	private static CharacterPropertyOption WithRole(CharacterPropertyOption option) => new()
	{
		Id = option.Id,
		Title = option.Title,
		Role = option.Id,
		Frequency = option.Frequency,
		Text = option.Text,
		When = option.When,
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

	private static QuarterTemplate CreateQuarter(string id, string kind, WealthLevel? wealth, int minHouses, int maxHouses, int businesses) => new()
	{
		Id = id,
		Title = $"{id} title",
		Description = Describe($"{id} short", $"{id} medium", $"{id} long 1", $"{id} long 2", $"{id} long 3"),
		Kind = kind,
		Houses = maxHouses > 0 ? new QuarterHouses { Minimum = minHouses, Maximum = maxHouses, Wealth = wealth!.Value } : null,
		Businesses = businesses,
	};

	private static HouseTemplate CreateHouse(string id, WealthLevel wealth) => new()
	{
		Id = id,
		Description = Describe($"{id} short", $"{id} medium", $"{id} long"),
		Wealth = wealth,
	};

	private static BusinessTemplate CreateBusiness(string id, string profession, params string[] quarterKinds) => new()
	{
		Id = id,
		Profession = profession,
		Title = $"{id} title",
		Description = Describe($"{id} short", $"{id} medium", $"{id} long"),
		QuarterKinds = quarterKinds,
	};
}
