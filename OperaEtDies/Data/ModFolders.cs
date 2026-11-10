using System.IO;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;

namespace OperaEtDies.Data;

/// <summary>
/// Checks the folders that mod files are loaded from.
/// </summary>
public static class ModFolders
{
	/// <summary>
	/// The names of the data files that mods can change.
	/// </summary>
	public static IReadOnlyList<string> DataFileNames { get; } = [GameInfo.FileName, CharacterData.FileName, LocationList.FileName];

	/// <summary>
	/// Returns an error for each mod folder that doesn't exist. JSON files in the folders that aren't data files are
	/// logged as warnings, since they're probably misnamed.
	/// </summary>
	public static IReadOnlyList<DataLoadError> Check(IReadOnlyList<string> modDirectories)
	{
		var errors = new List<DataLoadError>();
		foreach (var directory in modDirectories)
		{
			if (Directory.Exists(directory))
			{
				foreach (var fileName in GetJsonFileNames(directory).Where(x => !DataFileNames.Contains(x, StringComparer.OrdinalIgnoreCase)))
					Log.Warn($"The mod folder \"{directory}\" has a file \"{fileName}\" that isn't a data file, so it's ignored.");
			}
			else
			{
				errors.Add(new DataLoadError(directory, OurResources.ModFolderNotFound.FormatCurrentCulture(directory)));
			}
		}

		foreach (var error in errors)
			Log.Error(error.ToString());
		return errors;
	}

	private static IReadOnlyList<string> GetJsonFileNames(string directory)
	{
		try
		{
			return Directory.GetFiles(directory, "*.json").Select(Path.GetFileName).OfType<string>().ToList();
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return [];
		}
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(ModFolders));
}
