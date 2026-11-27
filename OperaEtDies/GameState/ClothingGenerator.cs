using GoldenAnvil.Utility.Logging;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// Gives people their clothing when a new game is created, and dresses them for the start of the game.
/// </summary>
internal static class ClothingGenerator
{
	/// <summary>
	/// How many outfits of each kind everyone is given, if there are outfits of that kind they can have: two everyday
	/// outfits, so there's something to wear on wash day, and one each for work, sleeping, and wearing nothing.
	/// </summary>
	public static IReadOnlyList<(OutfitKind Kind, int Count)> OutfitCounts { get; } = [(OutfitKind.Everyday, 2), (OutfitKind.Work, 1), (OutfitKind.Night, 1), (OutfitKind.Nothing, 1)];

	/// <summary>
	/// Gives a person their outfits, chosen by frequency from those of each kind whose conditions they meet and that
	/// they're comfortable enough in, made of the options chosen for them (see <see cref="OutfitModesty.Choose"/>), with each piece's colors chosen by
	/// frequency. Work clothes must do over each of their everyday outfits. Every piece belongs to the person and starts in
	/// their home. Their outfits share any accessory they have in common. Everyone has an outfit for wearing nothing, which
	/// is nothing at all if there's none they can have.
	/// </summary>
	public static void GiveOutfits(GameData gameData, WorldObjects objects, Random random, Person person)
	{
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

		var businessProfession = person.Business?.Profession.Id;
		foreach (var (kind, count) in OutfitCounts)
		{
			IReadOnlyList<IReadOnlyList<ClothingItem>> wornUnder = kind == OutfitKind.Work
				? person.GetOutfits(OutfitKind.Everyday).Select(x => (IReadOnlyList<ClothingItem>) x.Pieces.Select(y => y.Clothing).ToList()).ToList()
				: [];
			for (var index = 0; index < count; index++)
			{
				if (OutfitModesty.Choose(gameData, random, kind, person.Traits, businessProfession, wornUnder) is ({ } template, var clothing, var isModestEnough))
				{
					if (!isModestEnough)
						Log.Warn($"No {kind.ToString().ToLowerInvariant()} outfit is modest enough for {person.FullName}, so they were given {template.Id}.");
					var pieces = clothing.Select(GetPiece).ToList();
					objects.Create(id => new OwnedOutfit(id, person, kind, template, pieces));
				}
			}
		}

		GiveNothing(objects, person);
	}

	/// <summary>
	/// Gives someone with no outfit for wearing nothing one that's nothing at all, such as in a game saved before there
	/// were any.
	/// </summary>
	public static void GiveNothing(WorldObjects objects, Person person)
	{
		if (!person.GetOutfits(OutfitKind.Nothing).Any())
			objects.Create(id => new OwnedOutfit(id, person, OutfitKind.Nothing, null, []));
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

			// Night clothes of nothing at all are only changed into by what they replaced, which here is nothing.
			if (outfit.Kind == OutfitKind.Night && outfit.Pieces.Count == 0)
				person.SetReplacedClothes(outfit, []);
		}
	}

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(ClothingGenerator));

	private static ClothingPiece CreatePiece(GameData gameData, WorldObjects objects, Random random, ClothingItem clothing, Person owner)
	{
		var colors = clothing.Colors.ToDictionary(x => x, _ => gameData.FrequencyChances.Choose(random, gameData.ClothingColors), StringComparer.Ordinal);
		return objects.Create(id => new ClothingPiece(id, clothing, colors, owner));
	}
}
