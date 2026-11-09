using OperaEtDies.Data;

namespace OperaEtDies.Test;

/// <summary>
/// Small, predictable game data for tests.
/// </summary>
internal static class TestData
{
	public static GameInfo CreateGameInfo() => new() { Title = "Title", Description = "Description" };

	public static GameData CreateGameData() => new(
		new ProfessionList
		{
			Professions =
			[
				new Profession { Id = "smith", Title = "Blacksmith", Description = "a blacksmith", DefaultLastName = "Smith", Frequency = 3 },
				new Profession { Id = "miller", Title = "Miller", Description = "a miller", DefaultLastName = "Miller", Frequency = 2 },
			],
		},
		new FirstNameList
		{
			Male =
			[
				new WeightedName { Name = "John", Frequency = 3 },
				new WeightedName { Name = "Osric", Frequency = 1 },
			],
			Female = [new WeightedName { Name = "Alice", Frequency = 3 }],
		},
		new CharacterOptions
		{
			PlayerMinimumAge = 36,
			PlayerMaximumAge = 60,
			NpcMinimumAge = 16,
			NpcMaximumAge = 70,
			Heights =
			[
				new CharacterOption { Id = "tall", Title = "Tall", Description = "tall", Frequency = 2 },
				new CharacterOption { Id = "medium", Title = "Medium", Description = "medium", Frequency = 3 },
				new CharacterOption { Id = "short", Title = "Short", Description = "short", Frequency = 2 },
			],
			Weights =
			[
				new CharacterOption { Id = "slim", Title = "Slim", Description = "slim", Frequency = 2 },
				new CharacterOption { Id = "medium", Title = "Medium", Description = "medium", Frequency = 3 },
				new CharacterOption { Id = "heavy", Title = "Heavy", Description = "heavy", Frequency = 2 },
			],
		},
		new LocationList
		{
			StartingLocation = "home",
			Locations =
			[
				CreateLocation("home", "lane"),
				CreateLocation("lane", "home", "cottage"),
				CreateLocation("cottage", "lane"),
			],
		});

	private static Location CreateLocation(string id, params string[] connections) => new()
	{
		Id = id,
		Title = $"{id} title",
		ShortDescription = $"{id} short",
		MediumDescription = $"{id} medium",
		LongDescription = [$"{id} long 1", $"{id} long 2", $"{id} long 3"],
		Connections = connections,
	};
}
