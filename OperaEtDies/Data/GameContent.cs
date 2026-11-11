namespace OperaEtDies.Data;

/// <summary>
/// Everything loaded from the data folder: the game info and the game data.
/// </summary>
public sealed class GameContent
{
	/// <summary>
	/// Loads the game info and game data from the data folder, with the files from each mod folder applied in order.
	/// Every mod folder must be valid for the game data to load. The game info without mods can be passed in if it
	/// has already been loaded, and is used when there are no mods or the mod folders aren't valid.
	/// </summary>
	public static GameContent Load(string dataDirectory, IReadOnlyList<string> modDirectories, DataLoadResult<GameInfo>? baseGameInfo = null)
	{
		baseGameInfo ??= GameInfo.Load(dataDirectory);

		var modFolderErrors = ModFolders.Check(modDirectories);
		var gameInfo = modDirectories.Count == 0 || modFolderErrors.Count != 0 ? baseGameInfo : GameInfo.Load(dataDirectory, modDirectories);
		var gameData = modFolderErrors.Count != 0
			? DataLoadResult<GameData>.Failure(modFolderErrors)
			: GameData.Load(dataDirectory, modDirectories, gameInfo.Value?.FrequencyChances);

		return new GameContent(gameInfo, gameData);
	}

	private GameContent(DataLoadResult<GameInfo> gameInfo, DataLoadResult<GameData> gameData)
	{
		GameInfoResult = gameInfo;
		GameDataResult = gameData;
	}

	public DataLoadResult<GameInfo> GameInfoResult { get; }

	public DataLoadResult<GameData> GameDataResult { get; }

	/// <summary>
	/// The errors from loading both the game info and the game data.
	/// </summary>
	public IReadOnlyList<DataLoadError> Errors => [.. GameInfoResult.Errors, .. GameDataResult.Errors];
}
