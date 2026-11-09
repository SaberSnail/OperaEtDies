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
			var names = npcs.Select(x => x.GetFullName(gameData.CharacterTemplates)).ToList();

			Assert.That(npcs, Has.Count.EqualTo(10));
			Assert.That(names, Is.Unique);
			Assert.That(names, Does.Not.Contain("John Smith"));
		}
	}
}
