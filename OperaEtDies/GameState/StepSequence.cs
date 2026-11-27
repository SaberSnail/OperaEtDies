using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// One run through a need's steps by one character: the step it's on, and everything its steps keep track of. A
/// character can have several; see <see cref="Person.Sequences"/>. It belongs to its person, and is saved with them.
/// See Docs/Steps.md.
/// </summary>
public sealed class StepSequence : NotifyPropertyChangedBase
{
	internal StepSequence(Need need, int step = 0, int? minutesLeft = null, SequenceRole role = SequenceRole.Steps, SetOffState setOff = SetOffState.Gone)
	{
		Need = need;
		Role = role;
		m_setOff = setOff;
		m_step = step;
		m_minutesLeft = minutesLeft;
	}

	/// <summary>
	/// The need whose steps it runs through, or for taking part in someone else's steps, the need of theirs it's part of.
	/// </summary>
	public Need Need { get; }

	/// <summary>
	/// Whether it runs through the need's steps, or takes part in someone else's: as the client of an appointment, or
	/// following someone who leads them.
	/// </summary>
	public SequenceRole Role { get; }

	/// <summary>
	/// For taking part in someone else's steps, who the person follows, once they're following, or null.
	/// </summary>
	public Person? Leader
	{
		get => m_leader;
		internal set => SetPropertyField(value, ref m_leader);
	}

	/// <summary>
	/// The index in <see cref="Need.Steps"/> of the step it's on, or the number of steps once it's done.
	/// </summary>
	public int Step
	{
		get => m_step;
		internal set => SetPropertyField(value, ref m_step);
	}

	/// <summary>
	/// Whether it's done: past its last step.
	/// </summary>
	public bool IsDone => m_step >= Need.Steps.Count;

	/// <summary>
	/// The step it's on, or null once it's done.
	/// </summary>
	public Step? CurrentStep => IsDone ? null : Need.Steps[m_step];

	/// <summary>
	/// What to tell the player to do next: the text of the next step that goes somewhere, naming the sequence's partner
	/// where it names the other person (see <see cref="OtherText"/>), or null if there's none. The
	/// steps are followed in the order they're carried out, into the first option of a "choose" step, so steps that aren't
	/// on the way (the other options, such as other professions' steps, and the steps of an appointment) are left out.
	/// </summary>
	public string? NextText
	{
		get
		{
			var steps = Need.Steps;
			for (var index = m_step; index < steps.Count; index = Need.GetOptions(index) is [var (_, start, end), ..] && end > start ? start : Need.GetNextStep(index))
			{
				if (steps[index] is { Kind: StepKind.GoTo, Player.Text: { } text })
					return ActivityText.FillOther(text.Text, Partner);
			}

			return null;
		}
	}

	/// <summary>
	/// What's left of the time chosen for it (its shift), which "do" steps that take from it use up, or null for a level
	/// need met until it's met, or a quota measured in runs.
	/// </summary>
	public int? MinutesLeft
	{
		get => m_minutesLeft;
		internal set => SetPropertyField(value, ref m_minutesLeft);
	}

	/// <summary>
	/// Uses up some of the time chosen for it.
	/// </summary>
	internal void UseMinutes(int minutes)
	{
		if (m_minutesLeft is { } left)
			MinutesLeft = Math.Max(left - minutes, 0);
	}

	/// <summary>
	/// Whether the current step has been carried out by the game, with the tasks it gave the character, so once they're
	/// done, it's only checked.
	/// </summary>
	public bool IsCarriedOut { get; internal set; }

	/// <summary>
	/// Whether carrying out the current step was cut short by the end of its hours.
	/// </summary>
	public bool IsCut { get; internal set; }

	/// <summary>
	/// When the current "wait" or "wander" step is over, once it's been reached, or null.
	/// </summary>
	public TimePoint? Until { get; internal set; }

	/// <summary>
	/// Where the character was when they got to where the current steps are done (after the last step that goes
	/// somewhere), or null while it's waiting to set off for an appointment, or on hold for an appointment for the same
	/// need, after which it carries on from wherever they are. Leaving it partway through fails the step they're on.
	/// </summary>
	public Location? Place { get; internal set; }

	/// <summary>
	/// How much of it the game carries out for the player, rather than the player doing it themselves.
	/// </summary>
	public StepTakeover Takeover
	{
		get => m_takeover;
		internal set => SetPropertyField(value, ref m_takeover);
	}

