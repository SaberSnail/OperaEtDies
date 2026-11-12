using System.Collections.ObjectModel;
using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// Creates the world objects from a save's records. An object is created the first time something refers to it, so
/// records can be in any order. References that go in a circle, such as the exits between locations, are set once
/// every object exists, by deferring them.
/// </summary>
public sealed class WorldLoader
{
	internal WorldLoader(GameData gameData, Random random, IReadOnlyList<WorldObjectRecord> records)
	{
		GameData = gameData;
		Random = random;
		m_recordList = records;
		m_records = [];
		foreach (var record in records)
		{
			if (!record.Id.IsValid || !m_records.TryAdd(record.Id, record))
				throw new SaveLoadException(OurResources.SaveObjectDuplicate.FormatCurrentCulture(record.Id));
		}

		m_objects = [];
		m_creating = [];
		m_deferred = [];
	}

	public GameData GameData { get; }

	/// <summary>
	/// The random numbers for the loaded game.
	/// </summary>
	public Random Random { get; }

	/// <summary>
	/// Gets the object with the given id, creating it from its record if it hasn't been created yet.
	/// </summary>
	public T Get<T>(WorldId id)
		where T : class, IWorldObject
	{
		if (!m_objects.TryGetValue(id, out var item))
		{
			if (!m_records.TryGetValue(id, out var record))
				throw new SaveLoadException(OurResources.SaveObjectNotFound.FormatCurrentCulture(id));
			if (!m_creating.Add(id))
				throw new SaveLoadException(OurResources.SaveObjectCycle.FormatCurrentCulture(id));

			item = record.Create(this);
			if (item.Id != id)
				throw new InvalidOperationException($"The record for {id} created an object with id {item.Id}.");

			m_creating.Remove(id);
			m_objects.Add(id, item);
		}

		return item as T ?? throw new SaveLoadException(OurResources.SaveObjectWrongKind.FormatCurrentCulture(id, item.GetType().Name, typeof(T).Name));
	}

	/// <summary>
	/// Gets an item from the game data by id, failing to load if there's no such item.
	/// </summary>
	public static T GetData<T>(DataCatalog<T> catalog, string id, string kind)
		where T : class, IIdentified =>
		catalog.Find(id) ?? throw new SaveLoadException(OurResources.SaveDataNotFound.FormatCurrentCulture(kind, id));

	/// <summary>
	/// Runs an action once every object has been created, for references that go in a circle.
	/// </summary>
	public void Defer(Action action) => m_deferred.Add(action);

	/// <summary>
	/// Creates every object, then runs the deferred actions. The objects are returned in the order of their records.
	/// </summary>
	internal IReadOnlyList<IWorldObject> CreateAll()
	{
		var objects = m_recordList.Select(x => Get<IWorldObject>(x.Id)).ToList();
		foreach (var action in m_deferred)
			action();
		return objects;
	}

	private readonly IReadOnlyList<WorldObjectRecord> m_recordList;
	private readonly Dictionary<WorldId, WorldObjectRecord> m_records;
	private readonly Dictionary<WorldId, IWorldObject> m_objects;
	private readonly HashSet<WorldId> m_creating;
	private readonly Collection<Action> m_deferred;
}

/// <summary>
/// A problem that stops a saved game from loading. The message explains the problem to the player.
/// </summary>
public sealed class SaveLoadException : Exception
{
	public SaveLoadException(string message)
		: base(message)
	{
	}

	public SaveLoadException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
