using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// Validation checks shared by several data files.
/// </summary>
public static class DataValidation
{
	/// <summary>
	/// Checks that a list has items, and that each item has a unique id and a title.
	/// </summary>
	public static IEnumerable<string> ValidateIdentifiedItems<T>(string listName, IReadOnlyList<T?> items, Func<T, string?> getId, Func<T, string?> getTitle)
		where T : class
	{
		if (items.Count == 0)
		{
			yield return OurResources.DataListEmpty.FormatCurrentCulture(listName);
			yield break;
		}

		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (var item in items)
		{
			// JSON deserialization doesn't stop null list items, even when the type is non-nullable.
			if (item is null)
			{
				yield return OurResources.DataListItemEmpty.FormatCurrentCulture(listName);
				continue;
			}

			var id = getId(item);
			if (string.IsNullOrWhiteSpace(id))
			{
				yield return OurResources.DataListItemIdMissing.FormatCurrentCulture(listName);
				continue;
			}

			if (!ids.Add(id))
				yield return OurResources.DataListItemIdDuplicate.FormatCurrentCulture(id, listName);
			if (string.IsNullOrWhiteSpace(getTitle(item)))
				yield return OurResources.DataListItemTitleMissing.FormatCurrentCulture(id, listName);
		}
	}

	/// <summary>
	/// Checks that a list of weighted strings has items, and that each string is filled in.
	/// </summary>
	public static IEnumerable<string> ValidateWeightedStrings(string listName, IReadOnlyList<WeightedString?> values)
	{
		if (values.Count == 0)
		{
			yield return OurResources.DataListEmpty.FormatCurrentCulture(listName);
			yield break;
		}

		foreach (var value in values)
		{
			if (value is null)
				yield return OurResources.DataListItemEmpty.FormatCurrentCulture(listName);
			else if (string.IsNullOrWhiteSpace(value.Value))
				yield return OurResources.WeightedValueMissing.FormatCurrentCulture(listName);
		}
	}
}