	/// <summary>
	/// The appointment it's for: its steps are the appointment's, carried out once both are there. Null otherwise.
	/// </summary>
	public Appointment? Appointment { get; internal set; }

	/// <summary>
	/// For an appointment's sequence, whether the person has set off for it yet. Until they have, it's never their current
	/// sequence, and none of its steps are carried out.
	/// </summary>
	public SetOffState SetOff
	{
		get => m_setOff;
		internal set => SetPropertyField(value, ref m_setOff);
	}

	/// <summary>
	/// Whether the person has set off for it, as for any sequence that isn't for an appointment.
	/// </summary>
	public bool HasSetOff => m_setOff == SetOffState.Gone;

	/// <summary>
	/// The person its steps last talked with or asked, or null if they haven't talked with anyone.
	/// </summary>
	public Person? Partner
	{
		get => m_partner;
		internal set => SetPropertyField(value, ref m_partner);
	}

	/// <summary>
	/// Which friend (if any) its "seek" step chose to look for, where, and why, or null if it hasn't sought anyone.
	/// </summary>
	public SeekChoice? Seeking
	{
		get => m_seeking;
		internal set => SetPropertyField(value, ref m_seeking);
	}

	/// <summary>
	/// The answers to the questions its steps asked, by name.
	/// </summary>
	public IReadOnlyDictionary<string, bool> Answers => m_answers;

	/// <summary>
	/// The questions its steps asked that were answered no because someone would have been too shy, by name, with who
	/// would have been.
	/// </summary>
	public IReadOnlyDictionary<string, Person> ShyAnswers => m_shyAnswers;

	internal void SetAnswer(string name, bool isYes, Person? shy = null)
	{
		m_answers[name] = isYes;
		if (shy is not null && !isYes)
			m_shyAnswers[name] = shy;
		else
			m_shyAnswers.Remove(name);
	}

	/// <summary>
	/// The places its steps remembered, by name.
	/// </summary>
	public IReadOnlyDictionary<string, Location> RememberedPlaces => m_rememberedPlaces;

	internal void RememberPlace(string name, Location location) => m_rememberedPlaces[name] = location;

	/// <summary>
	/// The bundles its steps took or made, which the character carries, by the step's bundle id.
	/// </summary>
	public IReadOnlyDictionary<string, Bundle> Bundles => m_bundles;

	internal void AddBundle(string id, Bundle bundle) => m_bundles[id] = bundle;

	internal Bundle? RemoveBundle(string id) => m_bundles.Remove(id, out var bundle) ? bundle : null;

	/// <summary>
	/// While its steps are being left, such as on failing, the index of the first "finally" step of the list of steps
	/// whose "finally" steps it's carrying out, or null. See <see cref="Unwinding"/>.
	/// </summary>
	public int? UnwindRunning { get; private set; }

	/// <summary>
	/// While its steps are being left, the first "finally" steps of the other lists of steps it's leaving, to carry out
	/// after those running, innermost first.
	/// </summary>
	public IReadOnlyList<int> UnwindQueue { get; private set; } = [];

	/// <summary>
	/// While its steps are being left, the step it goes on to once their "finally" steps are done, or the number of steps
	/// to end it.
	/// </summary>
	public int? UnwindTo { get; private set; }

	/// <summary>
	/// Whether it's leaving its steps, carrying out their "finally" steps on the way to another step, or to its end.
	/// </summary>
	public bool IsUnwinding => UnwindRunning is not null;

	/// <summary>
	/// Whether it's finishing up: carrying out "finally" steps on the way to its end, since it's been given up.
	/// </summary>
	public bool IsFinishingUp => IsUnwinding && UnwindTo >= Need.Steps.Count;

	/// <summary>
	/// Starts leaving its steps, carrying out the "finally" steps of each list of steps it leaves, innermost first, then
	/// going on to a step (or its end). Returns the first "finally" step to go to.
	/// </summary>
	internal int StartUnwinding(IReadOnlyList<int> finallyStarts, int to)
	{
		UnwindRunning = finallyStarts[0];
		UnwindQueue = [.. finallyStarts.Skip(1)];
		UnwindTo = to;
		return finallyStarts[0];
	}

