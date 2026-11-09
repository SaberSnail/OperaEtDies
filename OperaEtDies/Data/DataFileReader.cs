using System.IO;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// Reads the raw contents of data files, turning file system problems into errors.
/// </summary>
internal static class DataFileReader
{
	public static DataLoadResult<T> Read<T>(string filePath, Func<string, T> read)
		where T : class
	{
		try
		{
			return DataLoadResult<T>.Success(read(filePath));
		}
		catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
		{
			return DataLoadResult<T>.Failure(new DataLoadError(filePath, OurResources.DataFileNotFound));
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
		{
			return DataLoadResult<T>.Failure(new DataLoadError(filePath, OurResources.DataFileReadError.FormatCurrentCulture(e.Message)));
		}
	}
}
