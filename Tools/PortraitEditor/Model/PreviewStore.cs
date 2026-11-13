using System.IO;
using System.Text.Json;
using PortraitEditor.ViewModels;

namespace PortraitEditor.Model;

/// <summary>
/// Keeps the preview characters between sessions, in the user's local application data.
/// </summary>
public static class PreviewStore
{
	/// <summary>
	/// Loads the preview characters, or returns null if none have been kept (as opposed to an empty list kept on purpose)
	/// or they can't be read.
	/// </summary>
	public static IReadOnlyList<PreviewSettings>? Load()
	{
		try
		{
			return File.Exists(FilePath) ? JsonSerializer.Deserialize<List<PreviewSettings>>(File.ReadAllText(FilePath)) : null;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
		{
			return null;
		}
	}

	public static void Save(IReadOnlyList<PreviewSettings> previews)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			File.WriteAllText(FilePath, JsonSerializer.Serialize(previews, s_options));
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			// The previews are a convenience, so failing to keep them isn't a problem.
		}
	}

	private static string FilePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OperaEtDies", "PortraitEditor", "Previews.json");

	private static readonly JsonSerializerOptions s_options = new() { WriteIndented = true };
}
