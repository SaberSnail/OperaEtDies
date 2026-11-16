using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ModEditor.Model;

namespace ModEditor.Views.Help;

/// <summary>
/// Shows the documentation built into the editor, and goes to the part a help request is about. Closing it only hides
/// it, so it keeps its place for the next request.
/// </summary>
public partial class HelpWindow : Window
{
	public HelpWindow()
	{
		InitializeComponent();
		DocumentList.ItemsSource = HelpLibrary.Documents;
	}

	/// <summary>
	/// Shows the window and goes to a place in the help: a heading, with its property row picked out if it has one.
	/// </summary>
	public void Navigate(HelpTarget target)
	{
		ShowDocument(target.Document);
		if (m_current is not { } rendered)
			return;

		ClearHighlight();
		TextElement? element = null;
		if (target.Row is { } row && rendered.Rows.TryGetValue((target.Heading, row), out var tableRow))
		{
			m_highlighted = (tableRow, tableRow.Background);
			tableRow.Background = HighlightBrush;
			element = tableRow;
		}
		else if (rendered.Headings.TryGetValue(target.Heading, out var heading))
		{
			element = heading;
		}

		m_isNavigating = true;
		HeadingList.SelectedItem = HelpLibrary.Find(target.Document)?.FindHeading(target.Heading);
		HeadingList.ScrollIntoView(HeadingList.SelectedItem);
		m_isNavigating = false;
		if (element is not null)
			ScrollTo(element);
	}

	/// <summary>
	/// Closes the window, rather than hiding it, as the editor does when it closes.
	/// </summary>
	public void CloseForGood()
	{
		m_isClosingForGood = true;
		Close();
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		// Closing it only hides it, so it keeps its place for the next request.
		if (!m_isClosingForGood)
		{
			e.Cancel = true;
			Hide();
		}

		base.OnClosing(e);
	}

	private void ShowDocument(string name)
	{
		if (!IsVisible)
			Show();
		if (WindowState == WindowState.Minimized)
			WindowState = WindowState.Normal;
		Activate();

		if (HelpLibrary.Find(name) is not { } document || m_currentName == document.Name)
			return;
		if (!m_rendered.TryGetValue(document.Name, out var rendered))
		{
			rendered = MarkdownRenderer.Render(document, x => OnLink(document, x));
			m_rendered[document.Name] = rendered;
		}

		m_current = rendered;
		m_currentName = document.Name;
		m_highlighted = null;
		Reader.Document = rendered.Document;
		m_isNavigating = true;
		DocumentList.SelectedItem = document;
		HeadingList.ItemsSource = document.Headings;
		m_isNavigating = false;
	}

	/// <summary>
	/// Follows a link: to a heading in this document (#anchor), to another document (Name.md, with an optional
	/// #anchor), or, for a web address, to the browser.
	/// </summary>
	private void OnLink(HelpDocument from, string url)
	{
		if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
			return;
		}

