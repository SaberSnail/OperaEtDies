using System.Windows;
using System.Windows.Media;

namespace OperaEtDies.Controls.Game;

/// <summary>
/// A small dial showing the time of day: an arc over a horizon, with the sun moving along it through the day, and the moon
/// through the night. Marks on the arc show where the parts of the day in between start.
/// </summary>
public sealed class DayDial : FrameworkElement
{
	public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(nameof(Position), typeof(double), typeof(DayDial),
		new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty IsNightProperty = DependencyProperty.Register(nameof(IsNight), typeof(bool), typeof(DayDial),
		new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty MarksProperty = DependencyProperty.Register(nameof(Marks), typeof(IReadOnlyList<double>), typeof(DayDial),
		new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(DayDial),
		new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty SunBrushProperty = DependencyProperty.Register(nameof(SunBrush), typeof(Brush), typeof(DayDial),
		new FrameworkPropertyMetadata(Brushes.Goldenrod, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty MoonBrushProperty = DependencyProperty.Register(nameof(MoonBrush), typeof(Brush), typeof(DayDial),
		new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>
	/// How far through the day (or the night, while <see cref="IsNight"/>) it is, from 0 to 1.
	/// </summary>
	public double Position
	{
		get => (double) GetValue(PositionProperty);
		set => SetValue(PositionProperty, value);
	}

	/// <summary>
	/// Whether it's night, which shows the moon rather than the sun.
	/// </summary>
	public bool IsNight
	{
		get => (bool) GetValue(IsNightProperty);
		set => SetValue(IsNightProperty, value);
	}

	/// <summary>
	/// Where the parts of the day start along the arc, from 0 to 1.
	/// </summary>
	public IReadOnlyList<double>? Marks
	{
		get => (IReadOnlyList<double>?) GetValue(MarksProperty);
		set => SetValue(MarksProperty, value);
	}

	/// <summary>
	/// The arc, the horizon, the marks, and the outline of the moon.
	/// </summary>
	public Brush? Stroke
	{
		get => (Brush?) GetValue(StrokeProperty);
		set => SetValue(StrokeProperty, value);
	}

	/// <summary>
	/// The sun.
	/// </summary>
	public Brush? SunBrush
	{
		get => (Brush?) GetValue(SunBrushProperty);
		set => SetValue(SunBrushProperty, value);
	}

	/// <summary>
	/// The inside of the moon.
	/// </summary>
	public Brush? MoonBrush
	{
		get => (Brush?) GetValue(MoonBrushProperty);
		set => SetValue(MoonBrushProperty, value);
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		ArgumentNullException.ThrowIfNull(drawingContext);
		var width = ActualWidth;
		var height = ActualHeight;
		var marker = Math.Max(height * 0.18, 2);
		var radius = Math.Min((width / 2) - marker, height - (marker * 2));
		if (radius > 0)
		{
			var center = new Point(width / 2, marker + radius);
			var thickness = Math.Max(height / 14, 1);
			var pen = new Pen(Stroke, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
			pen.Freeze();

			drawingContext.DrawLine(pen, new Point(center.X - radius - marker, center.Y), new Point(center.X + radius + marker, center.Y));
			var arc = new StreamGeometry();
			using (var context = arc.Open())
			{
				context.BeginFigure(GetPoint(center, radius, 0), false, false);
				context.ArcTo(GetPoint(center, radius, 1), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
			}
			arc.Freeze();
			drawingContext.DrawGeometry(null, pen, arc);

			foreach (var mark in Marks ?? [])
				drawingContext.DrawLine(pen, GetPoint(center, radius - (marker * 0.6), mark), GetPoint(center, radius + (marker * 0.6), mark));

			var position = GetPoint(center, radius, Math.Clamp(Position, 0, 1));
			if (IsNight)
				drawingContext.DrawEllipse(MoonBrush, pen, position, marker * 0.85, marker * 0.85);
			else
				drawingContext.DrawEllipse(SunBrush, null, position, marker, marker);
		}
	}

	/// <summary>
	/// Gets the point on the arc a fraction of the way along it, from the left end to the right.
	/// </summary>
	private static Point GetPoint(Point center, double radius, double fraction)
	{
		var angle = Math.PI * (1 - fraction);
		return new Point(center.X + (radius * Math.Cos(angle)), center.Y - (radius * Math.Sin(angle)));
	}
}
