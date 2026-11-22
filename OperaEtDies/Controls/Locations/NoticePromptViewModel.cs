using GoldenAnvil.Utility;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// Tells the player something that happened while time passed, such as that they missed an appointment, shown on top of
/// everything else until they close it.
/// </summary>
public sealed class NoticePromptViewModel(string text) : ViewModelBase
{
	/// <summary>
	/// Raised when the player closes it.
	/// </summary>
	public event EventHandler? Dismissed;

	/// <summary>
	/// What happened, such as "Mary Smith gave up waiting for you."
	/// </summary>
	public string Text { get; } = text;

	public void Dismiss()
	{
		VerifyAccess();
		Dismissed.Raise(this);
	}
}
