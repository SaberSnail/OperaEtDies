using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using ModEditor.Model;
using Block = System.Windows.Documents.Block;
using Inline = System.Windows.Documents.Inline;
using MarkdownTable = Markdig.Extensions.Tables.Table;
using MarkdownTableCell = Markdig.Extensions.Tables.TableCell;
using MarkdownTableRow = Markdig.Extensions.Tables.TableRow;
using WpfTable = System.Windows.Documents.Table;
using WpfTableCell = System.Windows.Documents.TableCell;
using WpfTableRow = System.Windows.Documents.TableRow;

namespace ModEditor.Views.Help;

/// <summary>
/// A help document drawn as a WPF document, with where each heading and property row ended up, so the help can go to
/// them.
/// </summary>
public sealed class RenderedHelp(FlowDocument document, IReadOnlyDictionary<string, Block> headings, IReadOnlyDictionary<(string Heading, string Row), WpfTableRow> rows)
{
	public FlowDocument Document { get; } = document;

	public IReadOnlyDictionary<string, Block> Headings { get; } = headings;

	public IReadOnlyDictionary<(string Heading, string Row), WpfTableRow> Rows { get; } = rows;
}

/// <summary>
/// Draws the help's Markdown as a WPF document. It covers what the documentation uses: headings, paragraphs, lists,
/// tables, code, emphasis, and links. Anything else is shown as plain text.
/// </summary>
public static class MarkdownRenderer
{
	/// <summary>
	/// Draws a help document. Links call <paramref name="navigate"/> with their address.
	/// </summary>
	public static RenderedHelp Render(HelpDocument help, Action<string> navigate)
	{
		var document = new FlowDocument
		{
			FontFamily = new FontFamily("Segoe UI"),
			FontSize = 13,
			PagePadding = new Thickness(16, 12, 16, 24),
			// One column as wide as the window, rather than columns of a fixed width side by side.
			ColumnWidth = 100000,
			TextAlignment = TextAlignment.Left,
		};
		var renderer = new Renderer(navigate);
		foreach (var block in help.Markdown)
		{
			if (renderer.RenderBlock(block, isTopLevel: true) is { } rendered)
				document.Blocks.Add(rendered);
		}

		return new RenderedHelp(document, renderer.Headings, renderer.Rows);
	}

	private sealed class Renderer(Action<string> navigate)
	{
		public Dictionary<string, Block> Headings { get; } = [];

		public Dictionary<(string Heading, string Row), WpfTableRow> Rows { get; } = [];

		// Only blocks at the top of the document can be headings, as HelpDocument reads them, not those in lists or tables.
		public Block? RenderBlock(Markdig.Syntax.Block block, bool isTopLevel = false)
		{
			if (isTopLevel && HelpDocument.GetHeading(block) is { } heading)
			{
				m_heading = heading.Text;
				var paragraph = new Paragraph
				{
					FontWeight = FontWeights.SemiBold,
					FontSize = heading.Level switch { 1 => 24, 2 => 20, 3 => 17, 4 => 15, _ => 13 },
					Margin = new Thickness(0, heading.Level <= 2 ? 18 : 12, 0, 6),
				};
				paragraph.Inlines.Add(new Run(heading.Text));
				Headings.TryAdd(heading.Text, paragraph);
				return paragraph;
			}

			switch (block)
			{
			case ParagraphBlock { Inline: { } inline }:
				var text = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
				AddInlines(text.Inlines, inline);
				return text;

			case ListBlock list:
				var items = new List
				{
					MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
					Margin = new Thickness(0, 0, 0, 8),
					Padding = new Thickness(20, 0, 0, 0),
				};
				foreach (var item in list.OfType<ListItemBlock>())
				{
					var listItem = new ListItem();
					foreach (var child in item)
					{
						if (RenderBlock(child) is { } rendered)
						{
							rendered.Margin = new Thickness(0, 0, 0, 3);
							listItem.Blocks.Add(rendered);
						}
					}

					items.ListItems.Add(listItem);
				}

				return items;

			case MarkdownTable table:
				return RenderTable(table);

			case FencedCodeBlock or CodeBlock:
				var code = new Paragraph(new Run(((LeafBlock) block).Lines.ToString()))
				{
					FontFamily = CodeFont,
					FontSize = 12,
					Background = CodeBackground,
					Padding = new Thickness(8, 6, 8, 6),
					Margin = new Thickness(0, 0, 0, 8),
				};
				return code;

			case QuoteBlock quote:
				var section = new Section { BorderBrush = Lines, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(10, 0, 0, 0) };
				foreach (var child in quote)
				{
					if (RenderBlock(child) is { } rendered)
						section.Blocks.Add(rendered);
				}

				return section;

			case ThematicBreakBlock:
				return new Paragraph { BorderBrush = Lines, BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 4, 0, 12) };

			default:
				var plain = HelpDocument.GetText(block);
				return plain.Length == 0 ? null : new Paragraph(new Run(plain));
			}
		}

