using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PortraitEditor.Model;
using PortraitEditor.ViewModels;

namespace PortraitEditor.Controls;

/// <summary>
/// Shows the focused character's portrait from a <see cref="CanvasViewModel"/>, with the selected shape's outline,
/// bounds, and points. Clicking chooses the shape under the mouse (Ctrl+click chooses the one under that), dragging
/// inside the bounds moves the shape, dragging the handles on the bounds stretches it (Shift keeps its proportions),
/// and dragging a point moves it (Shift keeps it level or upright). Ctrl+wheel zooms.
/// </summary>
public sealed class ShapeCanvas : FrameworkElement
{
	public ShapeCanvas()
	{
		Focusable = true;
		DataContextChanged += OnDataContextChanged;
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		var viewModel = ViewModel;
		return viewModel is null ? default : new Size((viewModel.PortraitSize.Width * viewModel.Zoom) + (CanvasMargin * 2), (viewModel.PortraitSize.Height * viewModel.Zoom) + (CanvasMargin * 2));
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		if (ViewModel is not { } viewModel)
			return;

		drawingContext.DrawRectangle(s_backgroundBrush, null, new Rect(RenderSize));
		var portraitRect = new Rect(CanvasMargin, CanvasMargin, viewModel.PortraitSize.Width * viewModel.Zoom, viewModel.PortraitSize.Height * viewModel.Zoom);
		drawingContext.DrawRectangle(Brushes.White, null, portraitRect);
		if (viewModel.Portrait is { } portrait)
			drawingContext.DrawImage(portrait, portraitRect);
		if (viewModel.ShowGrid)
			DrawGrid(drawingContext, viewModel, portraitRect);
		drawingContext.DrawRectangle(null, s_borderPen, portraitRect);

		if (viewModel.Geometry is { } geometry)
		{
			var toScreen = GetShapeToScreen(viewModel);
			var outline = geometry.Clone();
			outline.Transform = new MatrixTransform(toScreen);
			drawingContext.DrawGeometry(null, s_outlineBackPen, outline);
			drawingContext.DrawGeometry(null, viewModel.IsShapeDrawn ? s_outlinePen : s_hiddenOutlinePen, outline);

			if (viewModel.Path is not null && GetBoxRect(viewModel) is { } box)
			{
				drawingContext.DrawRectangle(null, s_boxPen, box);
				foreach (var handle in GetHandles(box))
					drawingContext.DrawRectangle(Brushes.White, s_handlePen, CenteredRect(handle, HandleSize));
			}

			for (var index = 0; index < viewModel.Points.Count; index++)
			{
				var point = viewModel.Points[index];
				var position = toScreen.Transform(point.Position);
				foreach (var anchor in point.Anchors)
					drawingContext.DrawLine(s_controlLinePen, toScreen.Transform(anchor), position);

				var fill = index == viewModel.SelectedPointIndex ? s_selectedPointBrush : Brushes.White;
				if (point.Role == SvgPointRole.End)
					drawingContext.DrawRectangle(fill, s_pointPen, CenteredRect(position, PointSize));
				else
					drawingContext.DrawEllipse(fill, s_controlPen, position, PointSize / 2, PointSize / 2);
			}
		}
	}

	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonDown(e);
		if (ViewModel is not { } viewModel)
			return;

		Focus();
		CaptureMouse();
		m_start = e.GetPosition(this);
		m_mode = DragMode.Select;
		m_originalPath = viewModel.CreateEditablePath();
		m_originalBounds = viewModel.Bounds;

		if (m_originalPath is not null)
		{
			var pointIndex = FindPoint(viewModel, m_start);
			var box = GetBoxRect(viewModel);
			var handle = box is { } boxRect ? GetHandles(boxRect).ToList().FindIndex(x => (x - m_start).Length <= HandleSize) : -1;
			if (pointIndex >= 0)
			{
				m_mode = DragMode.Point;
				m_point = viewModel.Points[pointIndex];
				viewModel.SelectedPointIndex = pointIndex;
			}
			else if (handle >= 0)
			{
				m_mode = DragMode.Resize;
				m_handle = handle;
			}
			else if (box is { } moveBox && moveBox.Contains(m_start))
			{
				m_mode = DragMode.Move;
			}
		}

