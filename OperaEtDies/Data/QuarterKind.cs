using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// A kind of quarter, such as a lane or the fields. The kind decides where a quarter goes on the map, which
/// businesses it can hold, and what the paths to it are like.
/// </summary>
public sealed class QuarterKind : IIdentified
{
	public required string Id { get; init; }

	/// <summary>
	/// The part of the map quarters of this kind are in.
	/// </summary>
	public required MapArea Area { get; init; }

	/// <summary>
	/// Whether every town has exactly one quarter of this kind, such as the village square, so events can refer to it.
	/// </summary>
	public bool Unique { get; init; }

	/// <summary>
	/// What a path to or from a quarter of this kind is called, such as "lane" or "track".
	/// </summary>
	public required string Path { get; init; }

	/// <summary>
	/// The id of a map feature every quarter of this kind borders, such as a river. Quarters of this kind are only used in
	/// towns that have the feature.
	/// </summary>
	public string? Borders { get; init; }

	/// <summary>
	/// How many minutes it takes to walk a path to or from a quarter of this kind. A path between two quarters takes the
	/// longer of their kinds' times.
	/// </summary>
	public required int TravelTime { get; init; }
}

/// <summary>
/// The parts of the map: the village itself, and the outskirts around it.
/// </summary>
public enum MapArea
{
	Village,
	Outskirts,
}

/// <summary>
/// One of the shapes a town can be laid out in, chosen by frequency.
/// </summary>
public sealed class TownLayoutChoice : IHasFrequency
{
	/// <summary>
	/// The shape, which is built by code in the engine.
	/// </summary>
	public required LayoutShape Shape { get; init; }

	/// <summary>
	/// The id of the unique village quarter kind the shape is built around, such as the village square.
	/// </summary>
	public required string Center { get; init; }

	public FrequencyLevel Frequency { get; init; } = new(Utility.Random.Frequency.Common);
}

/// <summary>
/// The shapes the engine can lay a town out in.
/// </summary>
public enum LayoutShape
{
	/// <summary>
	/// Village quarters ring the center and branch outward from it, with the outskirts beyond them.
	/// </summary>
	Radial,

	/// <summary>
	/// A main street runs straight through the center, sometimes with a second street alongside it.
	/// </summary>
	Linear,

	/// <summary>
	/// Two streets cross at the center, with arms leading out to the north, east, south, and west.
	/// </summary>
	Crossroads,

	/// <summary>
	/// A tight tangle of lanes around the center, with many small loops.
	/// </summary>
	Warren,
}

/// <summary>
/// The size of a quarter on the map, in grid cells. The quarter can be turned either way.
/// </summary>
public sealed class QuarterSize
{
	public int Length { get; init; } = 1;

	public int Width { get; init; } = 1;
}
