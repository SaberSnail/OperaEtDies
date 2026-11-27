namespace OperaEtDies.Data;

/// <summary>
/// A need's steps laid out in the order they're carried out, with the steps of "repeat" and "group" steps, "choose"
/// options, and called step lists in line, where to go next from each step that doesn't go on to the one after it, what
/// happens when each step fails, and the "finally" steps of each list of steps, laid out after them (for a "repeat"
/// step, after the "repeat" step itself).
/// </summary>
internal sealed class StepLayout
{
	/// <summary>
	/// Lays out steps, with the step lists that "call" steps can carry out, what happens when any of them fails, if
	/// nothing closer to the step says, and the steps carried out whenever they're left. A step list that would call
	/// itself, directly or not, or that isn't found, is left out.
	/// </summary>
	public static StepLayout Create(IEnumerable<Step?> steps, IReadOnlyDictionary<string, StepList>? stepLists, StepFailure? onFail = null, IEnumerable<Step?>? finallySteps = null)
	{
		var layout = new StepLayout();
		var root = new FailureFrame(onFail, new Dictionary<string, int>(StringComparer.Ordinal), null, []);
		layout.m_root = root;
		layout.AddBlock(steps, finallySteps, stepLists, new HashSet<string>(StringComparer.Ordinal), root.Scope, root, []);

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
	/// The lists of steps with "finally" steps, innermost first where they're nested.
	/// </summary>
	public IReadOnlyList<FinallyBlock> Blocks => m_blocks;

	/// <summary>
	/// Whether a step is among some "finally" steps.
	/// </summary>
	public bool IsInFinally(int step) => m_blocks.Any(x => x.IsInFinally(step));

	/// <summary>
	/// Gets the lists of steps with "finally" steps that going from one step to another leaves (that the step is in, but
	/// not in their "finally" steps, and the step gone to isn't in at all), innermost first. Only lists of the same steps
	/// count: a need's own, or the same appointment's.
	/// </summary>
	public IReadOnlyList<FinallyBlock> GetBlocksLeft(int from, int to)
	{
		var context = m_contexts.GetValueOrDefault(from, MainContext);
		return m_blocks.Where(x => x.Context == context && x.IsInBody(from) && !x.Contains(to)).ToList();
	}

	/// <summary>
	/// Gets the list of steps whose "finally" steps start at a step, or null if there's none.
	/// </summary>
	public FinallyBlock? GetBlockFinallyAt(int finallyStart) => m_blocks.FirstOrDefault(x => x.FinallyStart == finallyStart);

	/// <summary>
	/// Gets what happens when a step fails for a cause: its own "onFail" for the cause, or that of the "repeat" step,
	/// option, step list, or need it's in, the closest first, or failing all of them, stopping. A step skipped to is given
	/// by its index (the first step a step list carries out, for a "call" step). Skipping to a step that isn't found stops.
	/// </summary>
	public (StepFailureKind Kind, int? Target) GetFailure(int step, FailCause cause)
	{
		// The "finally" steps go on whatever happens.
		if (IsInFinally(step))
			return (StepFailureKind.Next, null);

		for (var frame = m_failures.TryGetValue(step, out var found) ? found : m_root; frame is not null; frame = frame.Outer)
		{
			if (frame.Failure?.Get(cause) is { } result)
				return Resolve(frame, result);
		}

		return (StepFailureKind.Stop, null);
	}

	/// <summary>
	/// Works out where a failure's result goes: the step skipped to, or for going on with a loop, its "repeat" step, where it
	/// decides whether to go round again, or for leaving one, the step after it. The loop is the one named, or the nearest
	/// around where the result is given (for a "repeat" step's own, itself). Skipping to a step or loop that isn't found
	/// stops.
	/// </summary>
	private (StepFailureKind Kind, int? Target) Resolve(FailureFrame frame, StepFailureResult result)
	{
		if (result.Kind == StepFailureKind.SkipTo)
			return frame.Scope.TryGetValue(result.Target ?? "", out var target) ? (StepFailureKind.SkipTo, target) : (StepFailureKind.Stop, null);
		if (result.Kind is not (StepFailureKind.LoopContinue or StepFailureKind.LoopBreak))
			return (result.Kind, null);

		var loop = result.Target is { } id ? frame.Loops.FirstOrDefault(x => x.Id == id) : frame.Loops.Count == 0 ? null : frame.Loops[0];
		return loop is null ? (StepFailureKind.Stop, null) :
			(result.Kind, result.Kind == StepFailureKind.LoopContinue ? loop.Index : m_next.TryGetValue(loop.Index, out var after) ? after : loop.Index + 1);
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

	/// <summary>
	/// Lays out a list of steps followed by its "finally" steps, if it has any, noting them as a block.
	/// </summary>
	private void AddBlock(IEnumerable<Step?> steps, IEnumerable<Step?>? finallySteps, IReadOnlyDictionary<string, StepList>? stepLists, HashSet<string> calling, Dictionary<string, int> scope,
		FailureFrame frame, IReadOnlyList<Loop> loops)
	{
		var start = m_steps.Count;
		Add(steps, stepLists, calling, scope, frame, loops);
		AddFinally(start, finallySteps, stepLists, calling, scope, frame, loops);
	}

	/// <summary>
	/// Lays out the "finally" steps of the steps from a start, if there are any, noting them as a block. A block laid out
	/// inside another comes before it, so the innermost are first.
	/// </summary>
	private void AddFinally(int start, IEnumerable<Step?>? finallySteps, IReadOnlyDictionary<string, StepList>? stepLists, HashSet<string> calling, Dictionary<string, int> scope,
		FailureFrame frame, IReadOnlyList<Loop> loops)
	{
		var finallyStart = m_steps.Count;
		Add(finallySteps ?? [], stepLists, calling, scope, frame, loops);
		if (m_steps.Count > finallyStart)
			m_blocks.Add(new FinallyBlock(start, finallyStart, m_steps.Count, m_context));
	}

	private void Add(IEnumerable<Step?> steps, IReadOnlyDictionary<string, StepList>? stepLists, HashSet<string> calling, Dictionary<string, int> scope, FailureFrame frame, IReadOnlyList<Loop> loops)
	{
		foreach (var step in steps.OfType<Step>())
		{
			// A step's own "onFail" comes first for it, and for a "repeat", "group", "choose", or "call" step, for the steps it
			// carries out. A "repeat" step's own is inside its loop.
			var loop = step.Kind == StepKind.Repeat ? new Loop(step.Id) : null;
			IReadOnlyList<Loop> inner = loop is null ? loops : [loop, .. loops];
			var own = step.OnFail is null ? frame : new FailureFrame(step.OnFail, scope, frame, inner);
			switch (step.Kind)
			{
			case StepKind.Repeat:
				var loopStart = m_steps.Count;
				Add(step.Steps ?? [], stepLists, calling, scope, own, inner);
				m_loopStarts[m_steps.Count] = loopStart;
				loop!.Index = m_steps.Count;
				AddStep(step, scope, own);

				// Its "finally" steps come once the loop as a whole is done.
				AddFinally(loopStart, step.Finally, stepLists, calling, scope, frame, loops);
				break;
			case StepKind.Group:
				if (step.Id is { } groupId)
					scope.TryAdd(groupId, m_steps.Count);
				AddBlock(step.Steps ?? [], step.Finally, stepLists, calling, scope, own, loops);
				break;
			case StepKind.Call:
				if (step.Id is { } callId)
					scope.TryAdd(callId, m_steps.Count);
				if (step.List is { } id && stepLists is not null && stepLists.TryGetValue(id, out var list) && calling.Add(id))
				{
					var listScope = new Dictionary<string, int>(StringComparer.Ordinal);
					AddBlock(list.Steps, list.Finally, stepLists, calling, listScope, list.OnFail is null ? own : new FailureFrame(list.OnFail, listScope, own, loops), loops);
					calling.Remove(id);
				}

				break;
			case StepKind.Choose:
				AddChoose(step, stepLists, calling, scope, own, loops);
				break;
			case StepKind.Ask when step.Appointment?.Steps is { } appointmentSteps:
				var ask = m_steps.Count;
				AddStep(step, scope, own);
				var start = m_steps.Count;

				// The appointment's steps are carried out on their own, so only the need's "onFail" applies to them, and they're
				// in no loop.
				var appointmentScope = new Dictionary<string, int>(StringComparer.Ordinal);
				var outerContext = m_context;
				m_context = ask;
				AddBlock(appointmentSteps, step.Appointment.Finally, stepLists, calling, appointmentScope, new FailureFrame(m_root!.Failure, appointmentScope, null, []), []);
				m_context = outerContext;
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
		if (m_context != MainContext)
			m_contexts[m_steps.Count] = m_context;
		m_steps.Add(step);
	}

	private void AddChoose(Step choose, IReadOnlyDictionary<string, StepList>? stepLists, HashSet<string> calling, Dictionary<string, int> scope, FailureFrame own, IReadOnlyList<Loop> loops)
	{
		var index = m_steps.Count;
		AddStep(choose, scope, own);
		var ranges = new List<(StepOption Option, int Start, int End)>();
		foreach (var option in (choose.Options ?? []).OfType<StepOption>())
		{
			// An option with steps of their own, such as a profession's, has ids of its own, as a step list does.
			var start = m_steps.Count;
			var optionScope = option.HasOwnIds ? new Dictionary<string, int>(StringComparer.Ordinal) : scope;
			AddBlock(option.Steps ?? [], option.Finally, stepLists, calling, optionScope, option.OnFail is null ? own : new FailureFrame(option.OnFail, optionScope, own, loops), loops);
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
	/// skip to, the loops it's in, innermost first, and what happens for causes it says nothing about.
	/// </summary>
	private sealed record FailureFrame(StepFailure? Failure, Dictionary<string, int> Scope, FailureFrame? Outer, IReadOnlyList<Loop> Loops);

	/// <summary>
	/// A loop: a "repeat" step's id, if it has one, and its index, once it's laid out.
	/// </summary>
	private sealed class Loop(string? id)
	{
		public string? Id { get; } = id;

		public int Index { get; set; }
	}

	private readonly System.Collections.ObjectModel.Collection<Step> m_steps = [];
	private readonly Dictionary<int, int> m_loopStarts = [];
	private readonly Dictionary<int, int> m_next = [];
	private readonly Dictionary<int, IReadOnlyList<(StepOption Option, int Start, int End)>> m_options = [];
	private readonly HashSet<int> m_optional = [];
	private readonly Dictionary<int, (int Start, int Meet, int End)> m_appointments = [];
	private readonly Dictionary<int, FailureFrame> m_failures = [];
	private readonly System.Collections.ObjectModel.Collection<FinallyBlock> m_blocks = [];
	private readonly Dictionary<int, int> m_contexts = [];
	private int m_context = MainContext;
	private FailureFrame? m_root;

	// The steps that aren't an appointment's. An appointment's are noted by the index of the "ask" step that makes it.
	private const int MainContext = -1;

	// Stands for the end of all the steps while they're laid out, since that isn't known until they all are.
	private const int EndOfSteps = int.MaxValue;

	// For each step in an option, the "choose" step and the start of the innermost option it's in.
	private readonly Dictionary<int, (int Choose, int Start)> m_containers = [];
}

/// <summary>
/// A list of steps with "finally" steps: where its steps start, where its "finally" steps start (after its steps, or for a
/// "repeat" step, after the "repeat" step), and the index after the last of them, with whose steps they are (a need's
/// own, or an appointment's, by the index of the "ask" step that makes it).
/// </summary>
internal sealed record FinallyBlock(int Start, int FinallyStart, int End, int Context)
{
	/// <summary>
	/// Whether a step is one of its steps, other than its "finally" steps.
	/// </summary>
	public bool IsInBody(int step) => step >= Start && step < FinallyStart;

	/// <summary>
	/// Whether a step is one of its "finally" steps.
	/// </summary>
	public bool IsInFinally(int step) => step >= FinallyStart && step < End;

	/// <summary>
	/// Whether a step is one of its steps, or one of its "finally" steps.
	/// </summary>
	public bool Contains(int step) => step >= Start && step < End;
}
