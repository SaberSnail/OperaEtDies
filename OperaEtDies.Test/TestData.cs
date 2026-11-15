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

	/// <summary>
	/// Creates game data for tests. Without schedules, people have no needs, so they stay at home.
	/// </summary>
	public static GameData CreateGameData(LayoutShape shape = LayoutShape.Radial, bool withFeatures = false, bool withSchedules = false, double napBelow = 0) =>
		new(CreateCharacterData(), CreateLocationList(shape, withFeatures), CreateProfessionList(), CreateFamilySettings(), CreateCalendarSettings(),
			schedules: withSchedules ? CreateScheduleSettings(napBelow) : null);

	/// <summary>
	/// Creates schedules for the test data: fatigue met at home at night, hunger met at home, restlessness met in the
	/// square or a landmark, and eight hours of work on the first six days of the week, and shared chores: upkeep at home,
	/// a trip to the square or a landmark for water for each family member each day, weekly mending, a weekly laundry trip on the first
	/// day with time at home afterward (overdue if it's missed), and a trip to the square on the sixth day. Everyone wakes within a few minutes of six, the infirm walk at half speed, and
	/// homemakers find chores three times as urgent. Water comes from the nearest square or landmark.
	/// </summary>
	public static ScheduleSettings CreateScheduleSettings(double napBelow = 0) => new()
	{
		MinimumUrgency = 40,
		SightingHours = 24,
		Sleep = new SleepSettings { NapBelow = napBelow, NapMinutes = Range(30, 30), BedtimeLead = 60 },
		Talking = new TalkingSettings { RefuseUrgency = 95, WakeLevel = 6, AskAboutMinutes = 1, AskDoingMinutes = 5, SocializeMinutes = 30, SocializeNeed = "socializing" },
		Socializing = new SocializingSettings { ChatUrgency = 25, ChatGoalWeight = 0.5, WakeUrgency = 90, NearMinutes = 15, SeekFactor = 0.6, SeekMinutes = 15, RecentHours = 12, AskPlayerAgainHours = 4, Clues = new SeekClues { Workplace = 3, Home = 2, Sighting = 4, Gathering = 2 } },
		UrgencyPower = 2,
		Needs =
		[
			new Need { Id = "fatigue", Title = "Fatigue", Activity = "Sleeping", Kind = NeedKind.Level, Place = NeedPlace.Home, PerHour = 6, SatisfyPerHour = 12, UrgentFrom = 85, Start = 21, End = 4,
				Levels = [new NeedLevel { Minimum = 0, Title = "Rested" }, new NeedLevel { Minimum = 50, Title = "Tired" }],
				PlayerAction = new PlayerAction { Title = "Sleep", Result = "You slept", Options = [new PlayerActionOption { Title = "Until rested", UntilMet = true }, new PlayerActionOption { Title = "Until dawn", UntilHour = 6 }, new PlayerActionOption { Minutes = 60 }] } },
			new Need { Id = "hunger", Title = "Hunger", Kind = NeedKind.Level, Place = NeedPlace.Home, PerHour = 10, SatisfyPerHour = 200, WakeUrgency = 90, Start = 5, End = 22, StartLevel = Range(10, 40) },
			new Need { Id = "restlessness", Title = "Restlessness", Kind = NeedKind.Level, Place = NeedPlace.Quarter, PerHour = 5, SatisfyPerHour = 60, PausedBy = ["fatigue"], Start = 9, End = 22,
				QuarterKinds = [new QuarterChoice { QuarterKind = "square" }, new QuarterChoice { QuarterKind = "landmark" }], StartLevel = Range(0, 50) },
			new Need { Id = "socializing", Title = "Socializing", Activity = "Chatting", Kind = NeedKind.Level, Place = NeedPlace.Quarter, PerHour = 2, SatisfyPerHour = 60, PausedBy = ["fatigue"], Start = 7, End = 21,
				QuarterKinds = [new QuarterChoice { QuarterKind = "square" }] },
			new Need { Id = "work", Title = "Work", Kind = NeedKind.Quota, Place = NeedPlace.Workplace, AtWork = true, Days = ["day1", "day2", "day3", "day4", "day5", "day6"], OnSpecialDays = false,
				Start = 6, End = 19, Minutes = 480, Shift = 120, Lead = 300,
				PlayerAction = new PlayerAction { Title = "Work", Result = "You worked", Options = [new PlayerActionOption { Title = "Until done", UntilMet = true }, new PlayerActionOption { Minutes = 120 }] } },
			new Need { Id = "upkeep", Title = "Upkeep", Kind = NeedKind.Quota, Place = NeedPlace.Home, Start = 6, End = 21, Minutes = 30, MinutesPerMember = 15, Shift = 30, Lead = 480, SharedByFamily = true,
				PlayerAction = new PlayerAction { Title = "Tidy up", Result = "You tidied up", Options = [new PlayerActionOption { Title = "Until done", UntilMet = true }, new PlayerActionOption { Minutes = 15 }] } },
			new Need { Id = "water", Title = "Water", Kind = NeedKind.Quota, Place = NeedPlace.Quarter, Nearest = true, QuarterKinds = [new QuarterChoice { QuarterKind = "square" }, new QuarterChoice { QuarterKind = "landmark" }], Start = 5, End = 20,
				VisitsPerMember = 1, VisitMinutes = 5, Lead = 600, SharedByFamily = true, ReturnHome = true,
				PlayerAction = new PlayerAction { Title = "Fetch water", Result = "You fetched water", Return = "Take it home to finish.", Home = new PlayerActionHome { Title = "Put away the water", Result = "You put away the water" } } },
			new Need { Id = "mending", Title = "Mending", Kind = NeedKind.Quota, Period = NeedPeriod.Week, Place = NeedPlace.Home, Start = 6, End = 21, Minutes = 60, MinutesPerMember = 30, Shift = 60,
				Lead = 3000, SharedByFamily = true },
			new Need { Id = "laundry", Title = "Laundry", Activity = "Washing", HomeActivity = "Hanging out the washing", Kind = NeedKind.Quota, Period = NeedPeriod.Week, OverdueUrgency = 80, Place = NeedPlace.Quarter, QuarterKinds = [new QuarterChoice { QuarterKind = "square" }, new QuarterChoice { QuarterKind = "landmark" }], Days = ["day1"],
				Start = 7, End = 17, Visits = 1, VisitMinutesPerMember = 20, HomeMinutesPerMember = 10, Lead = 300, SharedByFamily = true, ReturnHome = true,
				PlayerAction = new PlayerAction { Title = "Wash the clothes", Result = "You washed the clothes", Return = "Take the washing home to hang it out.", Home = new PlayerActionHome { Title = "Hang out the washing", Result = "You hung out the washing" } } },
			new Need { Id = "market", Title = "Market", Kind = NeedKind.Quota, Place = NeedPlace.Quarter, QuarterKinds = [new QuarterChoice { QuarterKind = "square" }], Days = ["day6"],
				Start = 7, End = 14, Visits = 1, VisitMinutes = 30, Lead = 300, SharedByFamily = true, ReturnHome = true,
				PlayerAction = new PlayerAction { Title = "Shop", Result = "You shopped", Return = "Go home.", Home = new PlayerActionHome { Title = "Put it away", Result = "You put it away" }, OnlyInHours = true } },
		],
		Idle = new IdleSettings { Place = NeedPlace.Home, Minutes = Range(10, 30), Activity = "Resting" },
		Start = new ScheduleStart { Need = "fatigue", WakeHour = 6, WakeMinutes = Range(-10, 10) },
		TravelTimeFactors = [new TravelTimeFactor { When = Condition("profession", "infirm"), Factor = 2 }],
		UrgencyFactors = [new UrgencyFactor { When = Condition("profession", "homemaker"), Needs = ["upkeep", "water", "mending", "laundry", "market"], Factor = 3 }],
	};

	private static IntegerRange Range(int minimum, int maximum) =>
		new() { Minimum = minimum, Maximum = maximum, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = Math.Max((maximum - minimum) / 4.0, 1) } };

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
				Values = CreateProfessionList().Professions.Select(x => x.ToOption()).ToList(),
				ValuesFrom = CharacterProperty.ProfessionsSource,
				Player = new CharacterRoleSettings { Values = ["smith", "miller"] },
			},
			new CharacterProperty
			{
				Id = "workRole",
				Title = "Work Role",
				Type = CharacterPropertyType.ValueList,
				Values = [CreateOption("independent", Frequency.Common), CreateOption("helper", Frequency.Common)],
				SetByGame = true,
			},
			new CharacterProperty
			{
				Id = "householdRole",
				Title = "Household Role",
				Type = CharacterPropertyType.ValueList,
				Values = [CreateOption("head", Frequency.Common), CreateOption("member", Frequency.Common)],
				SetByGame = true,
			},
			new CharacterProperty
			{
				Id = "headRelation",
				Title = "Head Relation",
				Type = CharacterPropertyType.ValueList,
				Values = new[] { "self", "husband", "father", "mother", "son", "daughter", "sonInLaw", "daughterInLaw" }.Select(x => CreateOption(x, Frequency.Common)).ToList(),
				SetByGame = true,
			},
			new CharacterProperty
			{
				Id = "headName",
				Title = "Head Name",
				Type = CharacterPropertyType.String,
				SetByGame = true,
			},
			new CharacterProperty
			{
				Id = "atWork",
				Title = "At Work",
				Type = CharacterPropertyType.ValueList,
				Values = [CreateOption("atWork", Frequency.Common), CreateOption("notAtWork", Frequency.Common)],
				SetByGame = true,
			},
			new CharacterProperty
			{
				Id = "married",
				Title = "Married",
				Type = CharacterPropertyType.ValueList,
				Values = [CreateOption("married", Frequency.Common), CreateOption("unmarried", Frequency.Common)],
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
		CreateCalendarSettings());

	/// <summary>
	/// Creates a condition that one property has one of the given values.
	/// </summary>
	public static CharacterCondition Condition(string propertyId, params string[] valueIds) =>
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
				new MapFeature { Id = "river", Title = "River", Type = MapFeatureType.River, Crossings = ["bridge"], Text = ["The river is {direction}."] },
				new MapFeature { Id = "woods", Title = "Woods", Type = MapFeatureType.Area, Size = new QuarterSize { Length = 2, Width = 2 }, Text = ["The woods are {direction}."] },
			]
			: [],
		FeatureCount = withFeatures ? new IntegerRange { Minimum = 2, Maximum = 2, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 1 } } : null,
		QuarterKinds =
		[
			new QuarterKind { Id = "square", Area = MapArea.Village, Unique = true, Path = "street", TravelTime = 2 },
			new QuarterKind { Id = "lane", Area = MapArea.Village, Path = "lane", TravelTime = 2 },
			new QuarterKind { Id = "landmark", Area = MapArea.Village, Path = "alley", TravelTime = 2 },
			new QuarterKind { Id = "fields", Area = MapArea.Outskirts, Path = "track", TravelTime = 5 },
			new QuarterKind { Id = "riverbank", Area = MapArea.Outskirts, Path = "towpath", Borders = "river", TravelTime = 5 },
		],
		LandmarkCount = new IntegerRange { Minimum = 1, Maximum = 3, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 1 } },
		BuildingTravelTime = 1,
		Layouts = [new TownLayoutChoice { Shape = shape, Center = "square" }],
		HouseTitle = "The {lastName} Home",
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
			new QuarterTemplate { Id = "clearing", Title = "Clearing", ShortDescription = "Short.", MediumDescription = "Medium.", LongDescription = ["Long."], Kind = "fields", Borders = "woods", FeatureText = new Dictionary<string, string> { ["woods"] = "The trees stand {direction}." } },
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
				Wealth = WealthLevel.High,
				MaxBusinesses = 1,
				Helpers = 1,
			},
			new Profession
			{
				Id = "miller",
				Title = "Miller",
				Frequency = new FrequencyLevel(Frequency.Uncommon),
				Text = new Dictionary<string, string> { ["description"] = "a miller" },
				Wealth = WealthLevel.Low,
				Helpers = 2,
				HelperWhen = Condition("gender", "male", "female"),
				WorksFromHome = true,
				ForHouseholdElders = true,
			},
			new Profession
			{
				Id = "homemaker",
				Title = "Homemaker",
				Frequency = new FrequencyLevel(Frequency.VeryRare),
				When = Condition("gender", "female"),
				Text = new Dictionary<string, string> { ["description"] = "a homemaker" },
			},
			new Profession
			{
				Id = "infirm",
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
			new CalendarDayPart { Id = "afternoon", Title = "Afternoon", Start = 12, Announcement = "The bell rings for noon." },
			new CalendarDayPart { Id = "evening", Title = "Evening", Start = 18 },
			new CalendarDayPart { Id = "night", Title = "Night", Start = 21 },
		],
		SpecialDays = [new CalendarSpecialDay { Id = "midsummer", Title = "Midsummer", Season = "summer", Week = "mid", Day = "day4" }],
		StartYear = 1,
		YearFormat = "year {year}",
	};

	public static FamilySettings CreateFamilySettings() => new()
	{
		Properties = new FamilyProperties { Gender = "gender", Age = "age", Profession = "profession", LastName = "lastName", WorkRole = "workRole", HouseholdRole = "householdRole", HeadRelation = "headRelation", HeadName = "headName", AtWork = "atWork", Married = "married" },
		Male = "male",
		Female = "female",
		Homemaker = "homemaker",
		Infirm = "infirm",
		Independent = "independent",
		Helper = "helper",
		Head = "head",
		Member = "member",
		HeadRelations = new HeadRelations { Self = "self", Husband = "husband", Father = "father", Mother = "mother", Son = "son", Daughter = "daughter", SonInLaw = "sonInLaw", DaughterInLaw = "daughterInLaw" },
		AtWork = "atWork",
		NotAtWork = "notAtWork",
		Married = "married",
		Unmarried = "unmarried",
		FamilyCount = new IntegerRange { Minimum = 3, Maximum = 5, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 1 } },
		PlayerRelations =
		[
			new PlayerRelation { When = Condition("headRelation", "husband"), Title = "wife" },
			new PlayerRelation { When = Both("headRelation", ["father", "mother"], "gender", "male"), Title = "son" },
			new PlayerRelation { When = Both("headRelation", ["father", "mother"], "gender", "female"), Title = "daughter" },
			new PlayerRelation { When = Both("headRelation", ["son", "daughter", "sonInLaw", "daughterInLaw"], "gender", "male"), Title = "father" },
			new PlayerRelation { When = Both("headRelation", ["son", "daughter", "sonInLaw", "daughterInLaw"], "gender", "female"), Title = "mother" },
		],
		Relationships = new RelationshipSettings
		{
			Score = new IntegerRange { Minimum = 0, Maximum = 100, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 15 } },
			FamilyBonus = 20,
			SpouseBonus = 30,
			ElderBonus = 15,
			ElderInLawBonus = 10,
			NeighborBonus = 10,
			FriendScore = 70,
			Levels = [new RelationshipLevel { Minimum = 0, Title = "Cold" }, new RelationshipLevel { Minimum = 40, Title = "Friendly" }, new RelationshipLevel { Minimum = 70, Title = "Friend" }],
		},
		FemaleHeadChance = 10,
		FemaleHeadMinWealth = WealthLevel.Low,
		HeadAge = new IntegerRange { Minimum = 25, Maximum = 60, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 6 } },
		WifeChance = 80,
		WifeAgeDifference = new IntegerRange { Minimum = -10, Maximum = 4, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 3 } },
		HouseholdElderChance = 40,
		FemaleHouseholdElderChance = 70,
		HouseholdElderAgeGap = new WeightShape { Peak = ShapePeak.Low, MostWithin = 8 },
		HouseholdElderMinAge = new GenderAges { Male = 65, Female = 45 },
		InfirmChance = new Dictionary<string, double> { ["old"] = 20, ["veryOld"] = 60 },
		HouseholdElderHomemakerChance = 50,
		MinParentAge = 16,
		MaxMotherAge = 42,
		MaxFatherAge = 55,
		ChildCount = new IntegerRange { Minimum = 0, Maximum = 2, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 1 } },
		ChildAge = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 5 },
		SiblingAgeSpread = 2,
		SonChance = 5,
		ChildHomemakerChance = 50,
		HelperChance = 80,
		HelperWealth = 0.5,
		HelperWealthFalloff = 0.7,
		HomemakerWealth = 0.5,
		InfirmWealth = -0.5,
		HouseholdFortune = new IntegerRange { Minimum = 80, Maximum = 120, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 10 } },
		HeadAgeWealth = new Dictionary<string, double> { ["young"] = 90, ["adult"] = 110 },
		WealthThresholds = new WealthThresholds { Medium = 5, High = 10 },
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
		ShortDescription = $"{id} short",
		MediumDescription = $"{id} medium",
		LongDescription = [$"{id} long 1", $"{id} long 2", $"{id} long 3"],
		Kind = kind,
		Wealth = wealth,
		Houses = maxHouses > 0 ? new CountRange { Minimum = minHouses, Maximum = maxHouses } : null,
		Businesses = businesses,
	};

	private static HouseTemplate CreateHouse(string id, WealthLevel wealth) => new()
	{
		Id = id,
		ShortDescription = $"{id} short",
		MediumDescription = $"{id} medium",
		LongDescription = [$"{id} long"],
		Wealth = wealth,
	};

	private static BusinessTemplate CreateBusiness(string id, string profession, params string[] quarterKinds) => new()
	{
		Id = id,
		Profession = profession,
		Title = $"{id} title",
		ShortDescription = $"{id} short",
		MediumDescription = $"{id} medium",
		LongDescription = [$"{id} long"],
		QuarterKinds = quarterKinds,
	};
}
