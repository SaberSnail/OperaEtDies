using System.IO;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// The locations that make up the map, loaded from Locations.json.
/// </summary>
public sealed class LocationList : IValidatedData
{
	public const string FileName = "Locations.json";

	public static DataLoadResult<LocationList> Load(string dataDirectory) =>
		JsonDataLoader.Load<LocationList>(Path.Combine(dataDirectory, FileName));

	/// <summary>
	/// The id of the location the player starts the game in.
	/// </summary>
	public required string StartingLocation { get; init; }

	public required IReadOnlyList<Location> Locations { get; init; }

	/// <summary>
	/// Gets the location with the given id, or null if there isn't one.
	/// </summary>
	public Location? GetLocation(string id) => LocationsById.GetValueOrDefault(id);

	/// <summary>
	/// Gets the locations that can be reached from the given location.
	/// </summary>
	public IReadOnlyList<Location> GetConnections(Location location) => location.Connections.Select(GetLocation).OfType<Location>().ToList();

	public IEnumerable<string> Validate()
	{
		foreach (var error in DataValidation.ValidateIdentifiedItems("locations", Locations, x => x.Id, x => x.Title))
			yield return error;

		// Items that are empty or missing an id are already reported above.
		var locations = Locations.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();
		var ids = locations.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);

		foreach (var location in locations)
		{
			if (string.IsNullOrWhiteSpace(location.ShortDescription))
				yield return OurResources.LocationDescriptionMissing.FormatCurrentCulture(location.Id, "shortDescription");
			if (string.IsNullOrWhiteSpace(location.MediumDescription))
				yield return OurResources.LocationDescriptionMissing.FormatCurrentCulture(location.Id, "mediumDescription");
			if (location.LongDescription.Count == 0)
				yield return OurResources.LocationDescriptionMissing.FormatCurrentCulture(location.Id, "longDescription");
			else if (location.LongDescription.Any(string.IsNullOrWhiteSpace))
				yield return OurResources.LocationDescriptionParagraphBlank.FormatCurrentCulture(location.Id);

			foreach (var connection in location.Connections)
			{
				if (string.IsNullOrWhiteSpace(connection))
					yield return OurResources.LocationConnectionBlank.FormatCurrentCulture(location.Id);
				else if (connection == location.Id)
					yield return OurResources.LocationConnectionToSelf.FormatCurrentCulture(location.Id);
				else if (!ids.Contains(connection))
					yield return OurResources.LocationConnectionUnknown.FormatCurrentCulture(location.Id, connection);
			}
		}

		if (string.IsNullOrWhiteSpace(StartingLocation))
			yield return OurResources.StartingLocationMissing;
		else if (!ids.Contains(StartingLocation))
			yield return OurResources.StartingLocationUnknown.FormatCurrentCulture(StartingLocation);
	}

	private Dictionary<string, Location> LocationsById => m_locationsById ??= Locations
		.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id))
		.DistinctBy(x => x.Id, StringComparer.Ordinal)
		.ToDictionary(x => x.Id, StringComparer.Ordinal);

	private Dictionary<string, Location>? m_locationsById;
}
