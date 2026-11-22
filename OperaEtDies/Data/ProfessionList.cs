using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// The professions, from the professions section of the data: the values of the property with the profession role, and
/// how the businesses of each profession work.
/// </summary>
public sealed class ProfessionList
{
	/// <summary>
	/// The role of the profession for a household elder who keeps house.
	/// </summary>
	public const string HomemakerRole = "homemaker";

	/// <summary>
	/// The role of the profession for a household elder who is too frail to work.
	/// </summary>
	public const string InfirmRole = "infirm";

	public required IReadOnlyList<Profession> Professions { get; init; }

	public IEnumerable<string> Validate()
	{
		var errors = new List<string>();
		errors.AddRange(DataValidation.ValidateIdentifiedItems("professions", Professions, x => x.Id, x => x.Title));
		var professions = Professions.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id)).ToList();
		foreach (var profession in professions)
			errors.AddRange(ValidateProfession(profession));

		// The homemaker and infirm professions are only given to household elders by the family generator.
		errors.AddRange(DataValidation.ValidateRoles("professions", professions, x => x.Id, x => x.Role, [HomemakerRole, InfirmRole]));
		foreach (var role in new[] { HomemakerRole, InfirmRole })
		{
			var profession = professions.FirstOrDefault(x => x.Role == role);
			if (profession is null)
				errors.Add(OurResources.RoleMissing.FormatCurrentCulture("professions", role));
			else if (profession.IsBusiness)
				errors.Add(OurResources.ProfessionNotBusiness.FormatCurrentCulture(profession.Id, role));
		}

		return errors;
	}

	private static IEnumerable<string> ValidateProfession(Profession profession)
	{
		var path = $"professions[{profession.Id}]";

		// The player always heads a household, and every head runs a business.
		if (profession.Player && !profession.IsBusiness)
			yield return OurResources.ProfessionPlayerNotBusiness.FormatCurrentCulture(profession.Id);

		foreach (var group in profession.Groups ?? [])
		{
			if (string.IsNullOrWhiteSpace(group))
				yield return OurResources.DataListItemBlank.FormatCurrentCulture($"{path}.groups");
		}

		if (profession.Business is { } business)
		{
			if (business.MaxBusinesses is < 1)
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.business.maxBusinesses", business.MaxBusinesses, 1);
			if (business.Helpers is < 0)
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.business.helpers", business.Helpers, 0);
			if (business.Activity is not null && string.IsNullOrWhiteSpace(business.Activity))
				yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.business.activity");
			if (business.Hours is { } hours)
			{
				foreach (var error in hours.Validate($"{path}.business.hours", business))
					yield return error;
			}
		}

		foreach (var (propertyId, values) in profession.Defaults ?? new Dictionary<string, IReadOnlyList<WeightedString>>())
		{
			foreach (var error in DataValidation.ValidateWeightedStrings($"{path}.defaults.{propertyId}", values))
				yield return error;
		}
	}
}
