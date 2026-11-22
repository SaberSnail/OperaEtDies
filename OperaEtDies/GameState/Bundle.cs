using GoldenAnvil.Utility;

namespace OperaEtDies.GameState;

/// <summary>
/// Something a step has a character carry until a later step puts it down, such as the household's washing or a bucket
/// of water, like an errand's token. It's carried in the character's inventory, and only its step can put it down. The
/// items it stands for, if any (such as the pieces of the washing), are carried along with it.
/// </summary>
public sealed class Bundle : Item
{
	internal Bundle(WorldId id, Person owner, string title, IEnumerable<Item> items)
		: base(id, owner)
	{
		m_title = title;
		m_items = [.. items];
	}

	public override string Title => m_title;

	public override string Description => m_items.Count == 0 ? m_title : OurResources.BundleDescription.FormatCurrentCulture(m_title, m_items.Count);

	public override string LongDescription => Description;

	/// <summary>
	/// The items the bundle stands for, which are carried with it.
	/// </summary>
	public IReadOnlyList<Item> Items => m_items;

	/// <summary>
	/// Stops standing for an item, such as a piece of clothing taken out to be worn.
	/// </summary>
	internal void Remove(Item item) => m_items.Remove(item);

	public override WorldObjectRecord CreateRecord() => new BundleRecord(Id, Owner!.Id, m_title, m_items.Count == 0 ? null : m_items.Select(x => x.Id).ToList());

	private readonly string m_title;
	private readonly System.Collections.ObjectModel.Collection<Item> m_items;
}

public sealed record BundleRecord(WorldId Id, WorldId Owner, string Title, IReadOnlyList<WorldId>? Items = null) : WorldObjectRecord(Id)
{
	internal override IWorldObject Create(WorldLoader loader) =>
		new Bundle(Id, loader.Get<Person>(Owner), Title, (Items ?? []).Select(loader.Get<Item>));
}
