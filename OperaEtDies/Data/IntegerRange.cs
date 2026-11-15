namespace OperaEtDies.Data;

/// <summary>
/// A range of whole numbers, and how likely each one is to be chosen.
/// </summary>
public sealed class IntegerRange
{
	public required int Minimum { get; init; }

	public required int Maximum { get; init; }

	/// <summary>
	/// Weights the numbers in the range by their order, or null for every number to be equally likely.
	/// </summary>
	public WeightShape? Shape { get; init; }

	/// <summary>
	/// Gets the percentage chance of each number in the range, from the minimum up.
	/// </summary>
	public IReadOnlyList<double> GetChances() => GetChances(Shape, Maximum - Minimum + 1);

	/// <summary>
	/// Gets the percentage chance of each of the given number of values, weighted by the shape, or equally likely
	/// without one.
	/// </summary>
	internal static IReadOnlyList<double> GetChances(WeightShape? shape, int count) =>
		shape?.GetChances(count) ?? Enumerable.Repeat(count > 0 ? 100.0 / count : 0, Math.Max(count, 0)).ToList();

	/// <summary>
	/// Gets the part of this range from the given minimum up. The numbers that are left keep their weights compared to
	/// each other, so the curve isn't squeezed to fit the shorter range.
	/// </summary>
	internal IntegerRange StartingAt(int minimum)
	{
		var skipped = Math.Max(minimum - Minimum, 0);
		return new IntegerRange { Minimum = Minimum + skipped, Maximum = Maximum, Shape = Shape?.Skipping(skipped, Maximum - Minimum + 1) };
	}
}
