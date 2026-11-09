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
				new Profession { Id = "smith", Title = "Blacksmith", DefaultLastName = "Smith", Frequency = 3 },
				new Profession { Id = "miller", Title = "Miller", DefaultLastName = "Miller", Frequency = 2 },
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
			Heights =
			[
				new CharacterOption { Id = "tall", Title = "Tall", Frequency = 2 },
				new CharacterOption { Id = "medium", Title = "Medium", Frequency = 3 },
				new CharacterOption { Id = "short", Title = "Short", Frequency = 2 },
			],
			Weights =
			[
				new CharacterOption { Id = "slim", Title = "Slim", Frequency = 2 },
				new CharacterOption { Id = "medium", Title = "Medium", Frequency = 3 },
				new CharacterOption { Id = "heavy", Title = "Heavy", Frequency = 2 },
			],
		});
}
