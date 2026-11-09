namespace OperaEtDies.Data;

/// <summary>
/// One of the values a character attribute (such as height) can have.
/// </summary>
public sealed class CharacterOption
{
	public required string Id { get; init; }

	public required string Title { get; init; }
}
