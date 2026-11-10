namespace OperaEtDies.Data;

/// <summary>
/// The values an integer character property can have, and how likely each one is.
/// </summary>
public sealed class IntegerRange
{
	public required int Minimum { get; init; }

	public required int Maximum { get; init; }

	public required WeightShape Shape { get; init; }
}
