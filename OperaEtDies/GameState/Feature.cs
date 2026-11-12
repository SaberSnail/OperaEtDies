using GoldenAnvil.Utility;
using OperaEtDies.Data;
using OperaEtDies.Town;

namespace OperaEtDies.GameState;

/// <summary>
/// A map feature in the town, such as a river or a forest, and where it is on the map. A river runs across the whole
/// map, so its bounds only cover the part beside the quarters.
/// </summary>
public sealed class Feature : NotifyPropertyChangedBase, IWorldObject
{
	internal Feature(WorldId id, MapFeature template, MapRect bounds)
	{
		Id = id;
		Template = template;
		Bounds = bounds;
	}

	public WorldId Id { get; }

	/// <summary>
	/// The map feature from the data.
	/// </summary>
	public MapFeature Template { get; }

	public MapRect Bounds { get; }

	public string Title => Template.Title;

	public WorldObjectRecord CreateRecord() => new FeatureRecord(Id, Template.Id, Bounds);
}

/// <summary>
/// The saved form of a map feature: the id of its map feature in the data, and its bounds.
/// </summary>
public sealed record FeatureRecord(WorldId Id, string Template, MapRect Bounds) : WorldObjectRecord(Id)
{
	internal override IWorldObject Create(WorldLoader loader) =>
		new Feature(Id, WorldLoader.GetData(loader.GameData.MapFeatures, Template, "map feature"), Bounds);
}