	/// <summary>
	/// Once the "finally" steps running are done, gets the next ones to carry out, or else where it was going, and stops
	/// leaving.
	/// </summary>
	internal int ContinueUnwinding()
	{
		if (UnwindQueue.Count != 0)
		{
			UnwindRunning = UnwindQueue[0];
			UnwindQueue = [.. UnwindQueue.Skip(1)];
			return UnwindRunning.Value;
		}

		var to = UnwindTo ?? Need.Steps.Count;
		UnwindRunning = null;
		UnwindQueue = [];
		UnwindTo = null;
		return to;
	}

	/// <summary>
	/// While its steps are being left, goes on to the end instead of the step it was going to, as when it's given up.
	/// </summary>
	internal void UnwindToEnd() => UnwindTo = Need.Steps.Count;

	/// <summary>
	/// For the player, when they started keeping a bundle its steps made to finish later, rather than dropping it as their
	/// steps were given up, or null if they aren't keeping one.
	/// </summary>
	public TimePoint? KeptSince
	{
		get => m_keptSince;
		internal set => SetPropertyField(value, ref m_keptSince);
	}

	/// <summary>
	/// Goes on to a step, forgetting how the one it was on was carried out.
	/// </summary>
	internal void GoTo(int step)
	{
		Step = step;
		IsCarriedOut = false;
		IsCut = false;
		Until = null;
	}

	internal StepSequenceRecord CreateRecord() =>
		new(Need.Id, m_step, m_minutesLeft, IsCarriedOut ? true : null, IsCut ? true : null, Until?.SaveToString(), Place?.Id,
			m_takeover == StepTakeover.None ? null : m_takeover, Appointment?.Id, Partner?.Id,
			m_seeking is { } seeking ? new SeekChoiceRecord(seeking.Friend?.Id, seeking.Place.Id, seeking.Clue) : null,
			m_answers.Count == 0 ? null : new Dictionary<string, bool>(m_answers, StringComparer.Ordinal),
			m_rememberedPlaces.Count == 0 ? null : m_rememberedPlaces.ToDictionary(x => x.Key, x => x.Value.Id, StringComparer.Ordinal),
			m_bundles.Count == 0 ? null : m_bundles.ToDictionary(x => x.Key, x => x.Value.Id, StringComparer.Ordinal),
			Role == SequenceRole.Steps ? null : Role, m_leader?.Id, m_setOff == SetOffState.Gone ? null : m_setOff, m_shyAnswers.Count == 0 ? null : m_shyAnswers.ToDictionary(x => x.Key, x => x.Value.Id, StringComparer.Ordinal),
			UnwindRunning is { } running ? new StepUnwindRecord(running, UnwindQueue.Count == 0 ? null : [.. UnwindQueue], UnwindTo ?? Need.Steps.Count) : null, m_keptSince?.SaveToString());

	private readonly Dictionary<string, bool> m_answers = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Person> m_shyAnswers = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Location> m_rememberedPlaces = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Bundle> m_bundles = new(StringComparer.Ordinal);
	private int m_step;
	private int? m_minutesLeft;
	private StepTakeover m_takeover;
	private Person? m_partner;
	private Person? m_leader;
	private SeekChoice? m_seeking;
	private SetOffState m_setOff;
	private TimePoint? m_keptSince;
}

/// <summary>
/// How much of the player's sequence the game carries out for them.
/// </summary>
public enum StepTakeover
{
	/// <summary>
	/// None: the player does each step themselves.
	/// </summary>
	None,

	/// <summary>
	/// The step it's on, after which the player carries on themselves.
	/// </summary>
	Step,

	/// <summary>
	/// All of it, as for anyone else, except that the player still decides whether to keep going.
	/// </summary>
	All,
}

/// <summary>
/// What a sequence of steps is: a run through a need's steps, or taking part in someone else's.
/// </summary>
public enum SequenceRole
{
	/// <summary>
	/// A run through the need's steps.
	/// </summary>
	Steps,

	/// <summary>
	/// Keeping an appointment as its client: going there, meeting the worker, and following them until they're done.
	/// </summary>
	Client,

	/// <summary>
	/// Following someone who leads them, until they're let go.
	/// </summary>
	Follower,
}

/// <summary>
/// Whether someone has set off for an appointment their sequence is for.
/// </summary>
public enum SetOffState
{
	/// <summary>
	/// They've set off (or it isn't for an appointment), so its steps are carried out.
	/// </summary>
	Gone,

