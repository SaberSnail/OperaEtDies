using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Test.Utility.Random;

// Inside this namespace, "Random" refers to the namespace, so System.Random is written out in full.
[TestFixture]
public sealed class WeightedSelectionTests
{
	[TestCase(1, 1)]
	[TestCase(2, 4)]
	[TestCase(3, 16)]
	public void FrequencyMapsToWeight(int frequency, int weight)
	{
		Assert.That(FrequencyUtility.GetWeight(frequency), Is.EqualTo(weight));
	}

	[TestCase(0)]
	[TestCase(4)]
	public void InvalidFrequencyThrows(int frequency)
	{
		Assert.That(FrequencyUtility.IsValid(frequency), Is.False);
		Assert.Throws<ArgumentOutOfRangeException>(() => FrequencyUtility.GetWeight(frequency));
	}

	[Test]
	public void SelectsByRollAgainstRunningTotal()
	{
		// Weights 1, 4, 16 give ranges 1, 2-5, and 6-21.
		var items = new[] { new Item("rare", 1), new Item("uncommon", 4), new Item("common", 16) };

		Assert.That(new FixedRandom(1).NextWeighted(items).Name, Is.EqualTo("rare"));
		Assert.That(new FixedRandom(2).NextWeighted(items).Name, Is.EqualTo("uncommon"));
		Assert.That(new FixedRandom(5).NextWeighted(items).Name, Is.EqualTo("uncommon"));
		Assert.That(new FixedRandom(6).NextWeighted(items).Name, Is.EqualTo("common"));
		Assert.That(new FixedRandom(21).NextWeighted(items).Name, Is.EqualTo("common"));
	}

	[Test]
	public void ZeroWeightItemsAreNeverSelected()
	{
		var items = new[] { new Item("never", 0), new Item("always", 1) };

		Assert.That(new FixedRandom(1).NextWeighted(items).Name, Is.EqualTo("always"));
	}

	[Test]
	public void EmptyOrZeroWeightListThrows()
	{
		Assert.Throws<ArgumentException>(() => new System.Random(0).NextWeighted(Array.Empty<Item>()));
		Assert.Throws<ArgumentException>(() => new System.Random(0).NextWeighted(new[] { new Item("never", 0) }));
	}

	[Test]
	public void NegativeWeightThrows()
	{
		Assert.Throws<ArgumentException>(() => new System.Random(0).NextWeighted(new[] { new Item("bad", -1) }));
	}

	[Test]
	public void CommonItemsAreSelectedMoreOften()
	{
		var names = new[] { new WeightedString { Value = "rare", Frequency = 1 }, new WeightedString { Value = "common", Frequency = 3 } };
		var random = new System.Random(12345);

		var commonCount = Enumerable.Range(0, 1700).Count(_ => random.NextWeighted(names).Value == "common");

		// Expected is 1600 of 1700 (16 / 17).
		Assert.That(commonCount, Is.InRange(1500, 1680));
	}

	private sealed record Item(string Name, int Weight) : IWeighted;

	// Always returns the given value from Next(min, max), so tests can choose the roll.
	private sealed class FixedRandom(int value) : System.Random
	{
		public override int Next(int minValue, int maxValue)
		{
			Assert.That(value, Is.InRange(minValue, maxValue - 1));
			return value;
		}
	}
}
