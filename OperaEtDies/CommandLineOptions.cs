using System.IO;
using System.Text;
using OperaEtDies.Reports;

namespace OperaEtDies;

/// <summary>
/// The options the game was started with on the command line. Options are matched ignoring case, and unknown
/// arguments are ignored.
/// </summary>
public sealed class CommandLineOptions
{
	public const string HelpArgument = "--help";

	/// <summary>
	/// The command line argument that stops mods from being loaded.
	/// </summary>
	public const string NoModsArgument = "--no-mods";

	/// <summary>
	/// The command line argument, followed by a folder, that loads that mod instead of the game's mod list. It can be
	/// given more than once, and the mods are applied in the order they're given.
	/// </summary>
	public const string ModArgument = "--mod";

	/// <summary>
	/// The command line argument that shows extra UI for developing the game.
	/// </summary>
	public const string DeveloperArgument = "--developer";

	public static CommandLineOptions Parse(IReadOnlyList<string> args) => new()
	{
		ShowHelp = HasArgument(args, HelpArgument),
		AreModsEnabled = !HasArgument(args, NoModsArgument),
		ModDirectories = GetModDirectories(args),
		IsDeveloper = HasArgument(args, DeveloperArgument),
		IsVillageReport = VillageReport.IsRequested(args),
	};

	/// <summary>
	/// Whether to show the list of options instead of starting the game.
	/// </summary>
	public bool ShowHelp { get; init; }

	public bool AreModsEnabled { get; init; } = true;

	/// <summary>
	/// The mod folders given on the command line, in order, which are loaded instead of the game's mod list, or null to
	/// load the mod list. They aren't loaded if mods are turned off.
	/// </summary>
	public IReadOnlyList<string>? ModDirectories { get; init; }

	/// <summary>
	/// Whether to show extra UI for developing the game.
	/// </summary>
	public bool IsDeveloper { get; init; }

	/// <summary>
	/// Whether to run the village report instead of starting the game.
	/// </summary>
	public bool IsVillageReport { get; init; }

	/// <summary>
	/// Gets the list of options, each with a short explanation.
	/// </summary>
	public static string GetHelpText()
	{
		(string Option, string Explanation)[] options =
		[
			(HelpArgument, OurResources.CommandLineHelp),
			(NoModsArgument, OurResources.CommandLineNoMods),
			($"{ModArgument} <folder>", OurResources.CommandLineMod),
			(DeveloperArgument, OurResources.CommandLineDeveloper),
			($"{VillageReport.Argument} <count>", OurResources.CommandLineGenerateVillages),
			($"{VillageReport.SeedArgument} <number>", OurResources.CommandLineSeed),
			($"{VillageReport.PortraitsArgument} <folder>", OurResources.CommandLinePortraits),
			(VillageReport.PortraitsAtWorkArgument, OurResources.CommandLinePortraitsAtWork),
			(VillageReport.ThroughSaveArgument, OurResources.CommandLineThroughSave),
			($"{VillageReport.SimulateDaysArgument} <days>", OurResources.CommandLineSimulateDays),
		];

		var width = options.Max(x => x.Option.Length) + 2;
		var text = new StringBuilder(OurResources.CommandLineOptionsHeader).AppendLine();
		foreach (var (option, explanation) in options)
			text.Append("  ").Append(option.PadRight(width)).AppendLine(explanation);
		return text.ToString();
	}

	private static bool HasArgument(IReadOnlyList<string> args, string argument) => args.Contains(argument, StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets the folder after each mod argument, or null if there are none. A mod argument at the end, with no folder
	/// after it, is ignored.
	/// </summary>
	private static IReadOnlyList<string>? GetModDirectories(IReadOnlyList<string> args)
	{
		var directories = new List<string>();
		for (var index = 0; index < args.Count - 1; index++)
		{
			if (string.Equals(args[index], ModArgument, StringComparison.OrdinalIgnoreCase))
			{
				directories.Add(GetFullPath(args[index + 1]));
				index++;
			}
		}

		return directories.Count == 0 ? null : directories;
	}

	// A folder that isn't a valid path is kept as it is, so loading reports it as missing.
	private static string GetFullPath(string path)
	{
		try
		{
			return Path.GetFullPath(path);
		}
		catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
		{
			return path;
		}
	}
}
