namespace PortraitEditor.Model;

/// <summary>
/// Matches the items of a list before and after it changed, so something kept for each item can follow it when items
/// are added, removed, moved, or changed.
/// </summary>
public static class ListAlignment
{
	/// <summary>
	/// Gets, for each new item, the index of the old item it was (or -1 for a new item). Items are matched by text: the
	/// longest run in the same order first, then the rest that are the same anywhere (moved items). Between matched items,
	/// the old and new items left over are taken to be changed versions of each other when there are as many of each.
	/// </summary>
	public static IReadOnlyList<int> Align(IReadOnlyList<string> oldItems, IReadOnlyList<string> newItems)
	{
		var result = Enumerable.Repeat(-1, newItems.Count).ToArray();
		var isOldMatched = new bool[oldItems.Count];

		// The longest common subsequence, from the end, so the walk from the start prefers the earliest matches.
		var lengths = new int[oldItems.Count + 1, newItems.Count + 1];
		for (var i = oldItems.Count - 1; i >= 0; i--)
		{
			for (var j = newItems.Count - 1; j >= 0; j--)
			{
				lengths[i, j] = string.Equals(oldItems[i], newItems[j], StringComparison.Ordinal)
					? lengths[i + 1, j + 1] + 1
					: Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
			}
		}

		var anchors = new List<(int Old, int New)>();
		var oldIndex = 0;
		var newIndex = 0;
		while (oldIndex < oldItems.Count && newIndex < newItems.Count)
		{
			if (string.Equals(oldItems[oldIndex], newItems[newIndex], StringComparison.Ordinal) && lengths[oldIndex, newIndex] == lengths[oldIndex + 1, newIndex + 1] + 1)
			{
				anchors.Add((oldIndex, newIndex));
				oldIndex++;
				newIndex++;
			}
			else if (lengths[oldIndex + 1, newIndex] >= lengths[oldIndex, newIndex + 1])
			{
				oldIndex++;
			}
			else
			{
				newIndex++;
			}
		}

		foreach (var (oldMatch, newMatch) in anchors)
		{
			result[newMatch] = oldMatch;
			isOldMatched[oldMatch] = true;
		}

		// Moved items: the same text somewhere else.
		for (var j = 0; j < newItems.Count; j++)
		{
			if (result[j] < 0)
			{
				var match = Enumerable.Range(0, oldItems.Count).FirstOrDefault(i => !isOldMatched[i] && string.Equals(oldItems[i], newItems[j], StringComparison.Ordinal), -1);
				if (match >= 0)
				{
					result[j] = match;
					isOldMatched[match] = true;
				}
			}
		}

		// Changed items: what's left between two anchors, when there are as many old items as new ones.
		var bounds = anchors.Prepend((-1, -1)).Append((oldItems.Count, newItems.Count)).ToList();
		for (var gap = 0; gap + 1 < bounds.Count; gap++)
		{
			var oldLeft = Enumerable.Range(bounds[gap].Item1 + 1, bounds[gap + 1].Item1 - bounds[gap].Item1 - 1).Where(i => !isOldMatched[i]).ToList();
			var newLeft = Enumerable.Range(bounds[gap].Item2 + 1, bounds[gap + 1].Item2 - bounds[gap].Item2 - 1).Where(j => result[j] < 0).ToList();
			if (oldLeft.Count == newLeft.Count)
			{
				for (var index = 0; index < oldLeft.Count; index++)
				{
					result[newLeft[index]] = oldLeft[index];
					isOldMatched[oldLeft[index]] = true;
				}
			}
		}

		return result;
	}
}
