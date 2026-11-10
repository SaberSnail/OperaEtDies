namespace OperaEtDies.Utility.Random;

// Inside this namespace, "Random" refers to the namespace, so System.Random is written out in full.
public static class WeightedRandomUtility
{
	/// <summary>
	/// Selects an item from the list, where each item's chance of being selected is proportional to its weight.
	/// Items with a weight of 0 are never selected.
	/// </summary>
	public static T NextWeighted<T>(this System.Random rng, IReadOnlyList<T> items, Func<T, double> getWeight)
	{
		var weights = items.Select(getWeight).ToList();
		if (weights.Any(x => x < 0 || !double.IsFinite(x)))
			throw new ArgumentException("Weights must be finite and not negative.", nameof(getWeight));

		var totalWeight = weights.Sum();
		if (totalWeight <= 0)
			throw new ArgumentException("The list must contain at least one item with a positive weight.", nameof(items));

		// Pick a point in the total weight, then walk the list until the running total passes it. Rounding can leave
		// the point just past the end, so the last item with a positive weight is the fallback.
		var target = rng.NextDouble() * totalWeight;
		var runningWeight = 0.0;
		var index = -1;
		for (var i = 0; i < items.Count && index < 0; i++)
		{
			runningWeight += weights[i];
			if (weights[i] > 0 && target < runningWeight)
				index = i;
		}

		return items[index >= 0 ? index : weights.FindLastIndex(x => x > 0)];
	}
}
