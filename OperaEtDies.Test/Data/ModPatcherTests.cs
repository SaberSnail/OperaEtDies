using System.Text.Json.Nodes;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class ModPatcherTests
{
	private const string Data = """
		{
			"items": [
				{ "id": "a", "title": "A", "rules": [ { "x": 1 }, { "x": 2 } ] },
				{ "id": "b", "title": "B" },
				{ "id": "c", "title": "C" }
			],
			"title": "Game",
			"map": { "key": "value" }
		}
		""";

	[Test]
	public void InsertsAtEndByDefault()
	{
		var result = Apply("""[ { "op": "insert", "path": "items", "value": { "id": "d" } } ]""");

		Assert.That(GetIds(result), Is.EqualTo(new[] { "a", "b", "c", "d" }));
	}

	[TestCase("\"index\": 0", new[] { "d", "a", "b", "c" })]
	[TestCase("\"index\": 3", new[] { "a", "b", "c", "d" })]
	[TestCase("\"before\": \"b\"", new[] { "a", "d", "b", "c" })]
	[TestCase("\"after\": \"b\"", new[] { "a", "b", "d", "c" })]
	public void InsertsAtPosition(string position, string[] expected)
	{
		var result = Apply($$"""[ { "op": "insert", "path": "items", "value": { "id": "d" }, {{position}} } ]""");

		Assert.That(GetIds(result), Is.EqualTo(expected));
	}

	[TestCase("", new[] { "a", "b", "c", "d", "e" })]
	[TestCase(", \"index\": 0", new[] { "d", "e", "a", "b", "c" })]
	[TestCase(", \"before\": \"b\"", new[] { "a", "d", "e", "b", "c" })]
	[TestCase(", \"after\": \"b\"", new[] { "a", "b", "d", "e", "c" })]
	public void InsertsListOfValues(string position, string[] expected)
	{
		var result = Apply($$"""[ { "op": "insert", "path": "items", "values": [ { "id": "d" }, { "id": "e" } ]{{position}} } ]""");

		Assert.That(GetIds(result), Is.EqualTo(expected));
	}

	[Test]
	public void InsertsEmptyListOfValues()
	{
		var result = Apply("""[ { "op": "insert", "path": "items[b].rules", "values": [] } ]""");

		Assert.That(GetIds(result), Is.EqualTo(new[] { "a", "b", "c" }));
		Assert.That(result["items"]![1]!["rules"]!.AsArray(), Is.Empty);
	}

	[Test]
	public void InsertsIntoNestedListById()
	{
		var result = Apply("""[ { "op": "insert", "path": "items[a].rules", "index": 1, "value": { "x": 9 } } ]""");

		Assert.That(result["items"]![0]!["rules"]!.AsArray().Select(x => (int) x!["x"]!), Is.EqualTo(new[] { 1, 9, 2 }));
	}

	[Test]
	public void InsertCreatesMissingList()
	{
		var result = Apply("""[ { "op": "insert", "path": "items[b].rules", "value": { "x": 9 } } ]""");

		Assert.That(result["items"]![1]!["rules"]!.AsArray(), Has.Count.EqualTo(1));
	}

	[Test]
	public void ReplacesListItemsAndProperties()
	{
		var result = Apply("""
			[
				{ "op": "replace", "path": "items[b]", "value": { "id": "b2" } },
				{ "op": "replace", "path": "items[a].rules", "value": [] },
				{ "op": "replace", "path": "items[c].Title", "value": "New" }
			]
			""");

		Assert.That(GetIds(result), Is.EqualTo(new[] { "a", "b2", "c" }));
		Assert.That(result["items"]![0]!["rules"]!.AsArray(), Is.Empty);
		Assert.That((string) result["items"]![2]!["title"]!, Is.EqualTo("New"));
	}

	[Test]
	public void ReplaceNeedsExistingTarget()
	{
		Assert.That(GetErrors("""[ { "op": "replace", "path": "items[a].missing", "value": 1 } ]"""), Has.Count.EqualTo(1));
	}

	[Test]
	public void SetsNewAndExistingProperties()
	{
		var result = Apply("""
			[
				{ "op": "set", "path": "items[b].when", "value": { "y": true } },
				{ "op": "set", "path": "map.key", "value": "other" }
			]
			""");

		Assert.That((bool) result["items"]![1]!["when"]!["y"]!, Is.True);
		Assert.That((string) result["map"]!["key"]!, Is.EqualTo("other"));
	}

	[Test]
	public void SetOnlyChangesObjectProperties()
	{
		Assert.That(GetErrors("""[ { "op": "set", "path": "items[0]", "value": {} } ]"""), Has.Count.EqualTo(1));
	}

	[Test]
	public void RemovesItemsByIdAndIndexAndProperties()
	{
		var result = Apply("""
			[
				{ "op": "remove", "path": "items[b]" },
				{ "op": "remove", "path": "items[a].rules[0]" },
				{ "op": "remove", "path": "map.key" }
			]
			""");

		Assert.That(GetIds(result), Is.EqualTo(new[] { "a", "c" }));
		Assert.That(result["items"]![0]!["rules"]!.AsArray(), Has.Count.EqualTo(1));
		Assert.That(result["map"]!.AsObject(), Is.Empty);
	}

	[TestCase("\"index\": 0", new[] { "c", "a", "b" })]
	[TestCase("\"index\": 2", new[] { "a", "b", "c" })]
	[TestCase("\"before\": \"b\"", new[] { "a", "c", "b" })]
	[TestCase("\"after\": \"a\"", new[] { "a", "c", "b" })]
	public void MovesItems(string position, string[] expected)
	{
		var result = Apply($$"""[ { "op": "move", "path": "items[c]", {{position}} } ]""");

		Assert.That(GetIds(result), Is.EqualTo(expected));
	}

	[Test]
	public void CopiesItemAfterOriginalByDefault()
	{
		var result = Apply("""[ { "op": "copy", "path": "items[a].rules[0]" } ]""");

		Assert.That(GetRules(result), Is.EqualTo(new[] { 1, 1, 2 }));
	}

	[TestCase("\"index\": 0", new[] { "d", "a", "b", "c" })]
	[TestCase("\"index\": 3", new[] { "a", "b", "c", "d" })]
	[TestCase("\"before\": \"b\"", new[] { "a", "d", "b", "c" })]
	[TestCase("\"after\": \"c\"", new[] { "a", "b", "c", "d" })]
	public void CopiesItemToPosition(string position, string[] expected)
	{
		var result = Apply($$"""[ { "op": "copy", "path": "items[b]", "changes": [ { "op": "set", "path": "id", "value": "d" } ], {{position}} } ]""");

		Assert.That(GetIds(result), Is.EqualTo(expected));
	}

	[Test]
	public void AppliesChangesToCopyOnly()
	{
		var result = Apply("""
			[
				{
					"op": "copy",
					"path": "items[a]",
					"changes": [
						{ "op": "set", "path": "id", "value": "a2" },
						{ "op": "set", "path": "rules[1].x", "value": 9 },
						{ "op": "copy", "path": "rules[0]", "changes": [ { "op": "set", "path": "x", "value": 5 } ] }
					]
				}
			]
			""");

		Assert.That(GetIds(result), Is.EqualTo(new[] { "a", "a2", "b", "c" }));
		Assert.That(GetRules(result), Is.EqualTo(new[] { 1, 2 }));
		Assert.That(result["items"]![1]!["rules"]!.AsArray().Select(x => (int) x!["x"]!), Is.EqualTo(new[] { 1, 5, 9 }));
		Assert.That((string) result["items"]![1]!["title"]!, Is.EqualTo("A"));
	}

	[Test]
	public void ReportsEveryProblemInChangesAndSkipsCopy()
	{
		var root = JsonNode.Parse(Data)!;
		var errors = ModPatcher.Apply(root, JsonNode.Parse("""
			[
				{
					"op": "copy",
					"path": "items[a]",
					"changes": [
						{ "op": "remove", "path": "missing" },
						{ "op": "set", "path": "id", "value": "a2" },
						{ "op": "remove", "path": "other" }
					]
				}
			]
			"""), []);

		Assert.That(errors, Has.Count.EqualTo(2));
		Assert.That(errors[0], Does.Contain("1.1"));
		Assert.That(errors[1], Does.Contain("1.3"));
		Assert.That(GetIds(root), Is.EqualTo(new[] { "a", "b", "c" }));
	}

	[TestCase("""[ { "op": "copy", "path": "items[a]" } ]""")]
	[TestCase("""[ { "op": "copy", "path": "items[a]", "changes": [ { "op": "set", "path": "id", "value": "b" } ] } ]""")]
	[TestCase("""[ { "op": "copy", "path": "items[b]", "changes": {} } ]""")]
	[TestCase("""[ { "op": "copy", "path": "map.key" } ]""")]
	[TestCase("""[ { "op": "copy", "path": "items[z]" } ]""")]
	[TestCase("""[ { "op": "copy", "path": "items[a].rules[0]", "index": 0, "after": "a" } ]""")]
	[TestCase("""[ { "op": "set", "path": "map.key", "value": 1, "changes": [] } ]""")]
	[TestCase("""[ { "op": "move", "path": "items[c]" } ]""")]
	[TestCase("""[ { "op": "move", "path": "items[c]", "before": "c" } ]""")]
	[TestCase("""[ { "op": "move", "path": "map.key", "index": 0 } ]""")]
	[TestCase("""[ { "op": "insert", "path": "items", "value": 1, "index": 0, "after": "a" } ]""")]
	[TestCase("""[ { "op": "insert", "path": "items", "value": 1, "index": 4 } ]""")]
	[TestCase("""[ { "op": "insert", "path": "items", "value": 1, "before": "z" } ]""")]
	[TestCase("""[ { "op": "insert", "path": "map", "value": 1 } ]""")]
	[TestCase("""[ { "op": "insert", "path": "items" } ]""")]
	[TestCase("""[ { "op": "insert", "path": "items", "value": 1, "values": [ 2 ] } ]""")]
	[TestCase("""[ { "op": "insert", "path": "items", "values": 1 } ]""")]
	[TestCase("""[ { "op": "insert", "path": "items", "values": null } ]""")]
	[TestCase("""[ { "op": "replace", "path": "items[a]", "values": [ 1 ] } ]""")]
	[TestCase("""[ { "op": "set", "path": "map.key", "value": 1, "values": [ 1 ] } ]""")]
	[TestCase("""[ { "op": "remove", "path": "items[z]" } ]""")]
	[TestCase("""[ { "op": "remove", "path": "items.a" } ]""")]
	[TestCase("""[ { "op": "remove", "path": "title.a" } ]""")]
	[TestCase("""[ { "op": "remove", "path": "items[" } ]""")]
	[TestCase("""[ { "op": "delete", "path": "items" } ]""")]
	[TestCase("""[ { "path": "items" } ]""")]
	[TestCase("""[ { "op": "remove" } ]""")]
	[TestCase("""[ 1 ]""")]
	[TestCase("""{ "op": "remove", "path": "items" }""")]
	public void ReportsInvalidOperations(string mod)
	{
		var errors = GetErrors(mod);

		Assert.That(errors, Has.Count.EqualTo(1));
		Assert.That(errors[0], Is.Not.Empty);
	}

	[Test]
	public void ReportsEveryProblem()
	{
		Assert.That(GetErrors("""[ { "op": "remove", "path": "x" }, { "op": "remove", "path": "items[a]" }, { "op": "remove", "path": "y" } ]"""), Has.Count.EqualTo(2));
	}

	[Test]
	public void ProtectedPropertiesCantChange()
	{
		var root = JsonNode.Parse(Data)!;
		var errors = ModPatcher.Apply(root, JsonNode.Parse("""[ { "op": "set", "path": "Title", "value": "Other" } ]"""), ["title"]);

		Assert.That(errors, Has.Count.EqualTo(1));
		Assert.That((string) root["title"]!, Is.EqualTo("Game"));
	}

	[Test]
	public void OperationNamesIgnoreCase()
	{
		var result = Apply("""[ { "Op": "Remove", "Path": "items[a]" } ]""");

		Assert.That(GetIds(result), Is.EqualTo(new[] { "b", "c" }));
	}

	private static JsonNode Apply(string mod)
	{
		var root = JsonNode.Parse(Data)!;
		var errors = ModPatcher.Apply(root, JsonNode.Parse(mod), []);
		Assert.That(errors, Is.Empty);
		return root;
	}

	private static IReadOnlyList<string> GetErrors(string mod) => ModPatcher.Apply(JsonNode.Parse(Data)!, JsonNode.Parse(mod), []);

	private static IEnumerable<string> GetIds(JsonNode root) => root["items"]!.AsArray().Select(x => (string) x!["id"]!);

	private static IEnumerable<int> GetRules(JsonNode root) => root["items"]![0]!["rules"]!.AsArray().Select(x => (int) x!["x"]!);
}
