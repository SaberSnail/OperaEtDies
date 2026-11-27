namespace OperaEtDies.Controls.NewGame;

/// <summary>
/// A step of setting up a new game, such as customizing the player's character, listed down the left of the new game
/// screen with its editor in the center.
/// </summary>
public abstract class NewGameStepViewModel(string title) : ViewModelBase
{
	public string Title { get; } = title;
}

/// <summary>
/// Customizing the player's character: an editor for each of their properties.
/// </summary>
public sealed class CharacterStepViewModel(string title, IReadOnlyList<CharacterPropertyEditorViewModel> properties) : NewGameStepViewModel(title)
{
	/// <summary>
	/// Editors for every property the player could have, in the order they're chosen.
	/// </summary>
	public IReadOnlyList<CharacterPropertyEditorViewModel> Properties { get; } = properties;
}
