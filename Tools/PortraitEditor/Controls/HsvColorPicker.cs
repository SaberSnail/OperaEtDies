using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GoldenAnvil.Utility.Windows;

namespace PortraitEditor.Controls;

/// <summary>
/// Picks a color by its saturation and value (the square), hue (the first bar), and opacity (the second bar).
/// </summary>
public sealed class HsvColorPicker : FrameworkElement
{
	public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(HsvColorPicker),
		new FrameworkPropertyMetadata(Colors.Black, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnSelectedColorChanged));

	public Color SelectedColor
	{
		get => (Color) GetValue(SelectedColorProperty);
		set => SetValue(SelectedColorProperty, value);
	}

	protected override Size MeasureOverride(Size availableSize) => new(BarLeft + (BarWidth * 2) + Gap, SquareSize);

	protected override void OnRender(DrawingContext drawingContext)
	{
		var hueColor = ColorUtility.FromHSV(m_hue, 1, 1);
		var square = new Rect(0, 0, SquareSize, SquareSize);
		drawingContext.DrawRectangle(new LinearGradientBrush(Colors.White, hueColor, 0), null, square);
		drawingContext.DrawRectangle(new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90), null, square);
		var marker = new Point(m_saturation * SquareSize, (1 - m_value) * SquareSize);
		drawingContext.DrawEllipse(null, new Pen(Brushes.White, 2), marker, 5, 5);
		drawingContext.DrawEllipse(null, new Pen(Brushes.Black, 1), marker, 6, 6);

		var hueBar = new Rect(BarLeft, 0, BarWidth, SquareSize);
		var hues = new GradientStopCollection(Enumerable.Range(0, 7).Select(x => new GradientStop(ColorUtility.FromHSV(x * 60, 1, 1), x / 6.0)));
		drawingContext.DrawRectangle(new LinearGradientBrush(hues, 90), s_barPen, hueBar);
		DrawBarMarker(drawingContext, hueBar, m_hue / 360);

		var alphaBar = new Rect(BarLeft + BarWidth + Gap, 0, BarWidth, SquareSize);
		var opaque = ColorUtility.FromHSV(m_hue, m_saturation, m_value);
		drawingContext.DrawRectangle(s_checkerBrush, null, alphaBar);
		drawingContext.DrawRectangle(new LinearGradientBrush(opaque, Color.FromArgb(0, opaque.R, opaque.G, opaque.B), 90), s_barPen, alphaBar);
		DrawBarMarker(drawingContext, alphaBar, 1 - (m_alpha / 255.0));
	}

	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonDown(e);
		var position = e.GetPosition(this);
		m_area = position.X < SquareSize ? Area.Square : position.X < BarLeft + BarWidth + (Gap / 2) ? Area.Hue : Area.Alpha;
		CaptureMouse();
		Pick(position);
		e.Handled = true;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		if (m_area != Area.None && e.LeftButton == MouseButtonState.Pressed)
			Pick(e.GetPosition(this));
	}

	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonUp(e);
		m_area = Area.None;
		ReleaseMouseCapture();
	}

	private void Pick(Point position)
	{
		var x = Math.Clamp(position.X / SquareSize, 0, 1);
		var y = Math.Clamp(position.Y / SquareSize, 0, 1);
		switch (m_area)
		{
		case Area.Square:
			m_saturation = x;
			m_value = 1 - y;
			break;
		case Area.Hue:
			m_hue = y * 360;
			break;
		case Area.Alpha:
			m_alpha = (byte) Math.Round((1 - y) * 255);
			break;
		}

		var color = ColorUtility.FromAHSV(m_alpha, m_hue, m_saturation, m_value);
		m_lastPicked = color;
		SelectedColor = color;
		InvalidateVisual();
	}

	private static void DrawBarMarker(DrawingContext drawingContext, Rect bar, double fraction)
	{
		var y = bar.Top + (fraction * bar.Height);
		drawingContext.DrawRectangle(Brushes.White, s_barPen, new Rect(bar.Left - 2, y - 2, bar.Width + 4, 4));
	}

	/// <summary>
	/// Works out the hue, saturation, and value of a color set from outside, keeping the hue when the color has none.
	/// </summary>
	private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var picker = (HsvColorPicker) d;
		var color = (Color) e.NewValue;
		if (color != picker.m_lastPicked)
		{
			var red = color.R / 255.0;
			var green = color.G / 255.0;
			var blue = color.B / 255.0;
			var max = Math.Max(red, Math.Max(green, blue));
			var min = Math.Min(red, Math.Min(green, blue));
			var delta = max - min;
			if (delta > 0)
			{
				var hue = max == red ? (green - blue) / delta : max == green ? 2 + ((blue - red) / delta) : 4 + ((red - green) / delta);
				picker.m_hue = ((hue * 60) + 360) % 360;
			}

			picker.m_saturation = max == 0 ? 0 : delta / max;
			picker.m_value = max;
			picker.m_alpha = color.A;
			picker.m_lastPicked = color;
		}
	}

	private static SolidColorBrush CreateCheckerBrush()
	{
		var brush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
		brush.Freeze();
		return brush;
	}

	private static Pen CreateBarPen()
	{
		var pen = new Pen(Brushes.Gray, 1);
		pen.Freeze();
		return pen;
	}

	private enum Area
	{
		None,
		Square,
		Hue,
		Alpha,
	}

	private const double SquareSize = 160;
	private const double BarWidth = 18;
	private const double Gap = 10;
	private const double BarLeft = SquareSize + Gap;

	private static readonly Brush s_checkerBrush = CreateCheckerBrush();
	private static readonly Pen s_barPen = CreateBarPen();

	private Area m_area;
	private double m_hue;
	private double m_saturation;
	private double m_value;
	private byte m_alpha = 255;
	private Color m_lastPicked = Colors.Black;
}
