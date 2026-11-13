namespace OperaEtDies.Data;

/// <summary>
/// A named shift that portrait layers can use, which can depend on a character's properties, such as drawing taller
/// characters higher in the portrait. Layers are shifted after they're stretched by their scale.
/// </summary>
public sealed class PortraitOffset : IIdentified
{
	public required string Id { get; init; }

	/// <summary>
	/// How far shapes are moved right when none of the <see cref="Variants"/> apply. Defaults to 0.
	/// </summary>
	public double X { get; init; }

	/// <summary>
	/// How far shapes are moved down when none of the <see cref="Variants"/> apply. Defaults to 0.
	/// </summary>
	public double Y { get; init; }

	/// <summary>
	/// Shifts that replace <see cref="X"/> and <see cref="Y"/> when their conditions are met. The first one that's met is
	/// used.
	/// </summary>
	public IReadOnlyList<PortraitOffsetVariant>? Variants { get; init; }
}

/// <summary>
/// A shift that replaces a portrait offset's usual shift when its condition is met.
/// </summary>
public sealed class PortraitOffsetVariant
{
	public required CharacterCondition When { get; init; }

	/// <summary>
	/// How far shapes are moved right. Defaults to 0.
	/// </summary>
	public double X { get; init; }

	/// <summary>
	/// How far shapes are moved down. Defaults to 0.
	/// </summary>
	public double Y { get; init; }
}
