using System.Text.Json.Nodes;
using PortraitEditor.Model;

namespace OperaEtDies.Test.Tools;

[TestFixture]
public sealed class JsonFormatterTests
{
	[Test]
	public void ObjectsWithoutListsOfObjectsAreOnOneLine()
	{
		var shape = JsonNode.Parse("""{"path":"M0,0 H400","fill":"skin","when":{"values":{"gender":["gender_female"]}},"opacity":0.5}""");
		Assert.That(JsonFormatter.Format(shape), Is.EqualTo("""{ "path": "M0,0 H400", "fill": "skin", "when": { "values": { "gender": [ "gender_female" ] } }, "opacity": 0.5 }"""));
	}

	[Test]
	public void ListsOfObjectsAreIndentedWithTabs()
	{
		var layer = JsonNode.Parse("""{"id":"hair","shapes":[{"path":"M0,0"},{"path":"M1,1"}],"at":{}}""");
		var expected = "{\r\n\t\"id\": \"hair\",\r\n\t\"shapes\": [\r\n\t\t{ \"path\": \"M0,0\" },\r\n\t\t{ \"path\": \"M1,1\" }\r\n\t],\r\n\t\"at\": {}\r\n}";
		Assert.That(JsonFormatter.Format(layer), Is.EqualTo(expected));
	}
}
