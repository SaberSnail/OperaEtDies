namespace OperaEtDies.GameState;

/// <summary>
/// What games are saved with: the game's title, the hashes of the data and mods that are loaded (the mods hash is
/// empty without mods), the mod folders, and the folder saves are kept in.
/// </summary>
public sealed record SaveSettings(string GameTitle, string DataHash, string ModsHash, IReadOnlyList<string> Mods, string Directory)
{
	/// <summary>
	/// Creates the settings for saves kept in the usual folder for the game.
	/// </summary>
	public static SaveSettings Create(string gameTitle, string dataHash, string modsHash, IReadOnlyList<string> mods) =>
		new(gameTitle, dataHash, modsHash, mods, SaveFile.GetSaveDirectory(gameTitle));
}
