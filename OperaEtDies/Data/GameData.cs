using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using OperaEtDies.Characters;
using OperaEtDies.Time;

namespace OperaEtDies.Data;

/// <summary>
/// The game content loaded from the data files (everything other than the basic game info). This is read-only;
/// the state of a game in progress lives in the game state view models.
/// </summary>
public sealed class GameData
{
	public GameData(CharacterData characters, LocationList locations, ProfessionList professions, FamilySettings families, RelationshipSettings relationships, CalendarSettings calendar, FrequencyChances? frequencyChances = null, PortraitData? portraits = null, ScheduleSettings? schedules = null, ClothingData? clothing = null, ThemeSettings? theme = null, IReadOnlyList<GameIcon>? icons = null)
	{
		Icons = new DataCatalog<GameIcon>(icons ?? []);
		Theme = theme;
		Schedules = schedules ?? ScheduleSettings.None;
		var clothingData = clothing ?? ClothingData.None;
		ClothingSlots = new DataCatalog<ClothingSlot>(clothingData.Slots);
		ClothingLayers = new DataCatalog<ClothingLayer>(clothingData.Layers);
		ModestyRules = clothingData.ModestyRules;
		ModestySettings = clothingData.Modesty ?? ModestySettings.None;
		ClothingColors = new DataCatalog<ClothingColor>(clothingData.Colors);
		ClothingBulks = new DataCatalog<ClothingBulk>(clothingData.Bulks);
		ClothingSettings = clothingData.Settings ?? ClothingSettings.None;
		Clothing = new DataCatalog<ClothingItem>(clothingData.Items);
		Outfits = new DataCatalog<Outfit>(clothingData.Outfits);
		FrequencyChances = frequencyChances ?? new FrequencyChances();
		Calendar = new GameCalendar(calendar);

		// The properties and values the family generator depends on are found by their roles.
		families.Roles = characters.GetFamilyRoles();

		CharacterProperties = new DataCatalog<CharacterProperty>(characters.Properties);
		PortraitRenderer = portraits is null ? null : new PortraitRenderer(portraits, CharacterProperties);

		// The character data is validated on load, so the templates are always valid.
		CharacterTemplates = new TextTemplateSet(characters.Templates.Select(x => KeyValuePair.Create(x.Id, x.ParseVariants())));
		CharacterLowChanceWarning = characters.LowChanceWarning;

		Village = locations.Village;
		QuarterKinds = new DataCatalog<QuarterKind>(locations.QuarterKinds);
		Layouts = locations.Layouts;
		MapFeatures = new DataCatalog<MapFeature>(locations.Features);
		FeatureCount = locations.Village.FeatureCount ?? new IntegerRange { Minimum = 0, Maximum = 0 };
		QuarterTemplates = new DataCatalog<QuarterTemplate>(locations.Quarters);
		HouseTemplates = new DataCatalog<HouseTemplate>(locations.Houses);
		HouseTitle = TextTemplate.TryParse(locations.Village.HouseTitle ?? "", out var houseTitle) ? houseTitle : TextTemplate.Parse("");
		BusinessTemplates = new DataCatalog<BusinessTemplate>(locations.Businesses);
		m_businessTemplatesByProfession = locations.Businesses
			.GroupBy(x => x.Profession, StringComparer.Ordinal)
			.ToDictionary(x => x.Key, x => (IReadOnlyList<BusinessTemplate>) x.ToList(), StringComparer.Ordinal);

		// Professions without business settings, such as keeping house, aren't businesses. A business with no places of its
		// own works from home.
		Professions = new DataCatalog<Profession>(professions.Professions.Where(x => x.IsBusiness));
		foreach (var profession in professions.Professions.Where(x => x is not null))
			profession.WorksFromHome = profession.IsBusiness && !m_businessTemplatesByProfession.ContainsKey(profession.Id);
		Families = families;
		Relationships = relationships;

		// The player heads a household, so their age comes from the head ages, from the youngest age at which they can
		// have an adult child. The family settings are checked against the age property when the data is loaded.
		if (CharacterProperties.Find(families.Roles.Properties.Age) is { Type: CharacterPropertyType.Integer } age)
		{
			m_ageProperty = age;
			m_playerAgeRange = families.Head.Age.StartingAt(GetMinPlayerAge(age, families));
		}
	}

	/// <summary>
	/// Gets the youngest age at which the player can have an adult child: the youngest age a non-player character can be,
	/// plus the youngest age at which someone can have a child.
	/// </summary>
	internal static int GetMinPlayerAge(CharacterProperty age, FamilySettings families) => age.GetRange(CharacterRole.Npc).Minimum + families.ParentAge.Minimum;

