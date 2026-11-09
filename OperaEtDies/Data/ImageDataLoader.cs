using System.IO;
using System.Windows.Media.Imaging;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;

namespace OperaEtDies.Data;

/// <summary>
/// Loads image files. Problems are returned as errors rather than thrown, since data files may come from outside the game.
/// </summary>
public static class ImageDataLoader
{
	/// <summary>
	/// Reads the image file's contents. Safe to call from any thread.
	/// </summary>
	public static DataLoadResult<byte[]> Read(string filePath) => LogErrors(DataFileReader.Read(filePath, File.ReadAllBytes));

	/// <summary>
	/// Decodes image file contents from <see cref="Read"/>. The image belongs to the calling thread, so call this on the thread that will use it.
	/// </summary>
	public static DataLoadResult<BitmapFrame> Decode(string filePath, byte[] contents) => LogErrors(DecodeCore(filePath, contents));

	private static DataLoadResult<BitmapFrame> DecodeCore(string filePath, byte[] contents)
	{
		try
		{
			using var stream = new MemoryStream(contents);
			var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

			// The frame keeps a link to its decoder, which lets windows pick the best size from a multi-size icon.
			return DataLoadResult<BitmapFrame>.Success(decoder.Frames[0]);
		}
		catch (Exception e) when (e is NotSupportedException or FormatException or IOException or ArgumentException or InvalidOperationException or OverflowException)
		{
			return DataLoadResult<BitmapFrame>.Failure(new DataLoadError(filePath, OurResources.DataFileInvalidImage.FormatCurrentCulture(e.Message)));
		}
	}

	private static DataLoadResult<T> LogErrors<T>(DataLoadResult<T> result)
		where T : class
	{
		foreach (var error in result.Errors)
			Log.Error(error.ToString());
		return result;
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(ImageDataLoader));
}
