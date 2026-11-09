using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// A location the player can travel to from their current location.
/// </summary>
public sealed class LocationConnectionViewModel : ViewModelBase
{
	public LocationConnectionViewModel(LocationViewModel location, Action<LocationViewModel> travel)
	{
		Location = location;
		m_travel = travel;
	}

	public LocationViewModel Location { get; }

	public string Title => Location.Title;

	public string Description => Location.ShortDescription;

	public void Travel()
	{
		VerifyAccess();
		m_travel(Location);
	}

	private readonly Action<LocationViewModel> m_travel;
}
