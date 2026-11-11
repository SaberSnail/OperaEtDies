using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// Weights an ordered list of values by a bell curve: the value at the peak is the most likely, and values grow
/// less likely the farther they are from it. About two-thirds of choices fall within <see cref="MostWithin"/>
/// steps of the peak (it's the curve's standard deviation).
/// </summary>
public sealed class WeightShape
{
	public required PeakLevel Peak { get; init; }

	public required double MostWithin { get; init; }

	/// <summary>
	/// Gets the percentage chance of choosing each position in a list with the given number of values.
	/// </summary>
	public IReadOnlyList<double> GetChances(int count)
	{
		var peak = m_peakIndex ?? Peak.GetFraction() * (count - 1);
		var weights = Enumerable.Range(0, count).Select(x => Math.Exp(-Math.Pow(x - peak, 2) / (2 * MostWithin * MostWithin))).ToList();
		var total = weights.Sum();
		return weights.Select(x => x / total * 100).ToList();
	}

	public IEnumerable<string> Validate(string path)
	{
		if (!Peak.IsValid)
			yield return OurResources.ShapePeakOutOfRange.FormatCurrentCulture($"{path}.peak", Peak);
		if (!(MostWithin > 0) || !double.IsFinite(MostWithin))
			yield return OurResources.ShapeMostWithinInvalid.FormatCurrentCulture($"{path}.mostWithin", MostWithin);
	}

	/// <summary>
	/// Gets the shape of what's left of a list with the given number of values after leaving out the first ones, with
	/// the peak and spread where they were.
	/// </summary>
	internal WeightShape Skipping(int skipped, int count) => new()
	{
		Peak = Peak,
		MostWithin = MostWithin,
		m_peakIndex = (m_peakIndex ?? Peak.GetFraction() * (count - 1)) - skipped,
	};

	// The exact position of the peak, which replaces the position from Peak. It can't be set from a data file.
	private double? m_peakIndex;
}
