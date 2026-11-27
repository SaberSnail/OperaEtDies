using System.IO;
using System.Security;
using System.Windows;
using Microsoft.Win32;

namespace OperaEtDies.Windows.Main;

/// <summary>
/// Where the game's window was and whether it was maximized, kept in the current user's registry with the game's other
/// settings, so the window opens where it was last.
/// </summary>
public sealed record WindowPlacement(Rect Bounds, bool IsMaximized)
{
	/// <summary>
	/// Loads where the window was from the given registry key, or returns null if it hasn't been saved or can't be read.
	/// </summary>
	public static WindowPlacement? Load(string keyPath)
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(keyPath);
			int? Read(string name) => key?.GetValue(name) is int value ? value : null;
			return Read(LeftName) is { } left && Read(TopName) is { } top && Read(WidthName) is { } width && Read(HeightName) is { } height && width > 0 && height > 0
				? new WindowPlacement(new Rect(left, top, width, height), Read(MaximizedName) == 1)
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

	public void Save(string keyPath)
	{
		try
		{
			using var key = Registry.CurrentUser.CreateSubKey(keyPath);
			void Write(string name, double value) => key.SetValue(name, (int) Math.Round(value), RegistryValueKind.DWord);
			Write(LeftName, Bounds.Left);
			Write(TopName, Bounds.Top);
			Write(WidthName, Bounds.Width);
			Write(HeightName, Bounds.Height);
			Write(MaximizedName, IsMaximized ? 1 : 0);
		}
		catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
		{
			// Where the window was is a convenience, so failing to keep it isn't a problem.
		}
	}

	private const string LeftName = "WindowLeft";
	private const string TopName = "WindowTop";
	private const string WidthName = "WindowWidth";
	private const string HeightName = "WindowHeight";
	private const string MaximizedName = "WindowMaximized";
}
