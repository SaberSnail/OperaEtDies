using System.Text.Json;
using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Town;

namespace OperaEtDies.GameState;

/// <summary>
/// A person in the game world, including the player.
/// </summary>
public sealed class Person : NotifyPropertyChangedBase, IWorldObject
{
	/// <summary>
	/// Creates a person in a location. The traits must include the at work property. The random numbers decide whether
	/// someone who works from home is working when they arrive there.
	/// </summary>
	internal Person(WorldId id, GameData gameData, CharacterTraits traits, FamilyRole role, bool isPlayer, Location location, Random random)
	{
		var families = gameData.Families;
		var atWorkProperty = gameData.CharacterProperties.Get(families.Properties.AtWork);
		m_gameData = gameData;
		m_random = random;
		m_atWorkPropertyId = atWorkProperty.Id;
		m_atWork = atWorkProperty.Values!.First(x => x.Id == families.AtWork);
		m_notAtWork = atWorkProperty.Values!.First(x => x.Id == families.NotAtWork);

		Id = id;
		Role = role;
		IsPlayer = isPlayer;
		m_traits = traits;
		m_fullName = traits.GetFullName(gameData.CharacterTemplates);
		m_description = traits.GetDescription(gameData.CharacterTemplates);
		m_location = location;
		m_location.AddPerson(this);
	}

	public WorldId Id { get; }

	public bool IsPlayer { get; }

	/// <summary>
	/// How the person is related to the head of their family.
	/// </summary>
	public FamilyRole Role { get; }

	/// <summary>
	/// The values of the character properties defined in the data files. The game keeps the at work property up to date.
	/// </summary>
	public CharacterTraits Traits => m_traits;

	public string FullName => m_fullName;

	/// <summary>
	/// Describes the person in a few sentences.
	/// </summary>
	public string Description => m_description;

	/// <summary>
	/// The id of the person's profession, or null if they have none.
	/// </summary>
	public string? Profession => m_traits.GetValueId(m_gameData.Families.Properties.Profession);

	/// <summary>
	/// The family the person lives with.
	/// </summary>
	public Family Family => m_family ?? throw new InvalidOperationException($"Person {Id} isn't in a family.");

	/// <summary>
	/// The business the person owns or helps in, or null if they have none.
	/// </summary>
	public Business? Business { get; internal set; }

	public bool IsBusinessOwner => Business?.Owner == this;

	/// <summary>
	/// The house the person lives in.
	/// </summary>
	public Location Home => Family.Home;

	/// <summary>
	/// Where the person works: their business's place, or their home if they have no business or it's run from home.
	/// </summary>
	public Location Workplace => Business?.Location ?? Home;

	/// <summary>
	/// Whether the person is working right now, as given by the at work property in <see cref="Traits"/>.
	/// </summary>
	public bool IsAtWork
	{
		get => m_traits.GetValueId(m_atWorkPropertyId) == m_atWork.Id;
		set
		{
			if (value != IsAtWork)
			{
				using (ScopedPropertyChange(nameof(IsAtWork), nameof(Traits), nameof(Description), nameof(PortraitPlace)))
				{
					m_traits = m_traits.With(m_atWorkPropertyId, value ? m_atWork : m_notAtWork);
					m_description = m_traits.GetDescription(m_gameData.CharacterTemplates);
				}
			}
		}
	}

	/// <summary>
	/// Where the person is, for drawing their portrait: a business's place is the workplace of its profession, and a
	/// house is a home, unless it's the person's own home and they're working from it. Anywhere else is its kind of
	/// quarter.
	/// </summary>
	public PortraitPlace? PortraitPlace
	{
		get
		{
			var location = Location;
			return location.BusinessProfession is { } businessProfession ? Data.PortraitPlace.InWorkplace(businessProfession) :
				location.IsHouse ? (location == Workplace && IsAtWork && Profession is { } profession ? Data.PortraitPlace.InWorkplace(profession) : Data.PortraitPlace.Home) :
				location.QuarterKind is { } quarterKind ? Data.PortraitPlace.InQuarter(quarterKind) : null;
		}
	}

