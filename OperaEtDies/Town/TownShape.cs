using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Town;

/// <summary>
/// Arranges a town's quarters on the map in some shape. Each shape lays out the core of the village around the center
/// in its own way. The rest of the village then branches off the core, kept close to it, with quarters of the same
/// wealth tending to sit together. The outskirts leave the village from a few quarters on different sides of it, in
/// small groups of various patterns. Landmarks are kept out of dead ends where possible, so they're on the way somewhere.
/// </summary>
internal abstract class TownShape
{
	/// <summary>
	/// Creates the code that builds the given shape.
	/// </summary>
	public static TownShape Create(LayoutShape shape, TownMap map, Random random) => shape switch
	{
		LayoutShape.Radial => new RadialShape(map, random),
		LayoutShape.Linear => new LinearShape(map, random),
		LayoutShape.Crossroads => new CrossroadsShape(map, random),
		LayoutShape.Warren => new WarrenShape(map, random),
		_ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
	};

	/// <summary>
	/// Places every quarter, starting with the center. With a river, the river runs along one side of the village once
	/// the village is in place, so the outskirts can cross it, and quarters that must border it are placed along it once
	/// everything else is in place.
	/// </summary>
	public void Build(MapQuarter center, IReadOnlyList<MapQuarter> others, string? riverId = null)
	{
		Map.PlaceFirst(center);
		Depths[center] = 0;

		var byRiver = others.Where(x => riverId is not null && x.Borders.Contains(riverId)).ToList();
		var rest = others.Where(x => !byRiver.Contains(x)).ToList();
		var remaining = OrderVillage(rest.Where(x => x.Kind.Area == MapArea.Village).ToList());
		var ends = BuildCore(center, remaining);
		BuildBranches(center, remaining);
		if (riverId is not null)
			Map.AddRiver();
		BuildOutskirts(center, rest.Where(x => x.Kind.Area == MapArea.Outskirts).ToList(), ends);
		foreach (var quarter in byRiver)
			PlaceByRiver(quarter);

		foreach (var (first, second) in Map.GetPairsAcrossRiver().Where(_ => Random.NextChance(CrossingChance)).ToList())
			Map.TryConnect(first, second);
		ConnectDeadEndLandmarks();
	}

	protected TownShape(TownMap map, Random random)
	{
		Map = map;
		Random = random;
		Depths = [];
	}

	protected TownMap Map { get; }

	protected Random Random { get; }

	/// <summary>
	/// How many steps each placed quarter is from the center, following the paths that placed it.
	/// </summary>
	protected Dictionary<MapQuarter, int> Depths { get; }

	/// <summary>
	/// Lays out the core of the village around the center, taking the quarters it uses from the front of the remaining
	/// village quarters, which are roughly richest first. Returns the ends of the core, in the order the outskirts
	/// should prefer to leave from them, or an empty list if the outskirts can leave from anywhere.
	/// </summary>
	protected abstract IReadOnlyList<MapQuarter> BuildCore(MapQuarter center, IList<MapQuarter> remaining);

	/// <summary>
	/// How far from the center, in steps, the rest of the village can branch.
	/// </summary>
	protected virtual int MaxVillageDepth => 3;

	/// <summary>
	/// How much a village quarter prefers a spot for each quarter it would touch.
	/// </summary>
	protected virtual double TouchingWeight => 1.5;

	/// <summary>
	/// The chance that two village quarters that touch are connected, beyond the paths that placed them.
	/// </summary>
	protected virtual double VillageConnectionChance => 25;

	/// <summary>
	/// Whether the rest of the village can branch straight off the center, beyond the core.
	/// </summary>
	protected virtual bool CanBranchFromCenter => false;

	/// <summary>
	/// Orders the quarters the rest of the village can branch from, best first: by default, those nearest the center.
	/// </summary>
	protected virtual IEnumerable<MapQuarter> OrderParents(IReadOnlyList<MapQuarter> village) =>
		village.Where(x => Depths.GetValueOrDefault(x) < MaxVillageDepth).OrderBy(x => Depths.GetValueOrDefault(x)).ThenBy(_ => Random.Next());

	/// <summary>
	/// Village quarters stay close to the center, and prefer spots touching other quarters, especially quarters of the
	/// same wealth.
	/// </summary>
	protected double ScoreVillageSpot(MapQuarter quarter, MapRect rect) =>
		Map.Quarters.Where(x => x.Rect.GetSharedEdge(rect) is not null).Sum(x => TouchingWeight + (IsSameWealth(quarter, x) ? SameWealthWeight : 0))
		- Map.GetDistanceFromCenter(rect) + Random.NextDouble();

