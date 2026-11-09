namespace OperaEtDies.Data;

/// <summary>
/// The game content loaded from the data files (everything other than the basic game info). This is read-only;
/// the state of a game in progress lives in the game state view models.
/// </summary>
public sealed class GameData
{
	public GameData(CharacterData characters, LocationList locations)
	{
		CharacterProperties = new DataCatalog<CharacterProperty>(characters.Properties);

		// The character file is validated on load, so the templates are always valid.
		CharacterTemplates = new TextTemplateSet(characters.Templates.Select(x => KeyValuePair.Create(x.Id, TextTemplate.Parse(x.Template))));

		Locations = new DataCatalog<Location>(locations.Locations);

		// The locations file is validated on load, so the starting location always exists.
		StartingLocation = Locations.Get(locations.StartingLocation);
	}

	/// <summary>
	/// Loads all of the game content. Fails if any file fails, but reports the errors from every file.
	/// </summary>
	public static DataLoadResult<GameData> Load(string dataDirectory)
	{
		var characters = CharacterData.Load(dataDirectory);
		var locations = LocationList.Load(dataDirectory);

		if (characters.Value is null || locations.Value is null)
			return DataLoadResult<GameData>.Failure([.. characters.Errors, .. locations.Errors]);

		return DataLoadResult<GameData>.Success(new GameData(characters.Value, locations.Value));
	}

	/// <summary>
	/// The properties that describe a character, in the order they were defined.
	/// </summary>
	public DataCatalog<CharacterProperty> CharacterProperties { get; }

	/// <summary>
	/// The templates that create text about a character, such as their full name and description.
	/// </summary>
	public TextTemplateSet CharacterTemplates { get; }

	public DataCatalog<Location> Locations { get; }

	/// <summary>
	/// The location the player starts the game in.
	/// </summary>
	public Location StartingLocation { get; }
}
