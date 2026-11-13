using System.IO;
using System.Text.Json.Nodes;
using OperaEtDies.Data;
using OperaEtDies.Mods;

namespace PortraitEditor.Model;

/// <summary>
/// Everything the editor loads: the game data without portraits, and the portrait data as JSON, both with the mods
/// in the game's mod list applied, the same as the game does. The portrait data is kept as JSON so it can still be
/// edited when it has problems.
/// </summary>
public sealed class EditorData
{
	public static EditorData Load(string dataDirectory, bool areModsEnabled)
	{
		var errors = new List<DataLoadError>();

		// The mod list is kept under the game's title, which mods can't change.
		var baseGameInfo = GameInfo.Load(dataDirectory);
		errors.AddRange(baseGameInfo.Errors);
		var modDirectories = areModsEnabled && baseGameInfo.Value is { } info ? new RegistryModListStore(info.Title).Load() : [];
		var missingModErrors = ModFolders.Check(modDirectories);
		errors.AddRange(missingModErrors);
		modDirectories = modDirectories.Where(Directory.Exists).ToList();

		var gameInfo = modDirectories.Count == 0 ? baseGameInfo : GameInfo.Load(dataDirectory, modDirectories);
		if (modDirectories.Count != 0)
			errors.AddRange(gameInfo.Errors);

		var gameData = GameData.Load(dataDirectory, modDirectories, gameInfo.Value?.FrequencyChances, includePortraits: false);
		errors.AddRange(gameData.Errors);

		var portraitsPath = Path.Combine(dataDirectory, PortraitData.FileName);
		var portraits = JsonDataLoader.ReadJson(portraitsPath, modDirectories, []);
		errors.AddRange(portraits.Errors);
		var portraitsObject = portraits.Value as JsonObject;
		if (portraits.Value is not null && portraitsObject is null)
			errors.Add(new DataLoadError(portraitsPath, "The file must hold a JSON object."));

		return new EditorData(dataDirectory, modDirectories, gameData.Value, portraitsPath, portraitsObject, errors);
	}

	public string DataDirectory { get; }

	/// <summary>
	/// The mod folders that were applied, in order.
	/// </summary>
	public IReadOnlyList<string> ModDirectories { get; }

	/// <summary>
	/// The game data without portraits, or null if it failed to load.
	/// </summary>
	public GameData? GameData { get; }

	public string PortraitsFilePath { get; }

	/// <summary>
	/// The portrait data with the mods applied, or null if it couldn't be read.
	/// </summary>
	public JsonObject? Portraits { get; }

	public IReadOnlyList<DataLoadError> Errors { get; }

	/// <summary>
	/// Gets the folders whose files are loaded, so they can be watched for changes.
	/// </summary>
	public IEnumerable<string> GetWatchedDirectories() => ModDirectories.Prepend(DataDirectory);

	private EditorData(string dataDirectory, IReadOnlyList<string> modDirectories, GameData? gameData, string portraitsFilePath, JsonObject? portraits, IReadOnlyList<DataLoadError> errors)
	{
		DataDirectory = dataDirectory;
		ModDirectories = modDirectories;
		GameData = gameData;
		PortraitsFilePath = portraitsFilePath;
		Portraits = portraits;
		Errors = errors;
	}
}
