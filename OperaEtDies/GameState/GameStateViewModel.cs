using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// The state of the game world while a game is being played: the locations and the people in them.
/// </summary>
public sealed class GameStateViewModel : ViewModelBase
{
	/// <summary>
	/// Creates the world for a new game from the game data and the player's chosen traits.
	/// </summary>
	public static GameStateViewModel CreateNewGame(GameData gameData, CharacterTraits playerTraits, Random random)
	{
		var locations = gameData.Locations.Select(x => new LocationViewModel(x)).ToList();
		var locationsById = locations.ToDictionary(x => x.Id, StringComparer.Ordinal);

		// The catalog and the view models are in the same order. Connections are validated on load, so unknown ids are just skipped.
		foreach (var (location, viewModel) in gameData.Locations.Zip(locations))
			viewModel.SetConnections(location.Connections.Select(x => locationsById.GetValueOrDefault(x)).OfType<LocationViewModel>().ToList());

		var player = new PersonViewModel(gameData, playerTraits, locationsById[gameData.StartingLocation.Id], isPlayer: true);

		// Until villagers are defined in data, a few random ones are generated, each in a random location.
		var npcs = NpcGenerator.Generate(gameData, random, NpcCount, [player.FullName])
			.Select(x => new PersonViewModel(gameData, x, locations[random.Next(locations.Count)], isPlayer: false))
			.ToList();

		return new GameStateViewModel(locations, player, npcs);
	}

	private GameStateViewModel(IReadOnlyList<LocationViewModel> locations, PersonViewModel player, IReadOnlyList<PersonViewModel> npcs)
	{
		Locations = locations;
		Player = player;
		Npcs = npcs;
		m_locationsById = locations.ToDictionary(x => x.Id, StringComparer.Ordinal);
	}

	public IReadOnlyList<LocationViewModel> Locations { get; }

	public PersonViewModel Player { get; }

	/// <summary>
	/// The people in the game world, other than the player.
	/// </summary>
	public IReadOnlyList<PersonViewModel> Npcs { get; }

	/// <summary>
	/// Gets the location with the given id, or null if there isn't one.
	/// </summary>
	public LocationViewModel? FindLocation(string id) => m_locationsById.GetValueOrDefault(id);

	private const int NpcCount = 3;

	private readonly Dictionary<string, LocationViewModel> m_locationsById;
}
