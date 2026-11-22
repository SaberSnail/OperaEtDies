namespace OperaEtDies.Data;

/// <summary>
/// A need's steps laid out in the order they're carried out, with the steps of "repeat" steps, "choose" options, and
/// called step lists in line, where to go next from each step that doesn't go on to the one after it, and what happens
/// when each step fails.
/// </summary>
internal sealed class StepLayout
{
	/// <summary>
	/// Lays out steps, with the step lists that "call" steps can carry out, and what happens when any of them fails, if
	/// nothing closer to the step says. A step list that would call itself, directly or not, or that isn't found, is left
	/// out.
	/// </summary>
	public static StepLayout Create(IEnumerable<Step?> steps, IReadOnlyDictionary<string, StepList>? stepLists, StepFailure? onFail = null)
	{
		var layout = new StepLayout();
		var root = new FailureFrame(onFail, new Dictionary<string, int>(StringComparer.Ordinal), null);
		layout.m_root = root;
		layout.Add(steps, stepLists, new HashSet<string>(StringComparer.Ordinal), root.Scope, root);

		// The steps of an appointment end the steps once they're done.
		foreach (var key in layout.m_next.Where(x => x.Value == EndOfSteps).Select(x => x.Key).ToList())
			layout.m_next[key] = layout.m_steps.Count;
		return layout;
	}

	/// <summary>
	/// Every step, in the order they're carried out. A "repeat" step comes after the steps it repeats, and a "choose" step
	/// comes before the steps of each of its options, one after the other. "Call" steps aren't included, only the steps
	/// they carry out.
	/// </summary>
	public IReadOnlyList<Step> Steps => m_steps;

	/// <summary>
	/// For each "repeat" step, the index of the first step it repeats.
	/// </summary>
	public IReadOnlyDictionary<int, int> LoopStarts => m_loopStarts;

	/// <summary>
	/// For each step that isn't followed by the step after it, the index of the step that is: the end of a "choose" step's
	/// options, after the last step of an option (or after the "choose" step, if it chooses none).
	/// </summary>
	public IReadOnlyDictionary<int, int> Next => m_next;

	/// <summary>
	/// For each "choose" step, its options with the index of the first of their steps and the index after the last of them.
	/// </summary>
	public IReadOnlyDictionary<int, IReadOnlyList<(StepOption Option, int Start, int End)>> Options => m_options;

	/// <summary>
	/// The indexes of the steps that are part of a "choose" step's option or an appointment, which only some runs carry
	/// out.
	/// </summary>
	public IReadOnlySet<int> Optional => m_optional;

	/// <summary>
	/// For each "ask" step that makes appointments, the index of the first of the appointment's steps and the index after
	/// the last of them. They come right after the "ask" step, which goes on past them, and they're only carried out at
	/// the appointment, after which the steps are done.
	/// </summary>
	public IReadOnlyDictionary<int, (int Start, int Meet, int End)> Appointments => m_appointments;

	/// <summary>
	/// Gets what happens when a step fails for a cause: its own "onFail" for the cause, or that of the "repeat" step,
	/// option, step list, or need it's in, the closest first, or failing all of them, stopping. A step skipped to is given
	/// by its index (the first step a step list carries out, for a "call" step). Skipping to a step that isn't found stops.
	/// </summary>
	public (StepFailureKind Kind, int? Target) GetFailure(int step, FailCause cause)
	{
		for (var frame = m_failures.TryGetValue(step, out var found) ? found : m_root; frame is not null; frame = frame.Outer)
		{
			if (frame.Failure?.Get(cause) is { } result)
			{
				return result.Kind != StepFailureKind.SkipTo ? (result.Kind, null) :
					frame.Scope.TryGetValue(result.SkipTo ?? "", out var target) ? (StepFailureKind.SkipTo, target) :
					(StepFailureKind.Stop, null);
			}
		}

		return (StepFailureKind.Stop, null);
	}

	/// <summary>
	/// Gets the "choose" step and the option a step is in, the innermost if there are several, or null if it isn't in one.
	/// </summary>
	public (int Choose, int Start)? GetOption(int step) => m_containers.TryGetValue(step, out var found) ? found : null;

	/// <summary>
	/// Gets where a step is done: the place of the last "goTo" step carried out before it on the way to it, skipping the
	/// other options of any "choose" step it's in, or null if there's none.
	/// </summary>
	public SchedulePlace? GetPlaceOf(int step)
	{
		// An appointment's steps after its "meet" step are done where the appointment is, unless they go somewhere else. Those
		// before it are done wherever the person set off from, unless they go somewhere.
		foreach (var (ask, (start, meet, end)) in m_appointments)
		{
			if (step >= start && step < end)
			{
				var first = step > meet ? meet + 1 : start;
				for (var index = Math.Min(step, m_steps.Count) - 1; index >= first; index--)
				{
					if (m_steps[index].Kind == StepKind.GoTo)
						return m_steps[index].Place;
				}

				return step > meet ? m_steps[ask].Appointment!.Place : null;
			}
		}

		var container = m_containers.TryGetValue(step, out var found) ? found : ((int Choose, int Start)?) null;
		for (var index = Math.Min(step, m_steps.Count) - 1; index >= 0; index--)
		{
			if (container is { } option && index < option.Start)
			{
				// Before the start of the option, the steps are other options, so carry on from before the "choose" step.
				index = option.Choose;
				container = m_containers.TryGetValue(option.Choose, out var outer) ? outer : null;
			}
			else if (m_steps[index].Kind == StepKind.GoTo)
			{
				return m_steps[index].Place;
			}
		}

		return null;
	}

