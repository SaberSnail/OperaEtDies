using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Town;

/// <summary>
/// Random choices used while generating the town.
/// </summary>
internal static class TownRandom
{
	/// <summary>
	/// Returns true with the given percentage chance.
	/// </summary>
	public static bool NextChance(this Random random, double percent) => random.NextDouble() * 100 < percent;

	/// <summary>
	/// Picks a number in the range, weighted by its shape.
	/// </summary>
	public static int NextInRange(this Random random, IntegerRange range) =>
		random.NextInRange(range, _ => true) ?? range.Minimum;

	/// <summary>
	/// Picks a number in the range that's allowed, weighted by the range's shape. Returns null if no number is allowed.
	/// </summary>
	public static int? NextInRange(this Random random, IntegerRange range, Func<int, bool> isAllowed) =>
		random.NextShaped(range.Shape, range.Minimum, range.Maximum, isAllowed);

	/// <summary>
	/// Picks a number from minimum to maximum that's allowed, weighted by the shape. Returns null if no number is allowed.
	/// </summary>
	public static int? NextShaped(this Random random, WeightShape shape, int minimum, int maximum, Func<int, bool> isAllowed)
	{
		int? result = null;
		if (maximum >= minimum)
		{
			var chances = shape.GetChances(maximum - minimum + 1);
			var candidates = Enumerable.Range(0, chances.Count).Where(x => chances[x] > 0 && isAllowed(minimum + x)).ToList();
			if (candidates.Count != 0)
				result = minimum + random.NextWeighted(candidates, x => chances[x]);
		}

		return result;
	}

	/// <summary>
	/// Picks an item from a list, without regard to weights.
	/// </summary>
	public static T NextItem<T>(this Random random, IReadOnlyList<T> items) => items[random.Next(items.Count)];

	/// <summary>
	/// Shuffles a list in place.
	/// </summary>
	public static void Shuffle<T>(this Random random, IList<T> items)
	{
		for (var index = items.Count - 1; index > 0; index--)
		{
			var other = random.Next(index + 1);
			(items[index], items[other]) = (items[other], items[index]);
		}
	}
}
