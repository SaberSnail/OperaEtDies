namespace OperaEtDies.Town;

/// <summary>
/// The village grows as a tight tangle of lanes around the center, with no plan to it. Quarters crowd against each
/// other wherever there's room, and many of them are connected to the quarters they touch, so there are lots of small
/// loops and few long streets.
/// </summary>
internal sealed class WarrenShape(TownMap map, Random random) : TownShape(map, random)
{
	protected override IReadOnlyList<MapQuarter> BuildCore(MapQuarter center, IList<MapQuarter> remaining) => [];

	protected override int MaxVillageDepth => 5;

	protected override double TouchingWeight => 3;

	protected override double VillageConnectionChance => 55;

	protected override bool CanBranchFromCenter => true;

	/// <summary>
	/// Any quarter can grow a lane, with those near the center a little more likely, so the tangle grows around the
	/// center rather than off to one side.
	/// </summary>
	protected override IEnumerable<MapQuarter> OrderParents(IReadOnlyList<MapQuarter> village) =>
		village.Where(x => Depths.GetValueOrDefault(x) < MaxVillageDepth).OrderBy(x => Map.GetDistanceFromCenter(x.Rect) + Random.NextDouble() * ParentSpread);

	/// <summary>
	/// How far from the center, in grid cells, a parent can be and still be as likely as one at the center.
	/// </summary>
	private const double ParentSpread = 3;
}
