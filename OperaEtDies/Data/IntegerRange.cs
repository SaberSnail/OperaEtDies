namespace OperaEtDies.Data;

/// <summary>
/// The values an integer character property can have, and how likely each one is.
/// </summary>
public sealed class IntegerRange
{
	public required int Minimum { get; init; }

	public required int Maximum { get; init; }

	public required WeightShape Shape { get; init; }

	/// <summary>
	/// Gets the part of this range from the given minimum up. The numbers that are left keep their weights compared to
	/// each other, so the curve isn't squeezed to fit the shorter range.
	/// </summary>
	internal IntegerRange StartingAt(int minimum)
	{
		var skipped = Math.Max(minimum - Minimum, 0);
		return new IntegerRange { Minimum = Minimum + skipped, Maximum = Maximum, Shape = Shape.Skipping(skipped, Maximum - Minimum + 1) };
	}
}
