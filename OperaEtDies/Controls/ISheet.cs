namespace OperaEtDies.Controls;

/// <summary>
/// Detailed information shown in a sheet over the center during play (see <see cref="Game.GameScreenViewModel.Sheets"/>).
/// Sheets don't use the engine, and the sheet's frame draws its title and the button that closes it.
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
