using System.IO;
using System.Security;
using System.Windows;
using Microsoft.Win32;
using PortraitEditor.Model;

namespace PortraitEditor.Views;

/// <summary>
/// The editor window's layout: where it is, whether it's maximized, and how wide its side panels are. It's kept in
/// the current user's registry between sessions.
/// </summary>
public sealed record WindowLayout(Rect Bounds, bool IsMaximized, double TreeWidth, double EditorWidth, double PreviewsWidth)
{
	/// <summary>
	/// The path of the registry key under HKEY_CURRENT_USER.
	/// </summary>
	public const string KeyPath = EditorSettings.KeyPath;

	/// <summary>
	/// Loads the layout, or returns null if it hasn't been saved or can't be read.
	/// </summary>
	public static WindowLayout? Load()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
			int? Read(string name) => key?.GetValue(name) is int value ? value : null;
			return Read(LeftName) is { } left && Read(TopName) is { } top && Read(WidthName) is { } width && Read(HeightName) is { } height
				? new WindowLayout(new Rect(left, top, width, height), Read(MaximizedName) == 1, Read(TreeWidthName) ?? 0, Read(EditorWidthName) ?? 0, Read(PreviewsWidthName) ?? 0)
				: null;
		}
		catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
		{
			return null;
		}
	}

	/// <summary>
	/// Whether the window would be on a screen, so it isn't restored somewhere it can't be seen, such as a monitor that's
	/// no longer connected.
	/// </summary>
	public bool IsOnScreen()
	{
		var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
		var visible = Rect.Intersect(screen, Bounds);
		return !visible.IsEmpty && visible.Width >= 200 && visible.Height >= 100;
	}

	public void Save()
	{
		try
		{
			using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
			void Write(string name, double value) => key.SetValue(name, (int) Math.Round(value), RegistryValueKind.DWord);
			Write(LeftName, Bounds.Left);
			Write(TopName, Bounds.Top);
			Write(WidthName, Bounds.Width);
			Write(HeightName, Bounds.Height);
			Write(MaximizedName, IsMaximized ? 1 : 0);
			Write(TreeWidthName, TreeWidth);
			Write(EditorWidthName, EditorWidth);
			Write(PreviewsWidthName, PreviewsWidth);
		}
		catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
		{
			// The layout is a convenience, so failing to keep it isn't a problem.
		}
	}

	private const string LeftName = "Left";
	private const string TopName = "Top";
	private const string WidthName = "Width";
	private const string HeightName = "Height";
	private const string MaximizedName = "Maximized";
	private const string TreeWidthName = "TreeWidth";
	private const string EditorWidthName = "EditorWidth";
	private const string PreviewsWidthName = "PreviewsWidth";
}
