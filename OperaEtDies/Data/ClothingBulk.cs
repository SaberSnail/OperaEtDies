namespace OperaEtDies.Data;

/// <summary>
/// How loose or heavy clothing is, from the clothing bulks section of the data, such as light linen or a bulky cloak:
/// how it changes the modesty its coverage gives, and whether it hides the form of the body under it.
/// </summary>
public sealed class ClothingBulk : IIdentified
{
	public required string Id { get; init; }

	public required string Title { get; init; }

	/// <summary>
	/// How much more (or, when negative, less) modesty clothing of the bulk gives than its coverage, in each region it
	/// covers, such as light clothing that still shows the form of what it covers.
	/// </summary>
	public int Modesty { get; init; }

	/// <summary>
	/// Whether clothing of the bulk hides the form of the body under it, such as a paunch or broad shoulders.
	/// </summary>
	public bool HidesForm { get; init; }
}
