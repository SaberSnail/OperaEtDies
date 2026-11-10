namespace OperaEtDies.Data;

/// <summary>
/// Where in an ordered list of values a <see cref="WeightShape"/> is most likely to choose.
/// </summary>
public enum ShapePeak
{
	/// <summary>
	/// The first value (or lowest number) is the most likely.
	/// </summary>
	Low,

	/// <summary>
	/// The value (or number) in the middle is the most likely.
	/// </summary>
	Middle,

	/// <summary>
	/// The last value (or highest number) is the most likely.
	/// </summary>
	High,
}
