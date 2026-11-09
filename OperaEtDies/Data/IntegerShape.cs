namespace OperaEtDies.Data;

/// <summary>
/// Which part of an integer range random values favor.
/// </summary>
public enum IntegerShape
{
	/// <summary>
	/// Values near the minimum are the most common, and values near the maximum are rare.
	/// </summary>
	Low,

	/// <summary>
	/// Values near the middle are the most common, and values near either end are rare.
	/// </summary>
	Middle,

	/// <summary>
	/// Values near the maximum are the most common, and values near the minimum are rare.
	/// </summary>
	High,
}
