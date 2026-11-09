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
				new Profession { Id = "smith", Title = "Blacksmith", DefaultLastName = "Smith" },
				new Profession { Id = "miller", Title = "Miller", DefaultLastName = "Miller" },
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
			DefaultHeight = "tall",
			DefaultWeight = "heavy",
			Heights =
			[
				new CharacterOption { Id = "tall", Title = "Tall" },
				new CharacterOption { Id = "medium", Title = "Medium" },
				new CharacterOption { Id = "short", Title = "Short" },
			],
			Weights =
			[
				new CharacterOption { Id = "slim", Title = "Slim" },
				new CharacterOption { Id = "medium", Title = "Medium" },
				new CharacterOption { Id = "heavy", Title = "Heavy" },
			],
		});
}
