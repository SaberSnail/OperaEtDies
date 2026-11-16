namespace ModEditor.ViewModels;

/// <summary>
/// The questions and messages the editor shows in windows of their own.
/// </summary>
public interface IEditorDialogs
{
	/// <summary>
	/// Asks for a mod folder, returning its path, or null if the user cancelled.
	/// </summary>
	string? PickFolder(string? initialDirectory);

	/// <summary>
	/// Asks for a line of text, offering the suggestions in a list if there are any. Returns the text, or null if the
	/// user cancelled.
	/// </summary>
	string? PromptText(string title, string message, string initialText, IReadOnlyList<string>? suggestions = null);

	/// <summary>
	/// Asks whether to save changes before they're lost: true to save, false to discard them, or null to cancel.
	/// </summary>
	bool? AskToSave(string modName);

	void ShowError(string message);
}
