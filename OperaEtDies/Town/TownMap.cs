using System.Collections.ObjectModel;
using GoldenAnvil.Utility.Logging;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Town;

/// <summary>
/// Places quarters as rectangles on a grid and connects them where they touch, or where they face each other across
/// the river. Once every quarter is placed, each connection gets a compass direction from the side of the quarter it
/// leaves by and where along that side it is, so that several exits on one side point different ways and no quarter has
/// two exits in the same direction. Areas such as a forest can then be placed in the empty space between quarters.
/// </summary>
internal sealed class TownMap
{
	/// <summary>
	/// The most exits a quarter can have on one side, which keeps every exit's direction within one step of its side.
	/// </summary>
	public const int MaxExitsPerSide = 2;

	public TownMap(Random random)
	{
		m_random = random;
		m_quarters = [];
		m_connections = [];
		m_areas = [];
	}

	/// <summary>
	/// The quarters placed so far, in the order they were placed.
	/// </summary>
	public IReadOnlyList<MapQuarter> Quarters => m_quarters;

	/// <summary>
	/// The middle of the first quarter placed, which shapes build around.
	/// </summary>
	public (double X, double Y) Center { get; private set; }

	/// <summary>
	/// The river, if the map has one: a line of cells one cell wide across the whole map.
	/// </summary>
	public MapRiver? River { get; private set; }

	/// <summary>
	/// Places the first quarter at the middle of the map.
	/// </summary>
	public void PlaceFirst(MapQuarter quarter)
	{
		var (width, height) = ChooseOrientation(quarter);
		quarter.Rect = new MapRect(0, 0, width, height);
		Center = GetMiddle(quarter.Rect);
		m_quarters.Add(quarter);
	}

	/// <summary>
	/// Runs a river straight across the map, east to west or north to south, along one side of the quarters placed so far:
	/// touching the outermost of them, or one cell beyond them.
	/// </summary>
	public void AddRiver()
	{
		var rects = m_quarters.Select(x => x.Rect).ToList();
		var side = s_sides[m_random.Next(s_sides.Length)];
		var gap = m_random.Next(2);
		var line = side switch
		{
			CompassDirection.North => rects.Min(x => x.Y) - 1 - gap,
			CompassDirection.South => rects.Max(x => x.Bottom) + gap,
			CompassDirection.West => rects.Min(x => x.X) - 1 - gap,
			_ => rects.Max(x => x.Right) + gap,
		};
		River = new MapRiver(side is CompassDirection.North or CompassDirection.South, line);
	}

	/// <summary>
	/// Places a quarter touching the parent on the first of the given sides that has room, connecting the two. Where the
	/// river is in the way, the quarter goes on the far bank, facing the parent across it. Among the spots on that side,
	/// the one with the highest score is chosen, with ties broken at random. Only spots that are allowed count, if a test
	/// is given. Returns false if no side has room.
	/// </summary>
	public bool TryPlaceNextTo(MapQuarter quarter, MapQuarter parent, IReadOnlyList<CompassDirection> sides, Func<MapRect, double> score, Func<MapRect, bool>? isAllowed = null)
	{
		var isPlaced = false;
		foreach (var side in sides.Where(x => !isPlaced && parent.GetExitCount(x) < MaxExitsPerSide))
		{
			var candidates = GetSpots(quarter, parent.Rect, side)
				.Select(x => River is { } river && river.Overlaps(x) ? Shift(x, side) : x)
				.Where(x => IsFree(x) && (isAllowed?.Invoke(x) ?? true))
				.ToList();
			if (candidates.Count != 0)
			{
				m_random.Shuffle(candidates);
				quarter.Rect = candidates.MaxBy(score);
				m_quarters.Add(quarter);
				Connect(parent, quarter, isTree: true);
				isPlaced = true;
			}
		}

		return isPlaced;
	}

	/// <summary>
	/// Connects two placed quarters that touch along an edge, or face each other across the river, if they aren't
	/// connected already and both sides have room for another exit. Returns whether they're connected now.
	/// </summary>
	public bool TryConnect(MapQuarter first, MapQuarter second)
	{
		var isConnected = AreConnected(first, second);
		if (!isConnected && GetEdge(first, second) is { } edge && first.GetExitCount(edge.Side) < MaxExitsPerSide && second.GetExitCount(edge.Side.Opposite()) < MaxExitsPerSide)
		{
			Connect(first, second, isTree: false);
			isConnected = true;
		}

		return isConnected;
	}

	public static bool AreConnected(MapQuarter first, MapQuarter second) => first.Neighbors.Contains(second);

	/// <summary>
	/// Whether two placed quarters touch along an edge.
	/// </summary>
	public static bool AreTouching(MapQuarter first, MapQuarter second) => first.Rect.GetSharedEdge(second.Rect) is not null;

