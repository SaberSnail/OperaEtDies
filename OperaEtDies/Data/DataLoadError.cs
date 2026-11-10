using System.IO;

namespace OperaEtDies.Data;

/// <summary>
/// A problem found while loading a data file. The source, if given, says where the problem came from besides the
/// file itself, such as the mod that changed it.
/// </summary>
public sealed record DataLoadError(string FilePath, string Message, string? Source = null)
{
	public override string ToString() => Source is null ? $"{Path.GetFileName(FilePath)}: {Message}" : $"{Path.GetFileName(FilePath)} ({Source}): {Message}";
}
