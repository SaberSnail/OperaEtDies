using GoldenAnvil.Utility;

namespace OperaEtDies.GameState;

/// <summary>
/// Something a person can own, carry, and keep at home, such as a piece of clothing. An item is in at most one place at
/// a time: in an <see cref="ItemContainer"/>, or (for clothing) worn.
/// </summary>
public abstract class Item : NotifyPropertyChangedBase, IWorldObject
{
	protected Item(WorldId id, Person? owner)
	{
		Id = id;
		Owner = owner;
	}

	public WorldId Id { get; }

	/// <summary>
	/// The person the item belongs to, or null if it belongs to nobody.
	/// </summary>
	public Person? Owner { get; }

	/// <summary>
	/// A short name for the kind of item, such as "Kirtle".
	/// </summary>
	public abstract string Title { get; }

	/// <summary>
	/// A few words describing the item, as in "a russet kirtle".
	/// </summary>
	public abstract string Description { get; }

	/// <summary>
	/// What the item looks like, in a sentence or more.
	/// </summary>
	public abstract string LongDescription { get; }

	/// <summary>
	/// The container the item is in, or null if it isn't in one. Containers keep it up to date.
	/// </summary>
	public ItemContainer? Container
	{
		get => m_container;
		internal set => SetPropertyField(value, ref m_container);
	}

	/// <summary>
	/// Whether the item is somewhere that keeps it from being put somewhere else, such as a container.
	/// </summary>
	internal virtual bool IsPlaced => m_container is not null;

	public abstract WorldObjectRecord CreateRecord();

	private ItemContainer? m_container;
}
