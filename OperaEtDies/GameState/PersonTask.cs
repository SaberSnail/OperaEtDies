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
/// Walks to a location next to the person's. A retreat is part of heading home to cover up.
/// </summary>
public sealed record TravelTask(Location Destination, bool IsRetreat = false) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new TravelTaskRecord(Destination.Id, IsRetreat ? true : null) { ResumeMinutes = ResumeMinutes };
}

/// <summary>
/// Spends time where the person is, for the "do" step of their goal's steps with the given index, if any. Without a
/// number of minutes, it lasts until the goal's level need is fully met. Time aside, such as the player changing clothes,
/// meets no need.
/// </summary>
public sealed record BusyTask(int? Minutes, int? Step = null, bool IsAside = false) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new BusyTaskRecord(Minutes, Step, IsAside ? true : null) { ResumeMinutes = ResumeMinutes };
}

/// <summary>
/// Starts or stops working, which takes no time.
/// </summary>
public sealed record AtWorkTask(bool IsAtWork) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new AtWorkTaskRecord(IsAtWork);
}

/// <summary>
/// Changes into an outfit, or out of it, back into the rest of what the person was wearing, such as out of work clothes
/// still worn after work. The change is made when the task ends. Covering up, when someone finds themselves less covered
/// than they're comfortable with, is called off if they become comfortable before it ends.
/// </summary>
public sealed record ChangeClothesTask(OwnedOutfit Outfit, bool IsTakingOff, bool IsCoverUp = false) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new ChangeClothesTaskRecord(Outfit.Id, IsTakingOff, IsCoverUp ? true : null) { ResumeMinutes = ResumeMinutes };
}

/// <summary>
/// Waits where the person is, without spending the time on anything, until others leave so they can change clothes.
/// </summary>
public sealed record PrivacyTask(int Minutes) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new PrivacyTaskRecord(Minutes) { ResumeMinutes = ResumeMinutes };
}

/// <summary>
/// Talks with someone, which interrupts whatever the person was doing until it's over, meeting the given need while it
/// lasts, if there is one, such as socializing with a friend, or the quota of a "talk" step that meets it.
/// </summary>
public sealed record TalkTask(Person With, int Minutes, Need? Need = null) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new TalkTaskRecord(With.Id, Minutes, Need?.Id) { ResumeMinutes = ResumeMinutes };
}

/// <summary>
/// Waits for an appointment where it is, for a number of minutes: until it's time, or a while longer for the other person
/// to arrive. Someone waiting is too busy to stop and talk.
/// </summary>
public sealed record MeetTask(Appointment Appointment, int Minutes) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new MeetTaskRecord(Appointment.Id, Minutes) { ResumeMinutes = ResumeMinutes };
}

/// <summary>
/// Follows someone wherever they go, for at most a number of minutes, which interrupts whatever the person was doing
/// until they're let go. Someone following is too busy to stop and talk.
/// </summary>
public sealed record FollowTask(Person Leader, int Minutes) : PersonTask
{
	internal override PersonTaskRecord CreateRecord() => new FollowTaskRecord(Leader.Id, Minutes) { ResumeMinutes = ResumeMinutes };
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
[JsonDerivedType(typeof(ChangeClothesTaskRecord), "changeClothes")]
[JsonDerivedType(typeof(FollowTaskRecord), "follow")]
[JsonDerivedType(typeof(MeetTaskRecord), "meet")]
[JsonDerivedType(typeof(PrivacyTaskRecord), "privacy")]
public abstract record PersonTaskRecord
{
	/// <summary>
	/// For a task that was interrupted, the minutes it had left.
	/// </summary>
	public int? ResumeMinutes { get; init; }

	internal abstract PersonTask Create(WorldLoader loader);
}

public sealed record TravelTaskRecord(WorldId Destination, bool? IsRetreat = null) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new TravelTask(loader.Get<Location>(Destination), IsRetreat ?? false) { ResumeMinutes = ResumeMinutes };
}

public sealed record BusyTaskRecord(int? Minutes, int? Step = null, bool? IsAside = null) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new BusyTask(Minutes, Step, IsAside ?? false) { ResumeMinutes = ResumeMinutes };
}

public sealed record AtWorkTaskRecord(bool IsAtWork) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new AtWorkTask(IsAtWork);
}

public sealed record TalkTaskRecord(WorldId With, int Minutes, string? Need = null) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) =>
		new TalkTask(loader.Get<Person>(With), Minutes, Need is null ? null : loader.GameData.Schedules.Needs.FirstOrDefault(x => x.Id == Need)
			?? throw new SaveLoadException(OurResources.SaveDataNotFound.FormatCurrentCulture("need", Need))) { ResumeMinutes = ResumeMinutes };
}

public sealed record MeetTaskRecord(WorldId Appointment, int Minutes) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new MeetTask(loader.Get<Appointment>(Appointment), Minutes) { ResumeMinutes = ResumeMinutes };
}

public sealed record FollowTaskRecord(WorldId Leader, int Minutes) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new FollowTask(loader.Get<Person>(Leader), Minutes) { ResumeMinutes = ResumeMinutes };
}

public sealed record WaitTaskRecord(WorldId For, int Minutes) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new WaitTask(loader.Get<Person>(For), Minutes) { ResumeMinutes = ResumeMinutes };
}

public sealed record ChangeClothesTaskRecord(WorldId Outfit, bool IsTakingOff, bool? IsCoverUp = null) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new ChangeClothesTask(loader.Get<OwnedOutfit>(Outfit), IsTakingOff, IsCoverUp ?? false) { ResumeMinutes = ResumeMinutes };
}

public sealed record PrivacyTaskRecord(int Minutes) : PersonTaskRecord
{
	internal override PersonTask Create(WorldLoader loader) => new PrivacyTask(Minutes) { ResumeMinutes = ResumeMinutes };
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
