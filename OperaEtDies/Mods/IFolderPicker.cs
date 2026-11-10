namespace OperaEtDies.Mods;

/// <summary>
/// Asks the player to choose a folder.
/// </summary>
public interface IFolderPicker
{
	/// <summary>
	/// Returns the chosen folder's path, or null if the player cancelled.
	/// </summary>
	string? PickFolder(string title, string? initialDirectory);
}
