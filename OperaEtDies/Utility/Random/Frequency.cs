namespace OperaEtDies.Utility.Random;

/// <summary>
/// How common an item is when chosen at random from a list.
/// </summary>
public enum Frequency
{
	Common,
	Uncommon,
	Rare,
	VeryRare,
}

/// <summary>
/// An item that can be chosen at random based on its <see cref="Frequency"/>.
/// </summary>
public interface IHasFrequency
{
	FrequencyLevel Frequency { get; }
}
