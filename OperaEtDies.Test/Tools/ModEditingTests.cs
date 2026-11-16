using System.Text.Json.Nodes;
using ModEditor.Model;

namespace OperaEtDies.Test.Tools;

[TestFixture]
public sealed class ModEditingTests
{
	[Test]
	public void ItemsMoveToTheSameSectionOfAnotherFile()
	{
		var project = CreateProject(
			("A.json", """{ "properties": [ { "id": "eyes" }, { "id": "hair" } ] }"""),
			("B.json", """{ "professions": [] }"""));

		var (location, error) = ModEditing.Move(project, new ItemLocation("A.json", "properties", 0), new FileLocation("B.json"), after: true, isCopy: false);

		Assert.That(error, Is.Null);
		Assert.That(location, Is.EqualTo(new ItemLocation("B.json", "properties", 0)));
		Assert.That(Ids(project, "A.json", "properties"), Is.EqualTo(new[] { "hair" }));
		Assert.That(Ids(project, "B.json", "properties"), Is.EqualTo(new[] { "eyes" }));
	}

	[Test]
	public void MovingTheLastItemRemovesTheEmptySection()
	{
		var project = CreateProject(("A.json", """{ "properties": [ { "id": "eyes" } ], "professions": [ { "id": "smith" } ] }"""), ("B.json", "{}"));

		ModEditing.Move(project, new ItemLocation("A.json", "properties", 0), new FileLocation("B.json"), after: true, isCopy: false);

		Assert.That(((JsonObject) project.FindFile("A.json")!.Root!).ContainsKey("properties"), Is.False);
	}

	[Test]
	public void ItemsReorderWithinASection()
	{
		var project = CreateProject(("A.json", """{ "properties": [ { "id": "a" }, { "id": "b" }, { "id": "c" } ] }"""));

		var (location, _) = ModEditing.Move(project, new ItemLocation("A.json", "properties", 0), new ItemLocation("A.json", "properties", 2), after: true, isCopy: false);

		Assert.That(Ids(project, "A.json", "properties"), Is.EqualTo(new[] { "b", "c", "a" }));
		Assert.That(location, Is.EqualTo(new ItemLocation("A.json", "properties", 2)));
	}

	[Test]
	public void ItemsCantGoIntoAnotherSection()
	{
		var project = CreateProject(("A.json", """{ "properties": [ { "id": "a" } ], "professions": [ { "id": "smith" } ] }"""));
		var before = project.TakeSnapshot();

		var (location, error) = ModEditing.Move(project, new ItemLocation("A.json", "properties", 0), new ItemLocation("A.json", "professions", 0), after: false, isCopy: false);

		Assert.That(location, Is.Null);
		Assert.That(error, Is.Not.Null);
		Assert.That(project.TakeSnapshot().IsSameAs(before), Is.True);
	}

	[Test]
	public void DuplicatedItemsGetNewIds()
	{
		var project = CreateProject(("A.json", """{ "properties": [ { "id": "hair" }, { "id": "hairCopy" } ] }"""));

		var location = ModEditing.Duplicate(project, new ItemLocation("A.json", "properties", 0));

		Assert.That(location, Is.EqualTo(new ItemLocation("A.json", "properties", 1)));
		Assert.That(Ids(project, "A.json", "properties"), Is.EqualTo(new[] { "hair", "hairCopy2", "hairCopy" }));
	}

	[Test]
	public void SettingsThatAreAlreadySetCantBeAdded()
	{
		var project = CreateProject(("A.json", """{ "game": { "title": "X" } }"""), ("B.json", """{ "game": { "title": "Y" } }"""));

		var (_, error) = ModEditing.Move(project, new SettingLocation("A.json", "game", "title"), new FileLocation("B.json"), after: true, isCopy: false);

		Assert.That(error, Does.Contain("title"));
	}

	[Test]
	public void OperationsOnlyGoIntoOperationsFiles()
	{
		var project = CreateProject(("Ops.json", """[ { "op": "remove", "path": "properties[hair]" } ]"""), ("Data.json", "{}"));

		var (_, error) = ModEditing.Move(project, new OperationLocation("Ops.json", 0), new FileLocation("Data.json"), after: true, isCopy: false);
		var (location, _) = ModEditing.Insert(project, ModPiece.Operation(new JsonObject { ["op"] = "set" }), new OperationLocation("Ops.json", 0), after: false);

		Assert.That(error, Is.Not.Null);
		Assert.That(location, Is.EqualTo(new OperationLocation("Ops.json", 0)));
		Assert.That(((JsonArray) project.FindFile("Ops.json")!.Root!).Count, Is.EqualTo(2));
	}

	[Test]
	public void SectionsJoinTheSectionOfTheSameName()
	{
		var project = CreateProject(("A.json", """{ "properties": [ { "id": "a" } ] }"""), ("B.json", """{ "properties": [ { "id": "b" } ] }"""));

		ModEditing.Move(project, new SectionLocation("A.json", "properties"), new FileLocation("B.json"), after: true, isCopy: false);

		Assert.That(Ids(project, "B.json", "properties"), Is.EqualTo(new[] { "b", "a" }));
		Assert.That(((JsonObject) project.FindFile("A.json")!.Root!).Count, Is.EqualTo(0));
	}

	[Test]
	public void DataPathsQuoteIdsThatNeedIt()
	{
		Assert.That(ModEditing.GetDataPath(ModPiece.Item("properties", new JsonObject { ["id"] = "hair" })), Is.EqualTo("properties[hair]"));
		Assert.That(ModEditing.GetDataPath(ModPiece.Item("properties", new JsonObject { ["id"] = "12" })), Is.EqualTo("properties['12']"));
		Assert.That(ModEditing.GetDataPath(ModPiece.Setting("game", "frequencyChances", null)), Is.EqualTo("game.frequencyChances"));
		Assert.That(ModEditing.GetDataPath(ModPiece.Item("properties", new JsonObject())), Is.Null);
	}

	[Test]
	public void FoldersMoveWithTheirFiles()
	{
		var project = CreateProject(("Old/Sub/A.json", "{}"), ("B.json", "{}"));

		project.MoveFolder("Old", "New/Old");

		Assert.That(project.Files.Select(x => x.Path), Is.EqualTo(new[] { "B.json", "New/Old/Sub/A.json" }));
		Assert.That(project.Folders, Is.EquivalentTo(new[] { "New", "New/Old", "New/Old/Sub" }));
	}

	[Test]
	public void UnchangedTextIsKeptExactly()
	{
		const string text = "// A comment\r\n{ \"properties\": [] }";
		var project = CreateProject(("A.json", text));

		Assert.That(project.FindFile("A.json")!.Text, Is.EqualTo(text));
		Assert.That(project.IsModified, Is.False);
	}

	private static ModProject CreateProject(params (string Path, string Text)[] files) =>
		new(@"C:\Mods\Test", files, files.Select(x => ModFilePath.GetParent(x.Path)).Where(x => x.Length != 0).SelectMany(GetFolderAndParents));

	private static IEnumerable<string> GetFolderAndParents(string folder)
	{
		for (var current = folder; current.Length != 0; current = ModFilePath.GetParent(current))
			yield return current;
	}

	private static IEnumerable<string?> Ids(ModProject project, string file, string section) =>
		((JsonArray) ModEditing.GetSection(project.FindFile(file)!.Root, section)!).Select(ModJson.GetId);
}
