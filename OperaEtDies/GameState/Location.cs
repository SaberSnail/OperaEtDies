using System.Collections.ObjectModel;
using GoldenAnvil.Utility;
using OperaEtDies.Data;
using OperaEtDies.Town;

namespace OperaEtDies.GameState;

/// <summary>
/// A place on the map in the game world, and the people who are in it.
/// </summary>
public sealed class Location : NotifyPropertyChangedBase, IWorldObject
{
	/// <summary>
	/// Creates a location from its template. A quarter has bounds on the map, and the map features it borders, whose
	/// sentences are added to the end of its medium description.
	/// </summary>
	internal Location(WorldId id, GameData gameData, LocationTemplate template, MapRect? bounds = null, IReadOnlyList<LocationSurrounding>? surroundings = null)
	{
		Id = id;
		Template = template;
		Bounds = bounds;
		Surroundings = surroundings ?? [];
		Area = template is QuarterTemplate quarter ? gameData.QuarterKinds.Find(quarter.Kind)?.Area : null;
		MediumDescription = string.Join(" ", new[] { template.MediumDescription }.Concat(Surroundings.Select(x => x.Text)));
		m_gameData = gameData;
		m_exits = [];
		m_people = [];
		People = new ReadOnlyObservableCollection<Person>(m_people);
	}

	public WorldId Id { get; }

	/// <summary>
	/// The location's template from the data.
	/// </summary>
	public LocationTemplate Template { get; }

	/// <summary>
	/// The title of the location. A house is named after the head of the family that lives there.
	/// </summary>
	public string Title => Family is { } family ? family.Head.Traits.Format(m_gameData.HouseTitle) : Template.Title ?? "";

	/// <summary>
	/// One sentence, used when the location is listed as a place to go.
	/// </summary>
	public string ShortDescription => Template.ShortDescription;

	/// <summary>
	/// One paragraph, shown when arriving at the location.
	/// </summary>
	public string MediumDescription { get; }

	/// <summary>
	/// Several paragraphs, shown when examining the location.
	/// </summary>
	public IReadOnlyList<string> LongDescription => Template.LongDescription;

	/// <summary>
	/// For a quarter, where it is on the map.
	/// </summary>
	public MapRect? Bounds { get; }

	/// <summary>
	/// For a quarter, the part of the map it's in.
	/// </summary>
	public MapArea? Area { get; }

	/// <summary>
	/// For a quarter, the map features it borders.
	/// </summary>
	public IReadOnlyList<LocationSurrounding> Surroundings { get; }

	/// <summary>
	/// The quarter the location is in: itself for a quarter, or the quarter a house or business is in.
	/// </summary>
	public Location? Quarter => Template is QuarterTemplate ? this : m_exits.Select(x => x.Location).FirstOrDefault(x => x.Template is QuarterTemplate);

	/// <summary>
	/// The id of the kind of quarter the location is: the quarter itself, or the quarter a house or business is in.
	/// </summary>
	public string? QuarterKind => (Quarter?.Template as QuarterTemplate)?.Kind;

	/// <summary>
	/// Whether the location is a house someone lives in.
	/// </summary>
	public bool IsHouse => Template is HouseTemplate;

	/// <summary>
	/// For a business's place, the id of the profession it's for.
	/// </summary>
	public string? BusinessProfession => (Template as BusinessTemplate)?.Profession;

	/// <summary>
	/// Whether the location is a landmark quarter, with no houses or businesses.
	/// </summary>
	public bool IsLandmark => Template is QuarterTemplate { IsLandmark: true };

	/// <summary>
	/// For a house, the family that lives in it.
	/// </summary>
	public Family? Family { get; internal set; }

	/// <summary>
	/// The locations that can be reached from this one.
	/// </summary>
	public IReadOnlyList<Location> Connections => m_exits.Select(x => x.Location).ToList();

	/// <summary>
	/// The ways out of this location. Paths between quarters have a direction and a name.
	/// </summary>
	public IReadOnlyList<LocationExit> Exits => m_exits;

