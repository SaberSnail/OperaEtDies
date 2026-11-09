namespace OperaEtDies.Data;

/// <summary>
/// A place on the map that the player and other characters can be in.
/// </summary>
public sealed class Location
{
	public required string Id { get; init; }

	public required string Title { get; init; }

	/// <summary>
	/// One sentence, used when the location is listed as a place to go.
	/// </summary>
	public required string ShortDescription { get; init; }

	/// <summary>
	/// One paragraph, shown when arriving at the location.
	/// </summary>
	public required string MediumDescription { get; init; }

	/// <summary>
	/// Several paragraphs, shown when examining the location.
	/// </summary>
	public required IReadOnlyList<string> LongDescription { get; init; }

	/// <summary>
	/// The ids of the locations that can be reached from this one.
	/// </summary>
	public IReadOnlyList<string> Connections { get; init; } = [];
}
