namespace OperaEtDies.Data;

/// <summary>
/// The fewest and most minutes something could take, such as steps whose "choose" steps could go more than one way.
/// </summary>
public readonly record struct MinuteRange(int Min, int Max)
{
	/// <summary>
	/// No time at all.
	/// </summary>
	public static MinuteRange Zero => default;

	/// <summary>
	/// Gets the range that takes exactly so many minutes.
	/// </summary>
	public static MinuteRange Exactly(int minutes) => new(minutes, minutes);

	/// <summary>
	/// Gets the range covering both ranges, for something that could go either way.
	/// </summary>
	public MinuteRange Or(MinuteRange other) => new(Math.Min(Min, other.Min), Math.Max(Max, other.Max));

	public static MinuteRange operator +(MinuteRange first, MinuteRange second) => new(first.Min + second.Min, first.Max + second.Max);

	public static MinuteRange operator +(MinuteRange range, int minutes) => new(range.Min + minutes, range.Max + minutes);

	public static MinuteRange Add(MinuteRange first, MinuteRange second) => first + second;
}
