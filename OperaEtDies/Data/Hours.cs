using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// The hours of the day something happens in, from the start hour up to the end hour, and the days it happens on. An end
/// of 24 means midnight, and an end before the start means the hours run past midnight.
/// </summary>
public sealed class Hours
{
	public required int Start { get; init; }

	public required int End { get; init; }

	/// <summary>
	/// The ids of the days of the week it happens on, or null for every day.
	/// </summary>
	public IReadOnlyList<string>? Days { get; init; }

	/// <summary>
	/// Whether it happens on special days.
	/// </summary>
	public bool OnSpecialDays { get; init; } = true;

	/// <summary>
	/// How many hours long it is each day.
	/// </summary>
	public int Length => Start < End ? End - Start : 24 - Start + End;

	/// <summary>
	/// Whether the given hour of the day is within the hours.
	/// </summary>
	public bool Contains(int hour) => Start < End ? hour >= Start && hour < End : hour >= Start || hour < End;

	/// <summary>
	/// Checks the hours. Hours that can only have a start and an end report the other settings as not allowed.
	/// </summary>
	internal IEnumerable<string> Validate(string path, bool allowDays)
	{
		if (Start is < 0 or > 23 || End is < 1 or > 24 || Start == End)
			yield return OurResources.HoursInvalid.FormatCurrentCulture(path, Start, End);
		if (!allowDays && Days is not null)
			yield return OurResources.HoursSettingNotAllowed.FormatCurrentCulture($"{path}.days");
		if (!allowDays && !OnSpecialDays)
			yield return OurResources.HoursSettingNotAllowed.FormatCurrentCulture($"{path}.onSpecialDays");
		if (Days is not null)
		{
			foreach (var error in DataValidation.ValidateStrings($"{path}.days", Days))
				yield return error;
		}
	}
}
