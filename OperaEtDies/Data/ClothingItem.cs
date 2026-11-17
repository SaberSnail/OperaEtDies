namespace OperaEtDies.Data;

/// <summary>
/// An article of clothing, from the clothing section of the data. Each piece of it that someone owns has its own colors.
/// </summary>
public sealed class ClothingItem : IIdentified
{
	/// <summary>
	/// The field of a color placeholder in <see cref="Description"/> that adds the color's article, as in "an ochre".
	/// </summary>
	public const string ArticleField = "a";

	/// <summary>
	/// The most coverage a piece can give a region: it covers it completely.
	/// </summary>
	public const int MaxCoverage = 10;

	public required string Id { get; init; }

	public required string Title { get; init; }

	/// <summary>
	/// The ids of the clothing slots the clothing is anchored in. Someone can only wear one piece in each slot in each
	/// layer. Where it reaches is given by <see cref="Coverage"/>.
	/// </summary>
	public required IReadOnlyList<string> Slots { get; init; }

	/// <summary>
	/// The id of the clothing layer the clothing is worn in.
	/// </summary>
	public required string Layer { get; init; }

	/// <summary>
	/// How much of each region of the body the clothing covers, keyed by clothing slot id, from 0 (none of it) to
	/// <see cref="MaxCoverage"/> (all of it). A region that isn't listed is 0. The clothing can cover regions it isn't
	/// anchored in, such as a tunic hanging down over the hips.
	/// </summary>
	public IReadOnlyDictionary<string, int> Coverage { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

	/// <summary>
	/// The id of the clothing bulk the clothing has, or null if it has none, in which case its coverage counts as it is
	/// for modesty and it doesn't hide the body's form.
	/// </summary>
	public string? Bulk { get; init; }

	/// <summary>
	/// Whether the clothing is an accessory, such as shoes or a belt: it isn't washed, and someone given several outfits
	/// with the same accessory has one piece of it, which the outfits share.
	/// </summary>
	public bool Accessory { get; init; }

	/// <summary>
	/// The names of the clothing's colors, such as "main" and "trim". Each piece has a color for each of them.
	/// </summary>
	public IReadOnlyList<string> Colors { get; init; } = [];

	/// <summary>
	/// A few words describing a piece of the clothing, used in sentences about what someone is wearing. It's a template
	/// whose placeholders are the names of its colors, as in "{main.a} kirtle".
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// What the clothing looks like.
	/// </summary>
	public required LongText Text { get; init; }

	/// <summary>
	/// Gets how much of a region the clothing covers, which is 0 for a region it doesn't list.
	/// </summary>
	public int GetCoverage(string slotId) => Coverage.GetValueOrDefault(slotId);

	/// <summary>
	/// Whether two pieces of clothing can't be worn together: they're in the same layer and take a slot in common.
	/// </summary>
	public bool Overlaps(ClothingItem other) => Layer == other.Layer && Slots.Intersect(other.Slots, StringComparer.Ordinal).Any();

	/// <summary>
	/// Describes a piece of the clothing in a few words, with the colors it has, by color name.
	/// </summary>
	public string FormatDescription(Func<string, ClothingColor> getColor) =>
		TextTemplate.Parse(Description).Format(x => getColor(x.Id) is var color && x.Field == ArticleField ? $"{color.Article} {color.Name}" : color.Name);
}
