using System.Text.Json.Serialization;
using GoldenAnvil.Utility;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// A place that can be added to the town when a new game is created. Each template is meant to be used once, but can
/// be used again if the town runs out.
/// </summary>
public abstract class LocationTemplate : IIdentified, IHasFrequency
{
	public required string Id { get; init; }

	/// <summary>
	/// The name shown for the place. Houses don't have one, since they're named after the family that lives there.
	/// </summary>
	public string? Title { get; init; }

	/// <summary>
	/// The descriptions of the location.
	/// </summary>
	public required LocationDescription Description { get; init; }

	/// <summary>
	/// One sentence, used when the location is listed as a place to go.
	/// </summary>
	[JsonIgnore]
	public string ShortDescription => Description.Short.Text;

	/// <summary>
	/// One paragraph, shown when arriving at the location.
	/// </summary>
	[JsonIgnore]
	public string MediumDescription => Description.Medium.Text;

	/// <summary>
	/// Several paragraphs, shown when examining the location.
	/// </summary>
	[JsonIgnore]
	public IReadOnlyList<string> LongDescription => Description.Long.Select(x => x?.Text ?? "").ToList();

	/// <summary>
	/// How likely the template is to be chosen, compared to the other templates that could be used.
	/// </summary>
	public FrequencyLevel Frequency { get; init; } = new(Utility.Random.Frequency.Common);

	/// <summary>
	/// How likely someone is to come by, which decides how comfortable someone there alone is with how covered they are,
	/// or null to use the quarter kind's, or the modesty settings' for its kind of location.
	/// </summary>
	public FrequencyLevel? Traffic { get; init; }

	/// <summary>
	/// How much more (or, when negative, less) comfortable people there are with how covered they are, or null to use the
	/// quarter kind's.
	/// </summary>
	public int? ModestyComfort { get; init; }

	/// <summary>
	/// Checks that every description is filled in.
	/// </summary>
	internal IEnumerable<string> ValidateDescriptions()
	{
		if (string.IsNullOrWhiteSpace(ShortDescription))
			yield return OurResources.LocationDescriptionMissing.FormatCurrentCulture(Id, "description.short");
		if (string.IsNullOrWhiteSpace(MediumDescription))
			yield return OurResources.LocationDescriptionMissing.FormatCurrentCulture(Id, "description.medium");
		if (LongDescription.Count == 0)
			yield return OurResources.LocationDescriptionMissing.FormatCurrentCulture(Id, "description.long");
		else if (LongDescription.Any(string.IsNullOrWhiteSpace))
			yield return OurResources.LocationDescriptionParagraphBlank.FormatCurrentCulture(Id);
	}
}

/// <summary>
/// A group of houses and businesses, such as a lane, a square, or a cluster of farms, connected to other quarters. A
/// quarter with neither is a landmark, such as a churchyard or a common.
/// </summary>
public sealed class QuarterTemplate : LocationTemplate
{
	/// <summary>
	/// The kind of quarter, which decides which businesses can be in it.
	/// </summary>
	public required string Kind { get; init; }

	/// <summary>
	/// How many houses the quarter holds, and how wealthy the households living in them are. Optional; without it, nobody
	/// lives in the quarter.
	/// </summary>
	public QuarterHouses? Houses { get; init; }

	/// <summary>
	/// How wealthy the households living in the quarter are, or null if it has no houses.
	/// </summary>
	[JsonIgnore]
	public WealthLevel? Wealth => Houses?.Wealth;

	/// <summary>
	/// The most businesses the quarter holds.
	/// </summary>
	public int Businesses { get; init; }

	/// <summary>
	/// The quarter's size on the map. It can be turned either way.
	/// </summary>
	public QuarterSize Size { get; init; } = new();

	/// <summary>
	/// Whether the quarter is a landmark: a place with no houses or businesses of its own.
	/// </summary>
	public bool IsLandmark => Houses is null && Businesses == 0;

	/// <summary>
	/// The id of a map feature the quarter borders, such as a forest, beyond any its kind borders. The quarter is only used
	/// in towns that have the feature.
	/// </summary>
	public string? Borders { get; init; }

	/// <summary>
	/// Sentences used instead of a map feature's own text when the quarter borders it, by feature id. Each is a template
	/// whose only placeholder is the direction the feature is in.
	/// </summary>
	public IReadOnlyDictionary<string, LongText>? FeatureText { get; init; }

	/// <summary>
	/// The most houses the quarter holds.
	/// </summary>
	public int MaxHouses => Houses?.Maximum ?? 0;

	/// <summary>
	/// The fewest houses the quarter should have, if there are enough households to fill it.
	/// </summary>
	public int MinHouses => Houses?.Minimum ?? 0;
}

/// <summary>
/// A home for one household.
/// </summary>
public sealed class HouseTemplate : LocationTemplate
{
	/// <summary>
	/// How wealthy the household living in the house is.
	/// </summary>
	public required WealthLevel Wealth { get; init; }
}

/// <summary>
/// A place where a business is run, such as a forge or a mill.
/// </summary>
public sealed class BusinessTemplate : LocationTemplate
{
	/// <summary>
	/// The id of the profession whose businesses can be run from this place.
	/// </summary>
	public required string Profession { get; init; }

	/// <summary>
	/// The kinds of quarter the business can be in.
	/// </summary>
	public required IReadOnlyList<string> QuarterKinds { get; init; }
}

/// <summary>
/// How many houses a quarter holds, and how wealthy the households living in them are.
/// </summary>
public sealed class QuarterHouses
{
	public required int Minimum { get; init; }

	public required int Maximum { get; init; }

	/// <summary>
	/// Weights how many houses the quarter is planned to have, or null to lean toward fewer.
	/// </summary>
	public WeightShape? Shape { get; init; }

	public required WealthLevel Wealth { get; init; }
}

/// <summary>
/// The descriptions of a location.
/// </summary>
public sealed class LocationDescription
{
	/// <summary>
	/// One sentence, used when the location is listed as a place to go.
	/// </summary>
	public required LongText Short { get; init; }

	/// <summary>
	/// One paragraph, shown when arriving at the location.
	/// </summary>
	public required LongText Medium { get; init; }

	/// <summary>
	/// Several paragraphs, shown when examining the location.
	/// </summary>
	public required IReadOnlyList<LongText> Long { get; init; }
}
