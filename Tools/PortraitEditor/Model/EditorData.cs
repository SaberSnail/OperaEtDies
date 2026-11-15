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

		// The portrait sections are taken out of the merged data, so they can be edited on their own.
		var document = DataDocument.Read(dataDirectory, modDirectories);
		errors.AddRange(document.Errors);
		var gameData = document.Value is { } value ? GameData.Load(value, includePortraits: false) : null;
		errors.AddRange(gameData?.Errors ?? []);

		JsonObject? portraitsObject = null;
		var portraitsPath = dataDirectory;
		if (document.Value is { } merged)
		{
			portraitsObject = new JsonObject();
			foreach (var name in PortraitDataReader.SectionNames.Where(x => merged.GetSection(x) is not null))
				portraitsObject[name] = merged.GetSection(name)!.DeepClone();
			portraitsPath = merged.FindSectionFile("portraitLayers") ?? dataDirectory;
		}

		return new EditorData(dataDirectory, modDirectories, gameData?.Value, portraitsPath, portraitsObject, errors);
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
