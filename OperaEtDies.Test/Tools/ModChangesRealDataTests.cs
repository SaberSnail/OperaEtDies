using System.Text.Json.Nodes;
using ModEditor.Model;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Tools;

[TestFixture]
public sealed class ModChangesRealDataTests
{
	[Test]
	public void ASettingInsideASettingIsCreditedToIt()
	{
		var gameData = (JsonObject) JsonNode.Parse(DataDocument.Read(JsonDataLoader.DefaultDataDirectory, []).Value!.Root.ToJsonString(), new JsonNodeOptions { PropertyNameCaseInsensitive = true })!;

		var changes = ModChanges.Compute(gameData, [("Ops.json", """[ { "op": "set", "path": "families.head.femaleChance", "value": 35 } ]""")]);

		Assert.That(changes.GetChanges("families", "head").Select(x => $"{x.Kind} {x.Path} {x.Before} {x.After}"), Is.EqualTo(new[] { "Changed families.head.femaleChance 8 35" }));
	}
}
