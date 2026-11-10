using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// Weights an ordered list of values by a bell curve: the value at the peak is the most likely, and values grow
/// less likely the farther they are from it. About two-thirds of choices fall within <see cref="MostWithin"/>
/// steps of the peak (it's the curve's standard deviation).
/// </summary>
public sealed class WeightShape
{
	public required ShapePeak Peak { get; init; }

	public required double MostWithin { get; init; }

	/// <summary>
	/// Gets the percentage chance of choosing each position in a list with the given number of values.
	/// </summary>
	public IReadOnlyList<double> GetChances(int count)
	{
		var peak = Peak switch
		{
			ShapePeak.Low => 0,
			ShapePeak.Middle => (count - 1) / 2.0,
			ShapePeak.High => count - 1,
			_ => throw new InvalidOperationException($"Unknown shape peak {Peak}."),
		};

		var weights = Enumerable.Range(0, count).Select(x => Math.Exp(-Math.Pow(x - peak, 2) / (2 * MostWithin * MostWithin))).ToList();
		var total = weights.Sum();
		return weights.Select(x => x / total * 100).ToList();
	}

	public IEnumerable<string> Validate(string path)
	{
		if (!(MostWithin > 0) || !double.IsFinite(MostWithin))
			yield return OurResources.ShapeMostWithinInvalid.FormatCurrentCulture($"{path}.mostWithin", MostWithin);
	}
}
