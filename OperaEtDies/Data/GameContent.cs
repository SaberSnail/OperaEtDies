namespace OperaEtDies.Data;

/// <summary>
/// Everything loaded from the data folder: the game info and the game data.
/// </summary>
public sealed class GameContent
{
	/// <summary>
	/// Loads the game info and game data from the data folder, with the files from each mod folder applied in order.
	/// Every mod folder must be valid for the game data to load. The game info without mods can be passed in if it
	/// has already been loaded, and is used when there are no mods or the mods can't be applied.
	/// </summary>
	public static GameContent Load(string dataDirectory, IReadOnlyList<string> modDirectories, DataLoadResult<GameInfo>? baseGameInfo = null)
	{
		var modFolderErrors = ModFolders.Check(modDirectories);
		var document = modFolderErrors.Count != 0 ? DataLoadResult<DataDocument>.Failure(modFolderErrors) : DataDocument.Read(dataDirectory, modDirectories);
		DataLoadResult<GameInfo> gameInfo;
		DataLoadResult<GameData> gameData;
		if (document.Value is { } value)
		{
			gameInfo = GameInfo.Read(value);
			gameData = GameData.Load(value);
		}
		else
		{
			gameInfo = baseGameInfo ?? GameInfo.Load(dataDirectory);
			gameData = DataLoadResult<GameData>.Failure(document.Errors);
		}

		var modsHash = modDirectories.Count == 0 ? "" : DataFolderHash.Compute(modDirectories);
		return new GameContent(gameInfo, gameData, modDirectories, DataFolderHash.Compute([dataDirectory]), modsHash);
	}

	private GameContent(DataLoadResult<GameInfo> gameInfo, DataLoadResult<GameData> gameData, IReadOnlyList<string> modDirectories, string dataHash, string modsHash)
	{
		GameInfoResult = gameInfo;
		GameDataResult = gameData;
		ModDirectories = modDirectories;
		DataHash = dataHash;
		ModsHash = modsHash;
	}

	public DataLoadResult<GameInfo> GameInfoResult { get; }

	public DataLoadResult<GameData> GameDataResult { get; }

	/// <summary>
	/// The mod folders whose files were applied, in order.
	/// </summary>
	public IReadOnlyList<string> ModDirectories { get; }

	/// <summary>
	/// The hash of the data folder, saved with games to tell whether the data has changed.
	/// </summary>
	public string DataHash { get; }

	/// <summary>
	/// The hash of the mod folders, in order, saved with games to tell whether the mods have changed. Empty without mods.
	/// </summary>
	public string ModsHash { get; }

	/// <summary>
	/// The errors from loading the game info and the game data, without repeating the ones they share.
	/// </summary>
	public IReadOnlyList<DataLoadError> Errors => [.. GameInfoResult.Errors, .. GameDataResult.Errors.Except(GameInfoResult.Errors)];
}
