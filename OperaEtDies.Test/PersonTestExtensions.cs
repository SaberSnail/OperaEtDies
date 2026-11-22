using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test;

/// <summary>
/// Sets up people's plans for tests.
/// </summary>
internal static class PersonTestExtensions
{
	/// <summary>
	/// Gives someone tasks, and with a goal, a sequence of its steps that carries on from a step once the tasks are done,
	/// with the time chosen for it, replacing any sequences they had. A goal without a step is kept while the tasks last.
	/// Returns the sequence, if there is one.
	/// </summary>
	public static StepSequence? SetPlan(this Person person, Need? goal, IEnumerable<PersonTask> tasks, GoalChoice? choice = null, int? startStep = null, int? goalMinutes = null)
	{
		foreach (var existing in person.Sequences.ToList())
			person.RemoveSequence(existing);
		person.SetTasks(tasks, choice);
		if (goal is null)
			return null;

		var sequence = new StepSequence(goal, startStep ?? goal.Steps.Count, goalMinutes) { Place = person.Location };
		person.AddSequence(sequence);
		return sequence;
	}

	/// <summary>
	/// Gets the bundles someone is carrying, by title.
	/// </summary>
	public static IReadOnlyDictionary<string, Bundle> GetBundles(this Person person) =>
		person.Inventory.Items.OfType<Bundle>().ToDictionary(x => x.Title, StringComparer.Ordinal);
}
