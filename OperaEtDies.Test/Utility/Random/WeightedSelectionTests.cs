using GoldenAnvil.Utility;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Test.Utility.Random;

// Inside this namespace, "Random" refers to the namespace, so System.Random is written out in full.
[TestFixture]
public sealed class WeightedSelectionTests
{
	[TestCase(0.0, "first")]
	[TestCase(0.04, "first")]
	[TestCase(0.05, "second")]
	[TestCase(0.23, "second")]
	[TestCase(0.24, "third")]
	[TestCase(0.999, "third")]
	public void SelectsByRollAgainstRunningTotal(double roll, string expected)
	{
		// Weights 1, 4, 16 give ranges 0-1, 1-5, and 5-21 out of 21.
		var items = new[] { new Item("first", 1), new Item("second", 4), new Item("third", 16) };

		Assert.That(new FixedRandom(roll).NextWeighted(items, x => x.Weight).Name, Is.EqualTo(expected));
	}

	[Test]
	public void ZeroWeightItemsAreNeverSelected()
	{
		var items = new[] { new Item("never", 0), new Item("always", 1), new Item("never", 0) };

		Assert.That(new FixedRandom(0).NextWeighted(items, x => x.Weight).Name, Is.EqualTo("always"));
		Assert.That(new FixedRandom(0.999).NextWeighted(items, x => x.Weight).Name, Is.EqualTo("always"));
	}

	[Test]
	public void EmptyOrZeroWeightListThrows()
	{
		Assert.Throws<ArgumentException>(() => new System.Random(0).NextWeighted(Array.Empty<Item>(), x => x.Weight));
		Assert.Throws<ArgumentException>(() => new System.Random(0).NextWeighted(new[] { new Item("never", 0) }, x => x.Weight));
	}

	[Test]
	public void NegativeWeightThrows()
	{
		Assert.Throws<ArgumentException>(() => new System.Random(0).NextWeighted(new[] { new Item("bad", -1) }, x => x.Weight));
	}

	[Test]
	public void FrequenciesShareTheirChances()
	{
		// The 5 uncommon items share 20%, the rare item gets 5%, and the 2 common items share the other 75%.
		var items = new[] { Common("c1"), Common("c2"), Uncommon("u1"), Uncommon("u2"), Uncommon("u3"), Uncommon("u4"), Uncommon("u5"), Rare("r1") };

		var shares = GetShares(new FrequencyChances(), items);

		Assert.That(shares["c1"], Is.EqualTo(0.375).Within(0.015));
		Assert.That(shares["c2"], Is.EqualTo(0.375).Within(0.015));
		foreach (var id in new[] { "u1", "u2", "u3", "u4", "u5" })
			Assert.That(shares[id], Is.EqualTo(0.04).Within(0.008));
		Assert.That(shares["r1"], Is.EqualTo(0.05).Within(0.008));
	}

	[Test]
	public void MissingFrequenciesGiveTheirChanceToCommonItems()
	{
		var shares = GetShares(new FrequencyChances(), [Common("c"), Rare("r")]);

		Assert.That(shares["c"], Is.EqualTo(0.95).Within(0.01));
		Assert.That(shares["r"], Is.EqualTo(0.05).Within(0.01));
	}

	[Test]
	public void WithoutCommonItemsChancesAreProportional()
	{
		var shares = GetShares(new FrequencyChances { Uncommon = 30, Rare = 10 }, [Uncommon("u"), Rare("r")]);

		Assert.That(shares["u"], Is.EqualTo(0.75).Within(0.015));
		Assert.That(shares["r"], Is.EqualTo(0.25).Within(0.015));
	}

	[Test]
	public void FrequenciesWithNoChanceAreEquallyLikely()
	{
		var shares = GetShares(new FrequencyChances { Uncommon = 0, Rare = 0 }, [Uncommon("u"), Rare("r")]);

		Assert.That(shares["u"], Is.EqualTo(0.5).Within(0.02));
	}

	[Test]
	public void VeryRareItemsShareTheVeryRareChance()
	{
		var chances = new FrequencyChances().GetChances([Common("c"), Rare("r"), VeryRare("v1"), VeryRare("v2")]);

		Assert.That(chances, Is.EqualTo(new[] { 94.5, 5, 0.25, 0.25 }).Within(0.0001));
	}

	[Test]
	public void ChangedFrequenciesDontChangeOtherItems()
	{
		// Usually, "none" has 75%, each uncommon item 5%, and each rare item about 1.67%.
		var items = new[] { Common("none"), Uncommon("a"), Uncommon("b"), Uncommon("c"), Uncommon("d"), Rare("p"), Rare("q"), Rare("r") };
		var demoted = new HashSet<string>(StringComparer.Ordinal) { "a", "b" };

		var chances = new FrequencyChances().GetChances(items, x => x.Frequency, x => demoted.Contains(x.Value) ? Frequency.Rare : x.Frequency);
		var byValue = items.Select((x, index) => (x.Value, Chance: chances[index])).ToDictionary(x => x.Value, x => x.Chance);

		// The demoted items are as likely as the usual rare items, and the others keep their chances compared to each other.
		Assert.That(chances.Sum(), Is.EqualTo(100).Within(0.0001));
		Assert.That(byValue["a"], Is.EqualTo(byValue["p"]).Within(0.0001));
		Assert.That(byValue["c"] / byValue["none"], Is.EqualTo(5.0 / 75).Within(0.0001));
		Assert.That(byValue["c"], Is.EqualTo(5 / 0.9333).Within(0.01));
	}

