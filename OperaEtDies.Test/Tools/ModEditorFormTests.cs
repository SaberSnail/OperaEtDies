using System.IO;
using System.Text.Json.Nodes;
using ModEditor.Model;
using ModEditor.ViewModels.Form;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Tools;

[TestFixture]
public sealed class ModEditorFormTests
{
	[Test]
	public void EveryReferenceIsAFieldTheGameReads()
	{
		foreach (var (type, property) in DataReferences.Fields.Keys)
			Assert.That(DataSchema.GetFields(type).Any(x => x.PropertyName == property && x.DeclaringType == type), Is.True, $"{type.Name}.{property}");
	}

	[Test]
	public void EverySectionOfTheGameDataHasAShape()
	{
		var data = DataDocument.Read(JsonDataLoader.DefaultDataDirectory, []).Value!.Root;

		foreach (var (name, value) in data)
		{
			var sections = value is JsonArray ? DataSchema.ListSections : DataSchema.SettingsSections;
			Assert.That(sections.ContainsKey(name), Is.True, name);
		}
	}

	[Test]
	public void FieldsAreTheSettablePropertiesInCamelCase()
	{
		var fields = DataSchema.GetFields(typeof(Profession));

		Assert.That(fields.Select(x => x.Name), Does.Contain("id").And.Contain("frequencyWhen").And.Not.Contain("isBusiness"));
		Assert.That(fields.Single(x => x.Name == "id").IsRequired, Is.True);
		Assert.That(fields.Single(x => x.Name == "role").IsRequired, Is.False);
	}

	[Test]
	public void PathsLeadToTheShapeOfTheirData()
	{
		Assert.That(DataSchema.GetPathType("properties[hair]")?.ClrType, Is.EqualTo(typeof(CharacterProperty)));
		Assert.That(DataSchema.GetPathType("properties[hair].values")?.Element?.ClrType, Is.EqualTo(typeof(CharacterPropertyOption)));
		Assert.That(DataSchema.GetPathType("properties[hair].values[red].frequency")?.Kind, Is.EqualTo(DataKind.Text));
		Assert.That(DataSchema.GetPathType("game.title")?.Kind, Is.EqualTo(DataKind.Text));
		Assert.That(DataSchema.GetPathType("nothing[here]"), Is.Null);
		Assert.That(DataSchema.GetPathItem("properties[hair].values[red]"), Is.EqualTo(("properties", "hair")));
	}

	[TestCase("""{ "op": "replace", "path": "properties[hair]", "value": { "id": "hair" } }""", "hair")]
	[TestCase("""{ "op": "insert", "path": "properties", "values": [ { "id": "a" }, { "id": "b" } ] }""", "a,b")]
	[TestCase("""{ "op": "remove", "path": "properties[hair].values['red']" }""", "red")]
	[TestCase("""{ "op": "remove", "path": "properties[hair].values[2]" }""", "")]
	[TestCase("""{ "op": "set", "path": "game.title", "value": "X" }""", "")]
	[TestCase("""{ "op": "copy", "path": "properties[hair]", "changes": [ { "op": "set", "path": "id", "value": "hair2" } ] }""", "hair2")]
	public void OperationsNameTheItemsTheyWorkOn(string operation, string ids)
	{
		Assert.That(string.Join(",", ModOperations.GetItemIds((JsonObject) JsonNode.Parse(operation)!)), Is.EqualTo(ids));
	}

	[Test]
	public void FormFieldsChangeTheJson()
	{
		var gameData = (JsonObject) JsonNode.Parse("""{ "quarterKinds": [ { "id": "square" }, { "id": "lane" } ] }""")!;
		var piece = ModPiece.Item("quarters", JsonNode.Parse("""{ "id": "q", "kind": "square" }"""));
		JsonNode? changed = null;

		var form = FormViewModel.Create(piece, new IdCatalog(gameData, []), isReadOnly: false, x => changed = x.Value)!;
		var root = (ObjectFieldViewModel) form.Root;
		var kind = (TextFieldViewModel) root.Fields.Single(x => x.Label == "kind");
		kind.Text = "lane";

		Assert.That(kind.Suggestions, Is.EqualTo(new[] { "square", "lane" }));
		Assert.That(changed?["kind"]?.GetValue<string>(), Is.EqualTo("lane"));
		Assert.That(kind.Warning, Is.Null);

		kind.Text = "nowhere";
		Assert.That(kind.Warning, Is.Not.Null);
	}

