using ModEditor.Model;

namespace ModEditor.ViewModels.Form;

/// <summary>
/// What the mod changes in the data a form shows, by path, for marking its fields and going to the part of the mod
/// that changed them. Only a form showing the game data (with the mod) has it.
/// </summary>
public interface IFormChanges
{
	/// <summary>
	/// Gets what the mod does at a path: adds or changes it (or something in it), or null if nothing.
	/// </summary>
	ChangeKind? GetChange(string path);

	/// <summary>
	/// Gets the part of the mod that last changed the data at a path (or in it), or null if it isn't changed.
	/// </summary>
	ModLocation? GetSource(string path);

	/// <summary>
	/// Shows a part of the mod in the Mod tab.
	/// </summary>
	void GoTo(ModLocation location);
}
