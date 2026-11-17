namespace OperaEtDies.Data;

/// <summary>
/// What keeps a character's property values from being seen when describing them: how strongly each property is hidden,
/// by property id, which comes from how much the clothing worn covers the regions of the body the property shows on.
/// </summary>
public sealed class CharacterObscuring
{
	/// <summary>
	/// The lowest and highest prominence of a value, and strength of obscuring.
	/// </summary>
	public const int MinStrength = 1;
	public const int MaxStrength = ClothingItem.MaxCoverage;

	public CharacterObscuring(IEnumerable<IReadOnlyDictionary<string, int>> obscurers)
	{
		m_obscurers = obscurers.ToList();
	}

	/// <summary>
	/// Nothing is obscured.
	/// </summary>
	public static CharacterObscuring None { get; } = new([]);

	public static bool IsValidStrength(int strength) => strength is >= MinStrength and <= MaxStrength;

	/// <summary>
	/// Works out how strongly clothing worn together hides each property that shows on regions of the body: as strongly as
	/// the least it covers any of those regions, counting only clothing whose bulk hides form for a property about form.
	/// Properties it doesn't hide at all are left out.
	/// </summary>
	public static IReadOnlyDictionary<string, int> GetStrengths(IEnumerable<CharacterProperty> properties, IEnumerable<ClothingItem> clothing, Func<string, ClothingBulk?> findBulk)
	{
		var items = clothing.ToList();
		var strengths = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var property in properties.Where(x => x?.Regions is { Count: > 0 }))
		{
			var counted = property.Form ? items.Where(x => x.Bulk is { } bulk && findBulk(bulk)?.HidesForm == true).ToList() : items;
			var strength = property.Regions.Min(region => counted.Select(x => x.GetCoverage(region)).DefaultIfEmpty(0).Max());
			if (strength > 0)
				strengths.TryAdd(property.Id, strength);
		}

		return strengths;
	}

	/// <summary>
	/// Gets how strongly a property is hidden: the strongest of the obscurers, or 0 if none hide it.
	/// </summary>
	public int GetStrength(string propertyId) => m_obscurers.Count == 0 ? 0 : m_obscurers.Max(x => x.GetValueOrDefault(propertyId));

	/// <summary>
	/// Whether a property's value is hidden. Value list values use their prominence; other values have the highest prominence.
	/// </summary>
	public bool Hides(string propertyId, object value) =>
		GetStrength(propertyId) >= ((value as CharacterPropertyOption)?.GetProminence() ?? MaxStrength);

	private readonly IReadOnlyList<IReadOnlyDictionary<string, int>> m_obscurers;
}
