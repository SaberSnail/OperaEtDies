using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// Gives people their clothing when a new game is created, and dresses them for the start of the game.
/// </summary>
internal static class ClothingGenerator
{
	/// <summary>
	/// How many outfits of each kind everyone is given, if there are outfits of that kind they can have: two everyday
	/// outfits, so there's something to wear on wash day, and one each for work and sleeping.
	/// </summary>
	public static IReadOnlyList<(OutfitKind Kind, int Count)> OutfitCounts { get; } = [(OutfitKind.Everyday, 2), (OutfitKind.Work, 1), (OutfitKind.Night, 1)];

	/// <summary>
	/// Gives a person their outfits, chosen by frequency from those of each kind whose conditions they meet (with the
	/// shares worked out among those outfits alone, since outfits for different people aren't alternatives), with each
	/// piece's colors chosen by frequency. Every piece belongs to the person and starts in their home. Their outfits
	/// share any accessory they have in common.
	/// </summary>
	public static void GiveOutfits(GameData gameData, WorldObjects objects, Random random, Person person)
	{
		string? GetValueId(string propertyId) => gameData.CharacterProperties.Find(propertyId)?.GetConditionValueId(person.Traits.Values.GetValueOrDefault(propertyId));

		// Accessories already given are shared by the outfits after.
		var accessories = new Dictionary<ClothingItem, ClothingPiece>();
		ClothingPiece GetPiece(ClothingItem clothing)
		{
			if (!accessories.TryGetValue(clothing, out var piece))
			{
				piece = CreatePiece(gameData, objects, random, clothing, person);
				person.Home.Items.Add(piece);
				if (clothing.Accessory)
					accessories.Add(clothing, piece);
			}

			return piece;
		}

		foreach (var (kind, count) in OutfitCounts)
		{
			var outfits = gameData.Outfits.Where(x => x.Kind == kind && CharacterCondition.IsMet(x.When, GetValueId)).ToList();
			if (outfits.Count != 0)
			{
				for (var index = 0; index < count; index++)
				{
					var template = gameData.FrequencyChances.Choose(random, outfits);
					var pieces = template.Clothing.Select(x => GetPiece(gameData.Clothing.Get(x))).ToList();
					objects.Create(id => new OwnedOutfit(id, person, kind, template, pieces));
				}
			}
		}
	}

	/// <summary>
	/// Dresses a person for the start of the game. Someone starting at work wears their first everyday outfit with their
	/// work outfit over it, carrying the everyday pieces the work outfit replaces, or wears just a complete work outfit,
	/// carrying their first everyday outfit. Anyone else is asleep at home in their
	/// night clothes, or their first everyday outfit if they have none.
	/// </summary>
	public static void DressAtStart(Person person, bool isAtWork)
	{
		var everyday = person.GetOutfits(OutfitKind.Everyday).FirstOrDefault();
		var home = person.Home.Items;
		if (isAtWork)
		{
			if (everyday is not null)
				person.Clothing.PutOn(everyday, home);
			if (person.GetOutfits(OutfitKind.Work).FirstOrDefault() is { } work)
			{
				if (work.IsComplete)
					person.Clothing.TakeOffAll(person.Inventory);
				person.Clothing.PutOn(work, person.Inventory);
			}
		}
		else if ((person.GetOutfits(OutfitKind.Night).FirstOrDefault() ?? everyday) is { } outfit)
		{
			person.Clothing.PutOn(outfit, home);
		}
	}

	private static ClothingPiece CreatePiece(GameData gameData, WorldObjects objects, Random random, ClothingItem clothing, Person owner)
	{
		var colors = clothing.Colors.ToDictionary(x => x, _ => gameData.FrequencyChances.Choose(random, gameData.ClothingColors), StringComparer.Ordinal);
		return objects.Create(id => new ClothingPiece(id, clothing, colors, owner));
	}
}
