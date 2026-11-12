using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// A business, owned by one person and helped by members of their family.
/// </summary>
public sealed class Business : NotifyPropertyChangedBase, IWorldObject
{
	internal Business(WorldId id, Profession profession, Person owner, IReadOnlyList<Person> helpers, Location? location)
	{
		Id = id;
		Profession = profession;
		Owner = owner;
		Helpers = helpers;
		Location = location;

		owner.Business = this;
		foreach (var helper in helpers)
			helper.Business = this;
	}

	public WorldId Id { get; }

	public Profession Profession { get; }

	public Person Owner { get; }

	public IReadOnlyList<Person> Helpers { get; }

	/// <summary>
	/// The place the business is run from, or null if it's run from the owner's home.
	/// </summary>
	public Location? Location { get; }

	public WorldObjectRecord CreateRecord() => new BusinessRecord(Id, Profession.Id, Owner.Id, Helpers.Select(x => x.Id).ToList(), Location?.Id);
}

/// <summary>
/// The saved form of a business: its profession in the data, its owner and helpers, and where it's run from (or
/// nothing if it's run from home).
/// </summary>
public sealed record BusinessRecord(WorldId Id, string Profession, WorldId Owner, IReadOnlyList<WorldId> Helpers, WorldId? Location) : WorldObjectRecord(Id)
{
	internal override IWorldObject Create(WorldLoader loader) =>
		new Business(Id, WorldLoader.GetData(loader.GameData.Professions, Profession, "profession"), loader.Get<Person>(Owner),
			Helpers.Select(loader.Get<Person>).ToList(), Location is { } location ? loader.Get<Location>(location) : null);
}
