using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// Shows the location the player is in, with options to examine it, examine the characters in it, and travel to connected locations.
/// </summary>
public sealed class LocationViewModel : ViewModelBase
{
	public LocationViewModel(Location location, IReadOnlyList<Location> connections, IReadOnlyList<Character> characters)
	{
		m_location = location;
		Connections = connections.Select(x => new ConnectionViewModel(x, OnTravelRequested)).ToList();
		Characters = characters.Select(x => new CharacterViewModel(x)).ToList();
	}

	public event EventHandler<GenericEventArgs<Location>>? TravelRequested;

	public string Title => m_location.Title;

	public string Description => m_location.MediumDescription;

	/// <summary>
	/// The detailed description, which is only shown once the location has been examined.
	/// </summary>
	public IReadOnlyList<string> LongDescription => m_location.LongDescription;

	public bool IsExamined
	{
		get => VerifyAccess(m_isExamined);
		private set => SetPropertyField(value, ref m_isExamined);
	}

	public IReadOnlyList<ConnectionViewModel> Connections { get; }

	/// <summary>
	/// The characters in the location, other than the player.
	/// </summary>
	public IReadOnlyList<CharacterViewModel> Characters { get; }

	public bool HasCharacters => Characters.Count != 0;

	public bool CanExamine() => !m_isExamined;

	public void Examine()
	{
		VerifyAccess();
		IsExamined = true;
	}

	private void OnTravelRequested(Location location) => TravelRequested.Raise(this, new GenericEventArgs<Location>(location));

	private readonly Location m_location;
	private bool m_isExamined;
}
