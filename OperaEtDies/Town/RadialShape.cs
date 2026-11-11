using OperaEtDies.Utility.Random;

namespace OperaEtDies.Town;

/// <summary>
/// A ring of village quarters surrounds the center, some of them connected to their neighbors in the ring. The rest of
/// the village branches off the ring.
/// </summary>
internal sealed class RadialShape(TownMap map, Random random) : TownShape(map, random)
{
	protected override IReadOnlyList<MapQuarter> BuildCore(MapQuarter center, IList<MapQuarter> remaining)
	{
		// Each quarter tries to touch the ring quarters already placed, so neighbors in the ring can be connected.
		var ring = new List<MapQuarter>();
		var ringCount = Math.Min(remaining.Count, Random.Next(MinRing, MaxRing + 1));
		foreach (var quarter in remaining.Take(ringCount).ToList())
		{
			var sides = Sides.OrderBy(center.GetExitCount).ThenBy(_ => Random.Next()).ToList();
			if (Map.TryPlaceNextTo(quarter, center, sides, rect => ScoreVillageSpot(quarter, rect)))
			{
				ring.Add(quarter);
				remaining.Remove(quarter);
				Depths[quarter] = 1;
			}
		}

		ConnectTouching(ring, RingConnectionChance);
		return [];
	}

	private const int MinRing = 4;
	private const int MaxRing = 6;
	private const double RingConnectionChance = 60;
}
