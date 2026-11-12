using System.Globalization;
using System.Windows.Media;

namespace OperaEtDies.Data;

/// <summary>
/// A named color that portrait layers can use, which can depend on a character's properties.
/// </summary>
public sealed class PortraitColor : IIdentified
{
	public required string Id { get; init; }

	/// <summary>
	/// The color, used when none of the <see cref="Variants"/> apply: a hex color, or the id of an earlier color.
	/// </summary>
	public required string Color { get; init; }

	/// <summary>
	/// Colors that replace <see cref="Color"/> when their conditions are met. The first one that's met is used.
	/// </summary>
	public IReadOnlyList<PortraitColorVariant>? Variants { get; init; }

	/// <summary>
	/// A color to blend the color with, such as black to make a shadow: a hex color, or the id of an earlier color.
	/// </summary>
	public string? Mix { get; init; }

	/// <summary>
	/// How much of <see cref="Mix"/> to blend in, from 0 (none) to 1 (all).
	/// </summary>
	public double? Amount { get; init; }

	/// <summary>
	/// Parses a hex color written as #RRGGBB or #AARRGGBB.
	/// </summary>
	public static bool TryParseHex(string text, out Color color)
	{
		color = default;
		var value = 0u;
		var isValid = text.Length is 7 or 9 && text[0] == '#' && uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
		if (isValid)
		{
			var argb = text.Length == 7 ? 0xFF000000 | value : value;
			color = System.Windows.Media.Color.FromArgb((byte) (argb >> 24), (byte) (argb >> 16), (byte) (argb >> 8), (byte) argb);
		}

		return isValid;
	}
}

/// <summary>
/// A color that replaces a portrait color's usual color when its condition is met.
/// </summary>
public sealed class PortraitColorVariant
{
	public required CharacterCondition When { get; init; }

	/// <summary>
	/// A hex color, or the id of an earlier color.
	/// </summary>
	public required string Color { get; init; }
}