		m_isDragging = false;
		e.Handled = true;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		if (ViewModel is not { } viewModel)
			return;

		var position = e.GetPosition(this);
		var portraitPoint = ToPortrait(viewModel, position);
		var shapePoint = ToShape(viewModel, position);
		viewModel.CursorText = $"Portrait {Format(portraitPoint)}   Shape {Format(shapePoint)}";

		if (m_mode is DragMode.None or DragMode.Select)
		{
			UpdateCursor(viewModel, position);
		}
		else if (e.LeftButton == MouseButtonState.Pressed && m_originalPath is not null)
		{
			if (!m_isDragging && (position - m_start).Length > 3)
			{
				m_isDragging = true;
				viewModel.BeginDrag();
			}

			if (m_isDragging && SvgPath.TryParse(m_originalPath.ToString(), out var path, out _))
			{
				var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
				var startPoint = ToShape(viewModel, m_start);
				switch (m_mode)
				{
				case DragMode.Point when m_point is not null:
					var target = m_point.Position + (shapePoint - startPoint);
					if (shift)
						target = Math.Abs(target.X - m_point.Position.X) > Math.Abs(target.Y - m_point.Position.Y) ? new Point(target.X, m_point.Position.Y) : new Point(m_point.Position.X, target.Y);
					path.MovePoint(m_point, target);
					break;
				case DragMode.Move:
					var delta = shapePoint - startPoint;
					if (shift)
						delta = Math.Abs(delta.X) > Math.Abs(delta.Y) ? new Vector(delta.X, 0) : new Vector(0, delta.Y);
					path.Transform(default, 1, 1, new Vector(Math.Round(delta.X, SvgPath.Precision), Math.Round(delta.Y, SvgPath.Precision)));
					break;
				case DragMode.Resize:
					Resize(path, shapePoint, shift);
					break;
				}

				viewModel.SetPathText(path.ToString());
			}
		}
	}

	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonUp(e);
		if (ViewModel is { } viewModel && !m_isDragging && m_mode is DragMode.Select or DragMode.Move)
			viewModel.SelectAt(ToPortrait(viewModel, e.GetPosition(this)), Keyboard.Modifiers.HasFlag(ModifierKeys.Control));

		m_mode = DragMode.None;
		m_isDragging = false;
		m_point = null;
		ReleaseMouseCapture();
		e.Handled = true;
	}

	protected override void OnMouseWheel(MouseWheelEventArgs e)
	{
		base.OnMouseWheel(e);
		if (ViewModel is { } viewModel && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
		{
			if (e.Delta > 0)
				viewModel.ZoomIn();
			else
				viewModel.ZoomOut();
			e.Handled = true;
		}
	}

	private CanvasViewModel? ViewModel => DataContext as CanvasViewModel;

	/// <summary>
	/// Stretches the path so the dragged edges of its original bounds follow the mouse, keeping the opposite edges in
	/// place.
	/// </summary>
	private void Resize(SvgPath path, Point shapePoint, bool keepProportions)
	{
		var bounds = m_originalBounds.IsEmpty ? new Rect(0, 0, 1, 1) : m_originalBounds;

		// Handles go clockwise from the top left corner.
		var movesLeft = m_handle is 0 or 6 or 7;
		var movesRight = m_handle is 2 or 3 or 4;
		var movesTop = m_handle is 0 or 1 or 2;
		var movesBottom = m_handle is 4 or 5 or 6;
		var anchor = new Point(movesLeft ? bounds.Right : bounds.Left, movesTop ? bounds.Bottom : bounds.Top);

		var scaleX = 1.0;
		var scaleY = 1.0;
		if ((movesLeft || movesRight) && bounds.Width > 0)
			scaleX = Math.Max(MinimumSize, Math.Abs(shapePoint.X - anchor.X)) / bounds.Width;
		if ((movesTop || movesBottom) && bounds.Height > 0)
			scaleY = Math.Max(MinimumSize, Math.Abs(shapePoint.Y - anchor.Y)) / bounds.Height;
		if (keepProportions)
		{
			var scale = (movesLeft || movesRight) && (movesTop || movesBottom) ? Math.Max(scaleX, scaleY) : (movesLeft || movesRight ? scaleX : scaleY);
			scaleX = scale;
			scaleY = scale;
		}

		path.Transform(anchor, scaleX, scaleY, default);
	}

	private void UpdateCursor(CanvasViewModel viewModel, Point position)
	{
		var cursor = Cursors.Arrow;
		if (viewModel.Path is not null)
		{
			var box = GetBoxRect(viewModel);
			var handle = box is { } boxRect ? GetHandles(boxRect).ToList().FindIndex(x => (x - position).Length <= HandleSize) : -1;
			if (FindPoint(viewModel, position) >= 0)
				cursor = Cursors.Hand;
			else if (handle >= 0)
				cursor = handle switch { 0 or 4 => Cursors.SizeNWSE, 2 or 6 => Cursors.SizeNESW, 1 or 5 => Cursors.SizeNS, _ => Cursors.SizeWE };
			else if (box is { } moveBox && moveBox.Contains(position))
				cursor = Cursors.SizeAll;
		}

		Cursor = cursor;
	}

	private static int FindPoint(CanvasViewModel viewModel, Point position)
	{
		var toScreen = GetShapeToScreen(viewModel);
		var best = -1;
		var bestDistance = PointSize + 0.001;
		for (var index = 0; index < viewModel.Points.Count; index++)
		{
			var distance = (toScreen.Transform(viewModel.Points[index].Position) - position).Length;
			if (distance < bestDistance)
			{
				best = index;
				bestDistance = distance;
			}
		}

		return best;
	}

	private static Rect? GetBoxRect(CanvasViewModel viewModel)
	{
		var bounds = viewModel.Bounds;
		if (bounds.IsEmpty)
			return null;

		var toScreen = GetShapeToScreen(viewModel);
		var box = new Rect(toScreen.Transform(bounds.TopLeft), toScreen.Transform(bounds.BottomRight));
		box.Inflate(2, 2);
		return box;
	}

	private static IEnumerable<Point> GetHandles(Rect box)
	{
		var middleX = (box.Left + box.Right) / 2;
		var middleY = (box.Top + box.Bottom) / 2;
		return [box.TopLeft, new Point(middleX, box.Top), box.TopRight, new Point(box.Right, middleY), box.BottomRight, new Point(middleX, box.Bottom), box.BottomLeft, new Point(box.Left, middleY)];
	}

	private static void DrawGrid(DrawingContext drawingContext, CanvasViewModel viewModel, Rect portraitRect)
	{
		for (var x = GridSpacing; x < viewModel.PortraitSize.Width; x += GridSpacing)
		{
			var screenX = portraitRect.Left + (x * viewModel.Zoom);
			drawingContext.DrawLine(x % 100 == 0 ? s_majorGridPen : s_gridPen, new Point(screenX, portraitRect.Top), new Point(screenX, portraitRect.Bottom));
		}

		for (var y = GridSpacing; y < viewModel.PortraitSize.Height; y += GridSpacing)
		{
			var screenY = portraitRect.Top + (y * viewModel.Zoom);
			drawingContext.DrawLine(y % 100 == 0 ? s_majorGridPen : s_gridPen, new Point(portraitRect.Left, screenY), new Point(portraitRect.Right, screenY));
		}
	}

	/// <summary>
	/// Gets the transform from the selected shape's units to the control's.
	/// </summary>
	private static Matrix GetShapeToScreen(CanvasViewModel viewModel)
	{
		var matrix = viewModel.ShapeTransform;
		matrix.Scale(viewModel.Zoom, viewModel.Zoom);
		matrix.Translate(CanvasMargin, CanvasMargin);
		return matrix;
	}

	private static Point ToPortrait(CanvasViewModel viewModel, Point position) =>
		new((position.X - CanvasMargin) / viewModel.Zoom, (position.Y - CanvasMargin) / viewModel.Zoom);

	private static Point ToShape(CanvasViewModel viewModel, Point position)
	{
		var matrix = GetShapeToScreen(viewModel);
		if (!matrix.HasInverse)
			return position;
		matrix.Invert();
		return matrix.Transform(position);
	}

	private static Rect CenteredRect(Point center, double size) => new(center.X - (size / 2), center.Y - (size / 2), size, size);

	private static string Format(Point point) =>
		$"{point.X.ToString("0.#", CultureInfo.InvariantCulture)}, {point.Y.ToString("0.#", CultureInfo.InvariantCulture)}";

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (e.OldValue is CanvasViewModel oldViewModel)
			oldViewModel.Changed -= OnViewModelChanged;
		if (e.NewValue is CanvasViewModel newViewModel)
			newViewModel.Changed += OnViewModelChanged;
		OnViewModelChanged(this, EventArgs.Empty);
	}

	private void OnViewModelChanged(object? sender, EventArgs e)
	{
		InvalidateMeasure();
		InvalidateVisual();
	}

	private static Pen CreatePen(Color color, double thickness, DashStyle? dashStyle = null)
	{
		var pen = new Pen(new SolidColorBrush(color), thickness) { DashStyle = dashStyle ?? DashStyles.Solid };
		pen.Freeze();
		return pen;
	}

	private static SolidColorBrush CreateBrush(Color color)
	{
		var brush = new SolidColorBrush(color);
		brush.Freeze();
		return brush;
	}

	private enum DragMode
	{
		None,
		Select,
		Point,
		Move,
		Resize,
	}

	/// <summary>
	/// The space around the portrait.
	/// </summary>
	public const double CanvasMargin = 24;

	private const double HandleSize = 8;
	private const double PointSize = 8;
	private const double GridSpacing = 20;
	private const double MinimumSize = 0.5;

	private static readonly Brush s_backgroundBrush = CreateBrush(Color.FromRgb(0x80, 0x80, 0x80));
	private static readonly Brush s_selectedPointBrush = CreateBrush(Color.FromRgb(0xFF, 0x8C, 0x00));
	private static readonly Pen s_borderPen = CreatePen(Colors.Black, 1);
	private static readonly Pen s_gridPen = CreatePen(Color.FromArgb(0x40, 0x00, 0x00, 0x00), 1);
	private static readonly Pen s_majorGridPen = CreatePen(Color.FromArgb(0x80, 0x00, 0x00, 0x00), 1);
	private static readonly Pen s_outlineBackPen = CreatePen(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF), 3);
	private static readonly Pen s_outlinePen = CreatePen(Color.FromRgb(0x2F, 0x7C, 0xF6), 1.5);
	private static readonly Pen s_hiddenOutlinePen = CreatePen(Color.FromRgb(0xE0, 0x40, 0x40), 1.5, DashStyles.Dash);
	private static readonly Pen s_boxPen = CreatePen(Color.FromRgb(0x2F, 0x7C, 0xF6), 1, DashStyles.Dot);
	private static readonly Pen s_handlePen = CreatePen(Color.FromRgb(0x2F, 0x7C, 0xF6), 1);
	private static readonly Pen s_pointPen = CreatePen(Colors.Black, 1);
	private static readonly Pen s_controlPen = CreatePen(Color.FromRgb(0x80, 0x30, 0xC0), 1);
	private static readonly Pen s_controlLinePen = CreatePen(Color.FromArgb(0xC0, 0x80, 0x30, 0xC0), 1);

	private DragMode m_mode;
	private bool m_isDragging;
	private Point m_start;
	private SvgPath? m_originalPath;
	private Rect m_originalBounds;
	private SvgPathPoint? m_point;
	private int m_handle;
}
