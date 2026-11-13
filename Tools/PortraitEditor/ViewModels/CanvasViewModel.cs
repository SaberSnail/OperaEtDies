using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using GoldenAnvil.Utility;
using OperaEtDies;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using PortraitEditor.Model;

namespace PortraitEditor.ViewModels;

/// <summary>
/// The large view of the focused character's portrait, where the selected shape is edited with the mouse.
/// </summary>
public sealed class CanvasViewModel : ViewModelBase
{
	public CanvasViewModel(MainWindowViewModel owner)
	{
		m_owner = owner;
	}

	/// <summary>
	/// Raised when anything the canvas draws changes.
	/// </summary>
	public event EventHandler? Changed;

	public ImageSource? Portrait { get; private set; }

	public Size PortraitSize { get; private set; } = new(400, 480);

	public double Zoom
	{
		get => VerifyAccess(m_zoom);
		set
		{
			if (SetPropertyField(Math.Clamp(value, MinimumZoom, MaximumZoom), ref m_zoom))
				Changed.Raise(this);
		}
	}

	public bool ShowGrid
	{
		get => VerifyAccess(m_showGrid);
		set
		{
			if (SetPropertyField(value, ref m_showGrid))
				Changed.Raise(this);
		}
	}

	/// <summary>
	/// The selected shape, or null if no shape is selected.
	/// </summary>
	public DataItemViewModel? Shape { get; private set; }

	/// <summary>
	/// The selected shape's path, or null if there's none or it can't be edited.
	/// </summary>
	public SvgPath? Path { get; private set; }

	/// <summary>
	/// The selected shape's outline before it's stretched and moved.
	/// </summary>
	public Geometry? Geometry { get; private set; }

	/// <summary>
	/// How the selected shape is stretched and moved for the focused character.
	/// </summary>
	public Matrix ShapeTransform { get; private set; } = Matrix.Identity;

	public bool IsShapeDrawn { get; private set; }

	public IReadOnlyList<SvgPathPoint> Points { get; private set; } = [];

	public int SelectedPointIndex
	{
		get => VerifyAccess(m_selectedPointIndex);
		set
		{
			if (SetPropertyField(value, ref m_selectedPointIndex))
			{
				RaisePropertyChanged(nameof(PointX), nameof(PointY), nameof(HasSelectedPoint));
				Changed.Raise(this);
			}
		}
	}

	public bool HasSelectedPoint => SelectedPoint is not null;

	public SvgPathPoint? SelectedPoint => m_selectedPointIndex >= 0 && m_selectedPointIndex < Points.Count ? Points[m_selectedPointIndex] : null;

	public string? PathMessage
	{
		get => VerifyAccess(m_pathMessage);
		private set => SetPropertyField(value, ref m_pathMessage);
	}

	/// <summary>
	/// Where the mouse is, in the selected shape's units and in the portrait's.
	/// </summary>
	public string CursorText
	{
		get => VerifyAccess(m_cursorText);
		set => SetPropertyField(value, ref m_cursorText);
	}

	public string BoundsX
	{
		get => FormatBounds(x => x.X);
		set => SetBounds(value, (bounds, number) => bounds with { X = number });
	}

	public string BoundsY
	{
		get => FormatBounds(x => x.Y);
		set => SetBounds(value, (bounds, number) => bounds with { Y = number });
	}

	public string BoundsWidth
	{
		get => FormatBounds(x => x.Width);
		set => SetBounds(value, (bounds, number) => bounds with { Width = number });
	}

	public string BoundsHeight
	{
		get => FormatBounds(x => x.Height);
		set => SetBounds(value, (bounds, number) => bounds with { Height = number });
	}

	public string PointX
	{
		get => SelectedPoint is { } point ? Format(point.Position.X) : "";
		set => SetPoint(value, (point, number) => new Point(number, point.Y));
	}

