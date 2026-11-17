namespace OperaEtDies.Data;

/// <summary>
/// A place on the body that clothing is worn, such as the head or the feet, from the clothing slots section of the data.
/// Someone can only wear one piece of clothing in each slot in each layer. Worn clothing is described in the order
/// of the slots.
/// </summary>
public sealed class ClothingSlot : IIdentified
{
	public required string Id { get; init; }

	public required string Title { get; init; }
}
