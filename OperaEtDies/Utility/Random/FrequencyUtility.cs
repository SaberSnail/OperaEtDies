namespace OperaEtDies.Utility.Random;

/// <summary>
/// Frequency is how data files describe how common an item is: 1 (rare), 2 (uncommon), or 3 (common).
/// </summary>
public static class FrequencyUtility
{
	public const int MinFrequency = 1;
	public const int MaxFrequency = 3;

	public static bool IsValid(int frequency) => frequency is >= MinFrequency and <= MaxFrequency;

	/// <summary>
	/// Converts a frequency to a weight for random selection. Each step up in frequency is four times as likely.
	/// </summary>
	public static int GetWeight(int frequency) => frequency switch
	{
		1 => 1,
		2 => 4,
		3 => 16,
		_ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, $"Frequency must be from {MinFrequency} to {MaxFrequency}."),
	};
}
