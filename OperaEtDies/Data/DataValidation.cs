using GoldenAnvil.Utility;
using OperaEtDies.Utility.Random;

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
	/// Checks that a default id matches the id of an item in its list.
	/// </summary>
	public static IEnumerable<string> ValidateDefaultId<T>(string propertyName, string defaultId, string listName, IReadOnlyList<T?> items, Func<T, string?> getId)
		where T : class
	{
		if (!items.Any(x => x is not null && getId(x) == defaultId))
			yield return OurResources.DataDefaultIdNotFound.FormatCurrentCulture(propertyName, defaultId, listName);
	}

	/// <summary>
	/// Checks that a list of names has items, and that each name is filled in and has a valid frequency.
	/// </summary>
	public static IEnumerable<string> ValidateWeightedNames(string listName, IReadOnlyList<WeightedName?> names)
	{
		if (names.Count == 0)
		{
			yield return OurResources.DataListEmpty.FormatCurrentCulture(listName);
			yield break;
		}

		foreach (var name in names)
		{
			if (name is null)
				yield return OurResources.DataListItemEmpty.FormatCurrentCulture(listName);
			else if (string.IsNullOrWhiteSpace(name.Name))
				yield return OurResources.NameMissing.FormatCurrentCulture(listName);
			else if (!FrequencyUtility.IsValid(name.Frequency))
				yield return OurResources.NameFrequencyInvalid.FormatCurrentCulture(name.Name, listName, name.Frequency, FrequencyUtility.MinFrequency, FrequencyUtility.MaxFrequency);
		}
	}
}
