using GoldenAnvil.Utility;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// Shows the location the player is in, with options to examine it, examine the people in it, and travel to connected locations.
/// </summary>
public sealed class LocationScreenViewModel : ViewModelBase
{
	public LocationScreenViewModel(LocationViewModel location)
	{
		Location = location;
		Connections = location.Connections.Select(x => new LocationConnectionViewModel(x, OnTravelRequested)).ToList();
		People = location.People.Where(x => !x.IsPlayer).Select(x => new LocationPersonViewModel(x)).ToList();
	}

	public event EventHandler<GenericEventArgs<LocationViewModel>>? TravelRequested;

	/// <summary>
	/// The location being shown.
	/// </summary>
	public LocationViewModel Location { get; }

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

	public IReadOnlyList<LocationConnectionViewModel> Connections { get; }

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

	private void OnTravelRequested(LocationViewModel location) => TravelRequested.Raise(this, new GenericEventArgs<LocationViewModel>(location));

	private bool m_isExamined;
}
