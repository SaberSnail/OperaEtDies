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
	/// What to tell the player to do next: the text of the next step that goes somewhere, or null if there's none.
	/// </summary>
	public string? NextText => Need.Steps.Skip(m_step).FirstOrDefault(x => x.Kind == StepKind.GoTo && x.Player?.Text is not null)?.Player!.Text!.Text;

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
	/// somewhere), or null on the way somewhere. Leaving it partway through fails the step they're on.
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

	internal void SetAnswer(string name, bool isYes) => m_answers[name] = isYes;

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
			Role == SequenceRole.Steps ? null : Role, m_leader?.Id, m_setOff == SetOffState.Gone ? null : m_setOff);

	private readonly Dictionary<string, bool> m_answers = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Location> m_rememberedPlaces = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Bundle> m_bundles = new(StringComparer.Ordinal);
	private int m_step;
	private int? m_minutesLeft;
	private StepTakeover m_takeover;
	private Person? m_partner;
	private Person? m_leader;
	private SeekChoice? m_seeking;
	private SetOffState m_setOff;
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
	IReadOnlyDictionary<string, WorldId>? RememberedPlaces = null, IReadOnlyDictionary<string, WorldId>? Bundles = null, SequenceRole? Role = null, WorldId? Leader = null, SetOffState? SetOff = null)
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
		};

		loader.Defer(() =>
		{
			sequence.Place = Place is { } place ? loader.Get<Location>(place) : null;
			sequence.Appointment = Appointment is { } appointment ? loader.Get<Appointment>(appointment) : null;
			sequence.Partner = Partner is { } partner ? loader.Get<Person>(partner) : null;
			sequence.Leader = Leader is { } leader ? loader.Get<Person>(leader) : null;
			sequence.Seeking = Seeking is { } seeking ? new SeekChoice(seeking.Friend is { } friend ? loader.Get<Person>(friend) : null, loader.Get<Location>(seeking.Place), seeking.Clue) : null;
			foreach (var (name, isYes) in Answers ?? new Dictionary<string, bool>())
				sequence.SetAnswer(name, isYes);
			foreach (var (name, location) in RememberedPlaces ?? new Dictionary<string, WorldId>())
				sequence.RememberPlace(name, loader.Get<Location>(location));
			foreach (var (name, bundle) in Bundles ?? new Dictionary<string, WorldId>())
				sequence.AddBundle(name, loader.Get<Bundle>(bundle));
		});

		return sequence;
	}
}
