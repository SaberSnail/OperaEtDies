using GoldenAnvil.Utility;
using OperaEtDies.GameState;
using OperaEtDies.Town;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// A location the player can travel to from their current location. A path to another quarter says which way it goes
/// and what it's called.
/// </summary>
public sealed class LocationConnectionViewModel : ViewModelBase
{
	public LocationConnectionViewModel(LocationExit exit, string travelTime, Action<Location> travel)
	{
		Location = exit.Location;
		Direction = exit.Direction;
		Route = exit.Direction is { } direction
			? (exit.IsCrossing ? OurResources.ExitCrossingRoute : OurResources.ExitRoute).FormatCurrentCulture(direction.GetTitle(), exit.Path ?? "")
			: null;
		TravelTime = travelTime;
		m_travel = travel;
	}

	public Location Location { get; }

	public string Title => Location.Title;

	public string Description => Location.ShortDescription;

	/// <summary>
	/// For a path to another quarter, the direction it leads in.
	/// </summary>
	public CompassDirection? Direction { get; }

	/// <summary>
	/// For a path to another quarter, which way it goes and what it's called, such as "North-east along the lane".
	/// </summary>
	public string? Route { get; }

	public bool HasRoute => Route is not null;

	/// <summary>
	/// How long it takes to get there, such as "2 minutes".
	/// </summary>
	public string TravelTime { get; }

	/// <summary>
	/// How long it takes to get there, as shown beside the route, such as "(2 minutes)".
	/// </summary>
	public string TravelTimeText => OurResources.ConnectionTravelTime.FormatCurrentCulture(TravelTime);

	public void Travel()
	{
		VerifyAccess();
		m_travel(Location);
	}

	private readonly Action<Location> m_travel;
}