	public string PointY
	{
		get => SelectedPoint is { } point ? Format(point.Position.Y) : "";
		set => SetPoint(value, (point, number) => new Point(point.X, number));
	}

	public bool HasPath => Path is not null;

	/// <summary>
	/// The bounds of the selected shape before it's stretched and moved.
	/// </summary>
	public Rect Bounds => Geometry is { } geometry && !geometry.Bounds.IsEmpty ? geometry.Bounds : Rect.Empty;

	/// <summary>
	/// Zooms in to the next zoom level.
	/// </summary>
	public void ZoomIn() => Zoom = s_zoomLevels.FirstOrDefault(x => x > m_zoom + 0.001, MaximumZoom);

	/// <summary>
	/// Zooms out to the previous zoom level.
	/// </summary>
	public void ZoomOut() => Zoom = s_zoomLevels.LastOrDefault(x => x < m_zoom - 0.001, MinimumZoom);

	public void ResetZoom() => Zoom = 1;

	/// <summary>
	/// Zooms so the portrait and the space around it fit in an area.
	/// </summary>
	public void ZoomToFit(Size area, double margin)
	{
		if (area.Width > margin * 2 && area.Height > margin * 2)
			Zoom = Math.Min((area.Width - (margin * 2)) / PortraitSize.Width, (area.Height - (margin * 2)) / PortraitSize.Height) * 0.999;
	}

	/// <summary>
	/// Shows a portrait, with the placements of its shapes for choosing shapes with the mouse.
	/// </summary>
	internal void SetPortrait(ImageSource? portrait, Size size, IReadOnlyList<PortraitShapePlacement> placements, Func<PortraitShapePlacement, bool> isDrawn)
	{
		Portrait = portrait;
		PortraitSize = size;
		m_placements = placements;
		m_isDrawn = isDrawn;
		UpdateShapePlacement();
		Changed.Raise(this);
	}

	internal void SetShape(DataItemViewModel? shape)
	{
		if (shape != Shape)
		{
			Shape = shape;
			m_selectedPointIndex = -1;
		}

		OnShapeDataChanged();
	}

	/// <summary>
	/// Reads the selected shape's path again, after the data changes.
	/// </summary>
	internal void OnShapeDataChanged()
	{
		var text = Shape?.Node?.GetString("path");
		Path = null;
		Geometry = null;
		PathMessage = null;
		if (text is not null)
		{
			if (PortraitShape.TryParsePath(text, out var geometry, out var error))
				Geometry = geometry;
			else
				PathMessage = error;

			if (SvgPath.TryParse(text, out var path, out var pathError))
				Path = path;
			else
				PathMessage ??= $"The path can't be edited with the mouse: {pathError}";
		}

		Points = Path?.GetPoints() ?? [];
		if (m_selectedPointIndex >= Points.Count)
			m_selectedPointIndex = -1;
		UpdateShapePlacement();
		RaisePropertyChanged(nameof(HasPath), nameof(BoundsX), nameof(BoundsY), nameof(BoundsWidth), nameof(BoundsHeight), nameof(PointX), nameof(PointY), nameof(HasSelectedPoint), nameof(SelectedPointIndex));
		Changed.Raise(this);
	}

	/// <summary>
	/// Gets a copy of the selected shape's path to change, or null if it can't be edited.
	/// </summary>
	public SvgPath? CreateEditablePath() => Shape?.Node?.GetString("path") is { } text && SvgPath.TryParse(text, out var path, out _) ? path : null;

	/// <summary>
	/// Starts a change made by dragging, which is undone as one step.
	/// </summary>
	public void BeginDrag()
	{
		m_dragCount++;
		m_owner.BreakUndoMerge();
	}

	/// <summary>
	/// Changes the selected shape's path.
	/// </summary>
	public void SetPathText(string text)
	{
		if (Shape is { } shape)
			m_owner.EditItem(shape, $"drag{m_dragCount.ToString(CultureInfo.InvariantCulture)}", node => node.SetProperty("path", JsonValue.Create(text)));
	}

