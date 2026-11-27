using System.IO;
using System.Security;
using GoldenAnvil.Utility.Logging;
using Microsoft.Win32;

namespace OperaEtDies.Settings;

/// <summary>
/// Keeps the player's settings in the current user's registry, under the game's key with the mod list and where the
/// window was.
/// </summary>
public sealed class RegistryAppSettingsStore : IAppSettingsStore
{
	public RegistryAppSettingsStore(string gameTitle)
	{
		KeyPath = GameRegistry.GetKeyPath(gameTitle);
	}

	/// <summary>
	/// The path of the registry key under HKEY_CURRENT_USER.
	/// </summary>
	public string KeyPath { get; }

	public AppSettingsValues Load()
	{
		var defaults = new AppSettingsValues();
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
			return new AppSettingsValues
			{
				TextSize = key?.GetValue(TextSizeName) is int textSize ? textSize : defaults.TextSize,
				ReduceMotion = Enum.TryParse<ReduceMotion>(key?.GetValue(ReduceMotionName) as string, out var reduceMotion) ? reduceMotion : defaults.ReduceMotion,
				TimeSpeed = Enum.TryParse<TimeSpeed>(key?.GetValue(TimeSpeedName) as string, out var timeSpeed) ? timeSpeed : defaults.TimeSpeed,
				IsFullScreen = key?.GetValue(FullScreenName) is int isFullScreen ? isFullScreen == 1 : defaults.IsFullScreen,
			};
		}
		catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
		{
			Log.Error($"Couldn't read the settings from the registry: {e.Message}");
			return defaults;
		}
	}

	public void Save(AppSettingsValues values)
	{
		ArgumentNullException.ThrowIfNull(values);
		try
		{
			using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
			key.SetValue(TextSizeName, values.TextSize, RegistryValueKind.DWord);
			key.SetValue(ReduceMotionName, values.ReduceMotion.ToString(), RegistryValueKind.String);
			key.SetValue(TimeSpeedName, values.TimeSpeed.ToString(), RegistryValueKind.String);
			key.SetValue(FullScreenName, values.IsFullScreen ? 1 : 0, RegistryValueKind.DWord);
		}
		catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
		{
			Log.Error($"Couldn't save the settings to the registry: {e.Message}");
		}
	}

	public void Reset()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
			if (key is not null)
			{
				foreach (var name in new[] { TextSizeName, ReduceMotionName, TimeSpeedName, FullScreenName })
					key.DeleteValue(name, throwOnMissingValue: false);
			}
		}
		catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
		{
			Log.Error($"Couldn't reset the settings in the registry: {e.Message}");
		}
	}

	private const string TextSizeName = "TextSize";
	private const string ReduceMotionName = "ReduceMotion";
	private const string TimeSpeedName = "TimeSpeed";
	private const string FullScreenName = "FullScreen";
	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(RegistryAppSettingsStore));
}
