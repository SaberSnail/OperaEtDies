using OperaEtDies.Utility.Random;

namespace OperaEtDies.Town;

/// <summary>
/// A main street runs straight through the center, either east to west or north to south. Sometimes a second, shorter
/// street runs alongside it, with lanes crossing between the two where they touch. The rest of the village branches off
/// the streets, and the outskirts leave first from the ends of the main street.
/// </summary>
internal sealed class LinearShape(TownMap map, Random random) : TownShape(map, random)
{
	protected override IReadOnlyList<MapQuarter> BuildCore(MapQuarter center, IList<MapQuarter> remaining)
	{
		var isVertical = Random.Next(2) == 0;
		var (forward, backward) = isVertical ? (CompassDirection.North, CompassDirection.South) : (CompassDirection.East, CompassDirection.West);
		if (Random.Next(2) == 0)
			(forward, backward) = (backward, forward);

		// The main street grows out from both sides of the center, a quarter at a time.
		var length = Math.Min(remaining.Count, Random.Next(MinStreet, MaxStreet + 1));
		var street = remaining.Take(length).ToList();
		var ahead = PlaceLine(center, forward, street.Where((_, index) => index % 2 == 0), depth: 1);
		var behind = PlaceLine(center, backward, street.Where((_, index) => index % 2 == 1), depth: 1);
		foreach (var quarter in ahead.Concat(behind))
			remaining.Remove(quarter);

		var ends = new[] { ahead.Count != 0 ? ahead[^1] : null, behind.Count != 0 ? behind[^1] : null }.OfType<MapQuarter>().ToList();

		// A second street starts beside the main street and runs alongside it.
		var mainStreet = ahead.Concat(behind).ToList();
		if (mainStreet.Count >= 2 && remaining.Count >= MinSecondStreet + 1 && Random.NextChance(SecondStreetChance))
		{
			var sides = (isVertical ? new[] { CompassDirection.East, CompassDirection.West } : [CompassDirection.North, CompassDirection.South]).OrderBy(_ => Random.Next()).ToList();
			var start = remaining[0];
			var parent = mainStreet[Random.Next(mainStreet.Count)];
			if (Map.TryPlaceNextTo(start, parent, sides, rect => ScoreVillageSpot(start, rect)))
			{
				remaining.Remove(start);
				Depths[start] = 2;
				var secondLength = Math.Min(remaining.Count, Random.Next(MinSecondStreet, MaxSecondStreet + 1));
				var second = PlaceLine(start, Random.Next(2) == 0 ? forward : backward, remaining.Take(secondLength).ToList(), depth: 2);
				foreach (var quarter in second)
					remaining.Remove(quarter);

				ConnectTouching([.. mainStreet, start, .. second], CrossLaneChance);
				ends.Add(second.Count != 0 ? second[^1] : start);
			}
		}

		return ends;
	}

	private const int MinStreet = 4;
	private const int MaxStreet = 6;
	private const int MinSecondStreet = 2;
	private const int MaxSecondStreet = 3;
	private const double SecondStreetChance = 50;
	private const double CrossLaneChance = 50;
}