	/// <summary>
	/// Moves the selected point, or the selected shape if no point is selected, by a distance in the portrait's pixels.
	/// Nudges made close together are undone together.
	/// </summary>
	public void Nudge(Vector portraitDelta)
	{
		var toShape = ShapeTransform;
		if (toShape.HasInverse && CreateEditablePath() is { } path)
		{
			toShape.Invert();
			var delta = toShape.Transform(portraitDelta);
			if (SelectedPoint is { } point)
			{
				var target = point.Position + delta;
				path.MovePoint(point, new Point(Math.Round(target.X, SvgPath.Precision), Math.Round(target.Y, SvgPath.Precision)));
			}
			else
			{
				path.Transform(default, 1, 1, new Vector(Math.Round(delta.X, SvgPath.Precision), Math.Round(delta.Y, SvgPath.Precision)));
			}

			if (Shape is { } shape)
				m_owner.EditItem(shape, "nudge", node => node.SetProperty("path", JsonValue.Create(path.ToString())));
		}
	}

	/// <summary>
	/// Selects the shape drawn at a point in the portrait: the top one, or with cycling, the one under the selected
	/// shape (going back to the top after the bottom).
	/// </summary>
	public void SelectAt(Point point, bool cycle)
	{
		var hits = new List<PortraitShapePlacement>();
		for (var index = m_placements.Count - 1; index >= 0; index--)
		{
			var placement = m_placements[index];
			if (m_isDrawn(placement) && Contains(placement, point))
				hits.Add(placement);
		}

		// Mostly see-through shapes, such as lighting over the whole portrait, come after the rest, so clicking picks
		// what can be seen, and they can still be reached by cycling.
		hits = hits.OrderBy(x => IsMostlySeeThrough(x.Shape)).ToList();
		if (hits.Count != 0)
		{
			var current = Shape is { Parent: { } layer } shape ? hits.FindIndex(x => x.LayerIndex == layer.Index && x.ShapeIndex == shape.Index) : -1;
			var hit = cycle && current >= 0 ? hits[(current + 1) % hits.Count] : hits[0];
			m_owner.SelectShape(hit.LayerIndex, hit.ShapeIndex);
		}
	}

	/// <summary>
	/// Whether a shape is less than half opaque everywhere, for the focused character.
	/// </summary>
	private bool IsMostlySeeThrough(PortraitShape shape)
	{
		double GetAlpha(string? color) => color is null ? 0 : (m_owner.ResolveColor(color)?.A ?? 255) / 255.0;
		var fillAlpha = Math.Max(GetAlpha(shape.Fill), shape.Gradient is null ? 0 : GetAlpha(shape.FillTo));
		return Math.Max(fillAlpha, GetAlpha(shape.Stroke)) * (shape.Opacity ?? 1) < 0.5;
	}

	private static bool Contains(PortraitShapePlacement placement, Point point)
	{
		var inverse = placement.Transform;
		if (!inverse.HasInverse)
			return false;
		inverse.Invert();
		var local = inverse.Transform(point);
		var shape = placement.Shape;
		return (shape.Fill is not null && placement.Geometry.FillContains(local)) ||
			(shape.Stroke is not null && placement.Geometry.StrokeContains(new Pen(null, Math.Max(shape.StrokeWidth ?? 1, 4)), local));
	}

	private void UpdateShapePlacement()
	{
		var placement = Shape is { Parent: { } layer } shape ? m_placements.FirstOrDefault(x => x.LayerIndex == layer.Index && x.ShapeIndex == shape.Index) : null;
		ShapeTransform = placement?.Transform ?? Matrix.Identity;
		IsShapeDrawn = placement is not null && m_isDrawn(placement);
		NotDrawnMessage = placement is not null && !IsShapeDrawn ? GetNotDrawnMessage(placement) : null;
	}

