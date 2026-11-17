using System.Collections.ObjectModel;
using GoldenAnvil.Utility;

namespace OperaEtDies.GameState;

/// <summary>
/// Items kept together, such as what a person is carrying or what a household keeps at home. It's part of the world
/// object that holds it, which saves the ids of its items.
/// </summary>
public sealed class ItemContainer
{
	internal ItemContainer()
	{
		m_items = [];
		Items = new ReadOnlyObservableCollection<Item>(m_items);
	}

	/// <summary>
	/// The items in the container, in the order they were put in.
	/// </summary>
	public ReadOnlyObservableCollection<Item> Items { get; }

	/// <summary>
	/// The pieces of clothing in the container.
	/// </summary>
	public IEnumerable<ClothingPiece> Clothing => m_items.OfType<ClothingPiece>();

	/// <summary>
	/// Gets the items in the container that belong to a person.
	/// </summary>
	public IEnumerable<Item> GetOwnedBy(Person person) => m_items.Where(x => x.Owner == person);

	/// <summary>
	/// Puts an item in the container. It can't already be somewhere else.
	/// </summary>
	internal void Add(Item item)
	{
		if (item.IsPlaced)
			throw new InvalidOperationException($"Item {item.Id} is already somewhere else.");

		m_items.Add(item);
		item.Container = this;
	}

	/// <summary>
	/// Takes an item out of the container.
	/// </summary>
	internal void Remove(Item item)
	{
		if (item.Container != this || !m_items.Remove(item))
			throw new InvalidOperationException($"Item {item.Id} isn't in this container.");

		item.Container = null;
	}

	/// <summary>
	/// Gets the ids of the items for saving, or null if the container is empty.
	/// </summary>
	internal IReadOnlyList<WorldId>? CreateRecord() => m_items.Count == 0 ? null : m_items.Select(x => x.Id).ToList();

	/// <summary>
	/// Puts the items from a save in the container, failing to load if any of them is already somewhere else.
	/// </summary>
	internal void Load(IEnumerable<Item> items)
	{
		foreach (var item in items)
		{
			if (item.IsPlaced)
				throw new SaveLoadException(OurResources.SaveItemPlacedTwice.FormatCurrentCulture(item.Id));
			Add(item);
		}
	}

	private readonly ObservableCollection<Item> m_items;
}
