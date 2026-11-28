namespace OperaEtDies.Controls;

/// <summary>
/// A page shown in a sheet over the center during play (see <see cref="Game.GameScreenViewModel.Sheets"/>), such as
/// detailed information, what the player can start, or their clothes. No time passes while a sheet is open: anything done
/// in one that passes time closes it first, or waits until every sheet is closed. The sheet's frame draws its title and
/// the button that closes it. A sheet with numbered choices (<see cref="INumberedChoices"/>) takes the number keys while
/// it's on top.
/// </summary>
public interface ISheet
{
	/// <summary>
	/// The title along the top of the sheet.
	/// </summary>
	string Title { get; }

	/// <summary>
	/// Whether the sheet uses the whole width of the center, such as the map, rather than keeping to the line length of text.
	/// </summary>
	bool IsFullWidth { get; }
}
