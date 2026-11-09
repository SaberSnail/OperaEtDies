using System.Collections.ObjectModel;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// A place on the map in the game world, and the people who are in it.
/// </summary>
public sealed class LocationViewModel : ViewModelBase
{
	public LocationViewModel(Location location)
	{
		Id = location.Id;
		Title = location.Title;
		ShortDescription = location.ShortDescription;
		MediumDescription = location.MediumDescription;
		LongDescription = [.. location.LongDescription];
		m_connections = [];
		m_people = [];
		People = new ReadOnlyObservableCollection<PersonViewModel>(m_people);
	}

	public string Id { get; }

	public string Title { get; }

	/// <summary>
	/// One sentence, used when the location is listed as a place to go.
	/// </summary>
	public string ShortDescription { get; }

	/// <summary>
	/// One paragraph, shown when arriving at the location.
	/// </summary>
	public string MediumDescription { get; }

	/// <summary>
	/// Several paragraphs, shown when examining the location.
	/// </summary>
	public IReadOnlyList<string> LongDescription { get; }

	/// <summary>
	/// The locations that can be reached from this one.
	/// </summary>
	public IReadOnlyList<LocationViewModel> Connections => VerifyAccess(m_connections);

	/// <summary>
	/// The people in the location, including the player if they're here.
	/// </summary>
	public ReadOnlyObservableCollection<PersonViewModel> People { get; }

	/// <summary>
	/// Sets the connections once every location in the world has been created, since connections can go in both directions.
	/// </summary>
	internal void SetConnections(IReadOnlyList<LocationViewModel> connections) =>
		SetPropertyField(nameof(Connections), connections, ref m_connections);

	// People are added and removed by PersonViewModel when their location changes.
	internal void AddPerson(PersonViewModel person)
	{
		VerifyAccess();
		m_people.Add(person);
	}

	internal void RemovePerson(PersonViewModel person)
	{
		VerifyAccess();
		m_people.Remove(person);
	}

	private readonly ObservableCollection<PersonViewModel> m_people;
	private IReadOnlyList<LocationViewModel> m_connections;
}
