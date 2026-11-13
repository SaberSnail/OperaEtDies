using System.IO;
using System.Reflection;
using OperaEtDies.Data;

namespace PortraitEditor;

/// <summary>
/// The options the editor was started with on the command line. Options are matched ignoring case, and unknown
/// arguments are ignored.
/// </summary>
public sealed class EditorOptions
{
	/// <summary>
	/// The command line argument that stops mods from being loaded.
	/// </summary>
	public const string NoModsArgument = "--no-mods";

	/// <summary>
	/// The command line argument, followed by a folder, that loads the data files from that folder.
	/// </summary>
	public const string DataArgument = "--data";

	/// <summary>
	/// The command line argument, followed by a file, that saves the editor's icon to the file instead of starting the
	/// editor.
	/// </summary>
	public const string SaveIconArgument = "--save-icon";

	/// <summary>
	/// The command line argument, followed by a folder, that also saves each size of the icon there as a PNG file.
	/// </summary>
	public const string IconPreviewsArgument = "--icon-previews";

	public static EditorOptions Parse(IReadOnlyList<string> args)
	{
		string? GetPath(string argument)
		{
			var index = args.ToList().FindIndex(x => string.Equals(x, argument, StringComparison.OrdinalIgnoreCase));
			return index >= 0 && index + 1 < args.Count ? Path.GetFullPath(args[index + 1]) : null;
		}

		return new EditorOptions
		{
			AreModsEnabled = !args.Contains(NoModsArgument, StringComparer.OrdinalIgnoreCase),
			DataDirectory = GetPath(DataArgument) ?? GetDefaultDataDirectory(),
			SaveIconPath = GetPath(SaveIconArgument),
			IconPreviewDirectory = GetPath(IconPreviewsArgument),
		};
	}

	public bool AreModsEnabled { get; init; } = true;

	public string DataDirectory { get; init; } = "";

	/// <summary>
	/// Where to save the editor's icon, or null to start the editor.
	/// </summary>
	public string? SaveIconPath { get; init; }

	/// <summary>
	/// Where to save each size of the icon as a PNG file, or null not to.
	/// </summary>
	public string? IconPreviewDirectory { get; init; }

	/// <summary>
	/// Sub Corona's data folder, as found when the editor was built, or else the data folder next to the editor.
	/// </summary>
	private static string GetDefaultDataDirectory()
	{
		var subCoronaDirectory = typeof(EditorOptions).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
			.FirstOrDefault(x => x.Key == "SubCoronaDataDirectory")?.Value;
		return subCoronaDirectory is not null && Directory.Exists(subCoronaDirectory) ? subCoronaDirectory : JsonDataLoader.DefaultDataDirectory;
	}
}
