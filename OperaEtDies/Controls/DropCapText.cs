using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

namespace OperaEtDies.Controls;

/// <summary>
/// A paragraph that starts with a drop cap: its first letter drawn large (in <see cref="CapBrush"/>), sunk into the first
/// <see cref="CapLines"/> lines, with its top level with the tops of the first line's capitals and its baseline on the
/// baseline of the last of those lines. Those lines are indented to make room for it, and the rest of the text wraps at
/// the full width. Any punctuation before the first letter (such as an opening quotation mark) is part of the cap. The
/// text's font is the inherited one, as for a <see cref="System.Windows.Controls.TextBlock"/>, and "\n" starts a new line.
/// A <see cref="Trailer"/>, such as a link, follows the text: after the last line, with a space before it, if it fits
/// there, or else at the start of a new line, with the bottoms of the two lined up.
/// </summary>
[ContentProperty(nameof(Trailer))]
public sealed class DropCapText : FrameworkElement
{
	public static readonly DependencyProperty TrailerProperty = DependencyProperty.Register(nameof(Trailer), typeof(UIElement), typeof(DropCapText),
		new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnTrailerChanged));

	/// <summary>
	/// What follows the text, such as a link, or null. Collapsed, it takes no room.
	/// </summary>
	public UIElement? Trailer
	{
		get => (UIElement?) GetValue(TrailerProperty);
		set => SetValue(TrailerProperty, value);
	}

	/// <summary>
	/// Where the trailer is placed, as last laid out (empty when there's none, or it takes no room).
	/// </summary>
	public Rect TrailerBounds => m_layout?.TrailerBounds ?? Rect.Empty;

	protected override int VisualChildrenCount => Trailer is null ? 0 : 1;

	protected override Visual GetVisualChild(int index) => index == 0 && Trailer is { } trailer ? trailer : throw new ArgumentOutOfRangeException(nameof(index));

	protected override System.Collections.IEnumerator LogicalChildren => (Trailer is null ? Enumerable.Empty<object>() : [Trailer]).GetEnumerator();

	private static void OnTrailerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var text = (DropCapText) d;
		if (e.OldValue is UIElement oldTrailer)
		{
			text.RemoveVisualChild(oldTrailer);
			text.RemoveLogicalChild(oldTrailer);
		}

		if (e.NewValue is UIElement newTrailer)
		{
			text.AddLogicalChild(newTrailer);
			text.AddVisualChild(newTrailer);
		}

		text.ClearLayout();
	}
	public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(DropCapText),
		new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutChanged));

	public static readonly DependencyProperty CapLinesProperty = DependencyProperty.Register(nameof(CapLines), typeof(int), typeof(DropCapText),
		new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutChanged),
		x => x is int lines && lines >= 1);

	public static readonly DependencyProperty CapBrushProperty = DependencyProperty.Register(nameof(CapBrush), typeof(Brush), typeof(DropCapText),
		new FrameworkPropertyMetadata(Brushes.DarkRed, FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutChanged));

	public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(DropCapText),
		new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutChanged));

	public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(typeof(DropCapText),
		new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutChanged));

	public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(DropCapText),
		new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutChanged));

	public static readonly DependencyProperty FontStyleProperty = TextElement.FontStyleProperty.AddOwner(typeof(DropCapText),
		new FrameworkPropertyMetadata(FontStyles.Normal, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutChanged));

	public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(typeof(DropCapText),
		new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutChanged));

	/// <summary>
	/// The paragraph, including its first letter.
	/// </summary>
	public string Text
	{
		get => (string) GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	/// <summary>
	/// How many lines the cap sinks into (at least 1).
	/// </summary>
	public int CapLines
	{
		get => (int) GetValue(CapLinesProperty);
		set => SetValue(CapLinesProperty, value);
	}

	/// <summary>
	/// The drop cap.
	/// </summary>
	public Brush? CapBrush
	{
		get => (Brush?) GetValue(CapBrushProperty);
		set => SetValue(CapBrushProperty, value);
	}

	/// <summary>
	/// The rest of the text.
	/// </summary>
	public Brush? Foreground
	{
		get => (Brush?) GetValue(ForegroundProperty);
		set => SetValue(ForegroundProperty, value);
	}

	public FontFamily FontFamily
	{
		get => (FontFamily) GetValue(FontFamilyProperty);
		set => SetValue(FontFamilyProperty, value);
	}

	public double FontSize
	{
		get => (double) GetValue(FontSizeProperty);
		set => SetValue(FontSizeProperty, value);
	}

	public FontStyle FontStyle
	{
		get => (FontStyle) GetValue(FontStyleProperty);
		set => SetValue(FontStyleProperty, value);
	}

	public FontWeight FontWeight
	{
		get => (FontWeight) GetValue(FontWeightProperty);
		set => SetValue(FontWeightProperty, value);
	}

	/// <summary>
	/// The cap, as last laid out (empty when there's no text).
	/// </summary>
	public string Cap => m_layout?.Cap ?? "";

	/// <summary>
	/// How far the lines beside the cap are indented, as last laid out.
	/// </summary>
	public double CapIndent => m_layout?.Indent ?? 0;

	/// <summary>
	/// The lines of text after the cap, as last laid out.
	/// </summary>
	public IReadOnlyList<DropCapLine> Lines => m_layout?.Lines ?? [];

	/// <summary>
	/// Splits text into its cap (any punctuation before the first letter, and that letter) and the rest, ignoring leading spaces.
	/// </summary>
	public static (string Cap, string Remainder) SplitCap(string text)
	{
		var trimmed = text.TrimStart();
		var index = 0;
		var foundLetter = false;
		while (index < trimmed.Length && !foundLetter)
		{
			foundLetter = char.IsLetterOrDigit(trimmed, index);
			index += StringInfo.GetNextTextElementLength(trimmed, index);
		}
		return (trimmed[..index], trimmed[index..]);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		Trailer?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		var layout = GetLayout(availableSize.Width);
		var width = double.IsInfinity(availableSize.Width) ? layout.Width : Math.Min(layout.Width, availableSize.Width);
		return new Size(width, layout.Height);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		var layout = GetLayout(finalSize.Width);
		Trailer?.Arrange(layout.TrailerBounds.IsEmpty ? new Rect() : layout.TrailerBounds);
		return finalSize;
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		var layout = GetLayout(ActualWidth);
		if (layout.CapText is not null)
			drawingContext.DrawText(layout.CapText, layout.CapOrigin);
		foreach (var line in layout.TextLines)
			line.Line.Draw(drawingContext, line.Origin, InvertAxes.None);
	}

	private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((DropCapText) d).ClearLayout();

	private void ClearLayout()
	{
		m_layout?.Dispose();
		m_layout = null;
	}

	private Layout GetLayout(double width)
	{
		if (m_layout is null || !m_layout.WidthMatches(width) || m_layout.TrailerSize != GetTrailerSize())
		{
			ClearLayout();
			m_layout = CreateLayout(width);
		}
		return m_layout;
	}

	private Layout CreateLayout(double width)
	{
		var (cap, rest) = SplitCap((Text ?? "").Replace("\r", "", StringComparison.Ordinal));
		var layout = cap.Length == 0 ? new Layout(width, "", null, default, 0, 0, 0, []) : CreateLayout(width, cap, rest);
		return PlaceTrailer(layout, width);
	}

	/// <summary>
	/// Gets the room the trailer takes, or an empty size if there's none.
	/// </summary>
	private Size GetTrailerSize() => Trailer?.DesiredSize ?? default;

	/// <summary>
	/// Places the trailer after the last line, if it fits there, or else at the start of a new line below it, making the
	/// layout taller to fit it.
	/// </summary>
	private Layout PlaceTrailer(Layout layout, double width)
	{
		var size = GetTrailerSize();
		layout.TrailerSize = size;
		if (size.Width > 0 && size.Height > 0)
		{
			var maxWidth = double.IsInfinity(width) || double.IsNaN(width) ? double.PositiveInfinity : width;
			var space = FontSize * 0.3;
			Rect bounds;
			if (layout.TextLines.Count == 0)
			{
				bounds = new Rect(new Point(layout.Indent, 0), size);
			}
			else
			{
				var (line, origin) = layout.TextLines[^1];
				var end = origin.X + line.Width + space;
				var lineBottom = origin.Y + line.Height;
				var newLineX = layout.TextLines.Count < CapLines ? layout.Indent : 0;
				bounds = end + size.Width <= maxWidth
					? new Rect(new Point(end, lineBottom - size.Height), size)
					: new Rect(new Point(newLineX, lineBottom + Math.Max(0, line.Height - size.Height)), size);
			}

			layout.TrailerBounds = bounds;
			layout.Width = Math.Max(layout.Width, bounds.Right);
			layout.Height = Math.Max(layout.Height, bounds.Bottom);
		}

		return layout;
	}

	private Layout CreateLayout(double width, string cap, string rest)
	{
		var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
		var fontSize = FontSize;
		var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretches.Normal);
		var capTypeface = new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal);
		var lineHeight = FontFamily.LineSpacing * fontSize;
		var capsHeight = typeface.CapsHeight > 0 ? typeface.CapsHeight : 0.7;
		var capLines = CapLines;

		// The cap's capital height spans from the top of the first line's capitals to the baseline of the last line it sinks
		// into.
		var capFontSize = fontSize + (capLines - 1) * lineHeight / capsHeight;
		var capText = new FormattedText(cap, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, capTypeface, capFontSize, CapBrush, pixelsPerDip);
		var capInkWidth = capText.Width - capText.OverhangLeading - capText.OverhangTrailing;
		var indent = Math.Max(0, capInkWidth) + fontSize * 0.25;

		var properties = new RunProperties(typeface, fontSize, Foreground, CultureInfo.CurrentUICulture);
		var paragraph = new ParagraphProperties(properties);
		var source = new StringSource(rest.TrimStart(' '), properties);
		var formatter = GetFormatter();
		var lines = new List<(TextLine Line, Point Origin)>();
		var position = 0;
		var top = 0.0;
		var widest = indent;
		while (position < source.Length)
		{
			var x = lines.Count < capLines ? indent : 0;
			var lineWidth = double.IsInfinity(width) || double.IsNaN(width) ? 1e6 : Math.Max(1, width - x);
			var line = formatter.FormatLine(source, position, lineWidth, paragraph, null);
			lines.Add((line, new Point(x, top)));
			widest = Math.Max(widest, x + line.WidthIncludingTrailingWhitespace);
			top += line.Height;
			position += line.Length;
		}

		// Lines that aren't there yet (when the text is short) are as high as the first, or as the font's line spacing.
		var nominalHeight = lines.Count != 0 ? lines[0].Line.Height : lineHeight;
		var firstBaseline = lines.Count != 0 ? lines[0].Line.Baseline : FontFamily.Baseline * fontSize;
		var capBaseline = firstBaseline + (capLines - 1) * nominalHeight;
		var capOrigin = new Point(-capText.OverhangLeading, capBaseline - capText.Baseline);
		var capBottom = capOrigin.Y + capText.Height + capText.OverhangAfter;
		var height = Math.Max(top, Math.Max(capBottom, capLines * nominalHeight));

		return new Layout(width, cap, capText, capOrigin, indent, widest, height, lines);
	}

	private sealed class Layout(double layoutWidth, string cap, FormattedText? capText, Point capOrigin, double indent, double width, double height, IReadOnlyList<(TextLine Line, Point Origin)> textLines) : IDisposable
	{
		public string Cap { get; } = cap;
		public FormattedText? CapText { get; } = capText;
		public Point CapOrigin { get; } = capOrigin;
		public double Indent { get; } = indent;
		public double Width { get; set; } = width;
		public double Height { get; set; } = height;

		/// <summary>
		/// The room the trailer took when it was laid out, and where it went.
		/// </summary>
		public Size TrailerSize { get; set; }

		public Rect TrailerBounds { get; set; } = Rect.Empty;
		public IReadOnlyList<(TextLine Line, Point Origin)> TextLines { get; } = textLines;

		public IReadOnlyList<DropCapLine> Lines { get; } = textLines.Select(x => new DropCapLine(x.Origin.X, x.Line.Length, x.Line.WidthIncludingTrailingWhitespace)).ToList();

		public bool WidthMatches(double width) => layoutWidth.Equals(width) || (double.IsInfinity(layoutWidth) && double.IsInfinity(width));

		public void Dispose()
		{
			foreach (var line in TextLines)
				line.Line.Dispose();
		}
	}

	private sealed class StringSource(string text, TextRunProperties properties) : TextSource
	{
		public int Length => text.Length;

		public override TextRun GetTextRun(int textSourceCharacterIndex)
		{
			if (textSourceCharacterIndex >= text.Length)
				return new TextEndOfParagraph(1);
			if (text[textSourceCharacterIndex] == '\n')
				return new TextEndOfLine(1);

			var end = text.IndexOf('\n', textSourceCharacterIndex);
			if (end < 0)
				end = text.Length;
			return new TextCharacters(text, textSourceCharacterIndex, end - textSourceCharacterIndex, properties);
		}

		public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText(int textSourceCharacterIndexLimit)
		{
			var start = 0;
			var length = Math.Clamp(textSourceCharacterIndexLimit, 0, text.Length);
			return new TextSpan<CultureSpecificCharacterBufferRange>(length,
				new CultureSpecificCharacterBufferRange(properties.CultureInfo, new CharacterBufferRange(text, start, length)));
		}

		public override int GetTextEffectCharacterIndexFromTextSourceCharacterIndex(int textSourceCharacterIndex) => textSourceCharacterIndex;
	}

	private sealed class RunProperties(Typeface typeface, double fontSize, Brush? foreground, CultureInfo culture) : TextRunProperties
	{
		public override Typeface Typeface => typeface;
		public override double FontRenderingEmSize => fontSize;
		public override double FontHintingEmSize => fontSize;
		public override TextDecorationCollection? TextDecorations => null;
		public override Brush? ForegroundBrush => foreground;
		public override Brush? BackgroundBrush => null;
		public override CultureInfo CultureInfo => culture;
		public override TextEffectCollection? TextEffects => null;
	}

	private sealed class ParagraphProperties(TextRunProperties defaultProperties) : TextParagraphProperties
	{
		public override FlowDirection FlowDirection => FlowDirection.LeftToRight;
		public override TextAlignment TextAlignment => TextAlignment.Left;
		public override double LineHeight => 0;
		public override bool FirstLineInParagraph => false;
		public override TextRunProperties DefaultTextRunProperties => defaultProperties;
		public override TextWrapping TextWrapping => TextWrapping.Wrap;
		public override TextMarkerProperties? TextMarkerProperties => null;
		public override double Indent => 0;
	}

	// The lines it lays out are drawn later, so the formatter lives as long as the element, as a TextBlock's does.
	private TextFormatter GetFormatter()
	{
		var mode = TextOptions.GetTextFormattingMode(this);
		if (m_formatter is null || m_formattingMode != mode)
		{
			m_formatter = TextFormatter.Create(mode);
			m_formattingMode = mode;
		}
		return m_formatter;
	}

	private Layout? m_layout;
	private TextFormatter? m_formatter;
	private TextFormattingMode m_formattingMode;
}

/// <summary>
/// A line of a <see cref="DropCapText"/> after its cap: where it starts, how many characters of the text after the cap it
/// holds, and how wide it is.
/// </summary>
public sealed record DropCapLine(double X, int Length, double Width);
