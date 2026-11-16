using System.Text.Json.Nodes;
using ModEditor.Model;

namespace OperaEtDies.Test.Tools;

[TestFixture]
public sealed class ModFileTests
{
	[Test]
	public void DataFileCommentsAreKeptApartFromTheSections()
	{
		var file = new ModFile("A.json", """{ "comments": "Hair.", "properties": [ { "id": "hair" } ] }""");

		Assert.That(file.Kind, Is.EqualTo(ModFileKind.Data));
		Assert.That(file.Comments, Is.EqualTo("Hair."));
		Assert.That(((JsonObject) file.Root!).Select(x => x.Key), Is.EqualTo(new[] { "properties" }));
	}

	[Test]
	public void CommentsAreWrittenFirstWhenTheFileIsChanged()
	{
		var file = new ModFile("A.json", """{ "properties": [ { "id": "hair" } ], "comments": "Hair." }""");

		((JsonObject) file.Root!)["professions"] = new JsonArray();
		file.UpdateText();

		Assert.That(Reread(file.Text).Select(x => x.Key), Is.EqualTo(new[] { "comments", "properties", "professions" }));
		Assert.That(new ModFile("A.json", file.Text).Comments, Is.EqualTo("Hair."));
	}

	[Test]
	public void EmptyCommentsAreLeftOut()
	{
		var file = new ModFile("A.json", """{ "comments": "Hair.", "properties": [] }""");

		file.SetComments("  ");

		Assert.That(file.Comments, Is.Null);
		Assert.That(Reread(file.Text).ContainsKey("comments"), Is.False);
	}

	[Test]
	public void InvalidCommentsCantBeRead()
	{
		var file = new ModFile("A.json", """{ "comments": [ "Hair." ] }""");

		Assert.That(file.Kind, Is.EqualTo(ModFileKind.Invalid));
		Assert.That(file.ParseError, Is.Not.Null);
	}

	[Test]
	public void ChangesAreWorkedOutForEachOperationInAGroup()
	{
		var gameData = (JsonObject) JsonNode.Parse("""{ "properties": [ { "id": "hair" }, { "id": "eyes" } ] }""")!;

		var changes = ModChanges.Compute(gameData, [("A.json", """
			[
				{ "op": "remove", "path": "properties[eyes]" },
				{ "comments": "Removes hair.", "operations": [ { "op": "set", "path": "properties[hair].title", "value": "Hair" }, { "op": "remove", "path": "properties[hair]" } ] }
			]
			""")]);

		Assert.That(changes.Changes.Select(x => $"{x.Kind} {x.Path}"), Is.EquivalentTo(new[] { "Removed properties[eyes]", "Removed properties[hair]" }));
		Assert.That(changes.BySource.Keys, Is.EqualTo(new ModLocation[] { new OperationLocation("A.json", 0), new OperationLocation("A.json", 0, 1), new OperationLocation("A.json", 1, 1) }));
	}

	private static JsonObject Reread(string text) => (JsonObject) JsonNode.Parse(text)!;
}
