namespace OperaEtDies.Data;

/// <summary>
/// When a portrait layer is drawn: a condition on the character's properties, and if it lists skies, under one of them.
/// </summary>
public sealed class PortraitCondition : CharacterCondition
{
	/// <summary>
	/// The ids of the calendar's skies the layer is drawn under, or null to draw it under any sky, or none.
	/// </summary>
	public IReadOnlyList<string>? Skies { get; init; }

	/// <summary>
	/// Checks a condition on a character's own values, given a function that gets the value id of a value list property
	/// (or null if it has no value), under a sky (or none). A missing condition is always met.
	/// </summary>
	public static bool IsMet(PortraitCondition? condition, Func<string, string?> getValueId, string? sky) =>
		CharacterCondition.IsMet(condition, getValueId) &&
		(condition?.Skies is not { } skies || (sky is not null && skies.Contains(sky, StringComparer.Ordinal)));
}
