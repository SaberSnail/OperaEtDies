using System.IO;
using System.Security;
using System.Windows;
using Microsoft.Win32;

namespace ModEditor.Model;

/// <summary>
/// The editor's settings, kept in the current user's registry between sessions: the mod folders opened recently,
/// whether the game is run in developer mode, whether the selected item is edited in a form or as JSON, and the
/// window's layout.
/// </summary>
public sealed record EditorSettings(IReadOnlyList<string> RecentFolders, bool RunAsDeveloper, Rect? WindowBounds, bool IsMaximized, double TreeWidth, int EditorTab = 0)
{
	/// <summary>
	/// The path of the editor's registry key under HKEY_CURRENT_USER.
	/// </summary>
	public const string KeyPath = @"Software\Golden Anvil\Opera Et Dies Mod Editor";

	public const int MaximumRecentFolders = 10;

	/// <summary>
	/// Loads the settings, using the defaults for any that haven't been saved or can't be read.
	/// </summary>
	public static EditorSettings Load()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
			int? Read(string name) => key?.GetValue(name) is int value ? value : null;
			var recent = key?.GetValue(RecentFoldersName) is string[] folders ? folders.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() : [];
			Rect? bounds = Read(LeftName) is { } left && Read(TopName) is { } top && Read(WidthName) is { } width && Read(HeightName) is { } height
				? new Rect(left, top, width, height)
				: null;
			return new EditorSettings(recent, Read(DeveloperName) == 1, bounds, Read(MaximizedName) == 1, Read(TreeWidthName) ?? 0, Read(EditorTabName) is { } tab and >= 0 and <= 2 ? tab : 0);
		}
		catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
		{
			return new EditorSettings([], false, null, false, 0);
		}
	}

	/// <summary>
	/// Puts a folder at the top of the recent folders.
	/// </summary>
	public EditorSettings WithRecentFolder(string folder) => this with
	{
		RecentFolders = RecentFolders.Where(x => !string.Equals(x, folder, StringComparison.OrdinalIgnoreCase)).Prepend(folder).Take(MaximumRecentFolders).ToList(),
	};

	public EditorSettings WithoutRecentFolder(string folder) => this with
	{
		RecentFolders = RecentFolders.Where(x => !string.Equals(x, folder, StringComparison.OrdinalIgnoreCase)).ToList(),
	};

	/// <summary>
	/// Whether the window would be on a screen, so it isn't restored somewhere it can't be seen.
	/// </summary>
	public bool IsWindowOnScreen()
	{
		if (WindowBounds is not { } bounds)
			return false;
		var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
		var visible = Rect.Intersect(screen, bounds);
		return !visible.IsEmpty && visible.Width >= 200 && visible.Height >= 100;
	}

	public void Save()
	{
		try
		{
			using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
			void Write(string name, double value) => key.SetValue(name, (int) Math.Round(value), RegistryValueKind.DWord);
			key.SetValue(RecentFoldersName, RecentFolders.ToArray(), RegistryValueKind.MultiString);
			Write(DeveloperName, RunAsDeveloper ? 1 : 0);
			Write(MaximizedName, IsMaximized ? 1 : 0);
			Write(TreeWidthName, TreeWidth);
			Write(EditorTabName, EditorTab);
			if (WindowBounds is { } bounds)
			{
				Write(LeftName, bounds.Left);
				Write(TopName, bounds.Top);
				Write(WidthName, bounds.Width);
				Write(HeightName, bounds.Height);
			}
		}
		catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
		{
			// The settings are a convenience, so failing to keep them isn't a problem.
		}
	}

	private const string RecentFoldersName = "RecentFolders";
	private const string DeveloperName = "RunAsDeveloper";
	private const string LeftName = "Left";
	private const string TopName = "Top";
	private const string WidthName = "Width";
	private const string HeightName = "Height";
	private const string MaximizedName = "Maximized";
	private const string TreeWidthName = "TreeWidth";
	private const string EditorTabName = "EditorTab";
}
