using System.Collections.ObjectModel;
using System.Collections.Specialized;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Controls.Locations;
using OperaEtDies.GameState;
using OperaEtDies.Time;

namespace OperaEtDies.Controls.Log;

/// <summary>
/// Shows the event log, oldest first, kept up to date as entries are added. Each entry shows the time when it's different
/// from the entry before it, and the date when the day changes. The people and places in entries can be clicked, and the
/// log can be opened in a sheet.
/// </summary>
public sealed class EventLogViewModel : ViewModelBase
{
	public EventLogViewModel(EventLog log, GameCalendar calendar, Action<Person> examinePerson, Action<Location> showPlace, Action? openSheet = null)
	{
		m_log = log;
		m_calendar = calendar;
		m_examinePerson = examinePerson;
		m_showPlace = showPlace;
		m_openSheet = openSheet;
		m_entries = [];
		Entries = new ReadOnlyObservableCollection<LogEntryViewModel>(m_entries);
		Rebuild();

		// The log outlives the view model, so it's listened to weakly.
		CollectionChangedEventManager.AddHandler(log.Entries, OnEntriesChanged);
	}

	/// <summary>
	/// The entries, oldest first.
	/// </summary>
	public ReadOnlyObservableCollection<LogEntryViewModel> Entries { get; }

	/// <summary>
	/// Whether the log is collapsed down to its heading, giving the panel above it the room.
	/// </summary>
	public bool IsCollapsed
	{
		get => VerifyAccess(m_isCollapsed);
		set => SetPropertyField([nameof(IsCollapsed), nameof(CollapseToolTip)], value, ref m_isCollapsed);
	}

	/// <summary>
	/// The tooltip of the button that collapses the log, or opens it again.
	/// </summary>
	public string CollapseToolTip => VerifyAccess(m_isCollapsed ? OurResources.LogExpandToolTip : OurResources.LogCollapseToolTip);

	/// <summary>
	/// Collapses the log down to its heading, or opens it again.
	/// </summary>
	public void ToggleCollapsed()
	{
		VerifyAccess();
		IsCollapsed = !IsCollapsed;
	}

	/// <summary>
	/// Opens the log in a sheet, for reading at a larger size.
	/// </summary>
	public void OpenSheet()
	{
		VerifyAccess();
		m_openSheet?.Invoke();
	}

	private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex == m_entries.Count && e.NewItems is { Count: 1 } added)
		{
			m_entries.Add(CreateEntry((LogEntry) added[0]!, m_entries.Count == 0 ? null : m_entries[^1].Entry));
		}
		else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldStartingIndex == 0 && e.OldItems is { Count: 1 } && m_entries.Count != 0)
		{
			// The entry that's now first always shows its date and time.
			m_entries.RemoveAt(0);
			if (m_entries.Count != 0)
				m_entries[0] = CreateEntry(m_entries[0].Entry, null);
		}
		else
		{
			Rebuild();
		}
	}

	private void Rebuild()
	{
		m_entries.Clear();
		LogEntry? previous = null;
		foreach (var entry in m_log.Entries)
		{
			m_entries.Add(CreateEntry(entry, previous));
			previous = entry;
		}
	}

	private LogEntryViewModel CreateEntry(LogEntry entry, LogEntry? previous)
	{
		var day = previous is null || m_calendar.GetStartOfDay(previous.Time) != m_calendar.GetStartOfDay(entry.Time) ? m_calendar.FormatDate(entry.Time) : null;
		var clock = day is not null || previous!.Time != entry.Time ? m_calendar.FormatClock(entry.Time) : null;
		var parts = LogEntryText.Format(entry).Select(x => new LogTextPartViewModel(x.Text, x.Person is { } person ? () => m_examinePerson(person) : x.Place is { } place ? () => m_showPlace(place) : null, x.Person)).ToList();
		return new LogEntryViewModel(entry, day, day is not null && previous is not null, clock, parts);
	}

	private readonly EventLog m_log;
	private readonly GameCalendar m_calendar;
	private readonly Action<Person> m_examinePerson;
	private readonly Action<Location> m_showPlace;
	private readonly Action? m_openSheet;
	private readonly ObservableCollection<LogEntryViewModel> m_entries;
	private bool m_isCollapsed;
}

/// <summary>
/// An entry in the event log, with the date above it if the day changed, and the time beside it if it changed.
/// </summary>
public sealed class LogEntryViewModel(LogEntry entry, string? day, bool startsNewDay, string? clock, IReadOnlyList<LogTextPartViewModel> parts) : ViewModelBase
{
	public LogEntry Entry { get; } = entry;

	/// <summary>
	/// The date, such as "Aftday, Mid Winter, 227 CR", for the first entry of a day, or null.
	/// </summary>
	public string? Day { get; } = day;

	/// <summary>
	/// Whether this is the first entry of a day after entries from an earlier day, so a rule separates it from them. The
	/// first entry in the log shows its date, but has nothing before it to separate it from.
	/// </summary>
	public bool StartsNewDay { get; } = startsNewDay;

	/// <summary>
	/// The time on the clock, such as "6:05", when it's different from the entry before, or null.
	/// </summary>
	public string? Clock { get; } = clock;

	/// <summary>
	/// The text, in pieces, with the people and places in it able to be clicked.
	/// </summary>
	public IReadOnlyList<LogTextPartViewModel> Parts { get; } = parts;

	/// <summary>
	/// The text as plain text.
	/// </summary>
	public string Text => string.Concat(Parts.Select(x => x.Text));
}

/// <summary>
/// A piece of an entry's text: plain text, or a person or place, which does something when clicked. A person has the
/// tooltip a person's name has in the location.
/// </summary>
public sealed class LogTextPartViewModel(string text, Action? click, Person? person = null) : ViewModelBase
{
	public string Text { get; } = text;

	public bool IsLink => m_click is not null;

	/// <summary>
	/// The person the piece names, or null if it doesn't name one.
	/// </summary>
	public Person? Person { get; } = person;

	/// <summary>
	/// Gets what the person's tooltip shows, created the first time it's shown, since the log can name a great many people,
	/// or null if the piece doesn't name one.
	/// </summary>
	public LocationPersonViewModel? GetPersonToolTip()
	{
		VerifyAccess();
		if (m_personToolTip is null && Person is { } named)
		{
			var gameData = named.GameData;
			m_personToolTip = new LocationPersonViewModel(named, gameData.PortraitRenderer, gameData.Schedules, gameData.Relationships);
		}
		return m_personToolTip;
	}

	public void Click()
	{
		VerifyAccess();
		m_click?.Invoke();
	}

	private readonly Action? m_click = click;
	private LocationPersonViewModel? m_personToolTip;
}
