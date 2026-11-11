using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Town;

namespace OperaEtDies.GameState;

/// <summary>
/// The state of the game world while a game is being played: the locations and the people in them.
/// </summary>
public sealed class GameStateViewModel : ViewModelBase
{
	/// <summary>
	/// Creates the world for a new game from the game data and the player's chosen traits. The town is generated
	/// around the player's household, and everyone starts at home.
	/// </summary>
	public static GameStateViewModel CreateNewGame(GameData gameData, CharacterTraits playerTraits, Random random)
	{
		var town = TownGenerator.Generate(gameData, playerTraits, random);

		// Houses are named after the household's head.
		var houseTitles = town.Households.Where(x => x.House is not null).ToDictionary(x => x.House!, x => x.Head.Traits.Format(gameData.HouseTitle));
		var locationsByTownLocation = town.Locations.ToDictionary(x => x, x => new LocationViewModel(x.Id, x.Template, houseTitles.GetValueOrDefault(x), x.Bounds,
			x.Template is QuarterTemplate quarter ? gameData.QuarterKinds.Get(quarter.Kind).Area : null, x.Surroundings.Select(surrounding => surrounding.Text).ToList()));
		foreach (var (townLocation, location) in locationsByTownLocation)
			location.SetExits(townLocation.Exits.Select(x => new LocationExit(locationsByTownLocation[x.Location], x.Direction, x.Path, x.IsCrossing)).ToList());

		var locations = town.Locations.Select(x => locationsByTownLocation[x]).ToList();
		var families = town.Households
			.Select(household =>
			{
				var home = locationsByTownLocation[household.House!];
				var members = household.Members
					.Select(x =>
					{
						var workplace = x.Business?.Location is { } businessLocation ? locationsByTownLocation[businessLocation] : home;
						return new PersonViewModel(gameData, x.Traits, home, x.IsPlayer, workplace);
					})
					.ToList();
				return new FamilyViewModel(household.LastName, members, home, household.WealthLevel);
			})
			.ToList();

		return new GameStateViewModel(town, locations, families);
	}

	private GameStateViewModel(GeneratedTown town, IReadOnlyList<LocationViewModel> locations, IReadOnlyList<FamilyViewModel> families)
	{
		var people = families.SelectMany(x => x.Members).ToList();
		Town = town;
		Locations = locations;
		Families = families;
		Player = people.Single(x => x.IsPlayer);
		Npcs = people.Where(x => !x.IsPlayer).ToList();
		m_locationsById = locations.ToDictionary(x => x.Id, StringComparer.Ordinal);
	}

	/// <summary>
	/// The households and businesses the town was generated with.
	/// </summary>
	public GeneratedTown Town { get; }

	public IReadOnlyList<LocationViewModel> Locations { get; }

	/// <summary>
	/// Everyone in the game world, grouped by the family they live with, starting with the player's family.
	/// </summary>
	public IReadOnlyList<FamilyViewModel> Families { get; }

	public PersonViewModel Player { get; }

	/// <summary>
	/// The people in the game world, other than the player.
	/// </summary>
	public IReadOnlyList<PersonViewModel> Npcs { get; }

	/// <summary>
	/// Gets the location with the given id, or null if there isn't one.
	/// </summary>
	public LocationViewModel? FindLocation(string id) => m_locationsById.GetValueOrDefault(id);

	private readonly Dictionary<string, LocationViewModel> m_locationsById;
}
