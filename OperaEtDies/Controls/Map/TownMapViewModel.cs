using GoldenAnvil.Utility;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Town;

namespace OperaEtDies.Controls.Map;

/// <summary>
/// A map of the town, drawing each quarter as a rectangle, each path as a line through the edge the quarters share, and
/// map features such as the river underneath. The quarter the player is in is marked. Developers can choose a quarter to
/// go there. A mini-map shows just the player's quarter and the quarters connected to it, at a smaller size.
/// </summary>
public sealed class TownMapViewModel : ViewModelBase
{
	/// <summary>
	/// The size of one grid cell on the full map, in device-independent pixels.
	/// </summary>
	public const double DefaultCellSize = 64;

	/// <summary>
	/// The largest a grid cell gets on a mini-map, so a mini-map with few quarters doesn't look oversized.
	/// </summary>
	public const double MaxMiniMapCellSize = 40;

	/// <summary>
	/// Creates a map of every quarter in the town. Choosing a quarter goes there, if travel is allowed.
	/// </summary>
	public TownMapViewModel(IEnumerable<Location> locations, Location current, IEnumerable<Feature>? features = null, bool canTravel = false)
		: this(locations.Where(x => x.Bounds is not null).ToList(), current, features, canTravel, DefaultCellSize, clip: null)
	{
	}

	/// <summary>
	/// Creates a mini-map of the player's quarter (or the quarter of the house or business they're in) and the quarters
	/// its paths lead to, scaled to fit within the given size. Features are only shown where they're among those
	/// quarters.
	/// </summary>
	public static TownMapViewModel CreateMiniMap(Location current, IEnumerable<Feature>? features, double maxSize)
	{
		var quarter = GetQuarter(current);
		var quarters = quarter is null ? new List<Location>() : new[] { quarter }.Concat(quarter.Exits.Where(x => x.Direction is not null).Select(x => x.Location)).Where(x => x.Bounds is not null).Distinct().ToList();
		var clip = GetBounds(quarters.Select(x => x.Bounds!.Value).ToList());
		var cellSize = clip is { } area ? Math.Min(MaxMiniMapCellSize, maxSize / Math.Max(area.Width, area.Height)) : MaxMiniMapCellSize;
		return new TownMapViewModel(quarters, current, features, canTravel: false, cellSize, clip);
	}

	private TownMapViewModel(IReadOnlyList<Location> quarters, Location current, IEnumerable<Feature>? features, bool canTravel, double cellSize, MapRect? clip)
	{
		CanTravel = canTravel;
		CellSize = cellSize;

		// Features are cut down to the part of the map being shown.
		var shownFeatures = (features ?? [])
			.Select(x => (Feature: x, Bounds: clip is { } area ? Intersect(x.Bounds, area) : x.Bounds))
			.Where(x => x.Bounds is not null)
			.Select(x => (x.Feature, Bounds: x.Bounds!.Value))
			.ToList();
		var extent = clip ?? GetBounds(quarters.Select(x => x.Bounds!.Value).Concat(shownFeatures.Select(x => x.Bounds)).ToList());
		var minX = extent?.X ?? 0;
		var minY = extent?.Y ?? 0;
		var currentQuarter = GetQuarter(current);

		Quarters = quarters.Select(x => new TownMapQuarterViewModel(x, minX, minY, cellSize, x == currentQuarter, canTravel, VisitQuarter)).ToList();
		Features = shownFeatures.Select(x => new TownMapFeatureViewModel(x.Feature, x.Bounds, minX, minY, cellSize)).ToList();
		Width = (extent?.Width ?? 0) * cellSize;
		Height = (extent?.Height ?? 0) * cellSize;

		// Each path is drawn once, from the middle of one quarter to the middle of the edge it leaves by, and on to the
		// middle of the other quarter, crossing the river if it's in between.
		var indexes = quarters.Select((x, index) => (x, index)).ToDictionary(x => x.x, x => x.index);
		var segments = new List<TownMapSegment>();
		foreach (var (quarter, index) in quarters.Select((x, index) => (x, index)))
		{
			var bounds = quarter.Bounds!.Value;
			foreach (var exit in quarter.Exits.Where(x => x.Direction is not null && indexes.GetValueOrDefault(x.Location, -1) > index))
			{
				var other = exit.Location.Bounds!.Value;
				if ((bounds.GetSharedEdge(other) ?? bounds.GetSharedEdge(other, gap: 1)) is { } edge)
				{
					var (edgeX, edgeY) = edge.Side is CompassDirection.North or CompassDirection.South
						? (edge.SharedMiddle, edge.Side == CompassDirection.North ? (double) bounds.Y : bounds.Bottom)
						: (edge.Side == CompassDirection.West ? (double) bounds.X : bounds.Right, edge.SharedMiddle);
					var (startX, startY) = TownMap.GetMiddle(bounds);
					var (endX, endY) = TownMap.GetMiddle(other);
					segments.Add(new TownMapSegment((startX - minX) * cellSize, (startY - minY) * cellSize, (edgeX - minX) * cellSize, (edgeY - minY) * cellSize));
					segments.Add(new TownMapSegment((edgeX - minX) * cellSize, (edgeY - minY) * cellSize, (endX - minX) * cellSize, (endY - minY) * cellSize));
				}
			}
		}

		Paths = segments;
	}

	public event EventHandler? CloseRequested;

	/// <summary>
	/// Raised when a quarter on the map is chosen, to go there. Only raised if travel is allowed.
	/// </summary>
	public event EventHandler<GenericEventArgs<Location>>? TravelRequested;