	[Test]
	public void ConditionValuesSuggestTheValuesOfTheirProperty()
	{
		var gameData = (JsonObject) JsonNode.Parse("""
			{ "properties": [ { "id": "hair", "values": [ { "id": "red", "groups": [ "bright" ] }, { "id": "brown" } ] } ] }
			""")!;
		var piece = ModPiece.Item("professions", JsonNode.Parse("""{ "id": "smith", "when": { "values": { "hair": [ "red" ] } } }"""));

		var form = FormViewModel.Create(piece, new IdCatalog(gameData, []), isReadOnly: false, _ => { })!;
		var when = (ObjectFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "when");
		var values = (MapFieldViewModel) when.Fields.Single(x => x.Label == "values");
		var list = (ListFieldViewModel) values.Entries.Single().Field;
		var item = (TextFieldViewModel) list.Items.Single().Field;

		Assert.That(values.Entries.Single().KeySuggestions, Does.Contain("hair"));
		Assert.That(item.Suggestions, Is.EqualTo(new[] { "red", "bright", "brown" }));
	}

	[Test]
	public void OptionalFieldsLeftEmptyAreRemoved()
	{
		var piece = ModPiece.Item("professions", JsonNode.Parse("""{ "id": "smith", "role": "homemaker" }"""));
		JsonNode? changed = null;

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, x => changed = x.Value)!;
		((TextFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "role")).Text = "";