	/// <summary>
	/// The people in the location, including the player if they're here.
	/// </summary>
	public ReadOnlyObservableCollection<Person> People { get; }

	public WorldObjectRecord CreateRecord() =>
		new LocationRecord(Id, GetTemplateType(Template), Template.Id, Bounds,
			m_exits.Select(x => new LocationExitRecord(x.Location.Id, x.Direction, x.Path, x.IsCrossing)).ToList(),
			Surroundings.Select(x => new LocationSurroundingRecord(x.Feature.Id, x.Direction, x.Text)).ToList());

	/// <summary>
	/// Sets the exits once every location in the world has been created, since exits can go in both directions.
	/// </summary>
	internal void SetExits(IReadOnlyList<LocationExit> exits) =>
		SetPropertyField([nameof(Exits), nameof(Connections), nameof(Quarter), nameof(QuarterKind)], exits, ref m_exits);

	// People are added and removed by Person when their location changes.
	internal void AddPerson(Person person) => m_people.Add(person);

	internal void RemovePerson(Person person) => m_people.Remove(person);

	private static LocationType GetTemplateType(LocationTemplate template) => template switch
	{
		QuarterTemplate => LocationType.Quarter,
		HouseTemplate => LocationType.House,
		BusinessTemplate => LocationType.Business,
		_ => throw new InvalidOperationException($"Unknown kind of location template {template.GetType().Name}."),
	};

	private readonly GameData m_gameData;
	private readonly ObservableCollection<Person> m_people;
	private IReadOnlyList<LocationExit> m_exits;
}

/// <summary>
/// A way out of a location: where it leads, and for a path between quarters, which way it goes, what it's called, and
/// whether it crosses the river.
/// </summary>
public sealed record LocationExit(Location Location, CompassDirection? Direction, string? Path, bool IsCrossing = false);

/// <summary>
/// A map feature a quarter borders: the feature, the direction it's in from the quarter, and the sentence describing it.
/// </summary>
public sealed record LocationSurrounding(Feature Feature, CompassDirection Direction, string Text);

/// <summary>
/// The kinds of location template, which each have their own ids in the data.
/// </summary>
public enum LocationType
{
	Quarter,
	House,
	Business,
}

/// <summary>
/// The saved form of a location: its template in the data, its bounds, its exits, and the map features it borders.
/// The sentence for each map feature is saved, since it was chosen at random.
/// </summary>
public sealed record LocationRecord(WorldId Id, LocationType Type, string Template, MapRect? Bounds, IReadOnlyList<LocationExitRecord> Exits, IReadOnlyList<LocationSurroundingRecord> Surroundings) : WorldObjectRecord(Id)
{
	internal override IWorldObject Create(WorldLoader loader)
	{
		var gameData = loader.GameData;
		LocationTemplate template = Type switch
		{
			LocationType.Quarter => WorldLoader.GetData(gameData.QuarterTemplates, Template, "quarter"),
			LocationType.House => WorldLoader.GetData(gameData.HouseTemplates, Template, "house"),
			_ => WorldLoader.GetData(gameData.BusinessTemplates, Template, "business"),
		};
		var surroundings = Surroundings.Select(x => new LocationSurrounding(loader.Get<Feature>(x.Feature), x.Direction, x.Text)).ToList();
		var location = new Location(Id, gameData, template, Bounds, surroundings);

		// Exits lead to other locations, which can lead back, so they're set once every location exists.
		loader.Defer(() => location.SetExits(Exits.Select(x => new LocationExit(loader.Get<Location>(x.Location), x.Direction, x.Path, x.IsCrossing)).ToList()));
		return location;
	}
}

public sealed record LocationExitRecord(WorldId Location, CompassDirection? Direction, string? Path, bool IsCrossing);

public sealed record LocationSurroundingRecord(WorldId Feature, CompassDirection Direction, string Text);
