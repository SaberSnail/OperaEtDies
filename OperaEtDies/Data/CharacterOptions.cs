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

	public required int PlayerMaximumAge { get; init; }

	public required IReadOnlyList<CharacterOption> Heights { get; init; }

	public required IReadOnlyList<CharacterOption> Weights { get; init; }

	public IEnumerable<string> Validate()
	{
		if (PlayerMinimumAge < 1)
			yield return OurResources.PlayerMinimumAgeInvalid.FormatCurrentCulture(PlayerMinimumAge);
		if (PlayerMaximumAge < PlayerMinimumAge)
			yield return OurResources.PlayerMaximumAgeInvalid.FormatCurrentCulture(PlayerMaximumAge, PlayerMinimumAge);

		foreach (var error in DataValidation.ValidateIdentifiedItems("heights", Heights, x => x.Id, x => x.Title))
			yield return error;
		foreach (var error in DataValidation.ValidateItemFrequencies("heights", Heights, x => x.Id, x => x.Frequency))
			yield return error;

		foreach (var error in DataValidation.ValidateIdentifiedItems("weights", Weights, x => x.Id, x => x.Title))
			yield return error;
		foreach (var error in DataValidation.ValidateItemFrequencies("weights", Weights, x => x.Id, x => x.Frequency))
			yield return error;
	}
}
