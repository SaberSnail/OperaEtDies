namespace OperaEtDies.Town;

/// <summary>
/// The eight directions a path can lead in, clockwise from north.
/// </summary>
public enum CompassDirection
{
	North,
	NorthEast,
	East,
	SouthEast,
	South,
	SouthWest,
	West,
	NorthWest,
}

public static class CompassDirectionUtility
{
	/// <summary>
	/// Gets the direction pointing the other way.
	/// </summary>
	public static CompassDirection Opposite(this CompassDirection direction) => (CompassDirection) (((int) direction + 4) % 8);

	/// <summary>
	/// Gets the direction one step clockwise (positive) or counterclockwise (negative) from this one.
	/// </summary>
	public static CompassDirection Turn(this CompassDirection direction, int steps) => (CompassDirection) ((((int) direction + steps) % 8 + 8) % 8);

	/// <summary>
	/// Gets the name of a direction, starting with a capital letter, such as "North-east".
	/// </summary>
	public static string GetTitle(this CompassDirection direction) => direction switch
	{
		CompassDirection.North => OurResources.CompassNorth,
		CompassDirection.NorthEast => OurResources.CompassNorthEast,
		CompassDirection.East => OurResources.CompassEast,
		CompassDirection.SouthEast => OurResources.CompassSouthEast,
		CompassDirection.South => OurResources.CompassSouth,
		CompassDirection.SouthWest => OurResources.CompassSouthWest,
		CompassDirection.West => OurResources.CompassWest,
		CompassDirection.NorthWest => OurResources.CompassNorthWest,
		_ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
	};
}

/// <summary>
/// A rectangle of grid cells on the map. X grows to the east and Y grows to the south.
/// </summary>
public readonly record struct MapRect(int X, int Y, int Width, int Height)
{
	public int Right => X + Width;

	public int Bottom => Y + Height;

	public bool Overlaps(MapRect other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

	/// <summary>
	/// Gets the side of this rectangle that the other one touches along an edge, the length of edge they share, and where
	/// the middle of the shared part is along the side, or null if they only meet at a corner or don't touch at all. With a
	/// gap, the rectangles must face each other that many cells apart instead of touching.
	/// </summary>
	public (CompassDirection Side, int SharedLength, double SharedMiddle)? GetSharedEdge(MapRect other, int gap = 0)
	{
		(CompassDirection Side, int SharedLength, double SharedMiddle)? shared = null;
		var horizontal = Math.Min(Right, other.Right) - Math.Max(X, other.X);
		var vertical = Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y);
		var horizontalMiddle = (Math.Min(Right, other.Right) + Math.Max(X, other.X)) / 2.0;
		var verticalMiddle = (Math.Min(Bottom, other.Bottom) + Math.Max(Y, other.Y)) / 2.0;
		if (horizontal > 0 && other.Bottom + gap == Y)
			shared = (CompassDirection.North, horizontal, horizontalMiddle);
		else if (horizontal > 0 && other.Y == Bottom + gap)
			shared = (CompassDirection.South, horizontal, horizontalMiddle);
		else if (vertical > 0 && other.Right + gap == X)
			shared = (CompassDirection.West, vertical, verticalMiddle);
		else if (vertical > 0 && other.X == Right + gap)
			shared = (CompassDirection.East, vertical, verticalMiddle);

		return shared;
	}
}