		var hash = url.IndexOf('#', StringComparison.Ordinal);
		var documentName = hash < 0 ? url : url[..hash];
		var document = documentName.Length == 0 ? from : HelpLibrary.Find(documentName);
		if (document is null)
			return;
		var heading = hash < 0 ? (document.Headings.Count > 0 ? document.Headings[0] : null) : document.FindAnchor(url[(hash + 1)..]);
		if (heading is not null)
			Navigate(new HelpTarget(document.Name, heading.Text));
	}

	private void OnDocumentSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!m_isNavigating && DocumentList.SelectedItem is HelpDocument document && document.Headings is [var first, ..])
			Navigate(new HelpTarget(document.Name, first.Text));
	}

	private void OnHeadingSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!m_isNavigating && HeadingList.SelectedItem is HelpHeading heading && m_currentName is { } name)
			Navigate(new HelpTarget(name, heading.Text));
	}

	protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == System.Windows.Input.Key.F && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
		{
			FindBox.Focus();
			FindBox.SelectAll();
			e.Handled = true;
		}
		else if (e.Key == System.Windows.Input.Key.F3)
		{
			FindNext();
			e.Handled = true;
		}
	}

	private void OnFindBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == System.Windows.Input.Key.Enter)
		{
			FindNext();
			e.Handled = true;
		}
	}

	private void OnFindNextClick(object sender, RoutedEventArgs e) => FindNext();

	/// <summary>
	/// Selects the next place the search text appears in the document shown, after the selection (or from the start),
	/// going round to the start after the end. Text split between differently formatted words isn't found.
	/// </summary>
	private void FindNext()
	{
		var text = FindBox.Text.Trim();
		if (text.Length == 0 || Reader.Document is not { } document)
			return;

		var start = Reader.Selection is { IsEmpty: false } selection ? selection.End : document.ContentStart;
		var found = Find(start, document.ContentEnd, text) ?? Find(document.ContentStart, start, text);
		if (found is null)
		{
			FindStatus.Text = $"\"{text}\" isn't in this document.";
			return;
		}

		FindStatus.Text = "";
		Reader.Selection?.Select(found.Start, found.End);
		if (found.Start.Paragraph is { } paragraph)
			ScrollTo(paragraph);
		else if (found.Start.Parent is TextElement element)
			ScrollTo(element);
	}

	private static TextRange? Find(TextPointer from, TextPointer to, string text)
	{
		for (var position = from; position is not null && position.CompareTo(to) < 0; position = position.GetNextContextPosition(LogicalDirection.Forward))
		{
			if (position.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text)
				continue;
			var run = position.GetTextInRun(LogicalDirection.Forward);
			var index = run.IndexOf(text, StringComparison.CurrentCultureIgnoreCase);
			if (index >= 0)
			{
				var start = position.GetPositionAtOffset(index);
				var end = start?.GetPositionAtOffset(text.Length);
				if (start is not null && end is not null && end.CompareTo(to) <= 0)
					return new TextRange(start, end);
			}
		}

		return null;
	}

	private void ClearHighlight()
	{
		if (m_highlighted is var (row, background))
			row.Background = background;
		m_highlighted = null;
	}

	/// <summary>
	/// Scrolls the document so an element is near the top, once the document has been laid out.
	/// </summary>
	private void ScrollTo(TextElement element)
	{
		void Scroll()
		{
			var scroller = FindChild<ScrollViewer>(Reader);
			var rect = element.ContentStart.GetCharacterRect(LogicalDirection.Forward);
			if (scroller is null || rect.IsEmpty)
				element.BringIntoView();
			else
				scroller.ScrollToVerticalOffset(Math.Max(0, scroller.VerticalOffset + rect.Top - 12));
		}

		void OnLayoutUpdated(object? sender, EventArgs e)
		{
			Reader.LayoutUpdated -= OnLayoutUpdated;
			Scroll();
		}

		Reader.LayoutUpdated += OnLayoutUpdated;
		Reader.InvalidateMeasure();
	}

	private static T? FindChild<T>(DependencyObject parent)
		where T : DependencyObject
	{
		for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
		{
			var child = VisualTreeHelper.GetChild(parent, index);
			if (child is T found)
				return found;
			if (FindChild<T>(child) is { } deeper)
				return deeper;
		}

		return null;
	}

	private static readonly Brush HighlightBrush = CreateHighlight();

	private static Brush CreateHighlight()
	{
		var brush = new SolidColorBrush(Color.FromRgb(0xFF, 0xEF, 0xB0));
		brush.Freeze();
		return brush;
	}

	private readonly Dictionary<string, RenderedHelp> m_rendered = new(StringComparer.OrdinalIgnoreCase);
	private RenderedHelp? m_current;
	private string? m_currentName;
	private (TableRow Row, Brush? Background)? m_highlighted;
	private bool m_isNavigating;
	private bool m_isClosingForGood;
}