	/// <summary>
	/// Loads all of the game content from the data folder, with the files from the mod folders applied in order. The
	/// portraits can be left out, in which case characters have no portraits. Every problem found is reported. Values
	/// that are too unlikely to be chosen are logged as warnings.
	/// </summary>
	public static DataLoadResult<GameData> Load(string dataDirectory, IReadOnlyList<string>? modDirectories = null, bool includePortraits = true)
	{
		var document = DataDocument.Read(dataDirectory, modDirectories ?? []);
		var result = document.Value is { } value ? Load(value, includePortraits) : DataLoadResult<GameData>.Failure(document.Errors);
		if (document.Value is null)
		{
			foreach (var error in result.Errors)
				Log.Error(error.ToString());
		}

		return result;
	}

	/// <summary>
	/// Loads all of the game content from merged data. Every problem found is reported, and logged.
	/// </summary>
	public static DataLoadResult<GameData> Load(DataDocument document, bool includePortraits = true)
	{
		var errors = new List<DataLoadError>();
		T? Section<T>(string name, bool isRequired = true)
			where T : class
		{
			var section = document.ReadSection<T>(name, errors);
			if (section is null && isRequired && document.GetSection(name) is null)
				errors.Add(document.CreateError(OurResources.DataSectionMissing.FormatCurrentCulture(name)));
			return section;
		}

		IReadOnlyList<T> List<T>(string name) => document.ReadSection<List<T>>(name, errors) ?? [];

		var game = Section<GameInfo>(GameInfo.SectionName);
		var professions = new ProfessionList { Professions = List<Profession>("professions") };
		var clothing = new ClothingData
		{
			Slots = List<ClothingSlot>("clothingSlots"),
			Layers = List<ClothingLayer>("clothingLayers"),
			Bulks = List<ClothingBulk>("clothingBulks"),
			Colors = List<ClothingColor>("clothingColors"),
			Items = List<ClothingItem>("clothing"),
			Outfits = List<Outfit>("outfits"),
			ModestyRules = List<ModestyRule>("modestyRules"),
			Modesty = Section<ModestySettings>(ModestySettings.SectionName, isRequired: false) ?? ModestySettings.None,
			Settings = Section<ClothingSettings>(ClothingSettings.SectionName, isRequired: false) ?? ClothingSettings.None,
		};
		var properties = List<CharacterProperty>("properties");
		var bulks = clothing.Bulks.Where(x => !string.IsNullOrWhiteSpace(x?.Id)).GroupBy(x => x.Id, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
		var characters = new CharacterData
		{
			Properties = properties,
			Templates = List<CharacterTemplate>("templates"),
			// Templates are checked with every property hidden as strongly as all the clothing together could hide it.
			Obscurers = [CharacterObscuring.GetStrengths(properties.OfType<CharacterProperty>(), clothing.Items.OfType<ClothingItem>().Where(x => x.Coverage is not null), bulks.GetValueOrDefault)],
			LowChanceWarning = game?.LowChanceWarning,
		}.WithProfessions(professions.Professions);

		var village = Section<VillageSettings>(VillageSettings.SectionName);
		var quarterKinds = List<QuarterKind>("quarterKinds");
		var layouts = List<TownLayoutChoice>("layouts");
		var features = List<MapFeature>("features");
		var quarters = List<QuarterTemplate>("quarters");
		var houses = List<HouseTemplate>("houses");
		var businesses = List<BusinessTemplate>("businesses");
		var families = Section<FamilySettings>(FamilySettings.SectionName);
		var relationships = Section<RelationshipSettings>(RelationshipSettings.SectionName);
		var calendarSection = Section<CalendarSection>("calendar");
		var days = List<CalendarDay>("days");
		var weeks = List<CalendarWeek>("weeks");
		var seasons = List<CalendarSeason>("seasons");
		var dayParts = List<CalendarDayPart>("dayParts");
		var specialDays = List<CalendarSpecialDay>("specialDays");
		var skies = List<CalendarSky>("skies");
		var schedules = Section<ScheduleSettings>(ScheduleSettings.SectionName, isRequired: false) ?? (document.GetSection(ScheduleSettings.SectionName) is null ? ScheduleSettings.None : null);
		var needs = List<Need>("needs");
		var travelTimeFactors = List<TravelTimeFactor>("travelTimeFactors");
		var urgencyFactors = List<UrgencyFactor>("urgencyFactors");
		var stepLists = List<StepList>("stepLists");
		var icons = List<GameIcon>(GameIcon.SectionName);
		var theme = Section<ThemeSettings>(ThemeSettings.SectionName, isRequired: false);

		// Without portraits, characters have no portraits.
		var portraitSection = includePortraits ? Section<PortraitSection>("portraits", isRequired: false) : null;
		var portraits = portraitSection is null ? null : new PortraitData
		{
			Width = portraitSection.Width,
			Height = portraitSection.Height,
			Colors = List<PortraitColor>("portraitColors"),
			Scales = List<PortraitScale>("portraitScales"),
			Offsets = List<PortraitOffset>("portraitOffsets"),
			Layers = List<PortraitLayer>("portraitLayers"),
			ShapeSets = List<PortraitShapeSet>("portraitShapeSets"),
		};

		if (errors.Count != 0 || game is null || village is null || families is null || relationships is null || calendarSection is null || schedules is null)
			return Fail(errors);

		var locations = new LocationList { Village = village, QuarterKinds = quarterKinds, Layouts = layouts, Features = features, Quarters = quarters, Houses = houses, Businesses = businesses };
		var calendar = new CalendarSettings { Days = days, Weeks = weeks, Seasons = seasons, DayParts = dayParts, SpecialDays = specialDays, Skies = skies, Sunrise = calendarSection.Sunrise, Sunset = calendarSection.Sunset, StartYear = calendarSection.StartYear, YearFormat = calendarSection.YearFormat };
		schedules.Needs = needs;
		schedules.TravelTimeFactors = travelTimeFactors;
		schedules.UrgencyFactors = urgencyFactors;
		schedules.StepLists = stepLists;
		schedules.ProfessionSteps = professions.Professions.Where(x => x?.Business?.Steps is not null && !string.IsNullOrWhiteSpace(x.Id)).GroupBy(x => x.Id, StringComparer.Ordinal)
			.ToDictionary(x => x.Key, x => x.First().Business!.Steps!, StringComparer.Ordinal);
		schedules.ProfessionActivities = professions.Professions.Where(x => x?.Business?.Activity is not null && !string.IsNullOrWhiteSpace(x.Id)).GroupBy(x => x.Id, StringComparer.Ordinal)
			.ToDictionary(x => x.Key, x => x.First().Business!.Activity!, StringComparer.Ordinal);

		// Conditions can name groups of values, which stand for every value in the group.
		ResolveGroups(characters, professions, relationships, schedules, portraits, clothing);

		var messages = new List<string>();
		messages.AddRange(characters.Validate());
		messages.AddRange(characters.ValidateRoles());
		messages.AddRange(professions.Validate());
		messages.AddRange(locations.Validate());
		messages.AddRange(families.Validate());
		messages.AddRange(relationships.Validate());
		messages.AddRange(calendar.Validate());
		messages.AddRange(schedules.Validate());
		messages.AddRange(clothing.Validate());
		if (theme is not null)
			messages.AddRange(theme.Validate());
		messages.AddRange(GameIcon.Validate(icons));
		messages.AddRange(ValidateIconReferences(icons, schedules));
		if (portraits is not null)
			messages.AddRange(portraits.Validate());

		// Each part is fine on its own, so check that they agree with each other.
		if (messages.Count == 0)
			messages.AddRange(GameDataValidation.Validate(characters, locations, professions, families, relationships, calendar, schedules, portraits, clothing));
		if (messages.Count != 0)
			return Fail(messages.Distinct(StringComparer.Ordinal).Select(document.CreateError).ToList());

		var gameData = new GameData(characters, locations, professions, families, relationships, calendar, game.FrequencyChances, portraits, schedules, clothing, theme, icons);
		foreach (var warning in gameData.GetLowChanceWarnings().Concat(schedules.GetWarnings()))
			Log.Warn(warning);

		return DataLoadResult<GameData>.Success(gameData);
	}

	/// <summary>
	/// Checks that every icon the data refers to is in the icons section.
	/// </summary>
	private static IEnumerable<string> ValidateIconReferences(IReadOnlyList<GameIcon> icons, ScheduleSettings schedules)
	{
		var ids = icons.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		foreach (var need in schedules.Needs.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)))
		{
			if (!ids.Contains(need.Icon ?? ""))
				yield return OurResources.DataIdUnknown.FormatCurrentCulture($"needs[{need.Id}].icon", need.Icon, GameIcon.SectionName);
		}
	}

	private static DataLoadResult<GameData> Fail(IReadOnlyList<DataLoadError> errors)
	{
		foreach (var error in errors)
			Log.Error(error.ToString());
		return DataLoadResult<GameData>.Failure(errors);
	}

	/// <summary>
	/// Replaces the group ids in every condition with the ids of the values in each group.
	/// </summary>
	private static void ResolveGroups(CharacterData characters, ProfessionList professions, RelationshipSettings relationships, ScheduleSettings schedules, PortraitData? portraits, ClothingData clothing)
	{
		var properties = new Dictionary<string, CharacterProperty>(StringComparer.Ordinal);
		foreach (var property in characters.Properties.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)))
			properties.TryAdd(property.Id, property);

		IReadOnlyList<string>? GetGroupValues(string propertyId, string group) => properties.GetValueOrDefault(propertyId)?.GetGroupValueIds(group);

		var conditions = characters.GetConditions()
			.Concat(professions.Professions.Where(x => x is not null).SelectMany(x => new[] { x.When, x.HelperWhen }.Concat((x.FrequencyWhen ?? []).Select(y => y?.When))))
			.Concat(relationships.PlayerRelations.Select(x => x?.When))
			.Concat(schedules.GetConditions().Select(x => x.Condition))
			.Concat(portraits?.GetConditions().Select(x => x.Condition) ?? [])
			.Concat(clothing.GetConditions().Select(x => x.Condition));
		foreach (var condition in conditions.OfType<CharacterCondition>().Distinct())
			condition.ResolveGroups(GetGroupValues);
	}

	/// <summary>
	/// Checks portrait data, which has already been checked on its own, against the rest of the game data: the
	/// character properties its conditions use, the kinds of quarter, skies, and professions its layers are drawn in, and the
	/// clothing its layers draw and the regions of the body whose coverage hides them.
	/// </summary>
	public IReadOnlyList<string> ValidatePortraits(PortraitData portraits)
	{
		var properties = CharacterProperties.ToDictionary(x => x.Id, StringComparer.Ordinal);
		var quarterKinds = QuarterKinds.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		var skies = Calendar.Skies.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		return GameDataValidation.ValidatePortraits(portraits, properties, quarterKinds, skies, CharacterProperties.Find(Families.Roles.Properties.Profession), new ClothingData { Slots = ClothingSlots, Items = Clothing }).ToList();
	}

	/// <summary>
	/// How the game looks, or null if the data has no theme, in which case the built-in theme is used.
	/// </summary>
	public ThemeSettings? Theme { get; }

	/// <summary>
	/// The icons that stand for things in the data, such as needs.
	/// </summary>
	public DataCatalog<GameIcon> Icons { get; }

	/// <summary>
	/// Gets the lines of the icon with an id, or nothing if there's no icon with that id (loaded data always has it).
	/// </summary>
	public System.Windows.Media.Geometry GetIcon(string id) => Icons.Find(id)?.Geometry ?? System.Windows.Media.Geometry.Empty;

	/// <summary>
	/// The chances that items chosen at random are uncommon or rare.
	/// </summary>
	public FrequencyChances FrequencyChances { get; }

	/// <summary>
	/// The game's calendar, which creates, reads, and formats times.
	/// </summary>
	public GameCalendar Calendar { get; }

	/// <summary>
	/// How people decide what to do, and the needs that drive them.
	/// </summary>
	public ScheduleSettings Schedules { get; }

	/// <summary>
	/// The properties that describe a character, in the order they were defined.
	/// </summary>
	public DataCatalog<CharacterProperty> CharacterProperties { get; }

	/// <summary>
	/// Draws character portraits, or null if there's no portrait data.
	/// </summary>
	public PortraitRenderer? PortraitRenderer { get; }

	/// <summary>
	/// The templates that create text about a character, such as their full name and description.
	/// </summary>
	public TextTemplateSet CharacterTemplates { get; }

	/// <summary>
	/// The places on the body clothing is worn, in the order worn clothing is described.
	/// </summary>
	public DataCatalog<ClothingSlot> ClothingSlots { get; }

	/// <summary>
	/// How close to the body clothing is worn, from the innermost out.
	/// </summary>
	public DataCatalog<ClothingLayer> ClothingLayers { get; }

	/// <summary>
	/// How well covered people must be. Each person follows the first rule whose condition they meet.
	/// </summary>
	public IReadOnlyList<ModestyRule> ModestyRules { get; }

	/// <summary>
	/// How comfortable people are with how covered they are, which decides what their modesty rule calls for.
	/// </summary>
	public ModestySettings ModestySettings { get; }

	/// <summary>
	/// The colors clothing can be.
	/// </summary>
	public DataCatalog<ClothingColor> ClothingColors { get; }

	/// <summary>
	/// How loose or heavy clothing can be.
	/// </summary>
	public DataCatalog<ClothingBulk> ClothingBulks { get; }

	/// <summary>
	/// Settings for clothing as a whole.
	/// </summary>
	public ClothingSettings ClothingSettings { get; }

	/// <summary>
	/// The articles of clothing.
	/// </summary>
	public DataCatalog<ClothingItem> Clothing { get; }

	/// <summary>
	/// The outfits people are given when they're created.
	/// </summary>
	public DataCatalog<Outfit> Outfits { get; }

	/// <summary>
	/// Gets what wearing articles of clothing together keeps from being seen, from how much they cover the regions of the
	/// body each character property shows on.
	/// </summary>
	public CharacterObscuring GetObscuring(IEnumerable<ClothingItem> clothing) => new([CharacterObscuring.GetStrengths(CharacterProperties, clothing, ClothingBulks.Find)]);

	/// <summary>
	/// Settings for the village as a whole.
	/// </summary>
	public VillageSettings Village { get; }

	/// <summary>
	/// The kinds of quarter, by id.
	/// </summary>
	public DataCatalog<QuarterKind> QuarterKinds { get; }

	/// <summary>
	/// How many landmark quarters a town has, including unique ones.
	/// </summary>
	public IntegerRange LandmarkCount => Village.LandmarkCount;

	/// <summary>
	/// How many minutes it takes to go into or out of a house or business from its quarter.
	/// </summary>
	public int BuildingTravelTime => Village.BuildingTravelTime;

	/// <summary>
	/// The shapes a town can be laid out in, chosen by frequency.
	/// </summary>
	public IReadOnlyList<TownLayoutChoice> Layouts { get; }

	/// <summary>
	/// The map features a town can have, by id.
	/// </summary>
	public DataCatalog<MapFeature> MapFeatures { get; }

	/// <summary>
	/// How many map features a town has.
	/// </summary>
	public IntegerRange FeatureCount { get; }

	/// <summary>
	/// Gets the id of the map feature a quarter borders because of its kind, or because of the quarter itself, if any.
	/// </summary>
	public IEnumerable<string> GetBorderedFeatures(QuarterTemplate quarter) =>
		new[] { QuarterKinds.Find(quarter.Kind)?.Borders, quarter.Borders }.OfType<string>().Distinct(StringComparer.Ordinal);

	/// <summary>
	/// The quarters the town can be built from.
	/// </summary>
	public DataCatalog<QuarterTemplate> QuarterTemplates { get; }

	/// <summary>
	/// The houses households can live in.
	/// </summary>
	public DataCatalog<HouseTemplate> HouseTemplates { get; }

	/// <summary>
	/// The title of every house, filled in with the character properties of the household's head.
	/// </summary>
	public TextTemplate HouseTitle { get; }

	/// <summary>
	/// Gets the range of an integer property for a role. The player's age comes from the head ages in Families.json.
	/// </summary>
	public IntegerRange GetRange(CharacterProperty property, CharacterRole role) =>
		role == CharacterRole.Player && property == m_ageProperty && m_playerAgeRange is { } playerAgeRange ? playerAgeRange : property.GetRange(role);

	/// <summary>
	/// The places businesses can be run from.
	/// </summary>
	public DataCatalog<BusinessTemplate> BusinessTemplates { get; }

	/// <summary>
	/// Gets the places a business in the given profession can be run from. Empty if the profession works from home.
	/// </summary>
	public IReadOnlyList<BusinessTemplate> GetBusinessTemplates(string professionId) =>
		m_businessTemplatesByProfession.GetValueOrDefault(professionId) ?? [];

	/// <summary>
	/// The professions that are businesses, by id. Homemaker and infirm aren't included.
	/// </summary>
	public DataCatalog<Profession> Professions { get; }

	/// <summary>
	/// How households are generated.
	/// </summary>
	public FamilySettings Families { get; }

	/// <summary>
	/// How relationships between characters are chosen, and what their scores mean.
	/// </summary>
	public RelationshipSettings Relationships { get; }

	/// <summary>
	/// The percentage chance at or below which a character property value is reported as a warning, or null if
	/// there's no threshold.
	/// </summary>
	public double? CharacterLowChanceWarning { get; }

	/// <summary>
	/// Gets a warning for each value that's at or below the low chance warning threshold of its data file.
	/// </summary>
	public IReadOnlyList<string> GetLowChanceWarnings() =>
		CharacterLowChanceWarning is { } threshold ? CharacterChances.GetLowChanceWarnings(CharacterProperties, FrequencyChances, threshold, GetRange) : [];

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(GameData));

	private readonly Dictionary<string, IReadOnlyList<BusinessTemplate>> m_businessTemplatesByProfession;
	private readonly CharacterProperty? m_ageProperty;
	private readonly IntegerRange? m_playerAgeRange;
}
