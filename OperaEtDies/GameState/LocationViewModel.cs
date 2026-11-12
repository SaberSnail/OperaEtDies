using System.Collections.ObjectModel;
using OperaEtDies.Data;
using OperaEtDies.Town;

namespace OperaEtDies.GameState;

/// <summary>
/// A place on the map in the game world, and the people who are in it.
/// </summary>
public sealed class LocationViewModel : ViewModelBase
{
	/// <summary>
	/// Creates a location from its template. A house has no title of its own, so it's given one. A quarter has bounds on
	/// the map, the area of the map it's in, and sentences about the map features it borders, which are added to the end
	/// of its medium description. Every location has the kind of the quarter it's in.
	/// </summary>
	public LocationViewModel(string id, LocationTemplate template, string? title = null, MapRect? bounds = null, MapArea? area = null, IReadOnlyList<string>? surroundings = null, string? quarterKind = null)
	{
		QuarterKind = quarterKind;
		IsHouse = template is HouseTemplate;
		BusinessProfession = (template as BusinessTemplate)?.Profession;
		Bounds = bounds;
		Area = area;
		IsLandmark = template is QuarterTemplate { IsLandmark: true };
		Id = id;
		Title = title ?? template.Title ?? "";
		ShortDescription = template.ShortDescription;
		MediumDescription = string.Join(" ", new[] { template.MediumDescription }.Concat(surroundings ?? []));
		LongDescription = [.. template.LongDescription];
		m_exits = [];
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
	/// For a quarter, where it is on the map.
	/// </summary>
	public MapRect? Bounds { get; }

	/// <summary>
	/// For a quarter, the part of the map it's in.
	/// </summary>
	public MapArea? Area { get; }

	/// <summary>
	/// The id of the kind of quarter the location is: the quarter itself, or the quarter a house or business is in.
	/// </summary>
	public string? QuarterKind { get; }

	/// <summary>
	/// Whether the location is a house someone lives in.
	/// </summary>
	public bool IsHouse { get; }

	/// <summary>
	/// For a business's place, the id of the profession it's for.
	/// </summary>
	public string? BusinessProfession { get; }

	/// <summary>
	/// Whether the location is a landmark quarter, with no houses or businesses.
	/// </summary>
	public bool IsLandmark { get; }

	/// <summary>
	/// The locations that can be reached from this one.
	/// </summary>
	public IReadOnlyList<LocationViewModel> Connections => VerifyAccess(m_exits).Select(x => x.Location).ToList();

	/// <summary>
	/// The ways out of this location. Paths between quarters have a direction and a name.
	/// </summary>
	public IReadOnlyList<LocationExit> Exits => VerifyAccess(m_exits);

	/// <summary>
	/// The people in the location, including the player if they're here.
	/// </summary>
	public ReadOnlyObservableCollection<PersonViewModel> People { get; }

	/// <summary>
	/// Sets the exits once every location in the world has been created, since exits can go in both directions.
	/// </summary>
	internal void SetExits(IReadOnlyList<LocationExit> exits) =>
		SetPropertyField([nameof(Exits), nameof(Connections)], exits, ref m_exits);

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
	private IReadOnlyList<LocationExit> m_exits;
}

/// <summary>
/// A way out of a location: where it leads, and for a path between quarters, which way it goes, what it's called, and
/// whether it crosses the river.
/// </summary>
public sealed record LocationExit(LocationViewModel Location, CompassDirection? Direction, string? Path, bool IsCrossing = false);
