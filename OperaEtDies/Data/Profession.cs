namespace OperaEtDies.Data;

/// <summary>
/// A profession the player can choose when starting a new game.
/// </summary>
public sealed class Profession
{
	public required string Id { get; init; }

	public required string Title { get; init; }

	public required string DefaultLastName { get; init; }
}
