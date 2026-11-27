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
			Assert.That(DataSchema.GetFields(type).Any(x => x.PropertyName == property), Is.True, $"{type.Name}.{property}");
	}

	[Test]
	public void WhoSuggestsFamilyAlongWithTheProperties()
	{
		var gameData = (JsonObject) JsonNode.Parse("""{ "properties": [ { "id": "gender", "values": [ { "id": "female" } ] } ] }""")!;
		var catalog = new IdCatalog(gameData, []);

		// A step's who can also say whether someone is family.
		foreach (var name in new[] { "values", "notValues" })
		{
			var who = DataReferences.Find(DataSchema.GetFields(typeof(StepWho)).Single(x => x.Name == name))!;
			Assert.That(catalog.GetIds(who.Key!, null), Is.EqualTo(new[] { "gender", StepWho.FamilyKey }), name);
			Assert.That(catalog.GetIds(who.Value!, StepWho.FamilyKey), Is.EqualTo(new[] { StepWho.FamilyMember, StepWho.FamilyOther }), name);
			Assert.That(catalog.GetIds(who.Value!, "gender"), Is.EqualTo(new[] { "female" }), name);
		}

		// Other conditions only suggest the properties.
		var condition = DataReferences.Find(DataSchema.GetFields(typeof(CharacterCondition)).Single(x => x.Name == "values"))!;
		Assert.That(catalog.GetIds(condition.Key!, null), Is.EqualTo(new[] { "gender" }));
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
	public void PlacesAreChosenByNameOrAsAQuarter()
	{
		var gameData = (JsonObject) JsonNode.Parse("""{ "quarterKinds": [ { "id": "square" }, { "id": "lane" } ] }""")!;
		var piece = ModPiece.Item("needs", JsonNode.Parse("""{ "id": "n", "steps": [ { "step": "goTo", "place": "home" } ] }"""));
		JsonNode? changed = null;

		var form = FormViewModel.Create(piece, new IdCatalog(gameData, []), isReadOnly: false, x => changed = x.Value)!;
		var steps = (ListFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "steps");
		var place = (PlaceFieldViewModel) ((ObjectFieldViewModel) steps.Items[0].Field).Fields.Single(x => x.Label == "place");

		// The named places are offered, along with a quarter and a remembered place.
		Assert.That(place.Choices, Is.SupersetOf(new[] { "home", "workplace", "otherHome", "partnerHome", PlaceFieldViewModel.QuarterChoice, PlaceFieldViewModel.RememberedChoice }));
		Assert.That((place.Selected, place.Details), Is.EqualTo(("home", (ObjectFieldViewModel?) null)));

		place.Selected = "workplace";
		Assert.That(changed?["steps"]?[0]?["place"]?.GetValue<string>(), Is.EqualTo("workplace"));

		// A quarter is an object, whose kinds suggest the quarter kinds.
		place.Selected = PlaceFieldViewModel.QuarterChoice;
		Assert.That(changed?["steps"]?[0]?["place"], Is.InstanceOf<JsonObject>());
		var kinds = (ListFieldViewModel) place.Details!.Fields.Single(x => x.Label == "quarterKinds");
		kinds.Add();
		var kind = (TextFieldViewModel) ((ObjectFieldViewModel) kinds.Items[0].Field).Fields.Single(x => x.Label == "quarterKind");
		Assert.That(kind.Suggestions, Is.EqualTo(new[] { "square", "lane" }));
	}

	[Test]
	public void OutfitClothingIsChosenByIdOrAsOptions()
	{
		var gameData = (JsonObject) JsonNode.Parse("""{ "clothing": [ { "id": "tunic" }, { "id": "hood" } ] }""")!;
		var piece = ModPiece.Item("outfits", JsonNode.Parse("""{ "id": "o", "kind": "everyday", "clothing": [ "tunic" ] }"""));
		JsonNode? changed = null;

		var form = FormViewModel.Create(piece, new IdCatalog(gameData, []), isReadOnly: false, x => changed = x.Value)!;
		var clothing = (ListFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "clothing");
		var entry = (OutfitClothingFieldViewModel) clothing.Items[0].Field;

		// The clothing ids are offered, along with options.
		Assert.That(entry.Choices, Is.EqualTo(new[] { "tunic", "hood", OutfitClothingFieldViewModel.OptionsChoice }));
		Assert.That((entry.Selected, entry.Details), Is.EqualTo(("tunic", (ObjectFieldViewModel?) null)));

		// Options are an object, whose options suggest the clothing ids.
		entry.Selected = OutfitClothingFieldViewModel.OptionsChoice;
		Assert.That(changed?["clothing"]?[0]?["options"], Is.InstanceOf<JsonArray>());
		var options = (ListFieldViewModel) entry.Details!.Fields.Single(x => x.Label == "options");
		options.Add();
		var option = (TextFieldViewModel) ((ObjectFieldViewModel) options.Items[0].Field).Fields.Single(x => x.Label == "clothing");
		Assert.That(option.Suggestions, Is.EqualTo(new[] { "tunic", "hood" }));
	}

	[Test]
	public void ListItemsCanHaveAnItemAddedAfterThem()
	{
		var piece = ModPiece.Item("needs", JsonNode.Parse("""{ "id": "n", "steps": [ { "step": "goTo", "place": "home" }, { "step": "do", "minutes": 5 } ] }"""));
		JsonNode? changed = null;

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, x => changed = x.Value)!;
		var steps = (ListFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "steps");
		steps.Items[0].AddAfter();

		// The new item goes between the two, and starts empty.
		var list = (JsonArray) changed!["steps"]!;
		Assert.That(list.Select(x => x?["step"]?.GetValue<string>()), Is.EqualTo(new[] { "goTo", null, "do" }));
		Assert.That(steps.Items.Select(x => x.Index), Is.EqualTo(new[] { 0, 1, 2 }));
	}

	[Test]
	public void IndentLinesSayWhoseFieldsTheyAre()
	{
		var piece = ModPiece.Item("needs", JsonNode.Parse("""{ "id": "n", "steps": [ { "step": "repeat", "steps": [ { "step": "do", "minutes": 5 } ] } ] }"""));
		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;
		var steps = (ListFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "steps");
		var repeat = (ObjectFieldViewModel) steps.Items[0].Field;
		var repeated = (ListFieldViewModel) repeat.Fields.Single(x => x.Label == "steps");

		Assert.That((steps.IndentToolTip, repeat.IndentToolTip, repeated.IndentToolTip), Is.EqualTo(("needs[n].steps", "needs[n].steps[0]", "needs[n].steps[0].steps")));
	}

	[Test]
	public void OptionalPropertiesAreHiddenUntilAdded()
	{
		var piece = ModPiece.Item("needs", JsonNode.Parse("""{ "id": "n", "steps": [ { "step": "goTo", "place": "home" } ] }"""));
		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;
		ObjectFieldViewModel GetStep() => (ObjectFieldViewModel) ((ListFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "steps")).Items[0].Field;
		var step = GetStep();

		// Only what's set or required shows, and only what a "goTo" step allows can be added.
		Assert.That(step.VisibleFields.Select(x => x.Label), Is.EqualTo(new[] { "step", "place" }));
		Assert.That(step.AddableProperties.Select(x => x.Name), Does.Contain("when").And.Contain("player").And.Not.Contain("minutes").And.Not.Contain("withPartner"));

		// An added property shows at the end while it's unset, until the form is built again. That leaves just one that could be added,
		// which is shown instead of the list.
		foreach (var name in new[] { "id", "when", "hours", "onFail" })
			step.AddProperty(name);
		Assert.That(step.VisibleFields.Select(x => x.Label), Is.EqualTo(new[] { "step", "place", "id", "when", "hours", "onFail", "player" }));
		Assert.That(step.AddableProperties, Is.Empty);
		form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;
		Assert.That(GetStep().VisibleFields.Select(x => x.Label), Is.EqualTo(new[] { "step", "place" }));
	}

	[Test]
	public void AddedPropertiesGoLastInTheOrderTheyreAdded()
	{
		var piece = ModPiece.Item("needs", JsonNode.Parse("""{ "id": "n", "steps": [ { "step": "do", "minutes": 5 } ] }"""));
		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;
		var step = (ObjectFieldViewModel) ((ListFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "steps")).Items[0].Field;

		// Added after the fields already shown, where the list of properties to add is, even once they're set.
		step.AddProperty("atWork");
		step.AddProperty("activity");
		((TextFieldViewModel) step.Fields.Single(x => x.Label == "activity")).Text = "Resting";
		Assert.That(step.VisibleFields.Select(x => x.Label), Is.EqualTo(new[] { "step", "minutes", "atWork", "activity" }));
	}

	[Test]
	public void ChangingAStepsKindChangesWhatItShowsAndOffers()
	{
		var piece = ModPiece.Item("needs", JsonNode.Parse("""{ "id": "n", "steps": [ { "step": "goTo", "place": "home" } ] }"""));
		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;
		var step = (ObjectFieldViewModel) ((ListFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "steps")).Items[0].Field;

		// A "dress" step requires its outfit, and can change the partner too. The place it has, which it doesn't allow,
		// still shows so it can be removed.
		((ChoiceFieldViewModel) step.Fields.Single(x => x.Label == "step")).Selected = "dress";
		Assert.That(step.VisibleFields.Select(x => x.Label), Is.EqualTo(new[] { "step", "place", "outfit" }));
		Assert.That(step.AddableProperties.Select(x => x.Name), Does.Contain("withPartner").And.Not.Contain("place").And.Not.Contain("minutes"));
	}

	[Test]
	public void WhatANeedOffersDependsOnWhichSettingsItHas()
	{
		var piece = ModPiece.Item("needs", JsonNode.Parse("""{ "id": "n", "steps": [ { "step": "do" } ], "quota": { "lead": 30, "time": { "minutes": 60, "shift": 60 } } }"""));
		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;
		var need = (ObjectFieldViewModel) form.Root;
		var quota = (ObjectFieldViewModel) need.Fields.Single(x => x.Label == "quota");

		// A quota need with steps requires hours, and can't have a level or a place. A quota in time can't be in runs too,
		// and a daily one can't be overdue.
		Assert.That(need.VisibleFields.Select(x => x.Label), Does.Contain("hours"));
		Assert.That(need.AddableProperties.Select(x => x.Name), Does.Not.Contain("level").And.Not.Contain("place").And.Not.Contain("hours"));
		Assert.That(quota.AddableProperties.Select(x => x.Name), Does.Contain("period").And.Not.Contain("runs").And.Not.Contain("overdueUrgency"));
		((ChoiceFieldViewModel) quota.Fields.Single(x => x.Label == "period")).Selected = "week";
		quota.Refresh();
		Assert.That(quota.AddableProperties.Select(x => x.Name), Does.Contain("overdueUrgency"));

		// The hours of a quota need can have days.
		var hours = (ObjectFieldViewModel) need.Fields.Single(x => x.Label == "hours");
		hours.Add();
		Assert.That(hours.AddableProperties.Select(x => x.Name), Does.Contain("days").And.Contain("onSpecialDays"));
	}

	[Test]
	public void ACharacterPropertysRoleSettingsDependOnItsType()
	{
		var piece = ModPiece.Item("properties", JsonNode.Parse("""{ "id": "age", "title": "Age", "type": "integer", "player": {} }"""));
		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;
		var player = (ObjectFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "player");

		Assert.That(player.AddableProperties, Is.Empty);
		Assert.That(player.VisibleFields.Select(x => x.Label), Is.EqualTo(new[] { "range" }));
	}

	[Test]
	public void ShowingAllPropertiesShowsWhatsAllowed()
	{
		var piece = ModPiece.Item("needs", JsonNode.Parse("""{ "id": "n", "steps": [ { "step": "do", "minutes": 5 } ] }"""));
		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: true, _ => { })!;
		var step = (ObjectFieldViewModel) ((ListFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "steps")).Items[0].Field;

		// Looking at data that can't be changed offers nothing to add.
		Assert.That(step.AddableProperties, Is.Empty);
		form.ShowAllProperties = true;
		Assert.That(step.VisibleFields.Select(x => x.Label), Does.Contain("activity").And.Contain("meetsQuota").And.Not.Contain("place").And.Not.Contain("outfit"));
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
	public void TheMainObjectAlwaysShowsItsComments()
	{
		var piece = ModPiece.Item("professions", JsonNode.Parse("""{ "id": "smith", "business": { "wealth": "high" } }"""));
		JsonNode? changed = null;

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, x => changed = x.Value)!;
		var root = (ObjectFieldViewModel) form.Root;
		var business = (ObjectFieldViewModel) root.Fields.Single(x => x.Label == "business");

		Assert.That(root.IsCommentsShown, Is.True);
		Assert.That(root.CanAddComment(), Is.False);
		Assert.That(business.IsCommentsShown, Is.False);

		root.Comments = "Works iron.";
		Assert.That(((JsonObject) changed!).First().Key, Is.EqualTo("comments"), "Comments are added at the start.");
		Assert.That(changed!["comments"]!.GetValue<string>(), Is.EqualTo("Works iron."));

		root.Comments = " ";
		Assert.That(((JsonObject) changed!).ContainsKey("comments"), Is.False);
		Assert.That(root.IsCommentsShown, Is.True);
	}

	[Test]
	public void NestedObjectsShowCommentsOnceTheyreAdded()
	{
		var piece = ModPiece.Item("professions", JsonNode.Parse("""{ "id": "smith", "business": { "wealth": "high" } }"""));
		JsonNode? changed = null;

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, x => changed = x.Value)!;
		var business = (ObjectFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "business");
		Assert.That(business.CanAddComment(), Is.True);
		Assert.That(business.HasMenu, Is.True);

		business.AddComment();
		Assert.That(business.IsCommentsShown, Is.True);
		business.Comments = "";
		Assert.That(business.IsCommentsShown, Is.True, "Emptying a nested object's comments doesn't take its box away.");
		business.Comments = "Smiths are well off.";
		Assert.That(changed!["business"]!["comments"]!.GetValue<string>(), Is.EqualTo("Smiths are well off."));

		business.RemoveComment();
		Assert.That(business.IsCommentsShown, Is.False);
		Assert.That(((JsonObject) changed!["business"]!).ContainsKey("comments"), Is.False);
	}

	[Test]
	public void CommentsArentUnknownProperties()
	{
		var piece = ModPiece.Item("professions", JsonNode.Parse("""{ "id": "smith", "comments": "Iron.", "business": { "comments": 3 } }"""));

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: true, _ => { })!;
		var root = (ObjectFieldViewModel) form.Root;
		var business = (ObjectFieldViewModel) root.Fields.Single(x => x.Label == "business");

		Assert.That(root.Fields.Any(x => x.IsUnknown), Is.False);
		Assert.That(root.Comments, Is.EqualTo("Iron."));
		Assert.That(business.Fields.Single(x => x.IsUnknown).Label, Is.EqualTo("comments"), "Comments that aren't text are shown so they can be fixed.");
		Assert.That(root.CanAddComment(), Is.False);
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

		Assert.That(operation.Fields.Select(x => x.Label), Is.EqualTo(new[] { "comments", "op", "path", "value" }));
		Assert.That(operation.Fields.Single(x => x.Label == "value"), Is.InstanceOf<ObjectFieldViewModel>());

		((ChoiceFieldViewModel) operation.Fields.Single(x => x.Label == "op")).Selected = "remove";
		Assert.That(operation.Fields.Select(x => x.Label), Is.EqualTo(new[] { "comments", "op", "path", "value" }));
		Assert.That(operation.Fields.Single(x => x.Label == "value"), Is.InstanceOf<JsonFieldViewModel>(), "A value a remove doesn't take is shown as JSON, so it can be seen and removed.");
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
		Assert.That(longText.Items.Single().Field.Label, Is.EqualTo("0"));
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
		Assert.That(Values().Items.Select(x => x.Field.Label), Is.EqualTo(new[] { "0", "1", "2" }), "Items are numbered by their new places, from 0.");
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
	public void PortraitShapesCanUseTheirLayersColors()
	{
		var gameData = JsonNode.Parse("""
			{
				"portraitColors": [ { "id": "skin", "color": "#FFFFFF" } ],
				"clothing": [ { "id": "tunic", "colors": [ "main", "trim" ] }, { "id": "hat", "colors": "band" } ],
				"portraitShapeSets": [ { "id": "set", "shapes": [ { "fill": "shade" } ] }, { "id": "unused", "shapes": [ { "fill": "skin" } ] } ],
				"portraitLayers": [
					{ "id": "tunic", "clothing": [ "tunic" ], "colors": [ { "id": "shade", "color": "main" } ], "shapes": [ { "fill": "main" }, { "include": "set" } ] },
					{ "id": "hat", "clothing": "hat", "shapes": [ { "fill": "band" } ] }
				]
			}
			""")!.AsObject();
		var catalog = new IdCatalog(gameData, []);
		JsonObject Shape(string section, int index) => gameData[section]![index]!["shapes"]![0]!.AsObject();

		Assert.That(catalog.GetColorIds(Shape("portraitLayers", 0)), Is.EquivalentTo(new[] { "skin", "shade", "main", "trim" }));
		Assert.That(catalog.GetColorIds(gameData["portraitLayers"]![0]!["colors"]![0]!.AsObject()), Is.EquivalentTo(new[] { "skin", "shade", "main", "trim" }));
		Assert.That(catalog.GetColorIds(Shape("portraitLayers", 1)), Is.EquivalentTo(new[] { "skin", "band" }));
		Assert.That(catalog.GetColorIds(Shape("portraitShapeSets", 0)), Is.EquivalentTo(new[] { "skin", "shade", "main", "trim" }));
		Assert.That(catalog.GetColorIds(Shape("portraitShapeSets", 1)), Is.EquivalentTo(new[] { "skin" }));
		Assert.That(catalog.GetColorIds(gameData["portraitColors"]![0]!.AsObject()), Is.EquivalentTo(new[] { "skin" }));

		// A form edits a copy of the item, outside of its section.
		var copy = gameData["portraitShapeSets"]![0]!.DeepClone().AsObject();
		Assert.That(catalog.GetColorIds(copy["shapes"]![0]!.AsObject(), "portraitShapeSets"), Is.EquivalentTo(new[] { "skin", "shade", "main", "trim" }));
	}

	[Test]
	public void OperationsOnShapeSetsUseTheirLayersColors()
	{
		var gameData = JsonNode.Parse("""
			{
				"portraitColors": [ { "id": "skin", "color": "#FFFFFF" } ],
				"portraitShapeSets": [ { "id": "set", "shapes": [ { "fill": "shade" } ] } ],
				"portraitLayers": [ { "id": "tunic", "colors": [ { "id": "shade", "color": "#000000" } ], "shapes": [ { "include": "set" } ] } ]
			}
			""")!.AsObject();
		var catalog = new IdCatalog(gameData, []);
		TextFieldViewModel Value(string operation)
		{
			var form = FormViewModel.Create(ModPiece.Operation(JsonNode.Parse(operation)), catalog, isReadOnly: true, _ => { })!;
			form.Root.Refresh();
			var value = ((OperationFieldViewModel) form.Root).Fields.Single(x => x.Label == "value");
			return value as TextFieldViewModel ?? ((ListFieldViewModel) ((ObjectFieldViewModel) value).Fields.Single(x => x.Label == "shapes")).Items.Select(x => (ObjectFieldViewModel) x.Field).SelectMany(x => x.Fields).OfType<TextFieldViewModel>().Single(x => x.Label == "fill");
		}

		// Replacing the whole shape set, and setting one of its shapes' fills on its own.
		var replaced = Value("""{ "op": "replace", "path": "portraitShapeSets[set]", "value": { "id": "set", "shapes": [ { "fill": "shade" } ] } }""");
		Assert.That(replaced.Warning, Is.Null);
		var fill = Value("""{ "op": "set", "path": "portraitShapeSets[set].shapes[0].fill", "value": "shade" }""");
		Assert.That(fill.Suggestions, Is.EquivalentTo(new[] { "skin", "shade" }));
		Assert.That(fill.Warning, Is.Null);
	}

	[Test]
	public void ShippedPortraitColorsAreAllKnown()
	{
		var data = DataDocument.Read(JsonDataLoader.DefaultDataDirectory, []).Value!.Root;
		var catalog = new IdCatalog(data, []);
		foreach (var section in new[] { "portraitLayers", "portraitShapeSets" })
		{
			foreach (var item in data[section]!.AsArray().OfType<JsonObject>())
			{
				var owners = (item["shapes"]?.AsArray() ?? []).Concat(item["colors"]?.AsArray() ?? []).OfType<JsonObject>();
				foreach (var owner in owners)
				{
					var known = catalog.GetColorIds(owner);
					foreach (var name in new[] { "fill", "fillTo", "stroke", "color", "mix" })
					{
						if (owner[name] is JsonValue value && value.GetValue<string>() is var color && !color.StartsWith('#'))
							Assert.That(known, Does.Contain(color), $"{section}[{item["id"]}].{name}");
					}
				}
			}
		}
	}

	[Test]
	public void TheShippedShapeSetsColorsAreKnownInTheirForm()
	{
		var data = DataDocument.Read(JsonDataLoader.DefaultDataDirectory, []).Value!.Root;
		var catalog = new IdCatalog(data, []);
		foreach (var set in data["portraitShapeSets"]!.AsArray())
		{
			var form = FormViewModel.Create(ModPiece.Item("portraitShapeSets", set), catalog, isReadOnly: true, _ => { })!;
			form.Root.Refresh();
			var shapes = (ListFieldViewModel) ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "shapes");
			var colors = shapes.Items.Select(x => (ObjectFieldViewModel) x.Field).SelectMany(x => x.Fields).OfType<TextFieldViewModel>().Where(x => x.Label is "fill" or "fillTo" or "stroke").ToList();
			Assert.That(colors.Where(x => x.Text.Length != 0), Is.Not.Empty);
			Assert.That(colors.Select(x => x.Warning), Is.All.Null, set!["id"]!.GetValue<string>());
		}
	}

	[Test]
	public void WealthAndFrequencySuggestionsIncludeTheirSteps()
	{
		Assert.That(DataReferences.GetTextChoices(typeof(ProfessionWealth)), Is.SupersetOf(new[] { "veryLow", "low++", "medium", "high-", "high--" }));
		Assert.That(DataReferences.GetTextChoices(typeof(OperaEtDies.Utility.Random.FrequencyLevel)), Is.SupersetOf(new[] { "common", "common-", "rare+", "veryRare--" }));

		// Every wealth in the shipped data is one of the suggestions.
		var data = DataDocument.Read(JsonDataLoader.DefaultDataDirectory, []).Value!.Root;
		var wealths = data["professions"]!.AsArray().Select(x => x?["business"]?["wealth"]?.GetValue<string>()).OfType<string>().ToList();
		Assert.That(wealths, Is.Not.Empty);
		Assert.That(DataReferences.GetTextChoices(typeof(ProfessionWealth)), Is.SupersetOf(wealths.Distinct()));
	}

	[Test]
	public void GameDataFieldsCanBeReplacedOrRemovedByTheMod()
	{
		var changes = new RecordingChanges();
		var piece = ModPiece.Item("quarterKinds", JsonNode.Parse("""{ "id": "square", "path": "lane" }"""));
		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: true, _ => { }, changes: changes)!;
		var path = ((ObjectFieldViewModel) form.Root).Fields.Single(x => x.Label == "path");

		Assert.That(path.CanReplace(), Is.True);
		path.Replace();
		path.Remove();
		Assert.That(changes.Calls, Is.EqualTo(new[] { "replace quarterKinds[square].path \"lane\"", "remove quarterKinds[square].path" }));

		// Without a mod open, the mod can't be changed.
		changes.CanChangeMod = false;
		Assert.That(path.CanReplace(), Is.False);
		Assert.That(path.CanRemove(), Is.False);
	}

	private sealed class RecordingChanges : IFormChanges
	{
		public IReadOnlyList<string> Calls => m_calls;

		public bool CanChangeMod { get; set; } = true;

		public ChangeKind? GetChange(string path) => null;

		public ModLocation? GetSource(string path) => null;

		public void GoTo(ModLocation location)
		{
		}

		public void Replace(string path, JsonNode? value) => m_calls.Add($"replace {path} {value?.ToJsonString()}");

		public void Remove(string path) => m_calls.Add($"remove {path}");

		private readonly System.Collections.ObjectModel.Collection<string> m_calls = [];
	}

	[Test]
	public void OperationsHaveComments()
	{
		JsonNode? changed = null;
		var form = FormViewModel.Create(ModPiece.Operation(JsonNode.Parse("""{ "op": "remove", "path": "properties[hair]" }""")), new IdCatalog(null, []), isReadOnly: false, x => changed = x.Value)!;
		var comments = (TextFieldViewModel) ((OperationFieldViewModel) form.Root).Fields.Single(x => x.Label == "comments");

		comments.Text = "Nobody has hair.";
		Assert.That(changed?["comments"]?.GetValue<string>(), Is.EqualTo("Nobody has hair."));
		comments.Text = "";
		Assert.That(changed?.AsObject().ContainsKey("comments"), Is.False);
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
