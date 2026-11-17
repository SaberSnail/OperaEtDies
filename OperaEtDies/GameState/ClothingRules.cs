using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// The rules everyone follows for their clothes as they go about their day: night clothes for sleeping at home, work
/// clothes carried to work and changed into there, and the household's washing.
/// </summary>
internal static class ClothingRules
{
	/// <summary>
	/// Whether a piece of clothing is at hand for a person to put on at the given time: they're wearing or carrying it, or
	/// they're at home and it's kept there, and it isn't in the wash or drying.
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
	/// Puts night clothes on someone who's going to sleep at home, if they aren't wearing any, which takes no time. It's only
	/// done when a whole night outfit is at hand: the first that is, so the same clothes are worn until they're in the wash.
	/// Everything taken off is put away at home.
	/// </summary>
	public static void DressForSleep(Person person, Need? sleep, TimePoint time)
	{
		if (sleep is not null && person.Goal == sleep && person.Location == person.Home && !IsWearingAny(person, OutfitKind.Night))
			ChangeInto(person, OutfitKind.Night, time);
	}

	/// <summary>
	/// Takes off someone's night clothes once they've stopped sleeping at home, putting on the first everyday outfit at hand,
	/// which takes no time. Everything taken off is put away at home.
	/// </summary>
	public static void DressAfterSleep(Person person, TimePoint time)
	{
		if (person.Location == person.Home && IsWearingAny(person, OutfitKind.Night))
			ChangeInto(person, OutfitKind.Everyday, time);
	}

	/// <summary>
	/// Changes someone at home into the first of their outfits of a kind that's all at hand, if any is, putting away
	/// everything else.
	/// </summary>
	private static void ChangeInto(Person person, OutfitKind kind, TimePoint time)
	{
		if (FindAtHand(person, kind, time) is { } outfit)
		{
			person.UpdateNeeds(time);
			person.Clothing.TakeOffAll(person.Home.Items);
			PutAway(person);
			person.Clothing.PutOn(outfit, person.Home.Items);
		}
	}

	/// <summary>
	/// Puts away the clothes someone is carrying in their home, except the pieces of outfits they're partly wearing, such
	/// as the everyday clothes their work clothes replaced, which they'll change back into.
	/// </summary>
	public static void PutAway(Person person)
	{
		var keep = person.Outfits.Where(x => x.Pieces.Any(y => y.Wearer == person)).SelectMany(x => x.Pieces).ToHashSet();
		foreach (var piece in person.Inventory.Clothing.Where(x => x.Owner == person && !keep.Contains(x)).ToList())
		{
			person.Inventory.Remove(piece);
			person.Home.Items.Add(piece);
		}
	}

	/// <summary>
	/// Gets the work clothes someone would change into for work at a place, and whether they'd have to fetch them from
	/// home first, or null if they wouldn't change: they have none, they're already dressed in them, or they don't have
	/// them all at hand, at home or carried (in which case they work in what they're wearing). Someone who works at home
	/// changes there without fetching them.
	/// </summary>
	public static (OwnedOutfit Outfit, bool Fetch)? GetWorkChange(Person person, Location workplace, TimePoint time)
	{
		var outfit = person.GetOutfits(OutfitKind.Work).FirstOrDefault();
		(OwnedOutfit, bool)? change = null;
		if (outfit is not null && !IsDressedIn(person, outfit))
		{
			var isCarried = outfit.Pieces.All(x => x.Wearer == person || x.Container == person.Inventory);
			var isAtHome = outfit.Pieces.All(x => x.Wearer == person || x.Container == person.Inventory || (x.Container == person.Home.Items && x.IsReady(time)));
			if (isCarried || (isAtHome && workplace == person.Home))
				change = (outfit, false);
			else if (isAtHome)
				change = (outfit, true);
		}

		return change;
	}

	/// <summary>
	/// Picks up the pieces of an outfit kept at home, to carry them. The person must be at home.
	/// </summary>
	public static void Fetch(Person person, OwnedOutfit outfit, TimePoint time)
	{
		if (person.Location == person.Home)
		{
			foreach (var piece in outfit.Pieces.Where(x => x.Container == person.Home.Items && x.IsReady(time)))
			{
				person.Home.Items.Remove(piece);
				person.Inventory.Add(piece);
			}
		}
	}

	/// <summary>
	/// Changes into an outfit's pieces that are at hand (taking off everything else first, for a complete outfit), or out of
	/// the outfit, back into the rest of the day clothes the person is partly wearing (the outfit with the most pieces on),
	/// or after a complete outfit, into the everyday outfit with the most pieces at hand, from the pieces of it at hand.
	/// What's taken off is carried, or put away when at home.
	/// </summary>
	public static void Change(Person person, OwnedOutfit outfit, bool isTakingOff, TimePoint time)
	{
		var container = person.Location == person.Home ? person.Home.Items : person.Inventory;
		if (isTakingOff)
		{
			foreach (var piece in outfit.Pieces.Where(x => x.Wearer == person))
			{
				person.Clothing.TakeOff(piece);
				container.Add(piece);
			}

			var rest = person.Outfits.Where(x => x != outfit && x.Kind != OutfitKind.Night && x.Pieces.Any(y => y.Wearer == person)).MaxBy(x => x.Pieces.Count(y => y.Wearer == person));
			rest ??= person.GetOutfits(OutfitKind.Everyday).MaxBy(x => x.Pieces.Count(y => IsAtHand(person, y, time)));
			foreach (var piece in (rest?.Pieces ?? []).Where(x => x.Wearer != person && IsAtHand(person, x, time) && person.Clothing.AreSlotsFree(x)))
			{
				piece.Container?.Remove(piece);
				person.Clothing.Wear(piece);
			}
		}
		else
		{
			if (outfit.IsComplete)
			{
				foreach (var worn in person.Clothing.Pieces.Where(x => !outfit.Pieces.Contains(x)).ToList())
				{
					person.Clothing.TakeOff(worn);
					container.Add(worn);
				}
			}

			foreach (var piece in outfit.Pieces.Where(x => x.Wearer != person && IsAtHand(person, x, time)))
			{
				foreach (var worn in person.Clothing.Pieces.Where(x => x.Clothing.Overlaps(piece.Clothing)).ToList())
				{
					person.Clothing.TakeOff(worn);
					container.Add(worn);
				}

				piece.Container?.Remove(piece);
				person.Clothing.Wear(piece);
			}
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
		var rule = gameData.ModestyRules.FirstOrDefault(x => CharacterCondition.IsMet(x.When, person.Traits.GetValueId));
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
	/// Takes the household's clothes kept at home to be washed, other than accessories. They can't be worn until they've been hung out to dry and
	/// are dry.
	/// </summary>
	public static void TakeWashing(Location home)
	{
		foreach (var piece in home.Items.Clothing.Where(x => !x.Clothing.Accessory))
		{
			piece.IsInWash = true;
			piece.DryAt = null;
		}
	}

	/// <summary>
	/// Hangs out the household's washing at home, which is dry and can be worn again at the given time.
	/// </summary>
	public static void HangOutWashing(Location home, TimePoint dryAt)
	{
		foreach (var piece in home.Items.Clothing.Where(x => x.IsInWash))
		{
			piece.IsInWash = false;
			piece.DryAt = dryAt;
		}
	}

	/// <summary>
	/// Finds the first of a person's outfits of a kind that's all at hand, or null if none is.
	/// </summary>
	public static OwnedOutfit? FindAtHand(Person person, OutfitKind kind, TimePoint time) => person.GetOutfits(kind).FirstOrDefault(x => IsAtHand(x, time));
}