	public IReadOnlyList<TownMapQuarterViewModel> Quarters { get; }

	/// <summary>
	/// The map features, such as the river or a forest, drawn under the quarters.
	/// </summary>
	public IReadOnlyList<TownMapFeatureViewModel> Features { get; }

	/// <summary>
	/// The line segments that draw the paths between quarters.
	/// </summary>
	public IReadOnlyList<TownMapSegment> Paths { get; }

	/// <summary>
	/// Whether choosing a quarter goes there. Only developers can travel by the map.
	/// </summary>
	public bool CanTravel { get; }

	/// <summary>
	/// The size of one grid cell, in device-independent pixels.
	/// </summary>
	public double CellSize { get; }

	public double Width { get; }

	public double Height { get; }

	public void Close()
	{
		VerifyAccess();
		CloseRequested.Raise(this);
	}

	/// <summary>
	/// Gets the quarter a location is, or is in.
	/// </summary>
	private static Location? GetQuarter(Location location) =>
		location.Bounds is not null ? location : location.Connections.FirstOrDefault(x => x.Bounds is not null);

	/// <summary>
	/// Gets the smallest rectangle holding all of the given ones, or null if there are none.
	/// </summary>
	private static MapRect? GetBounds(IReadOnlyList<MapRect> rects)
	{
		MapRect? bounds = null;
		if (rects.Count != 0)
		{
			var minX = rects.Min(x => x.X);
			var minY = rects.Min(x => x.Y);
			bounds = new MapRect(minX, minY, rects.Max(x => x.Right) - minX, rects.Max(x => x.Bottom) - minY);
		}

		return bounds;
	}

	/// <summary>
	/// Gets the part of a rectangle inside another, or null if they don't overlap.
	/// </summary>
	private static MapRect? Intersect(MapRect rect, MapRect area)
	{
		var x = Math.Max(rect.X, area.X);
		var y = Math.Max(rect.Y, area.Y);
		var right = Math.Min(rect.Right, area.Right);
		var bottom = Math.Min(rect.Bottom, area.Bottom);
		return right > x && bottom > y ? new MapRect(x, y, right - x, bottom - y) : null;
	}

	private void VisitQuarter(Location quarter)
	{
		if (CanTravel)
			TravelRequested.Raise(this, new GenericEventArgs<Location>(quarter));
	}
}

/// <summary>
/// One quarter on the map.
/// </summary>
public sealed class TownMapQuarterViewModel : ViewModelBase
{
	public TownMapQuarterViewModel(Location quarter, int minX, int minY, double cellSize, bool isCurrent, bool canTravel, Action<Location> visit)
	{
		Quarter = quarter;
		var bounds = quarter.Bounds!.Value;
		Left = (bounds.X - minX) * cellSize;
		Top = (bounds.Y - minY) * cellSize;
		Width = bounds.Width * cellSize;
		Height = bounds.Height * cellSize;
		IsCurrent = isCurrent;
		Category = quarter.IsLandmark ? TownMapCategory.Landmark : quarter.Area == MapArea.Outskirts ? TownMapCategory.Outskirts : TownMapCategory.Village;
		m_canTravel = canTravel;
		m_visit = visit;
	}

	public Location Quarter { get; }

	public string Title => Quarter.Title;

	/// <summary>
	/// The quarter's one-sentence description, shown when hovering over it.
	/// </summary>
	public string Description => Quarter.ShortDescription;

	public double Left { get; }

	public double Top { get; }

	public double Width { get; }

	public double Height { get; }

	/// <summary>
	/// Whether the player is in this quarter, or in a house or business in it.
	/// </summary>
	public bool IsCurrent { get; }

	/// <summary>
	/// What kind of place the quarter is, which decides its color.
	/// </summary>
	public TownMapCategory Category { get; }

	/// <summary>
	/// Whether choosing the quarter goes there.
	/// </summary>
	public bool CanTravel => m_canTravel;

	public bool CanVisit() => m_canTravel;

	/// <summary>
	/// Goes to the quarter, if the map allows travel.
	/// </summary>
	public void Visit()
	{
		VerifyAccess();
		if (CanVisit())
			m_visit(Quarter);
	}

	private readonly bool m_canTravel;
	private readonly Action<Location> m_visit;
}

/// <summary>
/// A map feature on the map, such as the river or a forest.
/// </summary>
public sealed class TownMapFeatureViewModel
{
	public TownMapFeatureViewModel(Feature feature, MapRect bounds, int minX, int minY, double cellSize)
	{
		Title = feature.Title;
		IsRiver = feature.Template.Type == MapFeatureType.River;
		Left = (bounds.X - minX) * cellSize;
		Top = (bounds.Y - minY) * cellSize;
		Width = bounds.Width * cellSize;
		Height = bounds.Height * cellSize;
	}

	public string Title { get; }

	/// <summary>
	/// Whether the feature is the river, which is drawn differently from areas such as a forest.
	/// </summary>
	public bool IsRiver { get; }

	public double Left { get; }

	public double Top { get; }

	public double Width { get; }

	public double Height { get; }
}

/// <summary>
/// The kinds of quarter the map colors differently.
/// </summary>
public enum TownMapCategory
{
	Village,
	Outskirts,
	Landmark,
}

/// <summary>
/// A straight line on the map, in device-independent pixels.
/// </summary>
public sealed record TownMapSegment(double X1, double Y1, double X2, double Y2);
