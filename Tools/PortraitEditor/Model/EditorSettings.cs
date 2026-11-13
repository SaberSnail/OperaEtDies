using System.IO;
using System.Security;
using Microsoft.Win32;

namespace PortraitEditor.Model;

/// <summary>
/// The editor's view settings, kept in the current user's registry between sessions: whether the tree only shows
/// what's drawn for the focused character, whether the canvas shows its grid, and how big the preview portraits are.
/// </summary>
public sealed record EditorSettings(bool DrawnOnly, bool ShowGrid, double PreviewSize)
{
	/// <summary>
	/// The path of the editor's registry key under HKEY_CURRENT_USER.
	/// </summary>
	public const string KeyPath = @"Software\Golden Anvil\Opera Et Dies Portrait Editor";

	public const double MinimumPreviewSize = 80;

	public const double MaximumPreviewSize = 400;

	/// <summary>
	/// Loads the settings, using the defaults for any that haven't been saved or can't be read.
	/// </summary>
	public static EditorSettings Load()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
			int? Read(string name) => key?.GetValue(name) is int value ? value : null;
			return new EditorSettings(
				Read(DrawnOnlyName) is { } drawnOnly ? drawnOnly != 0 : Default.DrawnOnly,
				Read(ShowGridName) is { } showGrid ? showGrid != 0 : Default.ShowGrid,
				Read(PreviewSizeName) is { } previewSize ? Math.Clamp(previewSize, MinimumPreviewSize, MaximumPreviewSize) : Default.PreviewSize);
		}
		catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
		{
			return Default;
		}
	}

	public void Save()
	{
		try
		{
			using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
			key.SetValue(DrawnOnlyName, DrawnOnly ? 1 : 0, RegistryValueKind.DWord);
			key.SetValue(ShowGridName, ShowGrid ? 1 : 0, RegistryValueKind.DWord);
			key.SetValue(PreviewSizeName, (int) Math.Round(PreviewSize), RegistryValueKind.DWord);
		}
		catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
		{
			// The settings are a convenience, so failing to keep them isn't a problem.
		}
	}

	private static EditorSettings Default { get; } = new(false, false, 200);

	private const string DrawnOnlyName = "DrawnOnly";
	private const string ShowGridName = "ShowGrid";
	private const string PreviewSizeName = "PreviewSize";
}
