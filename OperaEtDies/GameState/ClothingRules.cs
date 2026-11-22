using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// The rules everyone follows for their clothes as they go about their day, and how the steps of their needs handle
/// clothes: changing into and out of outfits, and picking up and putting away clothes kept at home.
/// </summary>
internal static class ClothingRules
{
	/// <summary>
	/// Whether a piece of clothing is at hand for a person to put on at the given time: they're wearing or carrying it, or
	/// they're at home and it's kept there, and it isn't drying.
	/// </summary>
	public static bool IsAtHand(Person person, ClothingPiece piece, TimePoint time) =>
		piece.Wearer == person || ((piece.Container == person.Inventory || (person.Location == person.Home && piece.Container == person.Home.Items)) && piece.IsReady(time));

	/// <summary>
	/// Whether every piece of an outfit is at hand for its owner to put on.
	/// </summary>
	public static bool IsAtHand(OwnedOutfit outfit, TimePoint time) => outfit.Pieces.All(x => IsAtHand(outfit.Owner, x, time));

	/// <summary>
	/// Whether a person is wearing every piece of an outfit.
	/// </summary>
	public static bool IsWearing(Person person, OwnedOutfit outfit) => outfit.Pieces.All(x => x.Wearer == person);

	/// <summary>
	/// Whether a person is dressed in an outfit: wearing every piece of it, and for a complete outfit, nothing else.
	/// </summary>
	public static bool IsDressedIn(Person person, OwnedOutfit outfit) =>
		IsWearing(person, outfit) && (!outfit.IsComplete || person.Clothing.Pieces.Count == outfit.Pieces.Count);

	/// <summary>
	/// Whether a person is wearing any of their clothes of a kind.
	/// </summary>
	public static bool IsWearingAny(Person person, OutfitKind kind) => person.GetOutfits(kind).Any(x => x.Pieces.Any(y => y.Wearer == person));

	/// <summary>
	/// Whether a person has changed into an outfit and not out of it: they're wearing any of it, or for an outfit of
	/// nothing at all, they changed into it from the clothes it replaced.
	/// </summary>
	public static bool IsChangedInto(Person person, OwnedOutfit outfit) =>
		outfit.Pieces.Count == 0 ? person.ReplacedClothes.ContainsKey(outfit) : outfit.Pieces.Any(x => x.Wearer == person);

	/// <summary>
	/// Whether a person already has on an outfit of a kind that changing into one would put on: all of one (for an outfit
	/// of nothing at all, nothing else either).
	/// </summary>
	public static bool IsWearingKind(Person person, OutfitKind kind) =>
		person.GetOutfits(kind).Any(x => x.Pieces.Count == 0 ? person.Clothing.Pieces.Count == 0 : IsWearing(person, x));

	/// <summary>
	/// Puts away the household's clothes someone is carrying in their home, except the pieces of outfits they're partly
	/// wearing (other than through accessories, which outfits share), the clothes a later step of theirs will need (such
	/// as the everyday clothes their work clothes replaced, which they'll change back into), and the clothes in their
	/// bundles, which their steps will put away.
	/// </summary>
	public static void PutAway(Person person)
	{
		var keep = person.Outfits.Where(x => x.Pieces.Any(y => y.Wearer == person && !y.Clothing.Accessory)).SelectMany(x => x.Pieces).ToHashSet();
		keep.UnionWith(person.Sequences.SelectMany(x => GetNeededClothes(person, x)));
		foreach (var piece in person.Inventory.Clothing.Where(x => x.Owner is { } owner && person.Family.Members.Contains(owner) && !keep.Contains(x) && !person.IsInBundle(x)).ToList())
		{
			person.Inventory.Remove(piece);
			person.Home.Items.Add(piece);
		}
	}

	/// <summary>
	/// Gets the clothes the steps of a sequence from the one it's on will need: for an "undress" step, the clothes that
	/// changing into the outfit it takes off replaced, which it puts back on. Someone taking part in someone else's steps
	/// needs none, since the steps aren't theirs.
	/// </summary>
	public static IEnumerable<ClothingPiece> GetNeededClothes(Person person, StepSequence sequence) =>
		sequence.Role != SequenceRole.Steps ? [] :
		sequence.Need.Steps.Skip(sequence.Step)
			.Where(x => x.Kind == StepKind.Undress)
			.Select(x => x.Outfit!.Value)
			.Distinct()
			.SelectMany(person.GetOutfits)
			.SelectMany(x => person.ReplacedClothes.GetValueOrDefault(x) ?? []);

