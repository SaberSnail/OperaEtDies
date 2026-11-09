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

		for (var seed = 0; seed < 50; seed++)
		{
			var npcs = NpcGenerator.Generate(gameData, new Random(seed), 10, ["John Smith"]);

			Assert.That(npcs, Has.Count.EqualTo(10));
			Assert.That(npcs.Select(x => x.Traits.FullName), Is.Unique);
			Assert.That(npcs.Select(x => x.Traits.FullName), Does.Not.Contain("John Smith"));
		}
	}

	[Test]
	public void ShippedDataDescribesCharacters()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;

		var npc = NpcGenerator.Generate(gameData, new Random(0), 1, [])[0];

		Assert.That(npc.Traits.GetDescription(), Does.StartWith(npc.Traits.FullName));
		Assert.That(npc.Traits.GetDescription(), Does.Contain(npc.Traits.Profession.Description));
	}
}
