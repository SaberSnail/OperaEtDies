namespace OperaEtDies.Data;

/// <summary>
/// A template that creates text about a character, such as their name or a description. It has either a single
/// template, or variants that are chosen by the character's property values.
/// </summary>
public sealed class CharacterTemplate : IIdentified
{
	public required string Id { get; init; }

	/// <summary>
	/// The template, when it doesn't have variants.
	/// </summary>
	public string? Template { get; init; }

	/// <summary>
	/// Templates that are chosen by the character's property values. The first variant whose condition is met is used.
	/// </summary>
	public IReadOnlyList<CharacterTemplateVariant>? Variants { get; init; }

	/// <summary>
	/// Gets the variants, treating a single template as one variant with no condition.
	/// </summary>
	public IReadOnlyList<CharacterTemplateVariant> GetVariants() => Variants ?? [new CharacterTemplateVariant { Template = Template ?? "" }];

	/// <summary>
	/// Parses the variants of a template that was already checked when the data was loaded.
	/// </summary>
	public IReadOnlyList<TextTemplateVariant> ParseVariants() =>
		GetVariants().Select(x => new TextTemplateVariant(x.When, TextTemplate.Parse(x.Template))).ToList();
}

/// <summary>
/// One of the templates a <see cref="CharacterTemplate"/> can choose from.
/// </summary>
public sealed class CharacterTemplateVariant
{
	/// <summary>
	/// A condition on what can be seen of the character that must be met to use this variant.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// The template. Unlike a template without variants, it can be empty.
	/// </summary>
	public required string Template { get; init; }
}
