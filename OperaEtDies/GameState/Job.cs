using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// Work someone took on for someone else, when an "ask" step with a job was answered yes, such as thatching their roof:
/// so many minutes of work in all, at a place (the job site), for one of the worker's needs. While it isn't done, the
/// worker's "goTo" steps to the job site go there, and the time they spend there on steps that meet the need's quota
/// counts toward it. Once it's done, it's taken out of the world.
/// </summary>
public sealed class Job : NotifyPropertyChangedBase, IWorldObject
{
	internal Job(WorldId id, Person worker, Person client, Need need, Location place, int minutes)
	{
		Id = id;
		Worker = worker;
		Client = client;
		Need = need;
		Place = place;
		Minutes = minutes;
	}

	public WorldId Id { get; }

	/// <summary>
	/// Who does the work.
	/// </summary>
	public Person Worker { get; }

	/// <summary>
	/// Who it's done for.
	/// </summary>
	public Person Client { get; }

	/// <summary>
	/// The worker's need it's done for.
	/// </summary>
	public Need Need { get; }

	/// <summary>
	/// Where the work is done.
	/// </summary>
	public Location Place { get; }

	/// <summary>
	/// How many minutes the work takes in all.
	/// </summary>
	public int Minutes { get; }

	/// <summary>
	/// How many minutes of the work are done.
	/// </summary>
	public long MinutesDone
	{
		get => m_minutesDone;
		internal set => SetPropertyField(value, ref m_minutesDone);
	}

	/// <summary>
	/// Whether all of the work is done.
	/// </summary>
	public bool IsDone => m_minutesDone >= Minutes;

	public WorldObjectRecord CreateRecord() => new JobRecord(Id, Worker.Id, Client.Id, Need.Id, Place.Id, Minutes, m_minutesDone);

	private long m_minutesDone;
}

/// <summary>
/// The saved form of a job.
/// </summary>
public sealed record JobRecord(WorldId Id, WorldId Worker, WorldId Client, string Need, WorldId Place, int Minutes, long MinutesDone) : WorldObjectRecord(Id)
{
	internal override IWorldObject Create(WorldLoader loader)
	{
		var need = loader.GameData.Schedules.Needs.FirstOrDefault(x => x.Id == Need) ?? throw new SaveLoadException(OurResources.SaveDataNotFound.FormatCurrentCulture("need", Need));
		var worker = loader.Get<Person>(Worker);
		var client = loader.Get<Person>(Client);
		if (worker == client || Minutes < 1 || MinutesDone < 0)
			throw new SaveLoadException(OurResources.SaveJobInvalid.FormatCurrentCulture(Id));
		return new Job(Id, worker, client, need, loader.Get<Location>(Place), Minutes) { MinutesDone = MinutesDone };
	}
}
