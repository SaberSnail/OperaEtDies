using System.Text.Json.Serialization;
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
	/// How many quarters of this kind a town has, such as exactly one village square, or at least one field, or null for
	/// no limits.
	/// </summary>
	public QuarterCount? Count { get; init; }

	/// <summary>
	/// The fewest quarters of this kind every town has (unless they're optional).
	/// </summary>
	[JsonIgnore]
	public int MinCount => Count?.Min ?? 0;

	/// <summary>
	/// The most quarters of this kind a town has, or null for no limit.
	/// </summary>
	[JsonIgnore]
	public int? MaxCount => Count?.Max;

	/// <summary>
	/// For a kind with a minimum count, what makes its quarters optional, such as a well in a town with a river.
	/// </summary>
	public QuarterKindOptional? Optional { get; init; }

	/// <summary>
	/// The id of the map feature that makes a quarter of this kind optional, or null if it isn't optional.
	/// </summary>
	[JsonIgnore]
	public string? OptionalWith => Optional?.With;

	/// <summary>
	/// The percentage chance that a town with <see cref="OptionalWith"/> still has a quarter of this kind.
	/// </summary>
	[JsonIgnore]
	public double OptionalChance => Optional?.Chance ?? 50;

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

	/// <summary>
	/// How likely someone is to come by a quarter of this kind, which decides how comfortable someone there alone is with
	/// how covered they are, or null to use the modesty settings' traffic for quarters.
	/// </summary>
	public FrequencyLevel? Traffic { get; init; }

	/// <summary>
	/// How much more (or, when negative, less) comfortable people in a quarter of this kind are with how covered they are.
	/// </summary>
	public int ModestyComfort { get; init; }

	/// <summary>
	/// Whether the player's "Go to" menu offers the nearest quarter of this kind as a destination, such as the well.
	/// </summary>
	public bool GoTo { get; init; }
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
public sealed class TownLayoutChoice : IIdentified, IHasFrequency
{
	public required string Id { get; init; }

	/// <summary>
	/// The shape, which is built by code in the engine.
	/// </summary>
	public required LayoutShape Shape { get; init; }

	/// <summary>
	/// The id of the village quarter kind every town has exactly one of, which the shape is built around, such as the
	/// village square.
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

/// <summary>
/// How many quarters of a kind a town has: at least the minimum, and at most the maximum.
/// </summary>
public sealed class QuarterCount
{
	/// <summary>
	/// The fewest quarters of the kind every town has, or null for none.
	/// </summary>
	public int? Min { get; init; }

	/// <summary>
	/// The most quarters of the kind a town has, or null for no limit.
	/// </summary>
	public int? Max { get; init; }
}

/// <summary>
/// What makes the quarters a kind must have optional: a town with the feature only has the kind's minimum number of
/// quarters some of the time.
/// </summary>
public sealed class QuarterKindOptional
{
	/// <summary>
	/// The id of the map feature that makes the quarter optional.
	/// </summary>
	public required string With { get; init; }

	/// <summary>
	/// The percentage chance that a town with the feature still has a quarter of this kind.
	/// </summary>
	public double Chance { get; init; } = 50;
}
