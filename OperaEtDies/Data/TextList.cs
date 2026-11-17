using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// Joins items into a list in a sentence, such as "a, b, and c".
/// </summary>
public static class TextList
{
	/// <summary>
	/// Joins the items into a list, or gets empty text if there are none.
	/// </summary>
	public static string Join(IReadOnlyList<string> items) => items.Count switch
	{
		0 => "",
		1 => items[0],
		2 => OurResources.ListJoinTwo.FormatCurrentCulture(items[0], items[1]),
		_ => OurResources.ListJoinLast.FormatCurrentCulture(string.Join(OurResources.ListJoinSeparator, items.Take(items.Count - 1)), items[^1]),
	};
}
