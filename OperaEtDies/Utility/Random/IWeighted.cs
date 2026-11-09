namespace OperaEtDies.Utility.Random;

/// <summary>
/// An item that can be randomly selected from a list in proportion to its weight.
/// </summary>
public interface IWeighted
{
	/// <summary>
	/// The relative chance of this item being selected. Items with a weight of 0 are never selected.
	/// </summary>
	int Weight { get; }
}
