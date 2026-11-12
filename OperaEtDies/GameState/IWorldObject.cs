using System.Text.Json.Serialization;

namespace OperaEtDies.GameState;

/// <summary>
/// An object in the game world. Every world object is created through <see cref="WorldObjects"/>, and is saved by
/// writing the record it creates.
/// </summary>
public interface IWorldObject
{
	WorldId Id { get; }

	/// <summary>
	/// Creates the record that saves this object: the source of truth for its state, with other world objects referred
	/// to by id and the data referred to by its ids.
	/// </summary>
	WorldObjectRecord CreateRecord();
}

/// <summary>
/// The saved form of a world object. Each kind of world object has its own record, listed here so save files can say
/// which kind each record is.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(FeatureRecord), "feature")]
[JsonDerivedType(typeof(LocationRecord), "location")]
[JsonDerivedType(typeof(PersonRecord), "person")]
[JsonDerivedType(typeof(FamilyRecord), "family")]
[JsonDerivedType(typeof(BusinessRecord), "business")]
public abstract record WorldObjectRecord(WorldId Id)
{
	/// <summary>
	/// Creates the world object from the record, getting the objects it refers to from the loader.
	/// </summary>
	internal abstract IWorldObject Create(WorldLoader loader);
}
