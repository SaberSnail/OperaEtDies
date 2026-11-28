using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OperaEtDies.Controls.Game;

namespace OperaEtDies.Controls.Loading;

/// <summary>
/// The loading screen's animation: a faint circle with the sun going clockwise around its top half, from the left side
/// to the right, and the moon going on around the bottom half back to where the sun started, over and over. It's only
/// motion to show work is happening, not how far along it is. With reduced motion, the sun stands still at the top.
/// </summary>
public sealed class LoadingDial : FrameworkElement
{
	public LoadingDial()
	{
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(nameof(Position), typeof(double), typeof(LoadingDial),
		new FrameworkPropertyMetadata(0.25, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(LoadingDial),
		new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty SunBrushProperty = DependencyProperty.Register(nameof(SunBrush), typeof(Brush), typeof(LoadingDial),
		new FrameworkPropertyMetadata(Brushes.Goldenrod, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty MoonBrushProperty = DependencyProperty.Register(nameof(MoonBrush), typeof(Brush), typeof(LoadingDial),
		new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

	/// <summary>
	/// How far around the circle the sun or moon is, from 0 to 1, starting at the left side and going clockwise. The sun
	/// is shown for the first half and the moon for the second.
	/// </summary>
	public double Position
	{
		get => (double) GetValue(PositionProperty);
		set => SetValue(PositionProperty, value);
	}

	/// <summary>
	/// The circle, which is faint so the sun and moon stand out.
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
	/// The moon.
	/// </summary>
	public Brush? MoonBrush
	{
		get => (Brush?) GetValue(MoonBrushProperty);
		set => SetValue(MoonBrushProperty, value);
	}

	/// <summary>
	/// How long the sun and moon take to go all the way around.
	/// </summary>
	public static TimeSpan LoopDuration { get; } = TimeSpan.FromSeconds(1.5);

	protected override void OnRender(DrawingContext drawingContext)
	{
		ArgumentNullException.ThrowIfNull(drawingContext);
		var size = Math.Min(ActualWidth, ActualHeight);
		var marker = size * 0.11;
		var radius = (size / 2) - marker;
		if (radius > 0)
		{
			var center = new Point(ActualWidth / 2, ActualHeight / 2);
			var pen = new Pen(Stroke, Math.Max(size / 40, 1));
			pen.Freeze();
			drawingContext.DrawEllipse(null, pen, center, radius, radius);

			// Clockwise from the left side, with y going down the screen.
			var position = Position - Math.Floor(Position);
			var angle = Math.PI * (1 - (2 * position));
			var point = new Point(center.X + (radius * Math.Cos(angle)), center.Y - (radius * Math.Sin(angle)));
			if (position < 0.5)
				drawingContext.DrawEllipse(SunBrush, null, point, marker, marker);
			else
				drawingContext.DrawGeometry(MoonBrush, null, DayDial.CreateCrescent(point, marker));
		}
	}

	// The animation loops for as long as the dial is shown. It isn't one of Motion's animations, since those end when the
	// player presses a key, and this one has no end to skip to.
	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (!Motion.IsReduced)
			BeginAnimation(PositionProperty, new DoubleAnimation(0, 1, LoopDuration) { RepeatBehavior = RepeatBehavior.Forever });
	}

	private void OnUnloaded(object sender, RoutedEventArgs e) => BeginAnimation(PositionProperty, null);
}
