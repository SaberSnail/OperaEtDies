using System.IO;
using OperaEtDies.Data;

namespace ModEditor.Model;

/// <summary>
/// A problem found when the game data is loaded with the mod. The file is the mod file's path, or null if the
/// problem is in the game data.
/// </summary>
public sealed record ModProblem(string? FilePath, string Location, string Message);

/// <summary>
/// Checks a mod by loading the game data with it applied, exactly as the game does. The mod's files are checked as
/// they are in the editor, including changes that haven't been saved, by writing them to a temporary folder.
/// </summary>
public static class ModValidator
{
	/// <summary>
	/// The game data the mod is checked against: the data folder next to the editor, which is the one the game uses.
	/// </summary>
	public static string DataDirectory => JsonDataLoader.DefaultDataDirectory;

	/// <summary>
	/// Loads the game data with the mod's files applied, and returns the problems found. Can be run on any thread.
	/// </summary>
	public static IReadOnlyList<ModProblem> Validate(string modName, ModSnapshot snapshot)
	{
		// The temporary folder has the mod's name, since problems name the mod they're in.
		var parent = Path.Combine(Path.GetTempPath(), "OperaEtDiesModEditor", Guid.NewGuid().ToString("N"));
		var modDirectory = Path.Combine(parent, string.IsNullOrEmpty(modName) ? "Mod" : modName);
		try
		{
			Directory.CreateDirectory(modDirectory);
			foreach (var (path, text) in snapshot.Files)
			{
				var systemPath = ModFilePath.ToSystemPath(modDirectory, path);
				Directory.CreateDirectory(Path.GetDirectoryName(systemPath)!);
				File.WriteAllText(systemPath, text);
			}

			var content = GameContent.Load(DataDirectory, [modDirectory]);
			return content.Errors.Select(x => CreateProblem(x, modDirectory)).Distinct().ToList();
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return [new ModProblem(null, "", $"The mod couldn't be checked: {e.Message}")];
		}
		finally
		{
			try
			{
				Directory.Delete(parent, recursive: true);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// A temporary folder that's left behind does no harm.
			}
		}
	}

	private static ModProblem CreateProblem(DataLoadError error, string modDirectory)
	{
		var fullPath = Path.GetFullPath(error.FilePath);
		if (IsWithin(fullPath, modDirectory))
		{
			var path = ModFilePath.FromSystemPath(modDirectory, fullPath);
			return path == "." ? new ModProblem(null, "Mod", error.Message) : new ModProblem(path, path, error.Message);
		}

		var location = IsWithin(fullPath, DataDirectory) ? $"Game data: {ModFilePath.FromSystemPath(DataDirectory, fullPath)}" : error.FilePath;
		return new ModProblem(null, error.Source is null ? location : $"{location} ({error.Source})", error.Message);
	}

	private static bool IsWithin(string path, string directory)
	{
		var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
		return string.Equals(path, full, StringComparison.OrdinalIgnoreCase) || path.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
	}
}
