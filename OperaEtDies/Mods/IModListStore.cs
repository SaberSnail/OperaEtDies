namespace OperaEtDies.Mods;

/// <summary>
/// Keeps the list of folders that mod files are loaded from, in the order they're applied.
/// </summary>
public interface IModListStore
{
	IReadOnlyList<string> Load();

	void Save(IReadOnlyList<string> modDirectories);
}
