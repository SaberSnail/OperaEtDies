using System.Windows;
using Microsoft.Win32;
using ModEditor.ViewModels;

namespace ModEditor.Views;

/// <summary>
/// Shows the editor's questions and messages over its window.
/// </summary>
public sealed class EditorDialogs(Window owner) : IEditorDialogs
{
	public string? PickFolder(string? initialDirectory)
	{
		var dialog = new OpenFolderDialog { Title = "Open Mod Folder", InitialDirectory = initialDirectory ?? "" };
		return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
	}

	public string? PromptText(string title, string message, string initialText, IReadOnlyList<string>? suggestions = null)
	{
		var window = new TextPromptWindow(title, message, initialText, suggestions) { Owner = owner };
		return window.ShowDialog() == true ? window.Text : null;
	}

	public bool? AskToSave(string modName) =>
		MessageBox.Show(owner, $"Save the changes to \"{modName}\"?", "Mod Editor", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
		{
			MessageBoxResult.Yes => true,
			MessageBoxResult.No => false,
			_ => null,
		};

	public void ShowError(string message) => MessageBox.Show(owner, message, "Mod Editor", MessageBoxButton.OK, MessageBoxImage.Warning);
}
