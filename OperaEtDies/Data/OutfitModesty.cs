using OperaEtDies.Characters;

namespace OperaEtDies.Data;

/// <summary>
/// How modest worn clothing is by someone's modesty rule, and which outfits someone is comfortable enough to be given:
/// everyday clothes in the company of anyone, work clothes there too (less any comfort the steps of their work take
/// away), and night clothes in the company of their household, each changed by their boldness.
/// </summary>
public static class OutfitModesty
{
	/// <summary>
	/// Gets how modest someone wearing the given clothing is in each clothing slot their modesty rule (the first whose
	/// condition they meet) checks, and the least it calls for at a level of comfort, in the order of the slots. A slot's
	/// modesty is the most any worn clothing that covers it gives: its coverage, changed by its bulk.
	/// </summary>
	public static IReadOnlyList<(ClothingSlot Slot, int Modesty, int Minimum)> GetModesty(GameData gameData, Func<string, string?> getValueId, IEnumerable<ClothingItem> worn, double comfort)
	{
		var rule = gameData.ModestyRules.FirstOrDefault(x => CharacterCondition.IsMet(x.When, getValueId));
		var clothing = worn.ToList();
		return rule is null ? [] : gameData.ClothingSlots
			.Where(slot => rule.Slots.ContainsKey(slot.Id))
			.Select(slot => (slot, GetModesty(gameData, clothing, slot.Id), rule.GetMinimum(slot.Id, comfort)))
			.ToList();
	}

	/// <summary>
	/// Gets the comfort a character is expected to wear an outfit of a kind at, or null for a kind that isn't checked
	/// (clothes for wearing nothing, which are only worn when steps call for them): the comfort of the company of anyone for
	/// everyday and work clothes, and of the household for night clothes, plus the character's boldness. For work clothes,
	/// it also takes away the most comfort any step of their work takes away (with a business in the given profession).
	/// </summary>
	public static double? GetComfort(GameData gameData, OutfitKind kind, CharacterTraits traits, string? businessProfession)
	{
		var settings = gameData.ModestySettings;
		int? company = kind switch
		{
			OutfitKind.Everyday or OutfitKind.Work => settings.Company.Anyone,
			OutfitKind.Night => settings.Company.Household,
			_ => null,
		};
		if (company is null)
			return null;

		var steps = kind == OutfitKind.Work && businessProfession is not null ? GetLeastWorkStepComfort(gameData, traits, businessProfession) : 0;
		return Math.Clamp(company.Value + steps + settings.GetBoldnessComfort(traits), 0, ModestySettings.MaxComfort);
	}

	/// <summary>
	/// Chooses an outfit of a kind for a character, and the clothing it's made of, from the options of each of its entries
	/// the character can be given. The outfit is chosen by frequency from those whose conditions they meet and that can be
	/// made of clothing they'd be comfortable in (see <see cref="GetComfort"/>), with the shares worked out among those
	/// outfits alone, and then each entry's option by frequency from those that still leave clothing they'd be comfortable
	/// in. A work outfit that isn't complete must do over each set of clothes it may be worn over, in place of what it
	/// overlaps. When none would do, chooses among the outfits and options that leave the character least short of covered,
	/// and says so. Returns null when the character meets the conditions of no outfit of the kind.
	/// </summary>
	public static (Outfit Outfit, IReadOnlyList<ClothingItem> Clothing, bool IsModestEnough)? Choose(GameData gameData, Random random, OutfitKind kind, CharacterTraits traits, string? businessProfession, IReadOnlyList<IReadOnlyList<ClothingItem>> wornUnder)
	{
		var getValueId = GetValueIdFunc(gameData, traits);
		var outfits = gameData.Outfits.Where(x => x.Kind == kind && CharacterCondition.IsMet(x.When, getValueId)).ToList();
		if (outfits.Count == 0)
			return null;

		var comfort = GetComfort(gameData, kind, traits, businessProfession);
		IReadOnlyList<IReadOnlyList<ClothingItem>> unders = wornUnder.Count == 0 ? [[]] : wornUnder;
		var choices = outfits.ToDictionary(x => x, x => new OutfitChoices(gameData, getValueId, x, unders, comfort));
		var least = choices.Values.Min(x => x.Least);
		var outfit = gameData.FrequencyChances.Choose(random, outfits.Where(x => choices[x].Least == least).ToList());
		return (outfit, choices[outfit].Choose(gameData, random), least == 0);
	}

	/// <summary>
	/// Gets the clothes someone would have on in an outfit made of the given clothing, worn over the given clothes: just its
	/// own, unless it's a work outfit that isn't complete, which goes over them in place of what it overlaps.
	/// </summary>
	public static IReadOnlyList<ClothingItem> GetClothesIn(Outfit outfit, IReadOnlyList<ClothingItem> clothing, IEnumerable<ClothingItem> under) =>
		outfit.Kind != OutfitKind.Work || outfit.Complete ? clothing : [.. under.Where(x => !clothing.Any(x.Overlaps)), .. clothing];

	/// <summary>
	/// Gets a function that gives the id a character's value of a property has in conditions.
	/// </summary>
	public static Func<string, string?> GetValueIdFunc(GameData gameData, CharacterTraits traits) =>
		propertyId => gameData.CharacterProperties.Find(propertyId)?.GetConditionValueId(traits.Values.GetValueOrDefault(propertyId));

