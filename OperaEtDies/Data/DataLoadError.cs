using System.IO;

namespace OperaEtDies.Data;

/// <summary>
/// A problem found while loading a data file.
/// </summary>
public sealed record DataLoadError(string FilePath, string Message)
{
	public override string ToString() => $"{Path.GetFileName(FilePath)}: {Message}";
}
