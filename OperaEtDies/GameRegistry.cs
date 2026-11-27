using System.IO;

namespace OperaEtDies;

/// <summary>
/// Where the game keeps its settings in the current user's registry, such as the mod list and where the window was.
/// </summary>
public static class GameRegistry
{
	/// <summary>
	/// The path of the registry key under HKEY_CURRENT_USER for the game with the given title.
	/// </summary>
	public static string GetKeyPath(string gameTitle) =>
		// Backslashes would split the title into several keys.
		$@"Software\Golden Anvil\{gameTitle.Trim().Replace(Path.DirectorySeparatorChar, '_')}";
}
