using Microsoft.Win32;

namespace OperaEtDies.Mods;

/// <summary>
/// Asks the player to choose a folder with the standard folder dialog.
/// </summary>
public sealed class FolderPicker : IFolderPicker
{
	public string? PickFolder(string title, string? initialDirectory)
	{
		var dialog = new OpenFolderDialog { Title = title, InitialDirectory = initialDirectory ?? "" };
		return dialog.ShowDialog() == true ? dialog.FolderName : null;
	}
}
