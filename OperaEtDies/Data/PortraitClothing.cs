using OperaEtDies.Characters;

namespace OperaEtDies.Data;

/// <summary>
/// The clothing a character is wearing, as their portrait needs it: each article of clothing with the colors of its
/// piece. Two are equal when they have the same clothing in the same colors, so portraits can be kept for each.
/// </summary>
public sealed class PortraitClothing : IEquatable<PortraitClothing>
{
	/// <summary>
	/// Wearing nothing.
	/// </summary>
	public static PortraitClothing None { get; } = new([]);

	public PortraitClothing(IEnumerable<PortraitClothingPiece> pieces)
	{
		Pieces = pieces.ToList();
		m_key = string.Join(";", Pieces
			.Select(x => x.Clothing.Id + ":" + string.Join(",", x.Colors.OrderBy(y => y.Key, StringComparer.Ordinal).Select(y => $"{y.Key}={y.Value}")))
			.Order(StringComparer.Ordinal));
	}

	/// <summary>
	/// Chooses clothing for a character who isn't in the world, such as one being created or previewed: an outfit of
	/// each kind, in order, chosen by frequency from those whose conditions they meet, each over the ones before it (or instead of them, for a complete outfit),
	/// with each piece's colors chosen by frequency.
	/// </summary>
	public static PortraitClothing ChooseOutfits(GameData gameData, CharacterTraits traits, IEnumerable<OutfitKind> kinds, Random random)
	{
		string? GetValueId(string propertyId) => gameData.CharacterProperties.Find(propertyId)?.GetConditionValueId(traits.Values.GetValueOrDefault(propertyId));

		var pieces = new List<PortraitClothingPiece>();
		foreach (var kind in kinds)
		{
			var outfits = gameData.Outfits.Where(x => x.Kind == kind && CharacterCondition.IsMet(x.When, GetValueId)).ToList();
			if (outfits.Count != 0)
			{
				var outfit = gameData.FrequencyChances.Choose(random, outfits);
				if (outfit.Complete)
					pieces.Clear();
				foreach (var clothing in outfit.Clothing.Select(x => gameData.Clothing.Find(x)).OfType<ClothingItem>())
				{
					pieces.RemoveAll(x => x.Clothing.Overlaps(clothing));
					var colors = clothing.Colors.ToDictionary(x => x, _ => gameData.FrequencyChances.Choose(random, gameData.ClothingColors).Color, StringComparer.Ordinal);
					pieces.Add(new PortraitClothingPiece(clothing, colors));
				}
			}
		}

		return new PortraitClothing(pieces);
	}

	/// <summary>
	/// The pieces being worn.
	/// </summary>
	public IReadOnlyList<PortraitClothingPiece> Pieces { get; }

	/// <summary>
	/// Gets the worn piece of an article of clothing, or null if none is worn.
	/// </summary>
	public PortraitClothingPiece? Find(string clothingId) => Pieces.FirstOrDefault(x => x.Clothing.Id == clothingId);

	/// <summary>
	/// Gets how much the worn clothing covers a region of the body: the most any piece covers it.
	/// </summary>
	public int GetCoverage(string region) => Pieces.Select(x => x.Clothing.GetCoverage(region)).DefaultIfEmpty(0).Max();

	public bool Equals(PortraitClothing? other) => other is not null && m_key == other.m_key;

	public override bool Equals(object? obj) => Equals(obj as PortraitClothing);

	public override int GetHashCode() => m_key.GetHashCode(StringComparison.Ordinal);

	public override string ToString() => m_key;

	private readonly string m_key;
}

/// <summary>
/// A piece of clothing being worn, as a portrait needs it: its article of clothing, and the hex color of each of its
/// colors, by color name.
/// </summary>
public sealed record PortraitClothingPiece(ClothingItem Clothing, IReadOnlyDictionary<string, string> Colors);
