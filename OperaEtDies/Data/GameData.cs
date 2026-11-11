using GoldenAnvil.Utility.Logging;

namespace OperaEtDies.Data;

/// <summary>
/// The game content loaded from the data files (everything other than the basic game info). This is read-only;
/// the state of a game in progress lives in the game state view models.
/// </summary>
public sealed class GameData
{
	public GameData(CharacterData characters, LocationList locations, ProfessionList professions, FamilySettings families, FrequencyChances? frequencyChances = null)
	{
		FrequencyChances = frequencyChances ?? new FrequencyChances();

		CharacterProperties = new DataCatalog<CharacterProperty>(characters.Properties);

		// The character file is validated on load, so the templates are always valid.
		CharacterTemplates = new TextTemplateSet(characters.Templates.Select(x => KeyValuePair.Create(x.Id, x.ParseVariants())));
		CharacterObscurers = new DataCatalog<CharacterObscurer>(characters.Obscurers);
		CharacterLowChanceWarning = characters.LowChanceWarning;

		QuarterKinds = new DataCatalog<QuarterKind>(locations.QuarterKinds);
		LandmarkCount = locations.LandmarkCount;
		Layouts = locations.Layouts;
		MapFeatures = new DataCatalog<MapFeature>(locations.Features);
		FeatureCount = locations.FeatureCount ?? new IntegerRange { Minimum = 0, Maximum = 0, Shape = new WeightShape { Peak = ShapePeak.Low, MostWithin = 1 } };
		QuarterTemplates = new DataCatalog<QuarterTemplate>(locations.Quarters);
		HouseTemplates = new DataCatalog<HouseTemplate>(locations.Houses);
		HouseTitle = TextTemplate.TryParse(locations.HouseTitle ?? "", out var houseTitle) ? houseTitle : TextTemplate.Parse("");
		BusinessTemplates = new DataCatalog<BusinessTemplate>(locations.Businesses);
		m_businessTemplatesByProfession = locations.Businesses
			.GroupBy(x => x.Profession, StringComparer.Ordinal)
			.ToDictionary(x => x.Key, x => (IReadOnlyList<BusinessTemplate>) x.ToList(), StringComparer.Ordinal);

		// Homemaker and infirm are professions, but not businesses.
		Professions = new DataCatalog<Profession>(professions.Professions.Where(x => x.Id != families.Homemaker && x.Id != families.Infirm));
		Families = families;

		// The player heads a household, so their age comes from the head ages, from the youngest age at which they can
		// have an adult child. Families.json is checked against the age property when the data is loaded.
		if (CharacterProperties.Find(families.Properties.Age) is { Type: CharacterPropertyType.Integer } age)
		{
			m_ageProperty = age;
			m_playerAgeRange = families.HeadAge.StartingAt(GetMinPlayerAge(age, families));
		}
	}

	/// <summary>
	/// Gets the youngest age at which the player can have an adult child: the youngest age a non-player character can be,
	/// plus the youngest age at which someone can have a child.
	/// </summary>
	internal static int GetMinPlayerAge(CharacterProperty age, FamilySettings families) => age.GetRange(CharacterRole.Npc).Minimum + families.MinParentAge;

	/// <summary>
	/// Loads all of the game content. Fails if any file fails, but reports the errors from every file. The frequency
	/// chances come from the game info, and default to the standard chances if not given. Values that are too unlikely
	/// to be chosen are logged as warnings.
	/// </summary>
	public static DataLoadResult<GameData> Load(string dataDirectory, FrequencyChances? frequencyChances = null) =>
		Load(dataDirectory, [], frequencyChances);

	/// <summary>
	/// Loads all of the game content, with the changes from the mod folders applied in order.
	/// </summary>
	public static DataLoadResult<GameData> Load(string dataDirectory, IReadOnlyList<string> modDirectories, FrequencyChances? frequencyChances = null)
	{
		// The profession character property takes its values from Professions.json, so it's loaded first.
		var professions = ProfessionList.Load(dataDirectory, modDirectories);
		var characters = CharacterData.Load(dataDirectory, modDirectories, professions.Value?.Professions);
		var locations = LocationList.Load(dataDirectory, modDirectories);
		var families = FamilySettings.Load(dataDirectory, modDirectories);

		if (characters.Value is null || locations.Value is null || professions.Value is null || families.Value is null)
			return DataLoadResult<GameData>.Failure([.. characters.Errors, .. locations.Errors, .. professions.Errors, .. families.Errors]);

		// Each file is fine on its own, so check that they agree with each other.
		var errors = GameDataValidation.Validate(dataDirectory, characters.Value, locations.Value, professions.Value, families.Value);
		if (errors.Count != 0)
		{
			foreach (var error in errors)
				Log.Error(error.ToString());
			return DataLoadResult<GameData>.Failure(errors);
		}

		var gameData = new GameData(characters.Value, locations.Value, professions.Value, families.Value, frequencyChances);
		foreach (var warning in gameData.GetLowChanceWarnings())
			Log.Warn(warning);

		return DataLoadResult<GameData>.Success(gameData);
	}

	/// <summary>
	/// The chances that items chosen at random are uncommon or rare.
	/// </summary>
	public FrequencyChances FrequencyChances { get; }

	/// <summary>
	/// The properties that describe a character, in the order they were defined.
	/// </summary>
	public DataCatalog<CharacterProperty> CharacterProperties { get; }

	/// <summary>
	/// The templates that create text about a character, such as their full name and description.
	/// </summary>
	public TextTemplateSet CharacterTemplates { get; }

	/// <summary>
	/// The things that can keep some of a character's properties from being seen or heard.
	/// </summary>
	public DataCatalog<CharacterObscurer> CharacterObscurers { get; }

	/// <summary>
	/// Gets the obscuring caused by the obscurers with the given ids, which must exist.
	/// </summary>
	public CharacterObscuring GetObscuring(IEnumerable<string> obscurerIds) => new(obscurerIds.Select(CharacterObscurers.Get));

	/// <summary>
	/// The kinds of quarter, by id.
	/// </summary>
	public DataCatalog<QuarterKind> QuarterKinds { get; }

	/// <summary>
	/// How many landmark quarters a town has, including unique ones.
	/// </summary>
	public IntegerRange LandmarkCount { get; }

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
