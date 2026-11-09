using System.IO;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// The professions available to the player, loaded from Professions.json.
/// </summary>
public sealed class ProfessionList : IValidatedData
{
	public const string FileName = "Professions.json";

	public static DataLoadResult<ProfessionList> Load(string dataDirectory) =>
		JsonDataLoader.Load<ProfessionList>(Path.Combine(dataDirectory, FileName));

	public required IReadOnlyList<Profession> Professions { get; init; }

	public IEnumerable<string> Validate()
	{
		foreach (var error in DataValidation.ValidateIdentifiedItems("professions", Professions, x => x.Id, x => x.Title))
			yield return error;

		// Items with a missing id are already reported above.
		foreach (var profession in Professions.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id) && string.IsNullOrWhiteSpace(x.DefaultLastName)))
			yield return OurResources.ProfessionDefaultLastNameMissing.FormatCurrentCulture(profession.Id);
	}
}
