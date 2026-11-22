using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// The last time someone saw another person: where, when, and what that person was doing right then. Only what they were
/// doing at that moment is known, not their goal or where they were ultimately going.
/// </summary>
/// <param name="Location">Where the person was seen.</param>
/// <param name="Time">When the person was last seen.</param>
/// <param name="Activity">What the person was doing.</param>
/// <param name="Need">For <see cref="SightingActivity.Busy"/>, the need the person was spending time on.</param>
/// <param name="Destination">For <see cref="SightingActivity.Walking"/> and <see cref="SightingActivity.Left"/>, the next
/// place on the person's way.</param>
/// <param name="Source">Who told them about the sighting, or null if they saw it themselves.</param>
/// <param name="Plan">What the person said they were doing, if they were asked, or null if they weren't.</param>
/// <param name="Doing">For <see cref="SightingActivity.Busy"/>, what the person was shown to be doing, such as "Washing
/// clothes": the activity of the step they were on.</param>
public sealed record Sighting(Location Location, TimePoint Time, SightingActivity Activity, Need? Need = null, Location? Destination = null, Person? Source = null, SightingPlan? Plan = null,
	string? Doing = null)
{
	/// <summary>
	/// Whether the sighting was learned from someone else, rather than seen.
	/// </summary>
	public bool IsSecondhand => Source is not null;

	internal SightingRecord CreateRecord(Person person) =>
		new(person.Id, Location.Id, Time.SaveToString(), Activity, Need?.Id, Destination?.Id, Source?.Id, Plan is { } plan ? new SightingPlanRecord(plan.Goal?.Id, plan.Destination?.Id, plan.Doing) : null, Doing);
}

/// <summary>
/// What someone said they were doing when they were asked: their goal, and where they were ultimately going if they were
/// walking somewhere.
/// </summary>
/// <param name="Goal">The need they were trying to meet, or null if they were idle.</param>
/// <param name="Destination">Where their walk ended, or null if they weren't walking.</param>
/// <param name="Doing">What they said they were doing or going to do for their goal, such as "Washing clothes": the
/// activity of the step they were on, or the next one that spends time, or null if they were idle.</param>
public sealed record SightingPlan(Need? Goal, Location? Destination, string? Doing = null);

/// <summary>
/// What someone was doing when they were seen.
/// </summary>
public enum SightingActivity
{
	/// <summary>
	/// Nothing in particular, as for the player between actions.
	/// </summary>
	None,

	/// <summary>
	/// Idle, with no goal.
	/// </summary>
	Idle,

	/// <summary>
	/// Spending time on a need.
	/// </summary>
	Busy,

	/// <summary>
	/// Setting off along a path to the next place.
	/// </summary>
	Walking,

	/// <summary>
	/// Leaving for the next place.
	/// </summary>
	Left,

	/// <summary>
	/// Talking with someone.
	/// </summary>
	Talking,
}

/// <summary>
/// The saved form of a sighting, with the person seen.
/// </summary>
public sealed record SightingRecord(WorldId Person, WorldId Location, string Time, SightingActivity Activity, string? Need, WorldId? Destination, WorldId? Source, SightingPlanRecord? Plan = null,
	string? Doing = null);

/// <summary>
/// The saved form of what someone said they were doing.
/// </summary>
public sealed record SightingPlanRecord(string? Goal, WorldId? Destination, string? Doing = null);
