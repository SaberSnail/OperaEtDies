using GoldenAnvil.Utility;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// The percentage chance that an item chosen at random from a list is uncommon, rare, or very rare. The uncommon items
/// in a list share the uncommon chance equally, the rare and very rare items share theirs, and the common items share
/// the rest. If a list has no items of a frequency, its chance goes to the common items, or is shared by the other
/// frequencies in proportion to their chances if there are no common items.
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
	/// Chooses an item from a list at random, based on the items' frequencies.
	/// </summary>
	public T Choose<T>(System.Random random, IReadOnlyList<T> items)
		where T : IHasFrequency
	{
		var chances = GetChances(items);
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
	/// their usual frequencies. Chances are first worked out from the usual frequencies. An item whose frequency is
	/// changed then has the chance an item of its new frequency has in that usual list, and the chances are scaled to
	/// add up to 100 again. This way, changing one item's frequency doesn't change how likely the other items are
	/// compared to each other. Each step up or down then multiplies or divides an item's chance by
	/// <see cref="StepMultiplier"/>, and the chances are scaled to add up to 100.
	/// </summary>
	public IReadOnlyList<double> GetChances<T>(IReadOnlyList<T> items, Func<T, FrequencyLevel> getUsualFrequency, Func<T, FrequencyLevel> getFrequency)
	{
		var usualFrequencies = items.Select(x => getUsualFrequency(x).Tier).ToList();
		var usualChances = GetUsualChances(usualFrequencies);
		var levels = items.Select(getFrequency).ToList();
		var frequencies = levels.Select(x => x.Tier).ToList();

		// The items changed to a frequency the usual list doesn't have share that frequency's chance.
		double GetItemChance(Frequency frequency)
		{
			var index = usualFrequencies.IndexOf(frequency);
			return index >= 0 ? usualChances[index] : GetFrequencyChance(frequency, usualFrequencies) / frequencies.Count(x => x == frequency);
		}

		var weights = frequencies
			.Select((frequency, index) => (frequency == usualFrequencies[index] ? usualChances[index] : GetItemChance(frequency)) * Math.Pow(StepMultiplier, levels[index].Steps))
			.ToList();
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
	/// Gets each item's chance when the items of each frequency share that frequency's chance.
	/// </summary>
	private IReadOnlyList<double> GetUsualChances(IReadOnlyList<Frequency> frequencies)
	{
		var counts = frequencies.GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());

		// Without common items, the other frequencies share everything in proportion to their chances. If every
		// frequency in the list has no chance, the frequencies are equally likely.
		var total = counts.Keys.Sum(x => GetFrequencyChance(x, frequencies));
		return frequencies
			.Select(x => (total > 0 ? GetFrequencyChance(x, frequencies) / total * 100 : 100.0 / counts.Count) / counts[x])
			.ToList();
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
