using System.IO;
using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ModEditor.Model;

/// <summary>
/// A place in the help: a document, a heading in it, and optionally a row of the property table under that heading
/// (named by the property in its first column).
/// </summary>
public sealed record HelpTarget(string Document, string Heading, string? Row = null);

/// <summary>
/// A heading in a help document. A paragraph that's only bold text, such as "**Operation**", counts as a heading too.
/// </summary>
public sealed record HelpHeading(string Text, int Level, string Anchor);

/// <summary>
/// One of the documents shown in the help, read as Markdown, with its headings and the rows of its property tables.
/// </summary>
public sealed class HelpDocument
{
	public HelpDocument(string name, string title, string markdown)
	{
		Name = name;
		Title = title;
		Markdown = Markdig.Markdown.Parse(markdown, Pipeline);

		var headings = new List<HelpHeading>();
		string? heading = null;
		foreach (var block in Markdown)
		{
			if (GetHeading(block) is { } found)
			{
				heading = found.Text;
				headings.Add(found);
			}
			else if (block is Table table && heading is not null)
			{
				foreach (var row in table.OfType<TableRow>().Where(x => !x.IsHeader))
				{
					var cells = row.OfType<TableCell>().ToList();
					var text = string.Join(" — ", cells.Skip(1).Select(GetText).Where(x => x.Length != 0));
					foreach (var rowName in GetRowNames(row))
						m_rows.TryAdd((heading, rowName), text);
				}
			}
		}

		m_headings = headings;
	}

	/// <summary>
	/// How the help's Markdown is read: CommonMark, with tables.
	/// </summary>
	public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder().UsePipeTables().Build();

	/// <summary>
	/// The file name, such as "DataFiles.md", which links between documents use.
	/// </summary>
	public string Name { get; }

	public string Title { get; }

	public MarkdownDocument Markdown { get; }

	public IReadOnlyList<HelpHeading> Headings => m_headings;

	public HelpHeading? FindHeading(string text) => m_headings.FirstOrDefault(x => x.Text == text);

	public HelpHeading? FindAnchor(string anchor) => m_headings.FirstOrDefault(x => x.Anchor == anchor);

	public bool HasRow(string heading, string row) => m_rows.ContainsKey((heading, row));

	/// <summary>
	/// Gets the rest of a property's row (its type and notes), or null if the heading has no row for it.
	/// </summary>
	public string? GetRowText(string heading, string row) => m_rows.GetValueOrDefault((heading, row));

	/// <summary>
	/// Gets a block's heading, if it's a heading or a paragraph that's only bold text.
	/// </summary>
	public static HelpHeading? GetHeading(Block block)
	{
		switch (block)
		{
		case HeadingBlock heading:
			var text = GetText(heading.Inline);
			return new HelpHeading(text, heading.Level, MakeAnchor(text));
		case ParagraphBlock { Inline: { } inline } when inline.Count() == 1 && inline.FirstChild is EmphasisInline { DelimiterCount: 2 } bold:
			var boldText = GetText(bold).TrimEnd('.');
			return new HelpHeading(boldText, 5, MakeAnchor(boldText));
		default:
			return null;
		}
	}

	/// <summary>
	/// Gets the names in the first cell of a table row: the code spans in it, such as `days`, `weeks`, `seasons`.
	/// </summary>
	public static IReadOnlyList<string> GetRowNames(TableRow row) =>
		row.FirstOrDefault() is TableCell cell
			? cell.Descendants<CodeInline>().Select(x => x.Content).ToList()
			: [];

	/// <summary>
	/// Makes the anchor a link uses to find a heading, as GitHub does: lower case, with spaces as hyphens, and without
	/// punctuation.
	/// </summary>
	public static string MakeAnchor(string text)
	{
		var anchor = new StringBuilder();
		foreach (var c in text.Trim().ToLowerInvariant())
		{
			if (char.IsLetterOrDigit(c) || c is '-' or '_')
				anchor.Append(c);
			else if (c == ' ')
				anchor.Append('-');
		}

		return anchor.ToString();
	}

	/// <summary>
	/// Gets the plain text of a block or inline.
	/// </summary>
	public static string GetText(MarkdownObject? item)
	{
		var text = new StringBuilder();
		void Append(MarkdownObject? node)
		{
			switch (node)
			{
			case LiteralInline literal:
				text.Append(literal.Content.ToString());
				break;
			case CodeInline code:
				text.Append(code.Content);
				break;
			case LineBreakInline:
				text.Append(' ');
				break;
			case ContainerInline container:
				foreach (var child in container)
					Append(child);
				break;
			case LeafBlock { Inline: { } inline }:
				Append(inline);
				break;
			case ContainerBlock blocks:
				foreach (var child in blocks)
					Append(child);
				break;
			}
		}

		Append(item);
		return text.ToString().Trim();
	}

	private readonly IReadOnlyList<HelpHeading> m_headings;
	private readonly Dictionary<(string Heading, string Row), string> m_rows = [];
}

/// <summary>
/// The documents shown in the help, which are built into the editor so they match it.
/// </summary>
public static class HelpLibrary
{
	public const string DataFiles = "DataFiles.md";

	public const string Mods = "Mods.md";

	public const string ModEditor = "ModEditor.md";

	public static IReadOnlyList<HelpDocument> Documents => s_documents.Value;

	public static HelpDocument? Find(string name) => Documents.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Gets a target if its document has its heading (and row, if it has one), or else the heading alone if that's there,
	/// or else null.
	/// </summary>
	public static HelpTarget? Check(HelpTarget target)
	{
		if (Find(target.Document) is not { } document || document.FindHeading(target.Heading) is null)
			return null;
		return target.Row is null || document.HasRow(target.Heading, target.Row) ? target : target with { Row = null };
	}

	/// <summary>
	/// Gets the notes for a target's row, or null if it has none.
	/// </summary>
	public static string? GetRowText(HelpTarget? target) =>
		target?.Row is { } row ? Find(target.Document)?.GetRowText(target.Heading, row) : null;

	private static IReadOnlyList<HelpDocument> Load()
	{
		var assembly = typeof(HelpLibrary).Assembly;
		HelpDocument Read(string name, string title)
		{
			using var stream = assembly.GetManifestResourceStream($"Docs/{name}") ?? throw new InvalidOperationException($"The help document {name} isn't built into the editor.");
			using var reader = new StreamReader(stream);
			return new HelpDocument(name, title, reader.ReadToEnd());
		}

		return [Read(ModEditor, "Mod Editor"), Read(DataFiles, "Data Files"), Read(Mods, "Mods")];
	}

	private static readonly Lazy<IReadOnlyList<HelpDocument>> s_documents = new(Load);
}