	/// <summary>
	/// Gets the pairs of placed quarters that face each other directly across the river.
	/// </summary>
	public IEnumerable<(MapQuarter First, MapQuarter Second)> GetPairsAcrossRiver() =>
		m_quarters.SelectMany((first, index) => m_quarters.Skip(index + 1).Where(second => IsAcrossRiver(first, second)).Select(second => (first, second)));

	/// <summary>
	/// Gets the side of a quarter that touches the river, or null if it doesn't.
	/// </summary>
	public CompassDirection? GetRiverSide(MapRect rect) => River switch
	{
		{ IsHorizontal: true } river when rect.Y == river.Line + 1 => CompassDirection.North,
		{ IsHorizontal: true } river when rect.Bottom == river.Line => CompassDirection.South,
		{ IsHorizontal: false } river when rect.X == river.Line + 1 => CompassDirection.West,
		{ IsHorizontal: false } river when rect.Right == river.Line => CompassDirection.East,
		_ => null,
	};

	/// <summary>
	/// Gets how far a rectangle is from the river, in cells: 0 if it touches it. Without a river, it's very far.
	/// </summary>
	public double GetDistanceFromRiver(MapRect rect) => River switch
	{
		{ IsHorizontal: true } river => Math.Max(0, Math.Max(rect.Y - river.Line - 1, river.Line - rect.Bottom)),
		{ IsHorizontal: false } river => Math.Max(0, Math.Max(rect.X - river.Line - 1, river.Line - rect.Right)),
		_ => double.MaxValue,
	};

	/// <summary>
	/// Gets how far a rectangle's middle is from the middle of the map.
	/// </summary>
	public double GetDistanceFromCenter(MapRect rect)
	{
		var (x, y) = GetMiddle(rect);
		return Math.Sqrt(Math.Pow(x - Center.X, 2) + Math.Pow(y - Center.Y, 2));
	}

	/// <summary>
	/// Gets the four sides of a quarter, starting with the one facing most directly away from the middle of the map.
	/// Sides facing equally far away are in random order.
	/// </summary>
	public IReadOnlyList<CompassDirection> GetOutwardSides(MapQuarter quarter)
	{
		var (x, y) = GetMiddle(quarter.Rect);
		var sides = s_sides.ToList();
		m_random.Shuffle(sides);
		return sides.OrderByDescending(side => side switch
		{
			CompassDirection.North => Center.Y - y,
			CompassDirection.South => y - Center.Y,
			CompassDirection.East => x - Center.X,
			_ => Center.X - x,
		}).ToList();
	}

	/// <summary>
	/// Places an area of the given size in empty space touching the quarter, away from the middle of the map if possible,
	/// preferring spots that touch more quarters. Returns the area's rectangle, or null if there's no room next to the
	/// quarter.
	/// </summary>
	public MapRect? TryPlaceArea(QuarterSize size, MapQuarter quarter)
	{
		var orientations = size.Length == size.Width ? [(size.Length, size.Width)] : new[] { (size.Length, size.Width), (size.Width, size.Length) };
		MapRect? placed = null;
		foreach (var side in GetOutwardSides(quarter).Where(_ => placed is null))
		{
			var candidates = orientations.SelectMany(x => GetSpots(x.Item1, x.Item2, quarter.Rect, side))
				.Where(x => IsFree(x) && !m_areas.Any(area => area.Overlaps(x)))
				.ToList();
			if (candidates.Count != 0)
			{
				m_random.Shuffle(candidates);
				var rect = candidates.MaxBy(x => m_quarters.Count(other => other.Rect.GetSharedEdge(x) is not null) + GetDistanceFromCenter(x) / 10);
				m_areas.Add(rect);
				placed = rect;
			}
		}

		return placed;
	}

	/// <summary>
	/// Gives every connection a direction and turns it into a path between the quarters' locations, and records where
	/// each quarter is. Connections that can't get a direction of their own without clashing are dropped, unless the
	/// map needs them to stay connected.
	/// </summary>
	public void Finish(Func<MapQuarter, MapQuarter, bool, string> getPath)
	{
		var directions = AssignDirections();
		foreach (var connection in m_connections.Where(directions.ContainsKey))
		{
			var isCrossing = IsAcrossRiver(connection.First, connection.Second);
			connection.First.Location.Connect(connection.Second.Location, directions[connection], getPath(connection.First, connection.Second, isCrossing), isCrossing);
		}

		foreach (var quarter in m_quarters)
			quarter.Location.Bounds = quarter.Rect;
	}

	public static (double X, double Y) GetMiddle(MapRect rect) => (rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0);

