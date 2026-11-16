using System.Text.Json.Nodes;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using ModEditor.Model;
using ModEditor.ViewModels.Form;
using ModEditor.Views.Help;

namespace OperaEtDies.Test.Tools;

/// <summary>
/// Draws the help's documents as the help window does, and checks that everything a help request can go to is there.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class ModEditorHelpRenderingTests
{
	[Test]
	public void EveryHeadingAndRowCanBeGoneTo()
	{
		foreach (var document in HelpLibrary.Documents)
		{
			var rendered = MarkdownRenderer.Render(document, _ => { });

			Assert.That(rendered.Document.Blocks, Is.Not.Empty, document.Name);
			Assert.That(document.Headings.Where(x => !rendered.Headings.ContainsKey(x.Text)).Select(x => x.Text), Is.Empty, document.Name);
		}

		var dataFiles = MarkdownRenderer.Render(HelpLibrary.Find(HelpLibrary.DataFiles)!, _ => { });
		Assert.That(dataFiles.Rows.ContainsKey(("Character Property", "follows")), Is.True);
		Assert.That(dataFiles.Rows.ContainsKey(("Files and Sections", "seasons")), Is.True, "A row naming several properties can be gone to by each.");
	}

	[Test]
	public void EveryLinkBetweenTheDocumentsLeadsToAHeading()
	{
		var broken = new List<string>();
		foreach (var document in HelpLibrary.Documents)
		{
			foreach (var link in document.Markdown.Descendants<LinkInline>().Where(x => !x.IsImage))
			{
				var url = link.Url ?? "";
				if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
					continue;
				var hash = url.IndexOf('#', StringComparison.Ordinal);
				var name = hash < 0 ? url : url[..hash];
				var target = name.Length == 0 ? document : HelpLibrary.Find(name);

				// Links to documents that aren't in the help (such as CommandLine.md) can't be followed in it.
				if (target is null && name.Length != 0 && !name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
					broken.Add($"{document.Name}: {url}");
				else if (target is not null && hash >= 0 && target.FindAnchor(url[(hash + 1)..]) is null)
					broken.Add($"{document.Name}: {url}");
			}
		}

		Assert.That(broken, Is.Empty);
	}

	[Test]
	public void FieldsKnowWhereTheirHelpIs()
	{
		var piece = ModPiece.Item("properties", JsonNode.Parse("""{ "id": "hair", "follows": "eyes", "when": { "values": { "gender": [ "male" ] } } }"""));
		HelpTarget? shown = null;

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { }, x => shown = x)!;
		var root = (ObjectFieldViewModel) form.Root;
		var follows = root.Fields.Single(x => x.Label == "follows");
		var values = ((ObjectFieldViewModel) root.Fields.Single(x => x.Label == "when")).Fields.Single(x => x.Label == "values");

		Assert.That(follows.Help, Is.EqualTo(new HelpTarget(HelpLibrary.DataFiles, "Character Property", "follows")));
		Assert.That(values.Help, Is.EqualTo(new HelpTarget(HelpLibrary.DataFiles, "Condition", "values")));
		Assert.That(follows.ToolTipText, Does.StartWith("string — Optional.").And.Contain("Path: properties[hair].follows"));

		follows.ShowHelp();
		Assert.That(shown, Is.EqualTo(follows.Help));
	}

	[Test]
	public void OperationFieldsHaveTheOperationsHelp()
	{
		var piece = ModPiece.Operation(JsonNode.Parse("""{ "op": "remove", "path": "properties[hair]" }"""));

		var form = FormViewModel.Create(piece, new IdCatalog(null, []), isReadOnly: false, _ => { })!;

		Assert.That(((OperationFieldViewModel) form.Root).Fields.Select(x => x.Help?.Row), Is.EqualTo(new[] { "op", "path" }));
		Assert.That(((OperationFieldViewModel) form.Root).Fields.All(x => x.Help?.Document == HelpLibrary.Mods), Is.True);
	}
}
