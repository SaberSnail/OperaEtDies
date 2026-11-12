using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// Shows the location the player is in, with options to examine it, examine the people in it, and travel to connected locations.
/// </summary>
public sealed class LocationScreenViewModel : ViewModelBase
{
	public LocationScreenViewModel(Location location, PortraitRenderer? portraitRenderer = null)
	{
		Location = location;
		var connections = location.Exits.Select(x => new LocationConnectionViewModel(x, OnTravelRequested)).ToList();
		Paths = connections.Where(x => x.Direction is not null).OrderBy(x => x.Direction).ToList();
		Places = connections.Where(x => x.Direction is null).ToList();
		Connections = [.. Paths, .. Places];
		People = location.People.Where(x => !x.IsPlayer).Select(x => new LocationPersonViewModel(x, portraitRenderer)).ToList();
	}

	public event EventHandler<GenericEventArgs<Location>>? TravelRequested;

	/// <summary>
	/// The location being shown.
	/// </summary>
	public Location Location { get; }

	public string Title => Location.Title;

	public string Description => Location.MediumDescription;

	/// <summary>
	/// The detailed description, which is only shown once the location has been examined.
	/// </summary>
	public IReadOnlyList<string> LongDescription => Location.LongDescription;

	public bool IsExamined
	{
		get => VerifyAccess(m_isExamined);
		private set => SetPropertyField(value, ref m_isExamined);
	}

	/// <summary>
	/// Everywhere the player can go from here: the paths, then the places.
	/// </summary>
	public IReadOnlyList<LocationConnectionViewModel> Connections { get; }

	/// <summary>
	/// The paths to other quarters, in compass order starting from north.
	/// </summary>
	public IReadOnlyList<LocationConnectionViewModel> Paths { get; }

	public bool HasPaths => Paths.Count != 0;

	/// <summary>
	/// The places that aren't reached by a path: the houses and businesses in a quarter, or the quarter a house or
	/// business is in.
	/// </summary>
	public IReadOnlyList<LocationConnectionViewModel> Places { get; }

	public bool HasPlaces => Places.Count != 0;

	/// <summary>
	/// The heading for the places: the places in this quarter, or where to go from a house or business.
	/// </summary>
	public string PlacesHeader => HasPaths ? OurResources.PlacesHeader : OurResources.ConnectionsHeader;

	/// <summary>
	/// The people in the location when the player arrived, other than the player.
	/// </summary>
	public IReadOnlyList<LocationPersonViewModel> People { get; }

	public bool HasPeople => People.Count != 0;

	public bool CanExamine() => !m_isExamined;

	public void Examine()
	{
		VerifyAccess();
		IsExamined = true;
	}

	private void OnTravelRequested(Location location) => TravelRequested.Raise(this, new GenericEventArgs<Location>(location));

	private bool m_isExamined;
}
