namespace OperaEtDies.Data;

/// <summary>
/// A long piece of text in the data (a full sentence or more), with whether it was written by AI.
/// </summary>
public sealed class LongText
{
	public required string Text { get; init; }

	/// <summary>
	/// Whether the text was written by AI. The game doesn't use it; it's for the authors of the data.
	/// </summary>
	public required bool Ai { get; init; }

	public override string ToString() => Text;
}
