namespace OperaEtDies.Data;

/// <summary>
/// The game content loaded from the data files (everything other than the basic game info).
/// </summary>
public sealed class GameData
{
	public GameData(ProfessionList professions, FirstNameList firstNames, CharacterOptions characterOptions, LocationList locations)
	{
		Professions = professions;
		FirstNames = firstNames;
		CharacterOptions = characterOptions;
		Locations = locations;
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

	public ProfessionList Professions { get; }

	public FirstNameList FirstNames { get; }

	public CharacterOptions CharacterOptions { get; }

	public LocationList Locations { get; }
}
