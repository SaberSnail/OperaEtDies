namespace OperaEtDies.Data;

/// <summary>
/// How wealthy someone or something is, compared to the rest of the village. Each level is worth twice the one below it.
/// </summary>
public enum WealthLevel
{
	VeryLow,
	Low,
	Medium,
	High,
}

public static class WealthLevelUtility
{
	/// <summary>
	/// The wealth levels a household, a house, or a quarter can have.
	/// </summary>
	public static IReadOnlyList<WealthLevel> HouseholdLevels { get; } = [WealthLevel.Low, WealthLevel.Medium, WealthLevel.High];

	/// <summary>
	/// Gets the amount of wealth a level is worth: 1, 2, 4, or 8.
	/// </summary>
	public static int GetAmount(this WealthLevel level) => level switch
	{
		WealthLevel.VeryLow => 1,
		WealthLevel.Low => 2,
		WealthLevel.Medium => 4,
		WealthLevel.High => 8,
		_ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
	};

	/// <summary>
	/// Gets the name of a level as it's written in the data files.
	/// </summary>
	public static string GetDataName(this WealthLevel level) => level switch
	{
		WealthLevel.VeryLow => "veryLow",
		WealthLevel.Low => "low",
		WealthLevel.Medium => "medium",
		WealthLevel.High => "high",
		_ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
	};
}