	/// <summary>
	/// Places quarters in a straight line from the start, on the given side, as far as there's room. Each quarter lines
	/// up with the start as closely as it can. Returns the quarters placed, in order.
	/// </summary>
	protected IReadOnlyList<MapQuarter> PlaceLine(MapQuarter start, CompassDirection side, IEnumerable<MapQuarter> quarters, int depth)
	{
		var placed = new List<MapQuarter>();
		var end = start;
		var (startX, startY) = TownMap.GetMiddle(start.Rect);
		var isVertical = side is CompassDirection.North or CompassDirection.South;
		foreach (var quarter in quarters.Where(_ => end.GetExitCount(side) < TownMap.MaxExitsPerSide))
		{
			double Score(MapRect rect)
			{
				var (x, y) = TownMap.GetMiddle(rect);
				var isAlongLine = isVertical ? rect.Height >= rect.Width : rect.Width >= rect.Height;
				return -Math.Abs(isVertical ? x - startX : y - startY) * 2 + (isAlongLine ? 0.5 : 0) + Random.NextDouble() / 10;
			}

			if (Map.TryPlaceNextTo(quarter, end, [side], Score))
			{
				placed.Add(quarter);
				Depths[quarter] = depth;
				end = quarter;
			}
		}

		return placed;
	}

	/// <summary>
	/// Places a quarter next to the first of the given parents that has room, on a side facing away from the center if
	/// possible. Falls back to any quarter on the map if none of the parents has room.
	/// </summary>
	protected bool Place(MapQuarter quarter, IReadOnlyList<MapQuarter> parents, Func<MapRect, double> score)
	{
		var ordered = parents.Concat(Map.Quarters.Where(x => !parents.Contains(x)).OrderBy(x => Map.GetDistanceFromCenter(x.Rect))).ToList();
		var isPlaced = false;
		foreach (var parent in ordered.Where(_ => !isPlaced))
		{
			isPlaced = Map.TryPlaceNextTo(quarter, parent, Map.GetOutwardSides(parent), score);
			if (isPlaced)
				Depths[quarter] = Depths.GetValueOrDefault(parent) + 1;
		}

		return isPlaced;
	}

	/// <summary>
	/// Connects each pair of the given quarters that touch, with the given chance.
	/// </summary>
	protected void ConnectTouching(IReadOnlyList<MapQuarter> quarters, double chance)
	{
		foreach (var (first, second) in GetTouchingPairs(quarters).Where(_ => Random.NextChance(chance)))
			Map.TryConnect(first, second);
	}

	protected static IEnumerable<(MapQuarter First, MapQuarter Second)> GetTouchingPairs(IReadOnlyList<MapQuarter> quarters) =>
		quarters.SelectMany((first, index) => quarters.Skip(index + 1).Where(second => TownMap.AreTouching(first, second)).Select(second => (first, second)));

	protected static readonly CompassDirection[] Sides = [CompassDirection.North, CompassDirection.East, CompassDirection.South, CompassDirection.West];

	/// <summary>
	/// Orders the village quarters roughly richest first, so the core near the center tends to be wealthier. Some
	/// quarters are put anywhere instead, so the pattern isn't too regular. Landmarks go anywhere.
	/// </summary>
	private IList<MapQuarter> OrderVillage(IReadOnlyList<MapQuarter> village)
	{
		var shuffled = village.ToList();
		Random.Shuffle(shuffled);
		return shuffled
			.Select(x => (Quarter: x, Rank: x.Template.Wealth is not { } wealth || Random.NextChance(WealthExceptionChance) ? Random.NextDouble() * 3 : 3 - (int) wealth + Random.NextDouble()))
			.OrderBy(x => x.Rank)
			.Select(x => x.Quarter)
			.ToList();
	}

	/// <summary>
	/// Branches the rest of the village off the core. Landmarks go first, and the next quarter placed goes beyond a
	/// landmark that doesn't lead anywhere yet, so landmarks lead somewhere.
	/// </summary>
	private void BuildBranches(MapQuarter center, IList<MapQuarter> remaining)
	{
		foreach (var quarter in remaining.OrderBy(x => x.Template.IsLandmark ? 0 : 1).ToList())
		{
			var placed = Map.Quarters.Where(x => x.Kind.Area == MapArea.Village && (x != center || CanBranchFromCenter)).ToList();
			var deadEndLandmarks = placed.Where(x => x.Template.IsLandmark && x.Neighbors.Count < 2).ToList();
			// A quarter usually branches off one of the same wealth, so neighborhoods form, but not always.
			var prefersSameWealth = !Random.NextChance(WealthExceptionChance);
			var parents = !quarter.Template.IsLandmark && deadEndLandmarks.Count != 0
				? deadEndLandmarks
				: OrderParents(placed.Count != 0 ? placed : [center]).OrderBy(x => prefersSameWealth && IsSameWealth(quarter, x) ? 0 : 1).ToList();
			Place(quarter, parents, rect => ScoreVillageSpot(quarter, rect));
		}

		ConnectTouching(Map.Quarters.Where(x => x.Kind.Area == MapArea.Village && x != center).ToList(), VillageConnectionChance);
	}

