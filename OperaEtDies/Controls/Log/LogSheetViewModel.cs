namespace OperaEtDies.Controls.Log;

/// <summary>
/// The event log in a sheet, for reading at a larger size. It shows the same entries as the log in the right column.
/// </summary>
public sealed class LogSheetViewModel : ViewModelBase, ISheet
{
	public LogSheetViewModel(EventLogViewModel log) => Log = log;

	public string Title => OurResources.EventLogHeader;

	public bool IsFullWidth => false;

	/// <summary>
	/// The log whose entries are shown.
	/// </summary>
	public EventLogViewModel Log { get; }
}
