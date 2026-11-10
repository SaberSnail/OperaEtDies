using System.IO;
using System.Security;
using GoldenAnvil.Utility.Logging;
using Microsoft.Win32;

namespace OperaEtDies.Mods;

/// <summary>
/// Keeps the mod list in the current user's registry, under a key named for the game.
/// </summary>
public sealed class RegistryModListStore : IModListStore
{
	public RegistryModListStore(string gameTitle)
	{
		// Backslashes would split the title into several keys.
		KeyPath = $@"Software\Golden Anvil\{gameTitle.Trim().Replace(Path.DirectorySeparatorChar, '_')}";
	}

	/// <summary>
	/// The path of the registry key under HKEY_CURRENT_USER.
	/// </summary>
	public string KeyPath { get; }

	public IReadOnlyList<string> Load()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
			return key?.GetValue(ValueName) is string[] modDirectories ? modDirectories.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() : [];
		}
		catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
		{
			Log.Error($"Couldn't read the mod list from the registry: {e.Message}");
			return [];
		}
	}

	public void Save(IReadOnlyList<string> modDirectories)
	{
		try
		{
			using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
			key.SetValue(ValueName, modDirectories.ToArray(), RegistryValueKind.MultiString);
		}
		catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
		{
			Log.Error($"Couldn't save the mod list to the registry: {e.Message}");
		}
	}

	private const string ValueName = "Mods";
	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(RegistryModListStore));
}
