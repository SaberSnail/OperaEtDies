using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Characters;

[TestFixture]
public sealed class NpcGeneratorTests
{
	[Test]
	public void NamesAreNotRepeated()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		var takenName = NpcGenerator.Generate(gameData, new Random(-1), 1, []).Single().GetFullName(gameData.CharacterTemplates);

		for (var seed = 0; seed < 50; seed++)
		{
			var npcs = NpcGenerator.Generate(gameData, new Random(seed), 10, [takenName]);
			var names = npcs.Select(x => x.GetFullName(gameData.CharacterTemplates)).ToList();

			Assert.That(npcs, Has.Count.EqualTo(10));
			Assert.That(names, Is.Unique);
			Assert.That(names, Does.Not.Contain(takenName));
		}
	}

	[Test]
	public void ShippedCharactersAreComplete()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;

		foreach (var npc in NpcGenerator.Generate(gameData, new Random(0), 200, []))
		{
			var description = npc.GetDescription(gameData.CharacterTemplates);
			Assert.That(npc.GetFullName(gameData.CharacterTemplates), Is.Not.Empty);
			Assert.That(description, Is.Not.Empty.And.Not.Contain("  ").And.Not.Contain(" .").And.Not.Contain(" ,"));

			// A character has a value for exactly the properties whose conditions its other values meet.
			foreach (var property in gameData.CharacterProperties)
				Assert.That(npc.HasValue(property.Id), Is.EqualTo(property.HasValue(CharacterRole.Npc, npc.GetValueId)), $"{property.Id}: {description}");
		}
	}
}
