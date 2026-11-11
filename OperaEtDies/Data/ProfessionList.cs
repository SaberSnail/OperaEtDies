using System.IO;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// The professions, loaded from Professions.json: the values of the profession character property, and how the
/// businesses of each profession work.
/// </summary>
public sealed class ProfessionList : IValidatedData
{
	public const string FileName = "Professions.json";

	public static DataLoadResult<ProfessionList> Load(string dataDirectory) => Load(dataDirectory, []);

	/// <summary>
	/// Loads the file from the data folder, with the changes from the mod folders applied in order.
	/// </summary>
	public static DataLoadResult<ProfessionList> Load(string dataDirectory, IReadOnlyList<string> modDirectories) =>
		JsonDataLoader.Load<ProfessionList>(Path.Combine(dataDirectory, FileName), modDirectories, []);

	public required IReadOnlyList<Profession> Professions { get; init; }

	public IEnumerable<string> Validate()
	{
		var errors = new List<string>();
		if (Professions.Count == 0)
			errors.Add(OurResources.DataListEmpty.FormatCurrentCulture("professions"));

		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (var profession in Professions)
		{
			// JSON deserialization doesn't stop null list items, even when the type is non-nullable.
			if (profession is null)
			{
				errors.Add(OurResources.DataListItemEmpty.FormatCurrentCulture("professions"));
			}
			else if (string.IsNullOrWhiteSpace(profession.Id))
			{
				errors.Add(OurResources.DataListItemIdMissing.FormatCurrentCulture("professions"));
			}
			else
			{
				if (!ids.Add(profession.Id))
					errors.Add(OurResources.DataListItemIdDuplicate.FormatCurrentCulture(profession.Id, "professions"));
				if (string.IsNullOrWhiteSpace(profession.Title))
					errors.Add(OurResources.DataListItemTitleMissing.FormatCurrentCulture(profession.Id, "professions"));
				errors.AddRange(ValidateProfession(profession));
			}
		}

		return errors;
	}

	private static IEnumerable<string> ValidateProfession(Profession profession)
	{
		var path = $"professions[{profession.Id}]";
		if (profession.MaxBusinesses is < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.maxBusinesses", profession.MaxBusinesses, 1);
		if (profession.Helpers is < 0)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.helpers", profession.Helpers, 0);
		if (profession.ForHouseholdElders && !profession.WorksFromHome)
			yield return OurResources.ProfessionElderTradeNotFromHome.FormatCurrentCulture(profession.Id);
		foreach (var (propertyId, values) in profession.Defaults ?? new Dictionary<string, IReadOnlyList<WeightedString>>())
		{
			foreach (var error in DataValidation.ValidateWeightedStrings($"{path}.defaults.{propertyId}", values))
				yield return error;
		}
	}
}
