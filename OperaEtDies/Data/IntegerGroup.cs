namespace OperaEtDies.Data;

/// <summary>
/// A named part of an integer property's numbers, such as an age group, that conditions can use like a value.
/// </summary>
public sealed class IntegerGroup : IIdentified
{
	public required string Id { get; init; }

	/// <summary>
	/// The lowest number in the group, or null if the group has no lower limit.
	/// </summary>
	public int? Minimum { get; init; }

	/// <summary>
	/// The highest number in the group, or null if the group has no upper limit.
	/// </summary>
	public int? Maximum { get; init; }

	public bool Contains(int number) => number >= (Minimum ?? int.MinValue) && number <= (Maximum ?? int.MaxValue);

	public bool Overlaps(int minimum, int maximum) => (Minimum ?? int.MinValue) <= maximum && (Maximum ?? int.MaxValue) >= minimum;
}
