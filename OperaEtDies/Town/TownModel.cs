using System.Collections.ObjectModel;
using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Town;

/// <summary>
/// The town created for a new game: its households, their businesses, and the places they're in.
/// </summary>
public sealed class GeneratedTown
{
	public GeneratedTown(IReadOnlyList<Household> households, IReadOnlyList<TownBusiness> businesses, IReadOnlyList<TownLocation> locations, LayoutShape shape = LayoutShape.Radial, IReadOnlyList<TownFeature>? features = null)
	{
		Features = features ?? [];
		Shape = shape;
		Households = households;
		Businesses = businesses;
		Locations = locations;
	}

	/// <summary>
	/// The households, starting with the player's.
	/// </summary>
	public IReadOnlyList<Household> Households { get; }

	public IReadOnlyList<TownBusiness> Businesses { get; }

	/// <summary>
	/// Every place in the town: the quarters, and the houses and businesses in them.
	/// </summary>
	public IReadOnlyList<TownLocation> Locations { get; }

	/// <summary>
	/// The shape the town was laid out in.
	/// </summary>
	public LayoutShape Shape { get; }

	/// <summary>
	/// The map features in the town, such as a river or a forest, and where they are.
	/// </summary>
	public IReadOnlyList<TownFeature> Features { get; }

	public Household PlayerHousehold => Households[0];

	public TownPerson Player => PlayerHousehold.Head;
}

/// <summary>
/// The people who live together in one house.
/// </summary>
public sealed class Household
{
	public Household(string lastName)
	{
		LastName = lastName;
	}

	/// <summary>
	/// The last name of the head of the household, shared by most of its members.
	/// </summary>
	public string LastName { get; }

	/// <summary>
	/// The members, starting with the head.
	/// </summary>
	public IReadOnlyList<TownPerson> Members => m_members;

	public TownPerson Head => m_members[0];

	/// <summary>
	/// The household's total wealth, from the businesses its members own or help in, adjusted for the head's age and the
	/// household's fortune.
	/// </summary>
	public double Wealth { get; internal set; }

	/// <summary>
	/// The percentage the household's wealth was multiplied by for its good or bad fortune.
	/// </summary>
	public int Fortune { get; internal set; }

	public WealthLevel WealthLevel { get; internal set; }

	/// <summary>
	/// The house the household lives in, once the town is laid out.
	/// </summary>
	public TownLocation? House { get; internal set; }

	internal void AddMember(TownPerson person) => m_members.Add(person);

	private readonly Collection<TownPerson> m_members = [];
}

/// <summary>
/// How a person is related to the head of their household.
/// </summary>
public enum FamilyRole
{
	Head,
	Wife,
	Child,

	/// <summary>
	/// A parent of the head or the wife, who lives with the household because they no longer run a trade of their own.
	/// </summary>
	HouseholdElder,
}

/// <summary>
/// A person in the town, including the player.
/// </summary>
public sealed class TownPerson
{
	public TownPerson(CharacterTraits traits, Household household, FamilyRole role, bool isPlayer)
	{
		Traits = traits;
		Household = household;
		Role = role;
		IsPlayer = isPlayer;
	}

	public CharacterTraits Traits { get; }

	public Household Household { get; }

	public FamilyRole Role { get; }

	public bool IsPlayer { get; }

	/// <summary>
	/// The business the person owns or helps in, or null if they have no profession.
	/// </summary>
	public TownBusiness? Business { get; internal set; }

	public bool IsBusinessOwner => Business?.Owner == this;
}

/// <summary>
/// A business, owned by one person and helped by members of their household.
/// </summary>
public sealed class TownBusiness
{
	public TownBusiness(Profession profession, TownPerson owner)
	{
		Profession = profession;
		Owner = owner;
	}

	public Profession Profession { get; }

	public TownPerson Owner { get; }

	public IReadOnlyList<TownPerson> Helpers => m_helpers;

	public bool HasRoomForHelper => Profession.HasRoomForHelper(m_helpers.Count);

	/// <summary>
	/// The place the business is run from, or null if it's run from home.
	/// </summary>
	public TownLocation? Location { get; internal set; }

	internal void AddHelper(TownPerson person) => m_helpers.Add(person);

	private readonly Collection<TownPerson> m_helpers = [];
}

/// <summary>
/// A place in the town, created from a template.
/// </summary>
public sealed class TownLocation
{
	public TownLocation(string id, LocationTemplate template)
	{
		Id = id;
		Template = template;
	}

	/// <summary>
	/// A unique id: the template's id, with a number added if the template is used more than once.
	/// </summary>
	public string Id { get; }

	public LocationTemplate Template { get; }

	/// <summary>
	/// For a quarter, where it is on the map. Houses and businesses are inside their quarter, so they have none.
	/// </summary>
	public MapRect? Bounds { get; internal set; }

	/// <summary>
	/// The places that can be reached from this one. Connections always go both ways.
	/// </summary>
	public IReadOnlyList<TownLocation> Connections => m_exits.Select(x => x.Location).ToList();

	/// <summary>
	/// The quarter the place is in: itself for a quarter, or the quarter a house or business is in.
	/// </summary>
	public TownLocation? Quarter => Template is QuarterTemplate ? this : Connections.FirstOrDefault(x => x.Template is QuarterTemplate);

	/// <summary>
	/// The ways out of this place. A path between two quarters has a direction and a name, and leads the opposite way
	/// from the other end. Going between a quarter and a house or business in it has neither.
	/// </summary>
	public IReadOnlyList<TownExit> Exits => m_exits;

	internal void Connect(TownLocation other, CompassDirection? direction = null, string? path = null, bool isCrossing = false)
	{
		m_exits.Add(new TownExit(other, direction, path, isCrossing));
		other.m_exits.Add(new TownExit(this, direction?.Opposite(), path, isCrossing));
	}

	/// <summary>
	/// For a quarter, the map features it borders, each with the direction it's in and the sentence describing it.
	/// </summary>
	public IReadOnlyList<TownSurrounding> Surroundings { get; internal set; } = [];

	private readonly Collection<TownExit> m_exits = [];
}

/// <summary>
/// A way out of a place: where it leads, and for a path between quarters, which way it goes, what it's called, and
/// whether it crosses the river.
/// </summary>
public sealed record TownExit(TownLocation Location, CompassDirection? Direction, string? Path, bool IsCrossing = false);

/// <summary>
/// A map feature a quarter borders: the feature, the direction it's in from the quarter, and the sentence describing it.
/// </summary>
public sealed record TownSurrounding(MapFeature Feature, CompassDirection Direction, string Text);

/// <summary>
/// A map feature in a town, and where it is on the map. A river runs across the whole map, so its bounds only cover
/// the part beside the quarters.
/// </summary>
public sealed record TownFeature(MapFeature Feature, MapRect Bounds);
