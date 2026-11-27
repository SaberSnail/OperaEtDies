using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OperaEtDies.Controls;

/// <summary>
/// A double rule: two thin parallel lines with a gap between them, across its width (or down its height, when vertical).
/// A horizontal rule can have an ornament at its center, drawn from a path on the icons' 24 by 24 grid, filled with
/// <see cref="OrnamentBrush"/>, with the rules stopping short of it on either side.
/// </summary>
public sealed class DoubleRule : FrameworkElement
{
	public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(DoubleRule),
		new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(DoubleRule),
		new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty GapProperty = DependencyProperty.Register(nameof(Gap), typeof(double), typeof(DoubleRule),
		new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty OrnamentProperty = DependencyProperty.Register(nameof(Ornament), typeof(Geometry), typeof(DoubleRule),
		new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty OrnamentBrushProperty = DependencyProperty.Register(nameof(OrnamentBrush), typeof(Brush), typeof(DoubleRule),
		new FrameworkPropertyMetadata(Brushes.DarkRed, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty OrnamentSizeProperty = DependencyProperty.Register(nameof(OrnamentSize), typeof(double), typeof(DoubleRule),
		new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>
	/// Whether the rule runs across (the default) or down.
	/// </summary>
	public Orientation Orientation
	{
		get => (Orientation) GetValue(OrientationProperty);
		set => SetValue(OrientationProperty, value);
	}

	/// <summary>
	/// The lines.
	/// </summary>
	public Brush? Stroke
	{
		get => (Brush?) GetValue(StrokeProperty);
		set => SetValue(StrokeProperty, value);
	}

	/// <summary>
	/// The space between the lines, which are each one unit thick.
	/// </summary>
	public double Gap
	{
		get => (double) GetValue(GapProperty);
		set => SetValue(GapProperty, value);
	}

	/// <summary>
	/// The ornament at the center of a horizontal rule, if any, on a 24 by 24 grid.
	/// </summary>
	public Geometry? Ornament
	{
		get => (Geometry?) GetValue(OrnamentProperty);
		set => SetValue(OrnamentProperty, value);
	}

	/// <summary>
	/// The ornament's fill.
	/// </summary>
	public Brush? OrnamentBrush
	{
		get => (Brush?) GetValue(OrnamentBrushProperty);
		set => SetValue(OrnamentBrushProperty, value);
	}

	/// <summary>
	/// How wide and high the ornament's 24 by 24 grid is drawn.
	/// </summary>
	public double OrnamentSize
	{
		get => (double) GetValue(OrnamentSizeProperty);
		set => SetValue(OrnamentSizeProperty, value);
	}

	/// <summary>
	/// How thick the two lines and the gap between them are together.
	/// </summary>
	public double Thickness => 2 + Math.Max(0, Gap);

	protected override Size MeasureOverride(Size availableSize)
	{
		var thickness = Thickness;
		return Orientation == Orientation.Horizontal
			? new Size(Ornament is null ? 0 : OrnamentSize, Ornament is null ? thickness : Math.Max(thickness, OrnamentSize))
			: new Size(thickness, 0);
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		var isHorizontal = Orientation == Orientation.Horizontal;
		var length = isHorizontal ? ActualWidth : ActualHeight;
		var across = isHorizontal ? ActualHeight : ActualWidth;
		var start = Math.Round((across - Thickness) / 2);
		var ornament = isHorizontal ? Ornament : null;

		// The rules stop short of the ornament by a third of its size on either side.
		var ornamentStart = (length - OrnamentSize) / 2;
		var spans = ornament is null
			? new[] { (From: 0.0, To: length) }
			: [(0.0, ornamentStart - OrnamentSize / 3), (ornamentStart + OrnamentSize * 4 / 3, length)];

		foreach (var (from, to) in spans.Where(x => x.To > x.From))
		{
			DrawLine(drawingContext, isHorizontal, from, to, start);
			DrawLine(drawingContext, isHorizontal, from, to, start + 1 + Math.Max(0, Gap));
		}

		if (ornament is not null)
		{
			var scale = OrnamentSize / 24;
			drawingContext.PushTransform(new MatrixTransform(scale, 0, 0, scale, ornamentStart, (across - OrnamentSize) / 2));
			drawingContext.DrawGeometry(OrnamentBrush, null, ornament);
			drawingContext.Pop();
		}
	}

	// Each line is snapped to the device's pixels, so it stays crisp at any scaling.
	private void DrawLine(DrawingContext drawingContext, bool isHorizontal, double from, double to, double offset)
	{
		var rect = isHorizontal ? new Rect(from, offset, to - from, 1) : new Rect(offset, from, 1, to - from);
		var guidelines = new GuidelineSet();
		if (isHorizontal)
		{
			guidelines.GuidelinesY.Add(rect.Top);
			guidelines.GuidelinesY.Add(rect.Bottom);
		}
		else
		{
			guidelines.GuidelinesX.Add(rect.Left);
			guidelines.GuidelinesX.Add(rect.Right);
		}
		drawingContext.PushGuidelineSet(guidelines);
		drawingContext.DrawRectangle(Stroke, null, rect);
		drawingContext.Pop();
	}
}
