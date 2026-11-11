using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// Something on the map that isn't a place to go, such as a river or a forest. Quarters that border it mention it in
/// their descriptions, with the direction it's in.
/// </summary>
public sealed class MapFeature : IIdentified, IHasFrequency
{
	/// <summary>
	/// The name of the placeholder in a feature's text that's replaced with the direction the feature is in.
	/// </summary>
	public const string DirectionPlaceholder = "direction";

	public required string Id { get; init; }

	/// <summary>
	/// The name shown on the map.
	/// </summary>
	public required string Title { get; init; }

	public required MapFeatureType Type { get; init; }

	/// <summary>
	/// How likely the feature is to be in a town, compared to the other features.
	/// </summary>
	public FrequencyLevel Frequency { get; init; } = new(Utility.Random.Frequency.Common);

	/// <summary>
	/// For an area, its size on the map. It can be turned either way.
	/// </summary>
	public QuarterSize? Size { get; init; }

	/// <summary>
	/// For a river, what a path across it can be called, such as "bridge" or "ford". One is chosen for each crossing.
	/// </summary>
	public IReadOnlyList<string>? Crossings { get; init; }

	/// <summary>
	/// Sentences that can be added to the description of a quarter bordering the feature. Each is a template whose only
	/// placeholder is the direction the feature is in, such as "north-east".
	/// </summary>
	public required IReadOnlyList<string> Text { get; init; }
}

/// <summary>
/// The kinds of map feature, which the engine places differently.
/// </summary>
public enum MapFeatureType
{
	/// <summary>
	/// A channel running straight across the whole map. Quarters can't sit on it, but paths can cross it.
	/// </summary>
	River,

	/// <summary>
	/// A rectangle placed in empty space next to the quarters that border it, such as a forest or a pond.
	/// </summary>
	Area,
}