	/// <summary>
	/// Whether two quarters face each other directly across the river, with only the river between them.
	/// </summary>
	private bool IsAcrossRiver(MapQuarter first, MapQuarter second) =>
		River is { } && first.Rect.GetSharedEdge(second.Rect) is null && first.Rect.GetSharedEdge(second.Rect, gap: 1) is { } edge && River.Overlaps(Between(first.Rect, edge.Side));

	/// <summary>
	/// Gets the edge two quarters share, either touching or facing each other across the river.
	/// </summary>
	private (CompassDirection Side, int SharedLength, double SharedMiddle)? GetEdge(MapQuarter first, MapQuarter second) =>
		first.Rect.GetSharedEdge(second.Rect) ?? (IsAcrossRiver(first, second) ? first.Rect.GetSharedEdge(second.Rect, gap: 1) : null);

	private bool IsFree(MapRect rect) => !m_quarters.Any(other => other.Rect.Overlaps(rect)) && !(River?.Overlaps(rect) ?? false);

	private void Connect(MapQuarter first, MapQuarter second, bool isTree)
	{
		var side = GetEdge(first, second)!.Value.Side;
		first.AddExit(side, second);
		second.AddExit(side.Opposite(), first);
		m_connections.Add(new Connection(first, second, isTree));
	}

	private (int Width, int Height) ChooseOrientation(MapQuarter quarter)
	{
		var size = quarter.Template.Size;
		return m_random.Next(2) == 0 ? (size.Length, size.Width) : (size.Width, size.Length);
	}

	/// <summary>
	/// Gets every rectangle the quarter could take that shares at least one cell of edge with the given side of the
	/// parent, in either orientation.
	/// </summary>
	private static IEnumerable<MapRect> GetSpots(MapQuarter quarter, MapRect parent, CompassDirection side)
	{
		var size = quarter.Template.Size;
		var orientations = size.Length == size.Width ? [(size.Length, size.Width)] : new[] { (size.Length, size.Width), (size.Width, size.Length) };
		return orientations.SelectMany(x => GetSpots(x.Item1, x.Item2, parent, side));
	}

	private static IEnumerable<MapRect> GetSpots(int width, int height, MapRect parent, CompassDirection side)
	{
		if (side is CompassDirection.North or CompassDirection.South)
		{
			var y = side == CompassDirection.North ? parent.Y - height : parent.Bottom;
			for (var x = parent.X - width + 1; x < parent.Right; x++)
				yield return new MapRect(x, y, width, height);
		}
		else
		{
			var x = side == CompassDirection.West ? parent.X - width : parent.Right;
			for (var y = parent.Y - height + 1; y < parent.Bottom; y++)
				yield return new MapRect(x, y, width, height);
		}
	}

	/// <summary>
	/// Moves a rectangle one cell further toward the given side.
	/// </summary>
	private static MapRect Shift(MapRect rect, CompassDirection side) => side switch
	{
		CompassDirection.North => rect with { Y = rect.Y - 1 },
		CompassDirection.South => rect with { Y = rect.Y + 1 },
		CompassDirection.East => rect with { X = rect.X + 1 },
		_ => rect with { X = rect.X - 1 },
	};

	/// <summary>
	/// Gets the line of cells one cell wide just beyond the given side of a rectangle.
	/// </summary>
	private static MapRect Between(MapRect rect, CompassDirection side) => side switch
	{
		CompassDirection.North => new MapRect(rect.X, rect.Y - 1, rect.Width, 1),
		CompassDirection.South => new MapRect(rect.X, rect.Bottom, rect.Width, 1),
		CompassDirection.East => new MapRect(rect.Right, rect.Y, 1, rect.Height),
		_ => new MapRect(rect.X - 1, rect.Y, 1, rect.Height),
	};

