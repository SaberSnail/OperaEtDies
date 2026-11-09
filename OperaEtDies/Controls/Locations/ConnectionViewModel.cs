using OperaEtDies.Data;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// A location the player can travel to from their current location.
/// </summary>
public sealed class ConnectionViewModel : ViewModelBase
{
	public ConnectionViewModel(Location location, Action<Location> travel)
	{
		m_location = location;
		m_travel = travel;
	}

	public string Title => m_location.Title;

	public string Description => m_location.ShortDescription;

	public void Travel()
	{
		VerifyAccess();
		m_travel(m_location);
	}

	private readonly Location m_location;
	private readonly Action<Location> m_travel;
}
