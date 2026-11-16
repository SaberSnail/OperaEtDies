using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModEditor.Model;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Tools;

[TestFixture]
public sealed class ModChangesTests
{
	/// <summary>
	/// The mod editor merges data files one at a time with MergeSections, so it must give the same result as loading.
	/// </summary>
	[Test]
	public void MergingFilesOneAtATimeMatchesLoading()
	{
		var directory = JsonDataLoader.DefaultDataDirectory;
		var merged = new JsonObject(new JsonNodeOptions { PropertyNameCaseInsensitive = true });
		var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
		foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories).OrderBy(x => Path.GetRelativePath(directory, x).Replace('\\', '/'), StringComparer.Ordinal))
			DataDocument.MergeSections(merged, (JsonObject) JsonNode.Parse(File.ReadAllText(file), null, options)!);

		Assert.That(JsonNode.DeepEquals(merged, DataDocument.Read(directory, []).Value!.Root), Is.True);
	}

	[Test]
	public void ItemsAndSettingsAlreadyThereAreLeftOut()
	{
		var root = (JsonObject) JsonNode.Parse("""{ "properties": [ { "id": "a", "title": "A" } ], "game": { "title": "X" } }""")!;

		DataDocument.MergeSections(root, (JsonObject) JsonNode.Parse("""{ "properties": [ { "id": "a", "title": "B" }, { "id": "b" } ], "game": { "title": "Y", "icon": "I" } }""")!);

		Assert.That(JsonNode.DeepEquals(root, JsonNode.Parse("""{ "properties": [ { "id": "a", "title": "A" }, { "id": "b" } ], "game": { "title": "X", "icon": "I" } }""")), Is.True);
	}

	[Test]
	public void ChangesAreFoundAndCreditedToWhatMadeThem()
	{
		var gameData = (JsonObject) JsonNode.Parse("""{ "properties": [ { "id": "hair", "title": "Hair", "values": [ { "id": "red" }, { "id": "brown" } ] }, { "id": "eyes", "title": "Eyes" } ], "game": { "title": "X" } }""")!;
		(string, string)[] files =
		[
			("A.json", """{ "properties": [ { "id": "scar", "title": "Scar" } ] }"""),
			("B.json", """
				[
					{ "op": "set", "path": "properties[hair].values[red].frequency", "value": "rare" },
					{ "op": "set", "path": "properties[eyes].title", "value": "Eyes" },
					{ "op": "remove", "path": "properties[hair].values[brown]" },
					{ "op": "set", "path": "game.version", "value": "2" },
					{ "op": "remove", "path": "game.version" },
					{ "op": "remove", "path": "nothing[here]" }
				]
				"""),
		];

		var changes = ModChanges.Compute(gameData, files);

		Assert.That(changes.Changes.Select(x => $"{x.Kind} {x.Path}"), Is.EquivalentTo(new[]
		{
			"Added properties[scar]",
			"Added properties[hair].values[red].frequency",
			"Removed properties[hair].values[brown]",
		}));
		Assert.That(changes.GetChanges("properties", "hair"), Has.Count.EqualTo(2));
		Assert.That(changes.GetSources("properties[scar]"), Is.EqualTo(new ModLocation[] { new FileLocation("A.json") }));
		Assert.That(changes.GetSources("properties[hair].values[red].frequency"), Is.EqualTo(new ModLocation[] { new OperationLocation("B.json", 0) }));

		Assert.That(changes.GetOperationWarning(new OperationLocation("B.json", 0)), Is.Null);
		Assert.That(changes.GetOperationWarning(new OperationLocation("B.json", 1)), Does.Contain("changes nothing"), "It sets the value already there.");
		Assert.That(changes.GetOperationWarning(new OperationLocation("B.json", 3)), Does.Contain("changed back"), "A later operation undoes it.");
		Assert.That(changes.GetOperationWarning(new OperationLocation("B.json", 5)), Does.Contain("fails"));
		Assert.That(gameData["properties"]!.AsArray(), Has.Count.EqualTo(2), "The game data passed in isn't changed.");
	}

	[Test]
	public void ItemsThatOnlyMoveArentChanges()
	{
		var gameData = (JsonObject) JsonNode.Parse("""{ "properties": [ { "id": "a" }, { "id": "b" } ] }""")!;

		var changes = ModChanges.Compute(gameData, [("A.json", """[ { "op": "move", "path": "properties[b]", "index": 0 } ]""")]);

		Assert.That(changes.Changes, Is.Empty);
	}

	[Test]
	public void ThePlaceOfAChangeAndWhatItsInAreMarked()
	{
		var gameData = (JsonObject) JsonNode.Parse("""{ "properties": [ { "id": "hair", "values": [ { "id": "red" } ] } ] }""")!;

		var changes = ModChanges.Compute(gameData, [("A.json", """
			[
				{ "op": "set", "path": "properties[hair].values[red].frequency", "value": "rare" },
				{ "op": "insert", "path": "properties", "value": { "id": "scar", "title": "Scar" } }
			]
			""")]);

		Assert.That(changes.GetChangeAt("properties[hair].values[red].frequency"), Is.EqualTo(ChangeKind.Added), "The property is new.");
		Assert.That(changes.GetChangeAt("properties[hair].values[red]"), Is.EqualTo(ChangeKind.Changed), "Something in it changed.");
		Assert.That(changes.GetChangeAt("properties[hair].values"), Is.EqualTo(ChangeKind.Changed));
		Assert.That(changes.GetChangeAt("properties[scar].title"), Is.EqualTo(ChangeKind.Added), "It's in something added.");
		Assert.That(changes.GetChangeAt("properties[hair].title"), Is.Null);
		Assert.That(changes.GetLastChangeAt("properties[hair]")?.Path, Is.EqualTo("properties[hair].values[red].frequency"));
	}

	[Test]
	public void PathsOverlapWhenOneIsInTheOther()
	{
		Assert.That(ModChanges.Overlaps("properties[hair]", "properties[hair].values[red]"), Is.True);
		Assert.That(ModChanges.Overlaps("properties[hair].values", "properties[hair]"), Is.True);
		Assert.That(ModChanges.Overlaps("properties[hair]", "properties[hairColor]"), Is.False);
	}
}
