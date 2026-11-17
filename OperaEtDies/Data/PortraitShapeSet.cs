namespace OperaEtDies.Data;

/// <summary>
/// A named list of shapes that isn't drawn on its own, but is included in portrait layers, such as a paunch, which is
/// drawn on the skin and on clothing. Its shapes use the colors of the layer that includes them.
/// </summary>
public sealed class PortraitShapeSet : IIdentified
{
	public required string Id { get; init; }

	/// <summary>
	/// The id of the portrait scale that stretches the shapes, except shapes that name their own. Without it, they use
	/// the scale of the layer that includes them.
	/// </summary>
	public string? Scale { get; init; }

	/// <summary>
	/// The shapes, from bottom to top. They can't include shape sets themselves.
	/// </summary>
	public required IReadOnlyList<PortraitShape> Shapes { get; init; }

	/// <summary>
	/// Whether the art was drawn by AI. The game doesn't use it; it's for the authors of the data.
	/// </summary>
	public required bool Ai { get; init; }
}