		/// <summary>
		/// Draws a table. Each property row is remembered under the heading it's in, by the names in its first column.
		/// </summary>
		private WpfTable RenderTable(MarkdownTable table)
		{
			var result = new WpfTable { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 10), BorderBrush = Lines, BorderThickness = new Thickness(1, 1, 0, 0) };
			foreach (var weight in GetColumnWeights(table))
				result.Columns.Add(new TableColumn { Width = new GridLength(weight, GridUnitType.Star) });

			var group = new TableRowGroup();
			foreach (var row in table.OfType<MarkdownTableRow>())
			{
				var resultRow = new WpfTableRow();
				if (row.IsHeader)
				{
					resultRow.Background = HeaderBackground;
					resultRow.FontWeight = FontWeights.SemiBold;
				}
				else if (m_heading is not null)
				{
					foreach (var name in HelpDocument.GetRowNames(row))
						Rows.TryAdd((m_heading, name), resultRow);
				}

				foreach (var cell in row.OfType<MarkdownTableCell>())
				{
					var resultCell = new WpfTableCell { BorderBrush = Lines, BorderThickness = new Thickness(0, 0, 1, 1), Padding = new Thickness(6, 3, 6, 3) };
					foreach (var child in cell)
					{
						if (RenderBlock(child) is { } rendered)
						{
							rendered.Margin = new Thickness(0);
							resultCell.Blocks.Add(rendered);
						}
					}

					resultRow.Cells.Add(resultCell);
				}

				group.Rows.Add(resultRow);
			}

			result.RowGroups.Add(group);
			return result;
		}

		/// <summary>
		/// Shares a table's width among its columns by how much text they hold: each column's longest cell, between a
		/// short word and a long sentence, so a column of names stays narrow and a column of notes gets the rest.
		/// </summary>
		private static IReadOnlyList<double> GetColumnWeights(MarkdownTable table)
		{
			var rows = table.OfType<MarkdownTableRow>().ToList();
			var columnCount = rows.Select(x => x.Count).DefaultIfEmpty(0).Max();
			return Enumerable.Range(0, columnCount)
				.Select(column => (double) Math.Clamp(rows.Where(x => column < x.Count).Select(x => HelpDocument.GetText(x[column]).Length).DefaultIfEmpty(0).Max(), 8, 60))
				.ToList();
		}

		private void AddInlines(InlineCollection target, ContainerInline container)
		{
			foreach (var inline in container)
			{
				if (RenderInline(inline) is { } rendered)
					target.Add(rendered);
			}
		}

		private Inline? RenderInline(Markdig.Syntax.Inlines.Inline inline)
		{
			switch (inline)
			{
			case LiteralInline literal:
				return new Run(literal.Content.ToString());
			case CodeInline code:
				return new Run(code.Content) { FontFamily = CodeFont, Background = CodeBackground };
			case LineBreakInline lineBreak:
				return lineBreak.IsHard ? new LineBreak() : new Run(" ");
			case LinkInline { IsImage: false } link:
				var hyperlink = new Hyperlink { ToolTip = link.Url };
				AddInlines(hyperlink.Inlines, link);
				var url = link.Url ?? "";
				hyperlink.Click += (_, _) => navigate(url);
				return hyperlink;
			case EmphasisInline emphasis:
				Span span = emphasis.DelimiterCount >= 2 ? new Bold() : new Italic();
				AddInlines(span.Inlines, emphasis);
				return span;
			case ContainerInline container:
				var group = new Span();
				AddInlines(group.Inlines, container);
				return group;
			default:
				var text = HelpDocument.GetText(inline);
				return text.Length == 0 ? null : new Run(text);
			}
		}

		private string? m_heading;
	}

	private static readonly FontFamily CodeFont = new("Consolas");
	private static readonly Brush CodeBackground = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xEC, 0xE4)));
	private static readonly Brush HeaderBackground = Freeze(new SolidColorBrush(Color.FromRgb(0xF4, 0xF1, 0xEA)));
	private static readonly Brush Lines = Freeze(new SolidColorBrush(Color.FromRgb(0xD8, 0xD0, 0xC0)));

	private static Brush Freeze(Brush brush)
	{
		brush.Freeze();
		return brush;
	}
}
