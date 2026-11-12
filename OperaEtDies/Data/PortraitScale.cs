namespace OperaEtDies.Data;

/// <summary>
/// A named stretch that portrait layers and shapes can use, which can depend on a character's properties, such as
/// widening clothes to fit a broader body. Stretching a shape doesn't change the width of its outline.
/// </summary>
public sealed class PortraitScale : IIdentified
{
	public required string Id { get; init; }

	/// <summary>
	/// The horizontal position that stays in place as shapes are stretched.
	/// </summary>
	public double OriginX { get; init; }

	/// <summary>
	/// The vertical position that stays in place as shapes are stretched.
	/// </summary>
	public double OriginY { get; init; }

	/// <summary>
	/// How much shapes are stretched horizontally when none of the <see cref="Variants"/> apply. Defaults to 1.
	/// </summary>
	public double X { get; init; } = 1;

	/// <summary>
	/// How much shapes are stretched vertically when none of the <see cref="Variants"/> apply. Defaults to 1.
	/// </summary>
	public double Y { get; init; } = 1;

	/// <summary>
	/// Stretches that replace <see cref="X"/> and <see cref="Y"/> when their conditions are met. The first one that's met
	/// is used.
	/// </summary>
	public IReadOnlyList<PortraitScaleVariant>? Variants { get; init; }
}

/// <summary>
/// A stretch that replaces a portrait scale's usual stretch when its condition is met.
/// </summary>
public sealed class PortraitScaleVariant
{
	public required CharacterCondition When { get; init; }

	/// <summary>
	/// How much shapes are stretched horizontally. Defaults to 1.
	/// </summary>
	public double X { get; init; } = 1;

	/// <summary>
	/// How much shapes are stretched vertically. Defaults to 1.
	/// </summary>
	public double Y { get; init; } = 1;
}
