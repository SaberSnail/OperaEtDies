using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// How someone other than the player last chose their goal, for developers to see why they're doing what they're doing:
/// when they chose, how urgent each of their needs was then, and what came of it.
/// </summary>
/// <param name="Time">When the goal was chosen.</param>
/// <param name="Urgencies">Each need that was at all urgent then, with its urgency, in the order of the needs in the data.</param>
/// <param name="Need">The need chosen at random from the ones urgent enough, or null if none were.</param>
/// <param name="Result">Whether the chosen need became the goal, or why the person went idle instead.</param>
public sealed record GoalChoice(TimePoint Time, IReadOnlyList<NeedUrgency> Urgencies, Need? Need, GoalChoiceResult Result)
{
	/// <summary>
	/// Gets the chance each need had of being chosen, from 0 to 1, with the given settings: 0 for a need that wasn't urgent
	/// enough to be considered.
	/// </summary>
	public double GetChance(NeedUrgency urgency, ScheduleSettings settings)
	{
		var total = Urgencies.Where(x => settings.IsGoalCandidate(x.Urgency)).Sum(x => settings.GetGoalWeight(x.Urgency));
		return total > 0 && settings.IsGoalCandidate(urgency.Urgency) ? settings.GetGoalWeight(urgency.Urgency) / total : 0;
	}

	internal GoalChoiceRecord CreateRecord() =>
		new(Time.SaveToString(), Urgencies.Select(x => new NeedUrgencyRecord(x.Need.Id, x.Urgency)).ToList(), Need?.Id, Result);
}

/// <summary>
/// How urgent a need was when someone chose their goal.
/// </summary>
public sealed record NeedUrgency(Need Need, double Urgency);

/// <summary>
/// What came of choosing a goal.
/// </summary>
public enum GoalChoiceResult
{
	/// <summary>
	/// The chosen need became the goal.
	/// </summary>
	Chosen,

	/// <summary>
	/// No need was urgent enough, so the person went idle.
	/// </summary>
	NothingUrgent,

	/// <summary>
	/// There was nowhere to meet the chosen need, so the person went idle.
	/// </summary>
	NoPlace,

	/// <summary>
	/// There was no time left to spend on the chosen need once the person got there, so they went idle.
	/// </summary>
	NoTime,
}

/// <summary>
/// The saved form of a goal choice, with the needs by id.
/// </summary>
public sealed record GoalChoiceRecord(string Time, IReadOnlyList<NeedUrgencyRecord> Urgencies, string? Need, GoalChoiceResult Result);

/// <summary>
/// The saved form of a need's urgency in a goal choice.
/// </summary>
public sealed record NeedUrgencyRecord(string Need, double Urgency);
