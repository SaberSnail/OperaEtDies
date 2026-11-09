namespace OperaEtDies.Data;

/// <summary>
/// A template that creates text about a character, such as their name or a description.
/// </summary>
public sealed class CharacterTemplate : IIdentified
{
	public required string Id { get; init; }

	public required string Template { get; init; }
}
