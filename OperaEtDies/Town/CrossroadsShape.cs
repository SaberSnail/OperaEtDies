using OperaEtDies.Utility.Random;

namespace OperaEtDies.Town;

/// <summary>
/// Two streets cross at the center, so four arms of one or two quarters each lead straight out from it to the north,
/// east, south, and west. The rest of the village fills in between the arms, and the outskirts leave first from the
/// ends of the arms.
/// </summary>
internal sealed class CrossroadsShape(TownMap map, Random random) : TownShape(map, random)
{
	protected override IReadOnlyList<MapQuarter> BuildCore(MapQuarter center, IList<MapQuarter> remaining)
	{
		var ends = new List<MapQuarter>();
		var arms = Sides.ToList();
		Random.Shuffle(arms);
		foreach (var side in arms.Where(_ => remaining.Count != 0))
		{
			var length = Math.Min(remaining.Count, Random.Next(MinArm, MaxArm + 1));
			var arm = PlaceLine(center, side, remaining.Take(length).ToList(), depth: 1);
			foreach (var quarter in arm)
			{
				remaining.Remove(quarter);
				Depths[quarter] = arm.ToList().IndexOf(quarter) + 1;
			}

			if (arm.Count != 0)
				ends.Add(arm[^1]);
		}

		return ends;
	}

	/// <summary>
	/// The rest of the village fills in between the arms, near the center.
	/// </summary>
	protected override double TouchingWeight => 2.5;

	private const int MinArm = 1;
	private const int MaxArm = 2;
}
