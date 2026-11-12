using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// The people who live together in one house.
/// </summary>
public sealed class Family : NotifyPropertyChangedBase, IWorldObject
{
	internal Family(WorldId id, string lastName, IReadOnlyList<Person> members, Location home, WealthLevel wealthLevel)
	{
		if (members.Count == 0)
			throw new ArgumentException("A family must have at least one member.", nameof(members));

		Id = id;
		LastName = lastName;
		Members = members;
		Home = home;
		WealthLevel = wealthLevel;

		foreach (var member in members)
			member.SetFamily(this);
		home.Family = this;
	}

	public WorldId Id { get; }

	/// <summary>
	/// The last name of the head of the family.
	/// </summary>
	public string LastName { get; }

	/// <summary>
	/// The members of the family, starting with the head.
	/// </summary>
	public IReadOnlyList<Person> Members { get; }

	public Person Head => Members[0];

	/// <summary>
	/// The house the family lives in.
	/// </summary>
	public Location Home { get; }

	public WealthLevel WealthLevel { get; }

	public WorldObjectRecord CreateRecord() => new FamilyRecord(Id, LastName, Members.Select(x => x.Id).ToList(), Home.Id, WealthLevel);
}

/// <summary>
/// The saved form of a family: its last name, its members starting with the head, its house, and its wealth level.
/// </summary>
public sealed record FamilyRecord(WorldId Id, string LastName, IReadOnlyList<WorldId> Members, WorldId Home, WealthLevel WealthLevel) : WorldObjectRecord(Id)
{
	internal override IWorldObject Create(WorldLoader loader) =>
		Members.Count == 0
			? throw new SaveLoadException(OurResources.SaveFamilyEmpty.FormatCurrentCulture(Id))
			: new Family(Id, LastName, Members.Select(loader.Get<Person>).ToList(), loader.Get<Location>(Home), WealthLevel);
}
