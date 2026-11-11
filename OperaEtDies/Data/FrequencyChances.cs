using GoldenAnvil.Utility;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// The percentage chance that an item chosen at random from a list is uncommon, rare, or very rare. The uncommon items
/// in a list share the uncommon chance equally, the rare and very rare items share theirs, and the common items share
/// the rest. If a list has no items of a frequency, its chance goes to the common items, or is shared by the other
/// frequencies in proportion to their chances if there are no common items. When only some items of a list can be
/// chosen, the shares are still those of the whole list.
/// </summary>
public sealed class FrequencyChances
{
	public double Uncommon { get; init; } = 20;

	public double Rare { get; init; } = 5;

	public double VeryRare { get; init; } = 0.5;

	/// <summary>
	/// How much each "+" step multiplies an item's chance by (and each "-" step divides it by), before the chances
	/// are scaled to add up to 100.
	/// </summary>
	public double StepMultiplier { get; init; } = 1.5;

	/// <summary>
	/// Chooses an item from a list at random, based on the items' frequencies. If the items are only the ones that can
	/// be chosen from a longer list, pass the whole list too, so each frequency's chance is shared as it would be in the
	/// whole list (see <see cref="GetChances{T}(IReadOnlyList{T}, Func{T, FrequencyLevel}, Func{T, FrequencyLevel}, IReadOnlyList{T}?)"/>).
	/// </summary>
	public T Choose<T>(System.Random random, IReadOnlyList<T> items, IReadOnlyList<T>? allItems = null)
		where T : IHasFrequency
	{
		var chances = GetChances(items, x => x.Frequency, x => x.Frequency, allItems);
		return items[random.NextWeighted(Enumerable.Range(0, items.Count).ToList(), x => chances[x])];
	}

	/// <summary>
	/// Gets the percentage chance of choosing each item in a list, based on the items' frequencies.
	/// </summary>
	public IReadOnlyList<double> GetChances<T>(IReadOnlyList<T> items)
		where T : IHasFrequency =>
		GetChances(items, x => x.Frequency, x => x.Frequency);

	/// <summary>
	/// Gets the percentage chance of choosing each item in a list, where some items' frequencies may be changed from
	/// their usual frequencies.
	/// </summary>
	/// <remarks>
	/// <para>Each frequency's chance is shared equally by the items of that frequency in the whole list, using their usual
	/// frequencies. The whole list is <paramref name="allItems"/> if given, or else the items themselves. Items in the
	/// whole list that aren't being chosen from are left out without giving their share to the other items of their
	/// frequency, so a rare item stays as rare compared to the common items however many of the other rare items are
	/// left out. If no item in the whole list usually has a frequency, the items changed to it share its chance.</para>
	/// <para>An item whose frequency is changed has the share of an item of its new frequency. This way, changing one
	/// item's frequency doesn't change how likely the other items are compared to each other. Each step up or down then
	/// multiplies or divides an item's chance by <see cref="StepMultiplier"/>, and the chances are scaled to add up to
	/// 100.</para>
	/// </remarks>
	public IReadOnlyList<double> GetChances<T>(IReadOnlyList<T> items, Func<T, FrequencyLevel> getUsualFrequency, Func<T, FrequencyLevel> getFrequency, IReadOnlyList<T>? allItems = null)
	{
		var usualFrequencies = (allItems ?? items).Select(x => getUsualFrequency(x).Tier).ToList();
		var levels = items.Select(getFrequency).ToList();

		// If every frequency in the whole list has no chance, the frequencies are equally likely.
		var hasChance = usualFrequencies.Distinct().Any(x => GetFrequencyChance(x, usualFrequencies) > 0);
		double GetItemChance(Frequency frequency)
		{
			var count = usualFrequencies.Count(x => x == frequency);
			var chance = hasChance ? GetFrequencyChance(frequency, usualFrequencies) : 1;
			return chance / (count != 0 ? count : levels.Count(x => x.Tier == frequency));
		}

		var weights = levels.Select(x => GetItemChance(x.Tier) * Math.Pow(StepMultiplier, x.Steps)).ToList();
		var total = weights.Sum();
		return weights.Select(x => total > 0 ? x / total * 100 : 100.0 / weights.Count).ToList();
	}

	public IEnumerable<string> Validate(string path)
	{
		if (Uncommon < 0 || !double.IsFinite(Uncommon))
			yield return OurResources.FrequencyChanceInvalid.FormatCurrentCulture($"{path}.uncommon", Uncommon);
		if (Rare < 0 || !double.IsFinite(Rare))
			yield return OurResources.FrequencyChanceInvalid.FormatCurrentCulture($"{path}.rare", Rare);
		if (VeryRare < 0 || !double.IsFinite(VeryRare))
			yield return OurResources.FrequencyChanceInvalid.FormatCurrentCulture($"{path}.veryRare", VeryRare);
		if (!(StepMultiplier >= 1) || !double.IsFinite(StepMultiplier))
			yield return OurResources.FrequencyStepMultiplierInvalid.FormatCurrentCulture($"{path}.stepMultiplier", StepMultiplier);
		if (Uncommon + Rare + VeryRare >= 100)
			yield return OurResources.FrequencyChancesTooHigh.FormatCurrentCulture(path, Uncommon + Rare + VeryRare);
	}

	/// <summary>
	/// Gets the chance all the items of a frequency share, given the frequencies in the list. Common items get what the
	/// other frequencies in the list don't use.
	/// </summary>
	private double GetFrequencyChance(Frequency frequency, IReadOnlyList<Frequency> frequencies)
	{
		double GetIfPresent(Frequency other, double chance) => frequencies.Contains(other) ? chance : 0;
		return frequency switch
		{
			Frequency.Common => 100 - GetIfPresent(Frequency.Uncommon, Uncommon) - GetIfPresent(Frequency.Rare, Rare) - GetIfPresent(Frequency.VeryRare, VeryRare),
			Frequency.Uncommon => Uncommon,
			Frequency.Rare => Rare,
			Frequency.VeryRare => VeryRare,
			_ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null),
		};
	}
}
