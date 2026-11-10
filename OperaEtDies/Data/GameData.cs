using GoldenAnvil.Utility.Logging;

namespace OperaEtDies.Data;

/// <summary>
/// The game content loaded from the data files (everything other than the basic game info). This is read-only;
/// the state of a game in progress lives in the game state view models.
/// </summary>
public sealed class GameData
{
	public GameData(CharacterData characters, LocationList locations, FrequencyChances? frequencyChances = null)
	{
		FrequencyChances = frequencyChances ?? new FrequencyChances();

		CharacterProperties = new DataCatalog<CharacterProperty>(characters.Properties);

		// The character file is validated on load, so the templates are always valid.
		CharacterTemplates = new TextTemplateSet(characters.Templates.Select(x => KeyValuePair.Create(x.Id, x.ParseVariants())));
		CharacterObscurers = new DataCatalog<CharacterObscurer>(characters.Obscurers);
		CharacterLowChanceWarning = characters.LowChanceWarning;

		Locations = new DataCatalog<Location>(locations.Locations);

		// The locations file is validated on load, so the starting location always exists.
		StartingLocation = Locations.Get(locations.StartingLocation);
	}

	/// <summary>
	/// Loads all of the game content. Fails if any file fails, but reports the errors from every file. The frequency
	/// chances come from the game info, and default to the standard chances if not given. Values that are too unlikely
	/// to be chosen are logged as warnings.
	/// </summary>
	public static DataLoadResult<GameData> Load(string dataDirectory, FrequencyChances? frequencyChances = null)
	{
		var characters = CharacterData.Load(dataDirectory);
		var locations = LocationList.Load(dataDirectory);

		if (characters.Value is null || locations.Value is null)
			return DataLoadResult<GameData>.Failure([.. characters.Errors, .. locations.Errors]);

		var gameData = new GameData(characters.Value, locations.Value, frequencyChances);
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

	public DataCatalog<Location> Locations { get; }

	/// <summary>
	/// The location the player starts the game in.
	/// </summary>
	public Location StartingLocation { get; }

	/// <summary>
	/// The percentage chance at or below which a character property value is reported as a warning, or null if
	/// there's no threshold.
	/// </summary>
	public double? CharacterLowChanceWarning { get; }

	/// <summary>
	/// Gets a warning for each value that's at or below the low chance warning threshold of its data file.
	/// </summary>
	public IReadOnlyList<string> GetLowChanceWarnings() =>
		CharacterLowChanceWarning is { } threshold ? CharacterChances.GetLowChanceWarnings(CharacterProperties, FrequencyChances, threshold) : [];

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(GameData));
}
