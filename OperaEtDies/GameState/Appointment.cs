using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// A time and place two people agreed to meet, made when one asked the other with an "ask" step that makes appointments.
/// Each has a sequence for it from when it's made, and sets off in time to get there. Once both have, the one who asked
/// (the worker) carries out the appointment's steps, while the other (the client) stays with them until they're done.
/// </summary>
public sealed class Appointment : NotifyPropertyChangedBase, IWorldObject
{
	internal Appointment(WorldId id, Person worker, Person client, Need need, int step, Location place, TimePoint time)
	{
		Id = id;
		Worker = worker;
		Client = client;
		Need = need;
		Step = step;
		Place = place;
		Time = time;
	}

	public WorldId Id { get; }

	/// <summary>
	/// The one who asked, who carries out the appointment's steps.
	/// </summary>
	public Person Worker { get; }

	/// <summary>
	/// The one asked, who stays with the worker while they carry out the steps.
	/// </summary>
	public Person Client { get; }

	/// <summary>
	/// The need whose "ask" step made the appointment, which the worker's goal is while they keep it.
	/// </summary>
	public Need Need { get; }

	/// <summary>
	/// The index of the "ask" step in the need's steps.
	/// </summary>
	public int Step { get; }

	public Location Place { get; }

	/// <summary>
	/// When the two meet.
	/// </summary>
	public TimePoint Time { get; }

	/// <summary>
	/// Whether both have arrived, and the worker is carrying out its steps.
	/// </summary>
	public bool IsStarted { get; internal set; }

	/// <summary>
	/// Who last said no to starting it, while both are still there, or null if no one has since either of them last came.
	/// </summary>
	public Person? DeclinedBy { get; private set; }

	/// <summary>
	/// When <see cref="DeclinedBy"/> said no.
	/// </summary>
	public TimePoint? DeclinedAt { get; private set; }

	/// <summary>
	/// Remembers that someone said no to starting it.
	/// </summary>
	internal void Decline(Person person, TimePoint time) => (DeclinedBy, DeclinedAt) = (person, time);

	/// <summary>
	/// Forgets who said no to starting it, once either of them leaves.
	/// </summary>
	internal void ClearDeclined() => (DeclinedBy, DeclinedAt) = (null, null);

	/// <summary>
	/// Gets the other person keeping the appointment.
	/// </summary>
	public Person GetOther(Person person) => person == Worker ? Client : Worker;

	/// <summary>
	/// Gets someone's sequence for it: the worker's steps, or the client's part in them, which each has from when it's made
	/// until it's kept or called off. Returns null if they have none.
	/// </summary>
	public StepSequence? GetSequence(Person person) => person.Sequences.FirstOrDefault(x => x.Appointment == this);

	/// <summary>
	/// Whether someone has set off for it.
	/// </summary>
	public bool IsOnTheWay(Person person) => GetSequence(person) is { HasSetOff: true };

	public WorldObjectRecord CreateRecord() => new AppointmentRecord(Id, Worker.Id, Client.Id, Need.Id, Step, Place.Id, Time.SaveToString(), IsStarted, DeclinedBy?.Id, DeclinedAt?.SaveToString());
}

/// <summary>
/// The saved form of an appointment: who keeps it, the need and "ask" step that made it, where and when, whether it's
/// started, and who last said no to starting it, and when. Whether each has set off is in their sequence for it.
/// </summary>
public sealed record AppointmentRecord(WorldId Id, WorldId Worker, WorldId Client, string Need, int Step, WorldId Place, string Time, bool IsStarted = false,
	WorldId? DeclinedBy = null, string? DeclinedAt = null) : WorldObjectRecord(Id)
{
	internal override IWorldObject Create(WorldLoader loader)
	{
		var need = loader.GameData.Schedules.Needs.FirstOrDefault(x => x.Id == Need) ?? throw new SaveLoadException(OurResources.SaveDataNotFound.FormatCurrentCulture("need", Need));
		var worker = loader.Get<Person>(Worker);
		var client = loader.Get<Person>(Client);
		if (worker == client || need.GetAppointmentSteps(Step) is null)
			throw new SaveLoadException(OurResources.SaveAppointmentInvalid.FormatCurrentCulture(Id, Step));
		var appointment = new Appointment(Id, worker, client, need, Step, loader.Get<Location>(Place), loader.LoadTime(Time))
		{
			IsStarted = IsStarted,
		};
		if (DeclinedBy is { } declinedBy)
		{
			var by = loader.Get<Person>(declinedBy);
			if ((by != worker && by != client) || DeclinedAt is null)
				throw new SaveLoadException(OurResources.SaveAppointmentInvalid.FormatCurrentCulture(Id, Step));
			appointment.Decline(by, loader.LoadTime(DeclinedAt));
		}

		return appointment;
	}
}
