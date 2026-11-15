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
	/// Returns an error for each mod folder that doesn't exist.
	/// </summary>
	public static IReadOnlyList<DataLoadError> Check(IReadOnlyList<string> modDirectories)
	{
		var errors = modDirectories
			.Where(x => !Directory.Exists(x))
			.Select(x => new DataLoadError(x, OurResources.ModFolderNotFound.FormatCurrentCulture(x)))
			.ToList();
		foreach (var error in errors)
			Log.Error(error.ToString());
		return errors;
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(ModFolders));
}
