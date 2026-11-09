namespace OperaEtDies.Utility.Random;

// Inside this namespace, "Random" refers to the namespace, so System.Random is written out in full.
public static class WeightedRandomUtility
{
	/// <summary>
	/// Selects an item from the list, where each item's chance of being selected is proportional to its weight.
	/// </summary>
	public static T NextWeighted<T>(this System.Random rng, IReadOnlyList<T> items)
		where T : IWeighted
	{
		var totalWeight = 0;
		foreach (var item in items)
		{
			if (item.Weight < 0)
				throw new ArgumentException("Weights must not be negative.", nameof(items));
			totalWeight = checked(totalWeight + item.Weight);
		}

		if (totalWeight == 0)
			throw new ArgumentException("The list must contain at least one item with a positive weight.", nameof(items));

		// Pick a value from 1 to the total weight, then walk the list until the running total reaches it.
		var target = rng.Next(1, totalWeight + 1);
		var runningWeight = 0;
		foreach (var item in items)
		{
			runningWeight += item.Weight;
			if (runningWeight >= target)
				return item;
		}

		throw new InvalidOperationException("Item weights changed while selecting an item.");
	}
}