	/// <summary>
	/// Chooses a direction for each connection. A connection leaving by a side points that way, or one step to either
	/// side, preferring the step toward the end of the side it's nearest. Paths that were needed to place quarters are
	/// assigned first, searching every combination if needed. The other paths are then added where they fit.
	/// </summary>
	private Dictionary<Connection, CompassDirection> AssignDirections()
	{
		var used = m_quarters.ToDictionary(x => x, _ => new HashSet<CompassDirection>());
		var directions = new Dictionary<Connection, CompassDirection>();

		bool IsFree(Connection connection, CompassDirection direction) =>
			!used[connection.First].Contains(direction) && !used[connection.Second].Contains(direction.Opposite());

		void Use(Connection connection, CompassDirection direction, bool isUsed)
		{
			if (isUsed)
			{
				used[connection.First].Add(direction);
				used[connection.Second].Add(direction.Opposite());
				directions[connection] = direction;
			}
			else
			{
				used[connection.First].Remove(direction);
				used[connection.Second].Remove(direction.Opposite());
				directions.Remove(connection);
			}
		}

		var tree = m_connections.Where(x => x.IsTree).ToList();
		var steps = 0;
		bool AssignTree(int index)
		{
			var isDone = index == tree.Count;
			if (!isDone && steps++ < MaxSearchSteps)
			{
				foreach (var direction in GetCandidateDirections(tree[index]).Where(x => !isDone && IsFree(tree[index], x)))
				{
					Use(tree[index], direction, true);
					isDone = AssignTree(index + 1);
					if (!isDone)
						Use(tree[index], direction, false);
				}
			}

			return isDone;
		}

		if (!AssignTree(0))
		{
			// This shouldn't happen with at most two exits per side, but the map must stay connected regardless.
			Log.Warn("Couldn't give every path a direction of its own, so some quarters have two exits the same way.");
			foreach (var connection in tree.Where(x => !directions.ContainsKey(x)))
				directions[connection] = GetCandidateDirections(connection)[0];
		}

		foreach (var connection in m_connections.Where(x => !x.IsTree))
		{
			var direction = GetCandidateDirections(connection).Where(x => IsFree(connection, x)).Cast<CompassDirection?>().FirstOrDefault();
			if (direction is { } free)
				Use(connection, free, true);
		}

		return directions;
	}

	/// <summary>
	/// Gets the directions a connection could have, from the first quarter's point of view, best first.
	/// </summary>
	private IReadOnlyList<CompassDirection> GetCandidateDirections(Connection connection) =>
		GetDirectionsToward(connection.First.Rect, GetEdge(connection.First, connection.Second)!.Value);

	/// <summary>
	/// Gets the directions toward an edge of a rectangle, best first: the side the edge is on, or one step to either
	/// side, preferring the step toward the end of the side the edge's middle is nearest.
	/// </summary>
	public static IReadOnlyList<CompassDirection> GetDirectionsToward(MapRect rect, (CompassDirection Side, int SharedLength, double SharedMiddle) edge)
	{
		// How far along the side the shared edge's middle is, from the end that's counterclockwise from the side.
		var position = edge.Side switch
		{
			CompassDirection.North => (edge.SharedMiddle - rect.X) / rect.Width,
			CompassDirection.East => (edge.SharedMiddle - rect.Y) / rect.Height,
			CompassDirection.South => (rect.Right - edge.SharedMiddle) / rect.Width,
			_ => (rect.Bottom - edge.SharedMiddle) / rect.Height,
		};

		return new[] { (Direction: edge.Side.Turn(-1), Position: 0.0), (Direction: edge.Side, Position: 0.5), (Direction: edge.Side.Turn(1), Position: 1.0) }
			.OrderBy(x => Math.Abs(x.Position - position))
			.ThenBy(x => x.Direction == edge.Side ? 0 : 1)
			.Select(x => x.Direction)
			.ToList();
	}

	private sealed record Connection(MapQuarter First, MapQuarter Second, bool IsTree);

	private const int MaxSearchSteps = 100000;

	private static readonly CompassDirection[] s_sides = [CompassDirection.North, CompassDirection.East, CompassDirection.South, CompassDirection.West];

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(TownMap));

	private readonly Random m_random;
	private readonly Collection<MapQuarter> m_quarters;
	private readonly Collection<Connection> m_connections;
	private readonly Collection<MapRect> m_areas;
}

/// <summary>
/// A river running straight across the map, one cell wide: a row of cells if it runs east to west, or a column if it runs
/// north to south.
/// </summary>
internal sealed record MapRiver(bool IsHorizontal, int Line)
{
	public bool Overlaps(MapRect rect) => IsHorizontal ? rect.Y <= Line && Line < rect.Bottom : rect.X <= Line && Line < rect.Right;
}

/// <summary>
/// A quarter being placed on the map.
/// </summary>
internal sealed class MapQuarter(QuarterTemplate template, QuarterKind kind, TownLocation location, IReadOnlyList<string> borders)
{
	public QuarterTemplate Template { get; } = template;

	public QuarterKind Kind { get; } = kind;

	public TownLocation Location { get; } = location;

	/// <summary>
	/// The ids of the map features the quarter must border.
	/// </summary>
	public IReadOnlyList<string> Borders { get; } = borders;

	public MapRect Rect { get; set; }

	public int HouseCount { get; set; }

	public int BusinessCount { get; set; }

	/// <summary>
	/// The quarters this one is connected to so far.
	/// </summary>
	public IReadOnlyList<MapQuarter> Neighbors => m_exits.Select(x => x.Neighbor).ToList();

	public int GetExitCount(CompassDirection side) => m_exits.Count(x => x.Side == side);

	public void AddExit(CompassDirection side, MapQuarter neighbor) => m_exits.Add((side, neighbor));

	private readonly Collection<(CompassDirection Side, MapQuarter Neighbor)> m_exits = [];
}