	private void Add(IEnumerable<Step?> steps, IReadOnlyDictionary<string, StepList>? stepLists, HashSet<string> calling, Dictionary<string, int> scope, FailureFrame frame)
	{
		foreach (var step in steps.OfType<Step>())
		{
			// A step's own "onFail" comes first for it, and for a "repeat", "choose", or "call" step, for the steps it carries out.
			var own = step.OnFail is null ? frame : new FailureFrame(step.OnFail, scope, frame);
			switch (step.Kind)
			{
			case StepKind.Repeat:
				var loopStart = m_steps.Count;
				Add(step.Steps ?? [], stepLists, calling, scope, own);
				m_loopStarts[m_steps.Count] = loopStart;
				AddStep(step, scope, own);
				break;
			case StepKind.Call:
				if (step.Id is { } callId)
					scope.TryAdd(callId, m_steps.Count);
				if (step.List is { } id && stepLists is not null && stepLists.TryGetValue(id, out var list) && calling.Add(id))
				{
					var listScope = new Dictionary<string, int>(StringComparer.Ordinal);
					Add(list.Steps, stepLists, calling, listScope, list.OnFail is null ? own : new FailureFrame(list.OnFail, listScope, own));
					calling.Remove(id);
				}

				break;
			case StepKind.Choose:
				AddChoose(step, stepLists, calling, scope, own);
				break;
			case StepKind.Ask when step.Appointment?.Steps is { } appointmentSteps:
				var ask = m_steps.Count;
				AddStep(step, scope, own);
				var start = m_steps.Count;

				// The appointment's steps are carried out on their own, so only the need's "onFail" applies to them.
				var appointmentScope = new Dictionary<string, int>(StringComparer.Ordinal);
				Add(appointmentSteps, stepLists, calling, appointmentScope, new FailureFrame(m_root!.Failure, appointmentScope, null));
				var end = m_steps.Count;

				// The steps before the appointment's go on past them, and the appointment's steps end the steps.
				foreach (var key in m_next.Keys.Where(x => x >= start && x < end && m_next[x] == end).ToList())
					m_next[key] = EndOfSteps;
				if (end > start && !m_next.ContainsKey(end - 1))
					m_next[end - 1] = EndOfSteps;
				m_next[ask] = end;
				var meet = Enumerable.Range(start, end - start).FirstOrDefault(x => m_steps[x].Kind == StepKind.Meet, start);
				m_appointments[ask] = (start, meet, end);
				for (var index = start; index < end; index++)
					m_optional.Add(index);
				break;
			default:
				AddStep(step, scope, own);
				break;
			}
		}
	}

	private void AddStep(Step step, Dictionary<string, int> scope, FailureFrame frame)
	{
		if (step.Id is { } id)
			scope.TryAdd(id, m_steps.Count);
		m_failures[m_steps.Count] = frame;
		m_steps.Add(step);
	}

	private void AddChoose(Step choose, IReadOnlyDictionary<string, StepList>? stepLists, HashSet<string> calling, Dictionary<string, int> scope, FailureFrame own)
	{
		var index = m_steps.Count;
		AddStep(choose, scope, own);
		var ranges = new List<(StepOption Option, int Start, int End)>();
		foreach (var option in (choose.Options ?? []).OfType<StepOption>())
		{
			// An option with steps of their own, such as a profession's, has ids of its own, as a step list does.
			var start = m_steps.Count;
			var optionScope = option.HasOwnIds ? new Dictionary<string, int>(StringComparer.Ordinal) : scope;
			Add(option.Steps ?? [], stepLists, calling, optionScope, option.OnFail is null ? own : new FailureFrame(option.OnFail, optionScope, own));
			ranges.Add((option, start, m_steps.Count));
		}

		// Each option's last step, and anything in it that would go on to the step after it, goes on past the other
		// options instead, as does the "choose" step itself when it chooses none.
		var end = m_steps.Count;
		foreach (var (_, start, after) in ranges)
		{
			foreach (var key in m_next.Keys.Where(x => x >= start && x < after && m_next[x] == after).ToList())
				m_next[key] = end;
			if (after > start && !m_next.ContainsKey(after - 1))
				m_next[after - 1] = end;
			for (var step = start; step < after; step++)
			{
				m_optional.Add(step);
				m_containers.TryAdd(step, (index, start));
			}
		}

		m_next[index] = end;
		m_options[index] = ranges;
	}

	/// <summary>
	/// What happens when a step fails, from one thing the step is in (or the step itself), with the ids of the steps it can
	/// skip to, and what happens for causes it says nothing about.
	/// </summary>
	private sealed record FailureFrame(StepFailure? Failure, Dictionary<string, int> Scope, FailureFrame? Outer);

	private readonly System.Collections.ObjectModel.Collection<Step> m_steps = [];
	private readonly Dictionary<int, int> m_loopStarts = [];
	private readonly Dictionary<int, int> m_next = [];
	private readonly Dictionary<int, IReadOnlyList<(StepOption Option, int Start, int End)>> m_options = [];
	private readonly HashSet<int> m_optional = [];
	private readonly Dictionary<int, (int Start, int Meet, int End)> m_appointments = [];
	private readonly Dictionary<int, FailureFrame> m_failures = [];
	private FailureFrame? m_root;

	// Stands for the end of all the steps while they're laid out, since that isn't known until they all are.
	private const int EndOfSteps = int.MaxValue;

	// For each step in an option, the "choose" step and the start of the innermost option it's in.
	private readonly Dictionary<int, (int Choose, int Start)> m_containers = [];
}
