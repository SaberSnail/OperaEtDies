namespace OperaEtDies.Data;

/// <summary>
/// How close to the body clothing is worn, such as underclothes or outerwear, from the clothing layers section of the
/// data, which lists them from the innermost out. Someone can only wear one piece of clothing in each slot in each
/// layer, and a piece in a slot covers the pieces in that slot in the layers inside it.
/// </summary>
public sealed class ClothingLayer : IIdentified
{
	public required string Id { get; init; }

	public required string Title { get; init; }
}