	/// <summary>
	/// The location the person is in. Changing it moves the person between the locations' lists of people, and
	/// updates whether they're at work.
	/// </summary>
	public Location Location
	{
		get => m_location;
		set
		{
			ArgumentNullException.ThrowIfNull(value);

			var oldLocation = m_location;
			if (SetPropertyField([nameof(Location), nameof(PortraitPlace)], value, ref m_location))
			{
				oldLocation.RemovePerson(this);
				value.AddPerson(this);
				IsAtWork = GetIsAtWork(value);
			}
		}
	}

	public WorldObjectRecord CreateRecord() =>
		new PersonRecord(Id, IsPlayer, Role, m_location.Id, m_traits.Values.ToDictionary(x => x.Key, x => JsonSerializer.SerializeToElement(x.Value switch
		{
			CharacterPropertyOption option => option.Id,
			var value => value,
		}), StringComparer.Ordinal));

	/// <summary>
	/// Puts the person in a location when a new game starts, and works out whether they're at work there.
	/// </summary>
	internal void PlaceAt(Location location)
	{
		if (location == m_location)
			IsAtWork = GetIsAtWork(location);
		else
			Location = location;
	}

	internal void SetFamily(Family family) => m_family = family;

	/// <summary>
	/// Works out whether the person is at work when they arrive somewhere: always in their business's place, and half
	/// the time in their home if they have a profession they work at from home.
	/// </summary>
	// TODO: Use schedules to decide when people are working.
	private bool GetIsAtWork(Location location) =>
		location == Workplace && (Workplace != Home || (Profession is not null && m_random.Next(2) == 0));

	private readonly GameData m_gameData;
	private readonly Random m_random;
	private readonly string m_atWorkPropertyId;
	private readonly CharacterPropertyOption m_atWork;
	private readonly CharacterPropertyOption m_notAtWork;
	private readonly string m_fullName;
	private CharacterTraits m_traits;
	private string m_description;
	private Family? m_family;
	private Location m_location;
}

/// <summary>
/// The saved form of a person: whether they're the player, their role in their family, where they are, and their
/// character property values (with value list properties saved as the value's id). Their home and workplace come from
/// their family and business.
/// </summary>
public sealed record PersonRecord(WorldId Id, bool IsPlayer, FamilyRole Role, WorldId Location, IReadOnlyDictionary<string, JsonElement> Traits) : WorldObjectRecord(Id)
{
	internal override IWorldObject Create(WorldLoader loader)
	{
		var gameData = loader.GameData;
		var values = new Dictionary<string, object>(StringComparer.Ordinal);
		foreach (var (propertyId, element) in Traits)
		{
			var property = WorldLoader.GetData(gameData.CharacterProperties, propertyId, "character property");
			object? value = (property.Type, element.ValueKind) switch
			{
				(CharacterPropertyType.String, JsonValueKind.String) => element.GetString(),
				(CharacterPropertyType.Integer, JsonValueKind.Number) when element.TryGetInt32(out var number) => number,
				(CharacterPropertyType.ValueList, JsonValueKind.String) => property.Values?.FirstOrDefault(x => x.Id == element.GetString()),
				_ => null,
			};
			values.Add(propertyId, value ?? throw new SaveLoadException(OurResources.SaveTraitValueInvalid.FormatCurrentCulture(Id, element.GetRawText(), propertyId)));
		}

		var traits = new CharacterTraits(values);
		var atWorkProperty = gameData.Families.Properties.AtWork;
		if (!traits.HasValue(atWorkProperty))
			throw new SaveLoadException(OurResources.SaveTraitValueInvalid.FormatCurrentCulture(Id, "", atWorkProperty));

		return new Person(Id, gameData, traits, Role, IsPlayer, loader.Get<Location>(Location), loader.Random);
	}
}
