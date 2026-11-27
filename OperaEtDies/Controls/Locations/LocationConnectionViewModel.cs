using GoldenAnvil.Utility;
using OperaEtDies.GameState;
using OperaEtDies.Town;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// A location the player can travel to from their current location. A path to another quarter says which way it goes
/// and what it's called. The player's home, the place they work, and their next appointment's place are marked with
/// icons, and so is the way to their destination, which also offers walking all the way there.
/// </summary>
public sealed class LocationConnectionViewModel : ViewModelBase
{
	public LocationConnectionViewModel(LocationExit exit, string travelTime, Action<Location> travel, ExitMarker? placeMarker = null, string? badge = null)
	{
		Badge = badge;
		PlaceMarker = placeMarker;
		Location = exit.Location;
		Direction = exit.Direction;
		Route = exit.Direction is { } direction
			? (exit.IsCrossing ? OurResources.ExitCrossingRoute : OurResources.ExitRoute).FormatCurrentCulture(direction.GetTitle(), exit.Path ?? "")
			: null;
		TravelTime = travelTime;
		m_travel = travel;
		m_action = CreateAction();
	}

	public Location Location { get; }

	/// <summary>
	/// The key that goes there, such as "1", shown on its button, or null if it has none.
	/// </summary>
	public string? Badge { get; }

	public string Title => Location.Title;

	public string Description => Location.ShortDescription;

	/// <summary>
	/// What marks the place as the player's: their home, where they work, or their next appointment's place, or null.
	/// </summary>
	public ExitMarker? PlaceMarker { get; }

	/// <summary>
	/// What marks the way to the player's destination: <see cref="ExitMarker.Destination"/> if it's there, or
	/// <see cref="ExitMarker.TowardDestination"/> if the quickest way there goes through it, or null if neither.
	/// </summary>
	public ExitMarker? DestinationMarker
	{
		get => VerifyAccess(m_destinationMarker);
		private set => SetPropertyField([nameof(DestinationMarker), nameof(Markers)], value, ref m_destinationMarker);
	}

	/// <summary>
	/// The icons on the button, from the left: the place's marker, then the destination's.
	/// </summary>
	public IReadOnlyList<ExitMarker> Markers => new[] { PlaceMarker, DestinationMarker }.OfType<ExitMarker>().ToList();

	/// <summary>
	/// The button: going there, with its markers explained in its tooltip, and for the way to the destination (but not the
	/// destination itself, where one path is all the way), walking all the way there in its menu.
	/// </summary>
	public SplitActionViewModel Action
	{
		get => VerifyAccess(m_action);
		private set => SetPropertyField(value, ref m_action);
	}

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

	/// <summary>
	/// Marks the place as the player's destination or the way to it, or neither, with the destination's title and, for
	/// the way to it, walking all the way there, with how long that takes.
	/// </summary>
	public void SetDestination(ExitMarker? marker, string? destinationTitle = null, string? walkTime = null, Action? walkAll = null)
	{
		VerifyAccess();
		m_destinationTitle = destinationTitle;
		m_walkTime = walkTime;
		m_walkAll = walkAll;
		DestinationMarker = marker;
		Action = CreateAction();
	}

	/// <summary>
	/// Explains what an exit's icon means, such as "Your home".
	/// </summary>
	internal static string DescribeMarker(ExitMarker marker, string? destinationTitle) => marker switch
	{
		ExitMarker.Home => OurResources.ExitMarkerHome,
		ExitMarker.Work => OurResources.ExitMarkerWork,
		ExitMarker.Appointment => OurResources.ExitMarkerAppointment,
		ExitMarker.Destination => OurResources.ExitMarkerDestination,
		_ => OurResources.ExitMarkerTowardDestination.FormatCurrentCulture(destinationTitle ?? ""),
	};

	private SplitActionViewModel CreateAction()
	{
		var toolTip = Markers.Count == 0 ? null : string.Join(Environment.NewLine, Markers.Select(x => DescribeMarker(x, m_destinationTitle)));
		var alternatives = DestinationMarker == ExitMarker.TowardDestination && m_walkAll is { } walkAll
			? new[] { new MenuActionViewModel(OurResources.WalkAllTheWay.FormatCurrentCulture(m_destinationTitle ?? "", m_walkTime ?? ""), walkAll) }
			: [];
		return new SplitActionViewModel(Title, Travel, alternatives: alternatives, toolTip: toolTip) { Badge = Badge };
	}

	private readonly Action<Location> m_travel;
	private ExitMarker? m_destinationMarker;
	private SplitActionViewModel m_action;
	private string? m_destinationTitle;
	private string? m_walkTime;
	private Action? m_walkAll;
}

/// <summary>
/// An icon on an exit's button.
/// </summary>
public enum ExitMarker
{
	/// <summary>
	/// The player's home.
	/// </summary>
	Home,

	/// <summary>
	/// Where the player works (when it isn't their home).
	/// </summary>
	Work,

	/// <summary>
	/// The place of the player's next appointment.
	/// </summary>
	Appointment,

	/// <summary>
	/// The quickest way to the player's destination.
	/// </summary>
	TowardDestination,

	/// <summary>
	/// The player's destination.
	/// </summary>
	Destination,
}
