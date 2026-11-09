using System.IO;

namespace OperaEtDies.Data;

/// <summary>
/// The first names used for random selection, loaded from FirstNames.json.
/// </summary>
public sealed class FirstNameList : IValidatedData
{
	public const string FileName = "FirstNames.json";

	public static DataLoadResult<FirstNameList> Load(string dataDirectory) =>
		JsonDataLoader.Load<FirstNameList>(Path.Combine(dataDirectory, FileName));

	public required IReadOnlyList<WeightedName> Male { get; init; }

	public required IReadOnlyList<WeightedName> Female { get; init; }

	public IEnumerable<string> Validate() =>
		DataValidation.ValidateWeightedNames("male", Male)
			.Concat(DataValidation.ValidateWeightedNames("female", Female));
}
