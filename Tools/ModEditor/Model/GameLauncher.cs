using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using OperaEtDies;

namespace ModEditor.Model;

/// <summary>
/// Starts the game from the editor's folder, with only the mod being edited loaded.
/// </summary>
public static class GameLauncher
{
	/// <summary>
	/// The game's executable, which is built into the same folder as the editor.
	/// </summary>
	public static string GamePath => Path.Combine(AppContext.BaseDirectory, "OperaEtDies.exe");

	/// <summary>
	/// Starts the game with the mod in place of the game's mod list, in developer mode if asked. Returns the reason it
	/// couldn't start, or null if it did.
	/// </summary>
	public static string? Launch(string modDirectory, bool isDeveloper)
	{
		if (!File.Exists(GamePath))
			return $"The game wasn't found at \"{GamePath}\". The editor runs the game from its own folder.";

		var startInfo = new ProcessStartInfo(GamePath)
		{
			WorkingDirectory = AppContext.BaseDirectory,
			UseShellExecute = false,
		};
		startInfo.ArgumentList.Add(CommandLineOptions.ModArgument);
		startInfo.ArgumentList.Add(modDirectory);
		if (isDeveloper)
			startInfo.ArgumentList.Add(CommandLineOptions.DeveloperArgument);

		try
		{
			using var process = Process.Start(startInfo);
			return null;
		}
		catch (Exception e) when (e is Win32Exception or InvalidOperationException)
		{
			return $"The game couldn't be started: {e.Message}";
		}
	}
}
