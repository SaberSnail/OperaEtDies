using OperaEtDies.Data;

namespace OperaEtDies.Characters;

/// <summary>
/// Creates the traits for non-player characters at random.
/// </summary>
public static class NpcGenerator
{
	/// <summary>
	/// Creates traits for characters at random. No two characters share a name, and none use a name that's
	/// already taken (such as the player's).
	/// </summary>
	public static IReadOnlyList<CharacterTraits> Generate(GameData gameData, Random random, int count, IEnumerable<string> takenNames)
	{
		var randomizer = new CharacterRandomizer(gameData, random, CharacterRole.Npc);
		var names = new HashSet<string>(takenNames, StringComparer.Ordinal);

		var characters = new List<CharacterTraits>();
		for (var index = 0; index < count; index++)
		{
			randomizer.RandomizeAll();

			// Try a few other characters before allowing a repeated name, since the name lists might be small.
			for (var attempt = 0; attempt < MaxNameAttempts && names.Contains(randomizer.GetTraits().GetFullName(gameData.CharacterTemplates)); attempt++)
				randomizer.RandomizeAll();

			var traits = randomizer.GetTraits();
			names.Add(traits.GetFullName(gameData.CharacterTemplates));
			characters.Add(traits);
		}

		return characters;
	}

	private const int MaxNameAttempts = 20;
}
