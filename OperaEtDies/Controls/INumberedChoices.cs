namespace OperaEtDies.Controls;

/// <summary>
/// A view in the center whose choices have numbers, shown as badges on their buttons, which the number keys press.
/// </summary>
public interface INumberedChoices
{
	/// <summary>
	/// Presses the choice with the given number, from 1 to <see cref="Hotkeys.ChoiceCount"/>, if there is one and it can be
	/// pressed now. Returns whether it was pressed.
	/// </summary>
	bool ChooseNumber(int number);

	/// <summary>
	/// Opens the menu of other ways of the choice with the given number, when it's a split button with a menu. Returns
	/// whether it opened.
	/// </summary>
	bool OpenChoiceMenu(int number) => false;

}
