using System.IO;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// The values available when describing a character, loaded from CharacterOptions.json.
/// </summary>
public sealed class CharacterOptions : IValidatedData
{
	public const string FileName = "CharacterOptions.json";

	public static DataLoadResult<CharacterOptions> Load(string dataDirectory) =>
		JsonDataLoader.Load<CharacterOptions>(Path.Combine(dataDirectory, FileName));

	public required int PlayerMinimumAge { get; init; }

	public required IReadOnlyList<CharacterOption> Heights { get; init; }

	/// <summary>
	/// The id of the height selected by default.
	/// </summary>
	public required string DefaultHeight { get; init; }

	public required IReadOnlyList<CharacterOption> Weights { get; init; }

	/// <summary>
	/// The id of the weight selected by default.
	/// </summary>
	public required string DefaultWeight { get; init; }

	/// <summary>
	/// The height whose id is DefaultHeight. Only valid once the data has passed validation.
	/// </summary>
	public CharacterOption GetDefaultHeight() => Heights.First(x => x.Id == DefaultHeight);

	/// <summary>
	/// The weight whose id is DefaultWeight. Only valid once the data has passed validation.
	/// </summary>
	public CharacterOption GetDefaultWeight() => Weights.First(x => x.Id == DefaultWeight);

	public IEnumerable<string> Validate()
	{
		if (PlayerMinimumAge < 1)
			yield return OurResources.PlayerMinimumAgeInvalid.FormatCurrentCulture(PlayerMinimumAge);

		foreach (var error in DataValidation.ValidateIdentifiedItems("heights", Heights, x => x.Id, x => x.Title))
			yield return error;
		foreach (var error in DataValidation.ValidateDefaultId("defaultHeight", DefaultHeight, "heights", Heights, x => x.Id))
			yield return error;

		foreach (var error in DataValidation.ValidateIdentifiedItems("weights", Weights, x => x.Id, x => x.Title))
			yield return error;
		foreach (var error in DataValidation.ValidateDefaultId("defaultWeight", DefaultWeight, "weights", Weights, x => x.Id))
			yield return error;
	}
}