	/// <summary>
	/// Whether any of the clothes the steps of a sequence from the one it's on will need isn't at hand (see
	/// <see cref="GetNeededClothes"/>).
	/// </summary>
	public static bool IsMissingNeededClothes(Person person, StepSequence sequence, TimePoint time) =>
		GetNeededClothes(person, sequence).Any(x => !IsAtHand(person, x, time));

	/// <summary>
	/// Changes into an outfit's pieces that are at hand (taking off everything else first, for a complete outfit),
	/// remembering the pieces it replaced, or out of the outfit, back into the pieces it replaced that are at hand, and
	/// then into the rest of the day clothes the person is partly wearing (the outfit with the most pieces on), or after a
	/// complete outfit, into the everyday outfit with the most pieces at hand, from the pieces of it at hand, wherever
	/// there's room. What's taken off is carried, even at home.
	/// </summary>
	public static void Change(Person person, OwnedOutfit outfit, bool isTakingOff, TimePoint time)
	{
		var container = person.Inventory;
		if (isTakingOff)
		{
			foreach (var piece in outfit.Pieces.Where(x => x.Wearer == person))
			{
				person.Clothing.TakeOff(piece);
				container.Add(piece);
			}

			// What the outfit replaced goes back on first, as long as it's still at hand.
			foreach (var piece in person.RemoveReplacedClothes(outfit).Where(x => x.Wearer != person && IsAtHand(person, x, time) && person.Clothing.AreSlotsFree(x)))
			{
				piece.Container?.Remove(piece);
				person.Clothing.Wear(piece);
			}

			var rest = person.Outfits.Where(x => x != outfit && x.Kind is not (OutfitKind.Night or OutfitKind.Nothing) && x.Pieces.Any(y => y.Wearer == person)).MaxBy(x => x.Pieces.Count(y => y.Wearer == person));
			rest ??= person.GetOutfits(OutfitKind.Everyday).MaxBy(x => x.Pieces.Count(y => IsAtHand(person, y, time)));
			foreach (var piece in (rest?.Pieces ?? []).Where(x => x.Wearer != person && IsAtHand(person, x, time) && person.Clothing.AreSlotsFree(x)))
			{
				piece.Container?.Remove(piece);
				person.Clothing.Wear(piece);
			}
		}
		else
		{
			var replaced = new List<ClothingPiece>();
			if (outfit.IsComplete)
			{
				foreach (var worn in person.Clothing.Pieces.Where(x => !outfit.Pieces.Contains(x)).ToList())
				{
					person.Clothing.TakeOff(worn);
					container.Add(worn);
					replaced.Add(worn);
				}
			}

			foreach (var piece in outfit.Pieces.Where(x => x.Wearer != person && IsAtHand(person, x, time)))
			{
				foreach (var worn in person.Clothing.Pieces.Where(x => x.Clothing.Overlaps(piece.Clothing)).ToList())
				{
					person.Clothing.TakeOff(worn);
					container.Add(worn);
					replaced.Add(worn);
				}

				piece.Container?.Remove(piece);
				person.Clothing.Wear(piece);
			}

			// Changing into it again keeps what it replaced the first time, since that's what the person was dressed in.
			if (!person.ReplacedClothes.ContainsKey(outfit) || replaced.Count != 0)
				person.SetReplacedClothes(outfit, replaced);
		}
	}

	/// <summary>
	/// Gets the clothing slots in which someone wearing the given clothing isn't covered enough for a level of formality,
	/// by the first modesty rule whose condition they meet, in the order of the slots. A slot's modesty is the most any
	/// worn clothing that covers it gives: its coverage, changed by its bulk. While working, their profession can call for
	/// more or less.
	/// </summary>
	public static IReadOnlyList<ClothingSlot> GetUncoveredSlots(GameData gameData, Person person, IEnumerable<ClothingItem> worn, ModestyLevel level, bool isAtWork) =>
		GetModesty(gameData, person, worn, level, isAtWork).Where(x => x.Modesty < x.Minimum).Select(x => x.Slot).ToList();