	/// <summary>
	/// Explains why the selected shape isn't drawn on the canvas: which of its conditions the focused character doesn't
	/// meet, or that it's hidden.
	/// </summary>
	private string GetNotDrawnMessage(PortraitShapePlacement placement)
	{
		var shapeState = Shape?.CanvasState ?? CanvasState.Normal;
		var layerState = Shape?.Parent?.CanvasState ?? CanvasState.Normal;
		var reasons = new List<string>();
		if (shapeState == CanvasState.Hidden || (shapeState == CanvasState.Normal && layerState == CanvasState.Hidden))
		{
			reasons.Add("it's hidden on the canvas");
		}
		else
		{
			if (layerState == CanvasState.Normal && !placement.IsLayerConditionMet)
				reasons.Add("the layer's condition");
			if (layerState == CanvasState.Normal && !placement.IsPlaceMet)
				reasons.Add("the layer's place");
			if (!placement.IsShapeConditionMet)
				reasons.Add("the shape's condition");
		}

		return $"This shape isn't drawn for this character ({string.Join(", ", reasons)}).";
	}

	/// <summary>
	/// Why the selected shape isn't drawn on the canvas, or null if it is.
	/// </summary>
	public string? NotDrawnMessage
	{
		get => VerifyAccess(m_notDrawnMessage);
		private set => SetPropertyField(value, ref m_notDrawnMessage);
	}

	/// <summary>
	/// Forces the selected shape to be drawn on the canvas.
	/// </summary>
	public void ShowAnyway()
	{
		if (Shape is { } shape)
			shape.CanvasState = CanvasState.Forced;
	}

	private string FormatBounds(Func<Rect, double> getValue) => Bounds.IsEmpty ? "" : Format(getValue(Bounds));

	/// <summary>
	/// Stretches and moves the path so its bounds are changed as given.
	/// </summary>
	private void SetBounds(string text, Func<Rect, double, Rect> change)
	{
		var bounds = Bounds;
		if (!bounds.IsEmpty && TryParse(text, out var number) && CreateEditablePath() is { } path)
		{
			var target = change(bounds, number);
			var scaleX = bounds.Width > 0 && target.Width > 0 ? target.Width / bounds.Width : 1;
			var scaleY = bounds.Height > 0 && target.Height > 0 ? target.Height / bounds.Height : 1;
			path.Transform(bounds.TopLeft, scaleX, scaleY, target.TopLeft - bounds.TopLeft);
			m_owner.BreakUndoMerge();
			SetPathText(path.ToString());
		}
	}

	private void SetPoint(string text, Func<Point, double, Point> change)
	{
		if (SelectedPoint is { } point && TryParse(text, out var number) && CreateEditablePath() is { } path)
		{
			path.MovePoint(point, change(point.Position, number));
			m_owner.BreakUndoMerge();
			SetPathText(path.ToString());
		}
	}

	private static bool TryParse(string text, out double number) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);

	private static string Format(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

	// Zooming out goes down by 20% at a time, and zooming in goes up by 25% at a time.
	private const double MinimumZoom = 0.2;
	private const double MaximumZoom = 4;

	private static readonly double[] s_zoomLevels = [0.2, 0.4, 0.6, 0.8, 1, 1.25, 1.5, 1.75, 2, 2.25, 2.5, 2.75, 3, 3.25, 3.5, 3.75, 4];

	private readonly MainWindowViewModel m_owner;
	private IReadOnlyList<PortraitShapePlacement> m_placements = [];
	private Func<PortraitShapePlacement, bool> m_isDrawn = x => x.IsDrawn;
	private string? m_notDrawnMessage;
	private double m_zoom = 1.5;
	private bool m_showGrid;
	private int m_selectedPointIndex = -1;
	private int m_dragCount;
	private string? m_pathMessage;
	private string m_cursorText = "";
}