	private void BuildOutskirts(MapQuarter center, IReadOnlyList<MapQuarter> outskirts, IReadOnlyList<MapQuarter> ends)
	{
		var groups = new List<List<MapQuarter>>();
		if (outskirts.Count != 0)
		{
			// Every group gets a quarter that isn't a landmark if there are enough, so its landmarks can lead on to it.
			var shuffled = outskirts.ToList();
			Random.Shuffle(shuffled);
			var others = shuffled.Where(x => !x.Template.IsLandmark).ToList();
			var landmarks = shuffled.Where(x => x.Template.IsLandmark).ToList();
			var groupCount = Math.Min(others.Count != 0 ? others.Count : shuffled.Count, Random.Next(MinOutskirtGroups, MaxOutskirtGroups + 1));
			for (var index = 0; index < groupCount; index++)
				groups.Add([]);
			var ordered = others.Concat(landmarks).ToList();
			for (var index = 0; index < ordered.Count; index++)
				groups[index % groupCount].Add(ordered[index]);
		}

		var gateways = ChooseGateways(center, groups.Count, ends);
		for (var index = 0; index < groups.Count; index++)
		{
			// Landmarks go first, so they lead on to another quarter rather than ending the group.
			var group = groups[index].OrderBy(x => x.Template.IsLandmark ? 0 : 1).ToList();
			BuildOutskirtGroup(gateways[index], group, ChoosePattern(group.Count));
		}
	}

	/// <summary>
	/// Chooses how a group of outskirts is arranged. Loops and hubs need three quarters, and ladders four.
	/// </summary>
	private OutskirtPattern ChoosePattern(int count)
	{
		var patterns = new List<(OutskirtPattern Pattern, double Weight)> { (OutskirtPattern.Chain, 40) };
		if (count >= 3)
		{
			patterns.Add((OutskirtPattern.Loop, 25));
			patterns.Add((OutskirtPattern.Hub, 20));
		}

		if (count >= 4)
			patterns.Add((OutskirtPattern.Ladder, 15));

		return patterns[Random.NextWeighted(Enumerable.Range(0, patterns.Count).ToList(), x => patterns[x].Weight)].Pattern;
	}

	/// <summary>
	/// Places a group of outskirts, starting from the gateway, heading away from the center.
	/// <list type="bullet">
	/// <item>A chain goes on from the last quarter, sometimes forking from an earlier one.</item>
	/// <item>A loop is a chain of three whose third quarter connects back to the first.</item>
	/// <item>A hub has every quarter after the first branch off the first.</item>
	/// <item>A ladder is two chains side by side, with rungs connecting them where they touch.</item>
	/// </list>
	/// </summary>
	private void BuildOutskirtGroup(MapQuarter gateway, IReadOnlyList<MapQuarter> group, OutskirtPattern pattern)
	{
		var members = new List<MapQuarter>();
		foreach (var quarter in group)
		{
			IReadOnlyList<MapQuarter> parents = members.Count == 0 ? [gateway] : pattern switch
			{
				OutskirtPattern.Loop when members.Count < 3 => [members[^1]],
				OutskirtPattern.Hub => [members[0]],
				OutskirtPattern.Ladder when members.Count == 1 => [gateway, members[0]],
				OutskirtPattern.Ladder => [members[^2]],
				_ => Random.NextChance(ForkChance) ? members.ToList() : [members[^1]],
			};

			if (Place(quarter, parents, ScoreOutskirtsSpot))
				members.Add(quarter);
		}

		if (pattern == OutskirtPattern.Loop && members.Count >= 3)
			Map.TryConnect(members[0], members[2]);
		ConnectTouching(members, pattern == OutskirtPattern.Ladder ? LadderConnectionChance : OutskirtsConnectionChance);
	}

