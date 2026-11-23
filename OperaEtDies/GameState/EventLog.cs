using System.Collections.ObjectModel;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// What the player has seen happen: everything that happened to anyone in the player's location while they were there,
/// including the player themselves, oldest first. Only the most recent <see cref="MaxEntries"/> are kept.
/// </summary>
public sealed class EventLog : NotifyPropertyChangedBase, IWorldObject
{
	internal EventLog(WorldId id, IEnumerable<LogEntry> entries)
	{
		Id = id;
		m_entries = [.. entries];
		Entries = new ReadOnlyObservableCollection<LogEntry>(m_entries);
	}

	/// <summary>
	/// The most entries kept. Adding another drops the oldest.
	/// </summary>
	public const int MaxEntries = 500;

	public WorldId Id { get; }

	/// <summary>
	/// The entries, oldest first.
	/// </summary>
	public ReadOnlyObservableCollection<LogEntry> Entries { get; }

	/// <summary>
	/// Adds an entry, dropping the oldest once there are too many.
	/// </summary>
	internal void Add(LogEntry entry)
	{
		m_entries.Add(entry);
		while (m_entries.Count > MaxEntries)
			m_entries.RemoveAt(0);
	}

	/// <summary>
	/// Gets the latest entry about someone (as the one it happened to), or null if there's none.
	/// </summary>
	internal LogEntry? GetLatest(Person person)
	{
		for (var index = m_entries.Count - 1; index >= 0; index--)
		{
			if (m_entries[index].Person == person)
				return m_entries[index];
		}

		return null;
	}

	public WorldObjectRecord CreateRecord() => new EventLogRecord(Id, m_entries.Select(x => x.CreateRecord()).ToList());

	private readonly ObservableCollection<LogEntry> m_entries;
}

/// <summary>
/// Something the player saw happen. What else it holds depends on its kind. Its text is built from these when it's shown.
/// </summary>
/// <param name="Time">When it happened.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Person">Who it happened to.</param>
/// <param name="Location">Where it happened.</param>
/// <param name="Other">Anyone else it involved, such as who they talked with, waited for, or followed.</param>
/// <param name="Place">For <see cref="LogEntryKind.Arrived"/>, where they came from, and for <see cref="LogEntryKind.Left"/>,
/// where they went.</param>
/// <param name="Need">For <see cref="LogEntryKind.Busy"/>, the need they spent time on, and for
/// <see cref="LogEntryKind.Failed"/>, the need whose step failed.</param>
/// <param name="Step">The index of the step in <paramref name="Need"/>'s steps: the "do" step they spent time on, or the
/// step that failed.</param>
/// <param name="Cause">For <see cref="LogEntryKind.Failed"/>, why the step failed.</param>
public sealed record LogEntry(TimePoint Time, LogEntryKind Kind, Person Person, Location Location, Person? Other = null, Location? Place = null, Need? Need = null, int? Step = null,
	FailCause? Cause = null)
{
	internal LogEntryRecord CreateRecord() => new(Time.SaveToString(), Kind, Person.Id, Location.Id, Other?.Id, Place?.Id, Need?.Id, Step, Cause);
}

/// <summary>
/// What happened, for a <see cref="LogEntry"/>.
/// </summary>
public enum LogEntryKind
{
	/// <summary>
	/// Someone arrived, from <see cref="LogEntry.Place"/>. For the player, they arrived at <see cref="LogEntry.Location"/>.
	/// </summary>
	Arrived,

	/// <summary>
	/// Someone left, for <see cref="LogEntry.Place"/>.
	/// </summary>
	Left,

	/// <summary>
	/// Someone started spending time on a need.
	/// </summary>
	Busy,

	/// <summary>
	/// Someone started being idle, with nothing to do.
	/// </summary>
	Idle,

	/// <summary>
	/// Someone started changing clothes.
	/// </summary>
	ChangingClothes,

	/// <summary>
	/// Someone started talking with <see cref="LogEntry.Other"/>.
	/// </summary>
	Talking,

	/// <summary>
	/// Someone started waiting for <see cref="LogEntry.Other"/>, who they called out to or asked something.
	/// </summary>
	Waiting,

	/// <summary>
	/// Someone started waiting to meet <see cref="LogEntry.Other"/> for an appointment.
	/// </summary>
	Meeting,

	/// <summary>
	/// Someone started following <see cref="LogEntry.Other"/>.
	/// </summary>
	Following,

	/// <summary>
	/// A step of someone's failed.
	/// </summary>
	Failed,

	/// <summary>
	/// Someone the player was to meet gave up waiting for them.
	/// </summary>
	GaveUpWaiting,

	/// <summary>
	/// Someone the player was to meet never came.
	/// </summary>
	NeverCame,
}

/// <summary>
/// The saved form of the event log.
/// </summary>
public sealed record EventLogRecord(WorldId Id, IReadOnlyList<LogEntryRecord> Entries) : WorldObjectRecord(Id)
{
	internal override IWorldObject Create(WorldLoader loader) => new EventLog(Id, (Entries ?? []).Select(x => x.Load(loader, Id)).ToList());
}

/// <summary>
/// The saved form of an entry in the event log.
/// </summary>
public sealed record LogEntryRecord(string Time, LogEntryKind Kind, WorldId Person, WorldId Location, WorldId? Other = null, WorldId? Place = null, string? Need = null, int? Step = null,
	FailCause? Cause = null)
{
	internal LogEntry Load(WorldLoader loader, WorldId log)
	{
		var need = Need is null ? null : loader.GameData.Schedules.Needs.FirstOrDefault(x => x.Id == Need) ?? throw new SaveLoadException(OurResources.SaveDataNotFound.FormatCurrentCulture("need", Need));
		if (Step is { } step && (need is null || step < 0 || step >= need.Steps.Count))
			throw new SaveLoadException(OurResources.SaveLogEntryInvalid.FormatCurrentCulture(log, Time));
		return new LogEntry(loader.LoadTime(Time), Kind, loader.Get<Person>(Person), loader.Get<Location>(Location), Other is { } other ? loader.Get<Person>(other) : null,
			Place is { } place ? loader.Get<Location>(place) : null, need, Step, Cause);
	}
}
