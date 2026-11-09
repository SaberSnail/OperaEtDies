namespace OperaEtDies.Data;

/// <summary>
/// The values an integer character property can have, and which of them random values favor.
/// </summary>
public sealed class IntegerRange
{
	public required int Minimum { get; init; }

	public required int Maximum { get; init; }

	public required IntegerShape Shape { get; init; }
}
