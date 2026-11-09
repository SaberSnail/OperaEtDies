using System.Collections;

namespace OperaEtDies.Data;

/// <summary>
/// The read-only items of one kind from the data files, in the order they were defined, which can also be looked up by id.
/// Ids are expected to be unique (validation reports duplicates when loading). If they aren't, the first item with an id is found.
/// </summary>
public sealed class DataCatalog<T> : IReadOnlyList<T>
	where T : class, IIdentified
{
	public DataCatalog(IEnumerable<T> items)
	{
		m_items = items.ToList();
		m_itemsById = new Dictionary<string, T>(StringComparer.Ordinal);
		foreach (var item in m_items)
			m_itemsById.TryAdd(item.Id, item);
	}

	public int Count => m_items.Count;

	public T this[int index] => m_items[index];

	/// <summary>
	/// Gets the item with the given id, or null if there isn't one.
	/// </summary>
	public T? Find(string id) => m_itemsById.GetValueOrDefault(id);

	/// <summary>
	/// Gets the item with the given id. Use this for ids that were checked when the data was loaded.
	/// </summary>
	public T Get(string id) => Find(id) ?? throw new KeyNotFoundException($"There is no {typeof(T).Name} with id \"{id}\".");

	public IEnumerator<T> GetEnumerator() => m_items.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	private readonly IReadOnlyList<T> m_items;
	private readonly Dictionary<string, T> m_itemsById;
}
