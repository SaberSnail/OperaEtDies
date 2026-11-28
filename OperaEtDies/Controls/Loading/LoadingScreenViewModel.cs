namespace OperaEtDies.Controls.Loading;

/// <summary>
/// The screen shown while the game data loads, at start up and after the mod list changes: "Loading…" with a small
/// animation that shows work is happening, without showing how far along it is.
/// </summary>
public sealed class LoadingScreenViewModel : ViewModelBase
{
	public string Text { get; } = OurResources.LoadingGameData;
}