	/// <summary>
	/// Chooses the village quarters the groups of outskirts leave from. The ends of the core come first, in order. The
	/// rest are quarters at the edge of the village, spread around it so the groups head off in different directions.
	/// </summary>
	private IReadOnlyList<MapQuarter> ChooseGateways(MapQuarter center, int count, IReadOnlyList<MapQuarter> ends)
	{
		var candidates = Map.Quarters.Where(x => x.Kind.Area == MapArea.Village && x != center).ToList();
		if (candidates.Count == 0)
			candidates = [center];

		Random.Shuffle(candidates);
		candidates = candidates.OrderByDescending(x => Math.Round(Map.GetDistanceFromCenter(x.Rect))).ToList();

		var gateways = ends.Take(count).ToList();
		while (gateways.Count < count)
		{
			// The next gateway is the one whose direction from the center is farthest from every gateway so far.
			var next = candidates.MaxBy(x => gateways.Count == 0 ? 0 : gateways.Min(gateway => GetAngleBetween(gateway, x)))!;
			gateways.Add(next);
		}

		return gateways;
	}

	/// <summary>
	/// Gets the angle, in degrees from 0 to 180, between the directions of two quarters from the center of the map.
	/// </summary>
	private double GetAngleBetween(MapQuarter first, MapQuarter second)
	{
		double GetAngle(MapQuarter quarter)
		{
			var (x, y) = TownMap.GetMiddle(quarter.Rect);
			return Math.Atan2(y - Map.Center.Y, x - Map.Center.X) * 180 / Math.PI;
		}

		var difference = Math.Abs(GetAngle(first) - GetAngle(second)) % 360;
		return difference > 180 ? 360 - difference : difference;
	}

	/// <summary>
	/// Outskirts quarters head away from the center.
	/// </summary>
	private double ScoreOutskirtsSpot(MapRect rect) => Map.GetDistanceFromCenter(rect) + Random.NextDouble();

	/// <summary>
	/// Places a quarter that must border the river touching the river, next to whichever quarter can reach it that's
	/// closest to the center. If no quarter can, it goes as close to the river as there's room for.
	/// </summary>
	private void PlaceByRiver(MapQuarter quarter)
	{
		var parents = Map.Quarters.OrderBy(x => Map.GetDistanceFromRiver(x.Rect) + Map.GetDistanceFromCenter(x.Rect) / 10 + Random.NextDouble()).ToList();
		var isPlaced = false;
		foreach (var mustTouch in new[] { true, false })
		{
			foreach (var parent in parents.Where(_ => !isPlaced))
			{
				var sides = Map.GetOutwardSides(parent).OrderBy(side => Map.GetDistanceFromRiver(Move(parent.Rect, side))).ToList();
				isPlaced = Map.TryPlaceNextTo(quarter, parent, sides, rect => -Map.GetDistanceFromRiver(rect) * RiverWeight - Map.GetDistanceFromCenter(rect) / 5 + Random.NextDouble(),
					rect => !mustTouch || Map.GetRiverSide(rect) is not null);
				if (isPlaced)
					Depths[quarter] = Depths.GetValueOrDefault(parent) + 1;
			}
		}
	}

	/// <summary>
	/// Moves a rectangle one cell toward the given side.
	/// </summary>
	private static MapRect Move(MapRect rect, CompassDirection side) => side switch
	{
		CompassDirection.North => rect with { Y = rect.Y - 1 },
		CompassDirection.South => rect with { Y = rect.Y + 1 },
		CompassDirection.East => rect with { X = rect.X + 1 },
		_ => rect with { X = rect.X - 1 },
	};

	/// <summary>
	/// Landmarks that ended up at a dead end connect to a quarter that touches them, if there is one.
	/// </summary>
	private void ConnectDeadEndLandmarks()
	{
		foreach (var landmark in Map.Quarters.Where(x => x.Template.IsLandmark && x.Neighbors.Count < 2).ToList())
		{
			foreach (var other in Map.Quarters.Where(x => x != landmark && TownMap.AreTouching(x, landmark)).ToList())
			{
				if (landmark.Neighbors.Count < 2)
					Map.TryConnect(landmark, other);
			}
		}
	}

	private static bool IsSameWealth(MapQuarter first, MapQuarter second) =>
		first.Template.Wealth is { } wealth && second.Template.Wealth == wealth;

	private enum OutskirtPattern
	{
		Chain,
		Loop,
		Hub,
		Ladder,
	}

	private const double RiverWeight = 5;
	private const double CrossingChance = 25;
	private const int MinOutskirtGroups = 2;
	private const int MaxOutskirtGroups = 3;
	private const double SameWealthWeight = 1.5;
	private const double WealthExceptionChance = 25;
	private const double OutskirtsConnectionChance = 20;
	private const double LadderConnectionChance = 70;
	private const double ForkChance = 30;
}