	[Test]
	public void ItemsChangedToAnUnusedFrequencyShareIt()
	{
		var items = new[] { Common("c1"), Common("c2"), Uncommon("u1"), Uncommon("u2") };

		var chances = new FrequencyChances().GetChances(items, x => x.Frequency, x => x.Value.StartsWith('u') ? Frequency.Rare : x.Frequency);

		// The two items moved to rare share its 5%, while the common items keep their 40% each.
		Assert.That(chances, Is.EqualTo(new[] { 40, 40, 2.5, 2.5 }.Select(x => x / 85 * 100)).Within(0.0001));
	}

	[Test]
	public void LeftOutItemsDontGiveTheirShareToTheirFrequency()
	{
		// In the whole list, the common items share 95% and each rare item has 1%.
		var all = new[] { Common("c1"), Common("c2"), Rare("r1"), Rare("r2"), Rare("r3"), Rare("r4"), Rare("r5") };
		var items = new[] { all[0], all[1], all[2] };

		var chances = new FrequencyChances().GetChances(items, x => x.Frequency, x => x.Frequency, all);

		// Without the whole list, r1 would have the whole 5%.
		Assert.That(chances, Is.EqualTo(new[] { 47.5, 47.5, 1 }.Select(x => x / 96 * 100)).Within(0.0001));
	}

	[Test]
	public void InvalidChancesReportErrors()
	{
		Assert.That(new FrequencyChances().Validate("chances"), Is.Empty);
		Assert.That(new FrequencyChances { Uncommon = -1, Rare = 101 }.Validate("chances"), Is.EqualTo(new[]
		{
			OurResources.FrequencyChanceInvalid.FormatCurrentCulture("chances.uncommon", -1),
			OurResources.FrequencyChancesTooHigh.FormatCurrentCulture("chances", 100.5),
		}));
		Assert.That(new FrequencyChances { Uncommon = 60, Rare = 40 }.Validate("chances"), Is.EqualTo(new[] { OurResources.FrequencyChancesTooHigh.FormatCurrentCulture("chances", 100.5) }));
		Assert.That(new FrequencyChances { StepMultiplier = 0.5 }.Validate("chances"), Is.EqualTo(new[] { OurResources.FrequencyStepMultiplierInvalid.FormatCurrentCulture("chances.stepMultiplier", 0.5) }));
	}

	[TestCase("common", Frequency.Common, 0)]
	[TestCase("Uncommon+", Frequency.Uncommon, 1)]
	[TestCase("rare++", Frequency.Rare, 2)]
	[TestCase("common-", Frequency.Common, -1)]
	[TestCase("veryRare--", Frequency.VeryRare, -2)]
	public void FrequencyLevelsParse(string text, Frequency tier, int steps)
	{
		Assert.That(FrequencyLevel.TryParse(text, out var level), Is.True);
		Assert.That(level, Is.EqualTo(new FrequencyLevel(tier, steps)));
		Assert.That(FrequencyLevel.TryParse(level!.Value.ToString(), out var roundTrip) && roundTrip == level, Is.True);
	}

	[TestCase("")]
	[TestCase("sometimes")]
	[TestCase("common+++")]
	[TestCase("common+-")]
	[TestCase("+")]
	public void InvalidFrequencyLevelsDontParse(string text)
	{
		Assert.That(FrequencyLevel.TryParse(text, out _), Is.False);
	}

	[Test]
	public void StepsNudgeChances()
	{
		var items = new[]
		{
			new WeightedString { Value = "a" },
			new WeightedString { Value = "b", Frequency = new FrequencyLevel(Frequency.Common, 1) },
			new WeightedString { Value = "c", Frequency = new FrequencyLevel(Frequency.Common, -2) },
		};

		// Each common item has a third before the steps; then b is multiplied by 1.5 and c divided by 2.25.
		var chances = new FrequencyChances().GetChances(items);
		var weights = new[] { 1, 1.5, 1 / 2.25 };
		Assert.That(chances, Is.EqualTo(weights.Select(x => x / weights.Sum() * 100)).Within(0.0001));
	}

	private static Dictionary<string, double> GetShares(FrequencyChances chances, IReadOnlyList<WeightedString> items)
	{
		const int count = 20000;
		var random = new System.Random(12345);
		var counts = items.ToDictionary(x => x.Value, _ => 0);
		for (var i = 0; i < count; i++)
			counts[chances.Choose(random, items).Value]++;
		return counts.ToDictionary(x => x.Key, x => (double) x.Value / count);
	}

	private static WeightedString Common(string value) => new() { Value = value };

	private static WeightedString Uncommon(string value) => new() { Value = value, Frequency = Frequency.Uncommon };

	private static WeightedString Rare(string value) => new() { Value = value, Frequency = Frequency.Rare };

	private static WeightedString VeryRare(string value) => new() { Value = value, Frequency = Frequency.VeryRare };

	private sealed record Item(string Name, double Weight);

	// Always returns the given value from NextDouble(), so tests can choose the roll.
	private sealed class FixedRandom(double value) : System.Random
	{
		public override double NextDouble() => value;
	}
}