		Assert.That(((JsonObject) changed!).ContainsKey("role"), Is.False);
	}

	[Test]
	public void OperationValuesAreEditedAsTheDataTheirPathLeadsTo()
	{
		var piece = ModPiece.Operation(JsonNode.Parse("""{ "op": "replace", "path": "properties[hair]", "value": { "id": "hair" } }"""));

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;
		var operation = (OperationFieldViewModel) form.Root;

		Assert.That(operation.Fields.Select(x => x.Label), Is.EqualTo(new[] { "op", "path", "value" }));
		Assert.That(operation.Fields[2], Is.InstanceOf<ObjectFieldViewModel>());

		((ChoiceFieldViewModel) operation.Fields[0]).Selected = "remove";
		Assert.That(operation.Fields.Select(x => x.Label), Is.EqualTo(new[] { "op", "path", "value" }));
		Assert.That(operation.Fields[2], Is.InstanceOf<JsonFieldViewModel>(), "A value a remove doesn't take is shown as JSON, so it can be seen and removed.");
	}

	[Test]
	public void FieldsKnowThePathsOfTheirData()
	{
		var piece = ModPiece.Item("houses", JsonNode.Parse("""{ "id": "cottage", "description": { "short": { "text": "A", "ai": true }, "long": [ { "text": "B", "ai": true } ] } }"""));

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;
		var description = (ObjectFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "description");
		var shortText = (ObjectFieldViewModel) description.Fields.Single(x => x.Label == "short");
		var longText = (ListFieldViewModel) description.Fields.Single(x => x.Label == "long");

		Assert.That(description.DataPath, Is.EqualTo("houses[cottage].description"));
		Assert.That(shortText.Fields.Single(x => x.Label == "text").DataPath, Is.EqualTo("houses[cottage].description.short.text"));
		Assert.That(longText.Items.Single().Field.DataPath, Is.EqualTo("houses[cottage].description.long[0]"));
		Assert.That(longText.Items.Single().Field.Label, Is.EqualTo("1"));
	}

	[Test]
	public void EditorsLineUpWhateverTheirDepth()
	{
		var piece = ModPiece.Item("houses", JsonNode.Parse("""{ "id": "cottage", "description": { "short": { "text": "A", "ai": true } } }"""));

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;
		var description = (ObjectFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "description");
		var text = ((ObjectFieldViewModel) description.Fields.Single(x => x.Label == "short")).Fields.Single(x => x.Label == "text");

		Assert.That(description.LabelWidth - text.LabelWidth, Is.EqualTo(2 * FieldViewModel.IndentWidth));
	}

	[Test]
	public void OnlyOptionalPropertiesCanBeLeftOut()
	{
		var piece = ModPiece.Item("houses", JsonNode.Parse("""{ "id": "cottage", "description": { "short": { "text": "A", "ai": true }, "long": [ { "text": "B", "ai": true } ] } }"""));

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;
		var description = (ObjectFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "description");
		var listItem = (ObjectFieldViewModel) ((ListFieldViewModel) description.Fields.Single(x => x.Label == "long")).Items.Single().Field;

		Assert.That(listItem.IsOptional, Is.False, "A list item is removed with its list's button.");
		Assert.That(form.Root is ObjectFieldViewModel { IsOptional: false }, Is.True);
	}

	[Test]
	public void LongerPathsNarrowAReplacedValue()
	{
		var piece = ModPiece.Operation(JsonNode.Parse("""{ "op": "replace", "path": "houses[cottage]", "value": { "id": "cottage", "description": { "short": { "text": "A", "ai": true } }, "wealth": "low" } }"""));
		JsonNode? changed = null;

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, x => changed = x.Value)!;
		var path = (TextFieldViewModel) ((OperationFieldViewModel) form.Root).Fields.Single(x => x.Label == "path");
		path.Text = "houses[cottage].descr";
		Assert.That(changed?["value"]?["id"]?.GetValue<string>(), Is.EqualTo("cottage"), "A path that's still being typed leaves the value alone.");

		path.Text = "houses[cottage].description";
		Assert.That(JsonNode.DeepEquals(changed?["value"], JsonNode.Parse("""{ "short": { "text": "A", "ai": true } }""")), Is.True);
		Assert.That(((OperationFieldViewModel) form.Root).Fields.Single(x => x.Label == "value").DataPath, Is.EqualTo("houses[cottage].description"));
	}

	[Test]
	public void AnOperationsValueHasNoPathToCopyButItsFieldsDo()
	{
		var piece = ModPiece.Operation(JsonNode.Parse("""{ "op": "replace", "path": "houses[cottage].description", "value": { "short": { "text": "A", "ai": true } } }"""));

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;
		var value = (ObjectFieldViewModel) ((OperationFieldViewModel) form.Root).Fields.Single(x => x.Label == "value");

		Assert.That(value.HasCopyablePath, Is.False);
		Assert.That(value.Fields.Single(x => x.Label == "short").HasCopyablePath, Is.True);
		Assert.That(value.Fields.Single(x => x.Label == "short").DataPath, Is.EqualTo("houses[cottage].description.short"));
	}

	[TestCase("portraitColors[cloth].variants", true)]
	[TestCase("portraitColors[cloth].variants[0].when.values.profession", true)]
	[TestCase("properties", true)]
	[TestCase("game.title", true)]
	[TestCase("portraitColors[cloth].variantz", false)]
	[TestCase("nothing[here]", false)]
	public void OperationPathsAreCheckedAgainstTheShapeOfTheData(string path, bool isKnown)
	{
		var piece = ModPiece.Operation(new JsonObject { ["op"] = "remove", ["path"] = path });

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;
		var pathField = (TextFieldViewModel) ((OperationFieldViewModel) form.Root).Fields.Single(x => x.Label == "path");

		Assert.That(DataSchema.IsKnownPath(path), Is.EqualTo(isKnown));
		Assert.That(pathField.Warning is null, Is.EqualTo(isKnown));
	}

	[Test]
	public void TheValueIsKeptWhileItsPathIsRetyped()
	{
		var piece = ModPiece.Operation(JsonNode.Parse("""{ "op": "replace", "path": "houses[cottage]", "value": { "id": "cottage", "description": { "short": { "text": "A", "ai": true } }, "wealth": "low" } }"""));
		JsonNode? changed = null;

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, x => changed = x.Value)!;
		var path = (TextFieldViewModel) ((OperationFieldViewModel) form.Root).Fields.Single(x => x.Label == "path");
		path.Text = "houses[cottage].description";
		path.Text = "houses[cottage].wealth";
		Assert.That(changed?["value"]?.GetValue<string>(), Is.EqualTo("low"), "A different part of the original value is found, not of the narrowed one.");

		path.Text = "houses[cot";
		path.Text = "houses[cottage]";
		Assert.That(changed?["value"]?["id"]?.GetValue<string>(), Is.EqualTo("cottage"), "The original value comes back once the path is.");
	}

	[Test]
	public void EditingTheValueIsWhatALaterPathStartsFrom()
	{
		var piece = ModPiece.Operation(JsonNode.Parse("""{ "op": "replace", "path": "game.wrong", "value": 5 }"""));
		JsonNode? changed = null;

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, x => changed = x.Value)!;
		var operation = (OperationFieldViewModel) form.Root;
		((JsonFieldViewModel) operation.Fields.Single(x => x.Label == "value")).Text = "7";
		((TextFieldViewModel) operation.Fields.Single(x => x.Label == "path")).Text = "game.right";

		Assert.That(changed?["value"]?.GetValue<int>(), Is.EqualTo(7));
	}

	[Test]
	public void PathSuggestionsFollowWhatsTyped()
	{
		var gameData = (JsonObject) JsonNode.Parse("""{ "families": { "head": { "femaleChance": 10 } }, "properties": [ { "id": "hair" } ] }""")!;
		var catalog = new IdCatalog(gameData, []);

		Assert.That(catalog.GetPathSuggestions("fam"), Does.Contain("families").And.Contain("properties"));
		Assert.That(catalog.GetPathSuggestions("families.head.fem"), Does.Contain("families.head.femaleChance"));
		Assert.That(catalog.GetPathSuggestions("properties[h"), Is.EqualTo(new[] { "properties[hair]" }));
		Assert.That(catalog.GetPathSuggestions("properties[hair].v"), Does.Contain("properties[hair].values"), "Properties the game reads are offered even if the data doesn't have them.");
	}

	[Test]
	public void ListItemsMoveUpAndDown()
	{
		var piece = ModPiece.Item("properties", JsonNode.Parse("""{ "id": "hair", "values": [ { "id": "a" }, { "id": "b" }, { "id": "c" } ] }"""));
		JsonNode? changed = null;

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, x => changed = x.Value)!;
		ListFieldViewModel Values() => (ListFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "values");
		IEnumerable<string?> Ids() => ((JsonArray) changed!["values"]!).Select(ModJson.GetId);

		Assert.That(Values().Items[0].CanMoveUp(), Is.False);
		Assert.That(Values().Items[2].CanMoveDown(), Is.False);

		Values().Items[0].MoveDown();
		Assert.That(Ids(), Is.EqualTo(new[] { "b", "a", "c" }));

		Values().Items[2].MoveUp();
		Assert.That(Ids(), Is.EqualTo(new[] { "b", "c", "a" }));
		Assert.That(Values().Items.Select(x => x.Field.Label), Is.EqualTo(new[] { "1", "2", "3" }), "Items are numbered by their new places.");
	}

	[Test]
	public void ReadOnlyListItemsDontMove()
	{
		var piece = ModPiece.Item("properties", JsonNode.Parse("""{ "id": "hair", "values": [ { "id": "a" }, { "id": "b" } ] }"""));

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: true, _ => { })!;
		var values = (ListFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "values");

		Assert.That(values.Items[0].CanMoveDown(), Is.False);
	}

	[Test]
	public void ChangesOnDiskAreTellableFromTheEditorsOwn()
	{
		var directory = Path.Combine(Path.GetTempPath(), "OperaEtDiesModEditorTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(directory, "Folder"));
		try
		{
			File.WriteAllText(Path.Combine(directory, "Folder", "A.json"), "{}");
			var project = ModProject.Load(directory);
			Assert.That(ModProject.Load(directory).TakeSnapshot().IsSameAs(project.SavedSnapshot), Is.True, "Nothing changed yet.");

			project.AddFile("B.json", "[]");
			project.Save();
			Assert.That(ModProject.Load(directory).TakeSnapshot().IsSameAs(project.SavedSnapshot), Is.True, "The editor's own save isn't a change on disk.");

			File.WriteAllText(Path.Combine(directory, "Folder", "A.json"), """{ "properties": [] }""");
			Assert.That(ModProject.Load(directory).TakeSnapshot().IsSameAs(project.SavedSnapshot), Is.False, "Another program's change is.");
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[Test]
	public void PiecesPutInAnOperationsFileSayWhatTheyAre()
	{
		var project = new ModProject(@"C:\Mods\Test", [("Ops.json", "[]")], []);

		var (_, error) = ModEditing.Insert(project, ModPiece.Item("houses", JsonNode.Parse("""{ "id": "cottage" }""")), new FileLocation("Ops.json"), after: true);

		Assert.That(error, Does.Contain("\"cottage\", an item of \"houses\""));
	}

	[Test]
	public void EveryGameDataItemHasAForm()
	{
		var data = DataDocument.Read(JsonDataLoader.DefaultDataDirectory, []).Value!.Root;
		var catalog = new IdCatalog(data, []);

		foreach (var (name, value) in data)
		{
			var pieces = value is JsonArray items ? items.Select(x => ModPiece.Item(name, x)) : [ModPiece.WholeSection(name, value)];
			foreach (var piece in pieces)
				Assert.That(FormViewModel.Create(piece, catalog, isReadOnly: true, _ => { }), Is.Not.Null, name);
		}
	}
}
