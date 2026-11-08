namespace OperaEtDies.Data;

/// <summary>
/// Data classes implement this to report problems that JSON deserialization alone can't catch.
/// </summary>
public interface IValidatedData
{
	/// <summary>
	/// Returns a user-facing message for each problem found. Returns nothing if the data is valid.
	/// </summary>
	IEnumerable<string> Validate();
}