	/// <summary>
	/// Gets how far short of covered enough someone is in clothing at a level of comfort: the total of what each slot lacks.
	/// </summary>
	private static int GetShortfall(GameData gameData, Func<string, string?> getValueId, IEnumerable<ClothingItem> clothing, double comfort) =>
		GetModesty(gameData, getValueId, clothing, comfort).Sum(x => Math.Max(0, x.Minimum - x.Modesty));

	/// <summary>
	/// The ways an outfit can be made for a character from the options of its entries they can be given, and how far short
	/// of covered enough each leaves them. Options that make no difference to that, such as different hats, are grouped
	/// together, so only the combinations of the groups need checking: those that give the same modesty in each slot the
	/// character's modesty rule checks and replace the same clothes worn under the outfit.
	/// </summary>
	private sealed class OutfitChoices
	{
		public OutfitChoices(GameData gameData, Func<string, string?> getValueId, Outfit outfit, IReadOnlyList<IReadOnlyList<ClothingItem>> unders, double? comfort)
		{
			var rule = gameData.ModestyRules.FirstOrDefault(x => CharacterCondition.IsMet(x.When, getValueId));
			var slots = comfort is null ? [] : gameData.ClothingSlots.Where(x => rule?.Slots.ContainsKey(x.Id) == true).Select(x => x.Id).ToList();
			var isOver = outfit.Kind == OutfitKind.Work && !outfit.Complete;
			string GetKey(ClothingItem? item) => item is null || comfort is null ? "" :
				string.Join(",", slots.Select(slot => GetModesty(gameData, [item], slot))) + "|" +
				(isOver ? string.Join(";", unders.Select(under => string.Concat(under.Select(x => x.Overlaps(item) ? '1' : '0')))) : "");

			m_groups = outfit.GetOptions(getValueId)
				.Select(options => (IReadOnlyList<OptionGroup>) options
					.Select(option => (Option: option, Item: option.Clothing is { } id ? gameData.Clothing.Find(id) : null))
					.GroupBy(x => GetKey(x.Item))
					.Select(group => new OptionGroup(group.Select(x => x.Option).ToList(), group.Select(x => x.Item).ToList()))
					.ToList())
				.ToList();

			// Each combination is a group from each entry, by index.
			IEnumerable<int[]> combinations = [[]];
			foreach (var groups in m_groups)
				combinations = combinations.SelectMany(combination => Enumerable.Range(0, groups.Count).Select(index => (int[]) [.. combination, index]));
			m_combinations = combinations.Select(combination =>
			{
				var clothing = combination.Select((group, entry) => m_groups[entry][group].Items[0]).OfType<ClothingItem>().ToList();
				var shortfall = comfort is { } value ? unders.Sum(under => GetShortfall(gameData, getValueId, GetClothesIn(outfit, clothing, under), value)) : 0;
				return (combination, shortfall);
			}).ToList();
			Least = m_combinations.Min(x => x.Shortfall);
		}

		/// <summary>
		/// The least any combination leaves the character short of covered enough.
		/// </summary>
		public int Least { get; }

		/// <summary>
		/// Chooses the clothing of the outfit: for each entry in turn, an option by frequency from those in a combination
		/// that leaves the character least short of covered (with the entries chosen before).
		/// </summary>
		public IReadOnlyList<ClothingItem> Choose(GameData gameData, Random random)
		{
			var remaining = m_combinations.Where(x => x.Shortfall == Least).Select(x => x.Combination).ToList();
			var clothing = new List<ClothingItem>();
			for (var entry = 0; entry < m_groups.Count; entry++)
			{
				var groups = m_groups[entry];
				var allowed = remaining.Select(x => x[entry]).Distinct().ToList();
				var option = gameData.FrequencyChances.Choose(random, allowed.SelectMany(x => groups[x].Options).ToList());
				var chosen = allowed.First(x => groups[x].Options.Contains(option));
				if (groups[chosen].Items[groups[chosen].Options.ToList().IndexOf(option)] is { } item)
					clothing.Add(item);
				remaining = remaining.Where(x => x[entry] == chosen).ToList();
			}

			return clothing;
		}

		private sealed record OptionGroup(IReadOnlyList<OutfitClothingOption> Options, IReadOnlyList<ClothingItem?> Items);

		private readonly IReadOnlyList<IReadOnlyList<OptionGroup>> m_groups;
		private readonly IReadOnlyList<(int[] Combination, int Shortfall)> m_combinations;
	}

	/// <summary>
	/// Gets the most comfort any step of a character's work takes away (as a negative number), or 0 if none does. Walking
	/// between places, which can't change comfort, is part of every working day, so steps that add comfort don't count.
	/// </summary>
	private static int GetLeastWorkStepComfort(GameData gameData, CharacterTraits traits, string businessProfession)
	{
		var getValueId = GetValueIdFunc(gameData, traits);
		return gameData.Schedules.Needs.OfType<Need>()
			.Where(x => x.IsWork && CharacterCondition.IsMet(x.When, getValueId))
			.SelectMany(x => x.GetSteps(businessProfession))
			.Select(x => x.ModestyComfort)
			.Append(0)
			.Min();
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
}
