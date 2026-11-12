using GoldenAnvil.Utility;

namespace OperaEtDies.GameState;

/// <summary>
/// Every object in the game world, by id, in the order they were created. Objects are only created through the
/// registry, which hands out their ids, so everything in the world is saved.
/// </summary>
public sealed class WorldObjects
{
	public WorldObjects()
		: this(nextId: 1)
	{
	}

	private WorldObjects(int nextId)
	{
		m_objects = [];
		m_nextId = nextId;
	}

	/// <summary>
	/// Every object in the world, in the order they were created.
	/// </summary>
	public IEnumerable<IWorldObject> All => m_objects.Values;

	public int Count => m_objects.Count;

	/// <summary>
	/// Creates an object with a new id and adds it to the world.
	/// </summary>
	public T Create<T>(Func<WorldId, T> create)
		where T : IWorldObject
	{
		var id = new WorldId(m_nextId);
		var item = create(id);
		if (item.Id != id)
			throw new InvalidOperationException($"A world object was created with id {item.Id} instead of {id}.");

		m_nextId++;
		m_objects.Add(id, item);
		return item;
	}

	/// <summary>
	/// Gets the object with the given id, or null if there isn't one.
	/// </summary>
	public IWorldObject? Find(WorldId id) => m_objects.GetValueOrDefault(id);

	/// <summary>
	/// Gets the object with the given id, which must exist and be of the given kind.
	/// </summary>
	public T Get<T>(WorldId id)
		where T : IWorldObject =>
		Find(id) is T item ? item : throw new KeyNotFoundException($"There is no {typeof(T).Name} with id {id}.");

	/// <summary>
	/// Gets the objects of one kind, in the order they were created.
	/// </summary>
	public IEnumerable<T> OfType<T>()
		where T : IWorldObject => m_objects.Values.OfType<T>();

	/// <summary>
	/// The number the next id will have, for saving.
	/// </summary>
	internal int NextId => m_nextId;

	/// <summary>
	/// Creates the registry for a loaded game, from objects that already have their ids, in the order they were saved.
	/// </summary>
	internal static WorldObjects CreateLoaded(int nextId, IEnumerable<IWorldObject> objects)
	{
		var registry = new WorldObjects(nextId);
		foreach (var item in objects)
		{
			if (item.Id.Value >= nextId)
				throw new SaveLoadException(OurResources.SaveNextIdInvalid.FormatCurrentCulture(nextId, item.Id));
			if (!registry.m_objects.TryAdd(item.Id, item))
				throw new SaveLoadException(OurResources.SaveObjectDuplicate.FormatCurrentCulture(item.Id));
		}

		return registry;
	}

	private readonly OrderedDictionary<WorldId, IWorldObject> m_objects;
	private int m_nextId;
}
