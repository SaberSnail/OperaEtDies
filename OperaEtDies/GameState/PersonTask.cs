using System.Text.Json.Serialization;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// One step a person takes toward their goal, such as walking a path or spending time at work.
/// </summary>
public abstract record PersonTask
{
	/// <summary>
	/// For a task that was interrupted, the minutes it had left, which it takes when it starts again, or null if it wasn't
	/// interrupted.
	/// </summary>
	public int? ResumeMinutes { get; init; }

	internal abstract PersonTaskRecord CreateRecord();
}

/// <summary>
/// Walks to a location next to the person's.
/// </summary>
public sealed record TravelTask(Location Destination) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new TravelTaskRecord(Destination.Id) { ResumeMinutes = ResumeMinutes };
}

/// <summary>
/// Spends time where the person is, meeting their goal's need. Without a number of minutes, it lasts until the goal's
/// level need is fully met.
/// </summary>
public sealed record BusyTask(int? Minutes) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new BusyTaskRecord(Minutes) { ResumeMinutes = ResumeMinutes };
}

/// <summary>
/// Starts or stops working, which takes no time. Changing into or out of work clothes, if given, is part of starting or
/// stopping work.
/// </summary>
public sealed record AtWorkTask(bool IsAtWork, OwnedOutfit? Clothes = null) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new AtWorkTaskRecord(IsAtWork, Clothes?.Id);
}

/// <summary>
/// Picks up the pieces of the person's outfit kept at home to carry them, such as their work clothes on the way to work,
/// which takes no time.
/// </summary>
public sealed record FetchClothesTask(OwnedOutfit Outfit) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new FetchClothesTaskRecord(Outfit.Id);
}

/// <summary>
/// Changes into an outfit, or out of it, back into the rest of what the person was wearing, such as out of work clothes
/// still worn after work. The change is made when the task ends.
/// </summary>
public sealed record ChangeClothesTask(OwnedOutfit Outfit, bool IsTakingOff) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new ChangeClothesTaskRecord(Outfit.Id, IsTakingOff) { ResumeMinutes = ResumeMinutes };
}

/// <summary>
/// Talks with someone, which interrupts whatever the person was doing until it's over, meeting the given level need while
/// it lasts, if there is one, such as when socializing with a friend.
/// </summary>
public sealed record TalkTask(Person With, int Minutes, Need? Need = null) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new TalkTaskRecord(With.Id, Minutes, Need?.Id) { ResumeMinutes = ResumeMinutes };
}

/// <summary>
/// Waits where the person is for someone who called out to them, which interrupts whatever the person was doing until
/// it's over.
/// </summary>
public sealed record WaitTask(Person For, int Minutes) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new WaitTaskRecord(For.Id, Minutes) { ResumeMinutes = ResumeMinutes };
}

/// <summary>
/// The saved form of a task. Each kind of task has its own record, listed here so save files can say which kind each
/// record is.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TravelTaskRecord), "travel")]
[JsonDerivedType(typeof(BusyTaskRecord), "busy")]
[JsonDerivedType(typeof(AtWorkTaskRecord), "atWork")]
[JsonDerivedType(typeof(TalkTaskRecord), "talk")]
[JsonDerivedType(typeof(WaitTaskRecord), "wait")]
[JsonDerivedType(typeof(FetchClothesTaskRecord), "fetchClothes")]
[JsonDerivedType(typeof(ChangeClothesTaskRecord), "changeClothes")]
public abstract record PersonTaskRecord
{
	/// <summary>
	/// For a task that was interrupted, the minutes it had left.
	/// </summary>
	public int? ResumeMinutes { get; init; }

	internal abstract PersonTask Create(WorldLoader loader);
}

public sealed record TravelTaskRecord(WorldId Destination) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new TravelTask(loader.Get<Location>(Destination)) { ResumeMinutes = ResumeMinutes };
}

public sealed record BusyTaskRecord(int? Minutes) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new BusyTask(Minutes) { ResumeMinutes = ResumeMinutes };
}

public sealed record AtWorkTaskRecord(bool IsAtWork, WorldId? Clothes = null) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new AtWorkTask(IsAtWork, Clothes is { } clothes ? loader.Get<OwnedOutfit>(clothes) : null);
}

public sealed record TalkTaskRecord(WorldId With, int Minutes, string? Need = null) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) =>
		new TalkTask(loader.Get<Person>(With), Minutes, Need is null ? null : loader.GameData.Schedules.Needs.FirstOrDefault(x => x.Id == Need && x.Kind == NeedKind.Level)
			?? throw new SaveLoadException(OurResources.SaveDataNotFound.FormatCurrentCulture("level need", Need))) { ResumeMinutes = ResumeMinutes };
}

public sealed record WaitTaskRecord(WorldId For, int Minutes) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new WaitTask(loader.Get<Person>(For), Minutes) { ResumeMinutes = ResumeMinutes };
}

public sealed record FetchClothesTaskRecord(WorldId Outfit) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new FetchClothesTask(loader.Get<OwnedOutfit>(Outfit));
}

public sealed record ChangeClothesTaskRecord(WorldId Outfit, bool IsTakingOff) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new ChangeClothesTask(loader.Get<OwnedOutfit>(Outfit), IsTakingOff) { ResumeMinutes = ResumeMinutes };
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
