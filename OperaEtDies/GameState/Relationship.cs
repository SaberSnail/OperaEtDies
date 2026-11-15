using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// How two characters feel about each other, which is the same both ways, and when they last talked.
/// </summary>
public sealed class Relationship : NotifyPropertyChangedBase, IWorldObject
{
	internal Relationship(WorldId id, Person first, Person second, int score, TimePoint? lastTalked = null, TimePoint? lastDeclined = null)
	{
		if (first == second)
			throw new ArgumentException("A person can't have a relationship with themselves.", nameof(second));

		Id = id;
		First = first;
		Second = second;
		m_score = score;
		m_lastTalked = lastTalked;
		m_lastDeclined = lastDeclined;

		first.AddRelationship(this);
		second.AddRelationship(this);
	}

	public WorldId Id { get; }

	public Person First { get; }

	public Person Second { get; }

	/// <summary>
	/// How well the two get along, from 0 to <see cref="RelationshipSettings.MaximumScore"/>.
	/// </summary>
	public int Score
	{
		get => m_score;
		set => SetPropertyField(value, ref m_score);
	}

	/// <summary>
	/// When the two last talked, or null if they haven't since the game started.
	/// </summary>
	public TimePoint? LastTalked
	{
		get => m_lastTalked;
		set => SetPropertyField(value, ref m_lastTalked);
	}

	/// <summary>
	/// When the player last turned down the other person's request to chat, or null if they haven't.
	/// </summary>
	public TimePoint? LastDeclined
	{
		get => m_lastDeclined;
		set => SetPropertyField(value, ref m_lastDeclined);
	}

	/// <summary>
	/// Gets the other person in the relationship.
	/// </summary>
	public Person GetOther(Person person) =>
		person == First ? Second : person == Second ? First : throw new ArgumentException($"Person {person.Id} isn't in relationship {Id}.", nameof(person));

	public WorldObjectRecord CreateRecord() => new RelationshipRecord(Id, First.Id, Second.Id, Score, LastTalked?.SaveToString(), LastDeclined?.SaveToString());

	private int m_score;
	private TimePoint? m_lastTalked;
	private TimePoint? m_lastDeclined;
}

/// <summary>
/// The saved form of a relationship: the two people, their score, when they last talked, if they have, and when the player
/// last turned down a request to chat from the other, if they have.
/// </summary>
public sealed record RelationshipRecord(WorldId Id, WorldId First, WorldId Second, int Score, string? LastTalked, string? LastDeclined = null) : WorldObjectRecord(Id)
{
	internal override IWorldObject Create(WorldLoader loader)
	{
		var first = loader.Get<Person>(First);
		var second = loader.Get<Person>(Second);
		if (first == second || first.Relationships.ContainsKey(second) || Score < 0 || Score > RelationshipSettings.MaximumScore)
			throw new SaveLoadException(OurResources.SaveRelationshipInvalid.FormatCurrentCulture(Id));
		return new Relationship(Id, first, second, Score, LastTalked is { } lastTalked ? loader.LoadTime(lastTalked) : null,
			LastDeclined is { } lastDeclined ? loader.LoadTime(lastDeclined) : null);
	}
}