	/// <summary>
	/// They agreed to it, and it isn't time to set off yet.
	/// </summary>
	Waiting,

	/// <summary>
	/// It's time for the player to set off, and they're being asked whether to.
	/// </summary>
	Due,
}

/// <summary>
/// The saved form of a <see cref="StepSequence"/>.
/// </summary>
public sealed record StepSequenceRecord(string Need, int Step, int? MinutesLeft = null, bool? IsCarriedOut = null, bool? IsCut = null, string? Until = null, WorldId? Place = null,
	StepTakeover? Takeover = null, WorldId? Appointment = null, WorldId? Partner = null, SeekChoiceRecord? Seeking = null, IReadOnlyDictionary<string, bool>? Answers = null,
	IReadOnlyDictionary<string, WorldId>? RememberedPlaces = null, IReadOnlyDictionary<string, WorldId>? Bundles = null, SequenceRole? Role = null, WorldId? Leader = null, SetOffState? SetOff = null,
	IReadOnlyDictionary<string, WorldId>? ShyAnswers = null, StepUnwindRecord? Unwind = null, string? KeptSince = null)
{
	/// <summary>
	/// Creates the sequence for a person from the record, failing to load if the need or its step isn't found. The world
	/// objects it refers to are set once every object has been created.
	/// </summary>
	internal StepSequence Load(WorldLoader loader, Func<string, Need> getNeed)
	{
		var need = getNeed(Need);
		if (Step < 0 || Step > need.Steps.Count)
			throw new SaveLoadException(OurResources.SaveStepInvalid.FormatCurrentCulture(Step, Need));

		if (SetOff is not (null or SetOffState.Gone) && Appointment is null)
			throw new SaveLoadException(OurResources.SaveSetOffWithoutAppointment.FormatCurrentCulture(Need));

		var sequence = new StepSequence(need, Step, MinutesLeft, Role ?? SequenceRole.Steps, SetOff ?? SetOffState.Gone)
		{
			IsCarriedOut = IsCarriedOut ?? false,
			IsCut = IsCut ?? false,
			Until = Until is null ? null : loader.LoadTime(Until),
			Takeover = Takeover ?? StepTakeover.None,
			KeptSince = KeptSince is null ? null : loader.LoadTime(KeptSince),
		};

		// Leaving the steps carries out "finally" steps of the need's, on the way to one of its steps or its end.
		if (Unwind is { } unwind)
		{
			var starts = (IReadOnlyList<int>) [unwind.Running, .. unwind.Queue ?? []];
			if (starts.Any(x => need.GetBlockFinallyAt(x) is null) || unwind.To < 0 || unwind.To > need.Steps.Count)
				throw new SaveLoadException(OurResources.SaveUnwindInvalid.FormatCurrentCulture(Need));
			sequence.StartUnwinding(starts, unwind.To);
		}

		loader.Defer(() =>
		{
			sequence.Place = Place is { } place ? loader.Get<Location>(place) : null;
			sequence.Appointment = Appointment is { } appointment ? loader.Get<Appointment>(appointment) : null;
			sequence.Partner = Partner is { } partner ? loader.Get<Person>(partner) : null;
			sequence.Leader = Leader is { } leader ? loader.Get<Person>(leader) : null;
			sequence.Seeking = Seeking is { } seeking ? new SeekChoice(seeking.Friend is { } friend ? loader.Get<Person>(friend) : null, loader.Get<Location>(seeking.Place), seeking.Clue) : null;
			foreach (var (name, isYes) in Answers ?? new Dictionary<string, bool>())
				sequence.SetAnswer(name, isYes, ShyAnswers is not null && ShyAnswers.TryGetValue(name, out var shy) ? loader.Get<Person>(shy) : null);
			foreach (var (name, location) in RememberedPlaces ?? new Dictionary<string, WorldId>())
				sequence.RememberPlace(name, loader.Get<Location>(location));
			foreach (var (name, bundle) in Bundles ?? new Dictionary<string, WorldId>())
				sequence.AddBundle(name, loader.Get<Bundle>(bundle));
		});

		return sequence;
	}
}

/// <summary>
/// The saved form of a sequence leaving its steps: the first "finally" step of the list of steps whose "finally" steps
/// it's carrying out, those of the others it's leaving, and where it goes after them.
/// </summary>
public sealed record StepUnwindRecord(int Running, IReadOnlyList<int>? Queue, int To);
