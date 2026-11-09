namespace OperaEtDies.Data;

/// <summary>
/// The game content loaded from the data files (everything other than the basic game info). This is read-only;
/// the state of a game in progress lives in the game state view models.
/// </summary>
public sealed class GameData
{
	public GameData(ProfessionList professions, FirstNameList firstNames, CharacterOptions characterOptions, LocationList locations)
	{
		Professions = new DataCatalog<Profession>(professions.Professions);
		FirstNames = firstNames;
		CharacterOptions = characterOptions;
		Heights = new DataCatalog<CharacterOption>(characterOptions.Heights);
		Weights = new DataCatalog<CharacterOption>(characterOptions.Weights);
		Locations = new DataCatalog<Location>(locations.Locations);

		// The locations file is validated on load, so the starting location always exists.
		StartingLocation = Locations.Get(locations.StartingLocation);
	}

	/// <summary>
	/// Loads all of the game content. Fails if any file fails, but reports the errors from every file.
	/// </summary>
	public static DataLoadResult<GameData> Load(string dataDirectory)
	{
		var professions = ProfessionList.Load(dataDirectory);
		var firstNames = FirstNameList.Load(dataDirectory);
		var characterOptions = CharacterOptions.Load(dataDirectory);
		var locations = LocationList.Load(dataDirectory);

		if (professions.Value is null || firstNames.Value is null || characterOptions.Value is null || locations.Value is null)
			return DataLoadResult<GameData>.Failure([.. professions.Errors, .. firstNames.Errors, .. characterOptions.Errors, .. locations.Errors]);

		return DataLoadResult<GameData>.Success(new GameData(professions.Value, firstNames.Value, characterOptions.Value, locations.Value));
	}

	public DataCatalog<Profession> Professions { get; }

	public FirstNameList FirstNames { get; }

	/// <summary>
	/// The age ranges for characters. The heights and weights are also available as catalogs on <see cref="Heights"/> and <see cref="Weights"/>.
	/// </summary>
	public CharacterOptions CharacterOptions { get; }

	public DataCatalog<CharacterOption> Heights { get; }

	public DataCatalog<CharacterOption> Weights { get; }

	public DataCatalog<Location> Locations { get; }

	/// <summary>
	/// The location the player starts the game in.
	/// </summary>
	public Location StartingLocation { get; }
}
