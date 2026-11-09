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

	public required int NpcMinimumAge { get; init; }

	public required int NpcMaximumAge { get; init; }

	public required IReadOnlyList<CharacterOption> Heights { get; init; }

	public required IReadOnlyList<CharacterOption> Weights { get; init; }

	public IEnumerable<string> Validate()
	{
		if (PlayerMinimumAge < 1)
			yield return OurResources.PlayerMinimumAgeInvalid.FormatCurrentCulture(PlayerMinimumAge);
		if (PlayerMaximumAge < PlayerMinimumAge)
			yield return OurResources.PlayerMaximumAgeInvalid.FormatCurrentCulture(PlayerMaximumAge, PlayerMinimumAge);
		if (NpcMinimumAge < 1)
			yield return OurResources.NpcMinimumAgeInvalid.FormatCurrentCulture(NpcMinimumAge);
		if (NpcMaximumAge < NpcMinimumAge)
			yield return OurResources.NpcMaximumAgeInvalid.FormatCurrentCulture(NpcMaximumAge, NpcMinimumAge);

		foreach (var error in DataValidation.ValidateIdentifiedItems("heights", Heights, x => x.Id, x => x.Title))
			yield return error;
		foreach (var error in DataValidation.ValidateItemFrequencies("heights", Heights, x => x.Id, x => x.Frequency))
			yield return error;
		foreach (var error in DataValidation.ValidateItemDescriptions("heights", Heights, x => x.Id, x => x.Description))
			yield return error;

		foreach (var error in DataValidation.ValidateIdentifiedItems("weights", Weights, x => x.Id, x => x.Title))
			yield return error;
		foreach (var error in DataValidation.ValidateItemFrequencies("weights", Weights, x => x.Id, x => x.Frequency))
			yield return error;
		foreach (var error in DataValidation.ValidateItemDescriptions("weights", Weights, x => x.Id, x => x.Description))
			yield return error;
	}
}
