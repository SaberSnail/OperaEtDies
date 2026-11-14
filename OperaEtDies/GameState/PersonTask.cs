using System.Text.Json.Serialization;
using GoldenAnvil.Utility.Calendar;

namespace OperaEtDies.GameState;

/// <summary>
/// One step a person takes toward their goal, such as walking a path or spending time at work.
/// </summary>
public abstract record PersonTask
{
	internal abstract PersonTaskRecord CreateRecord();
}

/// <summary>
/// Walks to a location next to the person's.
/// </summary>
public sealed record TravelTask(Location Destination) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new TravelTaskRecord(Destination.Id);
}

/// <summary>
/// Spends time where the person is, meeting their goal's need. Without a number of minutes, it lasts until the goal's
/// level need is fully met.
/// </summary>
public sealed record BusyTask(int? Minutes) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new BusyTaskRecord(Minutes);
}

/// <summary>
/// Starts or stops working, which takes no time.
/// </summary>
public sealed record AtWorkTask(bool IsAtWork) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new AtWorkTaskRecord(IsAtWork);
}

/// <summary>
/// The saved form of a task. Each kind of task has its own record, listed here so save files can say which kind each
/// record is.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TravelTaskRecord), "travel")]
[JsonDerivedType(typeof(BusyTaskRecord), "busy")]
[JsonDerivedType(typeof(AtWorkTaskRecord), "atWork")]
public abstract record PersonTaskRecord
{
	internal abstract PersonTask Create(WorldLoader loader);
}

public sealed record TravelTaskRecord(WorldId Destination) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new TravelTask(loader.Get<Location>(Destination));
}

public sealed record BusyTaskRecord(int? Minutes) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new BusyTask(Minutes);
}

public sealed record AtWorkTaskRecord(bool IsAtWork) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new AtWorkTask(IsAtWork);
}

/// <summary>
/// Tells that a person started a task, and when.
/// </summary>
public sealed class PersonTaskEventArgs(Person person, PersonTask task, TimePoint time) : EventArgs
{
	public Person Person { get; } = person;

	public PersonTask Task { get; } = task;

	public TimePoint Time { get; } = time;
}