	/// <summary>
	/// Gets how modest someone wearing the given clothing is in each clothing slot their modesty rule checks, and the least
	/// it calls for at a level of formality, in the order of the slots. While working, their profession can call for more
	/// or less from people following that rule, though never less than 0 or more than full coverage.
	/// </summary>
	public static IReadOnlyList<(ClothingSlot Slot, int Modesty, int Minimum)> GetModesty(GameData gameData, Person person, IEnumerable<ClothingItem> worn, ModestyLevel level, bool isAtWork)
	{
		var rule = gameData.ModestyRules.FirstOrDefault(x => CharacterCondition.IsMet(x.When, person.GetConditionValueId));
		var adjustments = isAtWork && rule is not null ? person.Business?.Profession.Modesty.GetValueOrDefault(rule.Id) : null;
		var clothing = worn.ToList();
		var minimums = rule?.GetMinimums(level) ?? new Dictionary<string, int>();
		return gameData.ClothingSlots
			.Where(slot => minimums.ContainsKey(slot.Id))
			.Select(slot => (slot, GetModesty(gameData, clothing, slot.Id), Math.Clamp(minimums[slot.Id] + (adjustments?.GetValueOrDefault(slot.Id) ?? 0), 0, ClothingItem.MaxCoverage)))
			.ToList();
	}

	/// <summary>
	/// Gets how modestly clothing worn together covers a region: the most any of it that covers the region gives, which is
	/// its coverage changed by its bulk.
	/// </summary>
	private static int GetModesty(GameData gameData, IEnumerable<ClothingItem> clothing, string region) =>
		clothing.Where(x => x.GetCoverage(region) > 0)
			.Select(x => Math.Clamp(x.GetCoverage(region) + (x.Bulk is { } bulk ? gameData.ClothingBulks.Find(bulk)?.Modesty ?? 0 : 0), 0, ClothingItem.MaxCoverage))
			.DefaultIfEmpty(0)
			.Max();

	/// <summary>
	/// Picks up clothes kept at home that can be worn now, to carry them, if the person is at home: their own or their
	/// household's, only the pieces of their own outfits of a kind if one is given, and only if they aren't accessories,
	/// unless accessories are taken. Returns the pieces picked up.
	/// </summary>
	public static IReadOnlyList<ClothingPiece> Take(Person person, StepOwner owner, OutfitKind? outfit, bool accessories, TimePoint time)
	{
		var taken = new List<ClothingPiece>();
		if (person.Location == person.Home)
		{
			var outfitPieces = outfit is { } kind ? person.GetOutfits(kind).SelectMany(x => x.Pieces).ToHashSet() : null;
			taken.AddRange(person.Home.Items.Clothing.Where(x => x.IsReady(time) && (accessories || !x.Clothing.Accessory) && (outfitPieces?.Contains(x) ?? true) &&
				(owner == StepOwner.Household ? x.Owner is { } pieceOwner && person.Family.Members.Contains(pieceOwner) : x.Owner == person)));
			foreach (var piece in taken)
			{
				person.Home.Items.Remove(piece);
				person.Inventory.Add(piece);
			}
		}

		return taken;
	}

	/// <summary>
	/// Puts away clothes the person is still carrying in their home, if they're at home, to be ready to wear at the given
	/// time, if one is given, shown to be doing something until then, such as drying.
	/// </summary>
	public static void Put(Person person, IEnumerable<Item> items, TimePoint? readyAt, string? delayActivity = null)
	{
		if (person.Location == person.Home)
		{
			foreach (var item in items.Where(x => x.Container == person.Inventory).ToList())
			{
				person.Inventory.Remove(item);
				person.Home.Items.Add(item);
				if (item is ClothingPiece piece && readyAt is not null)
				{
					piece.ReadyAt = readyAt;
					piece.DelayActivity = delayActivity;
				}
			}
		}
	}

	/// <summary>
	/// Whether a person has an outfit of a kind that they aren't dressed in, with every piece of it worn, carried, or kept
	/// at home ready to wear at the given time, and some of it at home.
	/// </summary>
	public static bool IsOutfitAtHome(Person person, OutfitKind kind, TimePoint time) =>
		person.GetOutfits(kind).FirstOrDefault() is { } outfit && !IsDressedIn(person, outfit) &&
		outfit.Pieces.All(x => x.Wearer == person || x.Container == person.Inventory || (x.Container == person.Home.Items && x.IsReady(time))) &&
		outfit.Pieces.Any(x => x.Container == person.Home.Items);

	/// <summary>
	/// Finds the first of a person's outfits of a kind that's all at hand, or null if none is.
	/// </summary>
	public static OwnedOutfit? FindAtHand(Person person, OutfitKind kind, TimePoint time) => person.GetOutfits(kind).FirstOrDefault(x => IsAtHand(x, time));
}
