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
	/// The command line argument that shows extra UI for developing the game.
	/// </summary>
	public const string DeveloperArgument = "--developer";

	public static CommandLineOptions Parse(IReadOnlyList<string> args) => new()
	{
		ShowHelp = HasArgument(args, HelpArgument),
		AreModsEnabled = !HasArgument(args, NoModsArgument),
		IsDeveloper = HasArgument(args, DeveloperArgument),
		IsVillageReport = VillageReport.IsRequested(args),
	};

	/// <summary>
	/// Whether to show the list of options instead of starting the game.
	/// </summary>
	public bool ShowHelp { get; init; }

	public bool AreModsEnabled { get; init; } = true;

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
			(DeveloperArgument, OurResources.CommandLineDeveloper),
			($"{VillageReport.Argument} <count>", OurResources.CommandLineGenerateVillages),
			($"{VillageReport.SeedArgument} <number>", OurResources.CommandLineSeed),
		];

		var width = options.Max(x => x.Option.Length) + 2;
		var text = new StringBuilder(OurResources.CommandLineOptionsHeader).AppendLine();
		foreach (var (option, explanation) in options)
			text.Append("  ").Append(option.PadRight(width)).AppendLine(explanation);
		return text.ToString();
	}

	private static bool HasArgument(IReadOnlyList<string> args, string argument) => args.Contains(argument, StringComparer.OrdinalIgnoreCase);
}
