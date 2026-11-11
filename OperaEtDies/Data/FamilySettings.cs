using System.IO;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// How the households of the town are generated when a new game is created, loaded from Families.json. Chances are
/// percentages.
/// </summary>
public sealed class FamilySettings : IValidatedData
{
	public const string FileName = "Families.json";

	public static DataLoadResult<FamilySettings> Load(string dataDirectory) => Load(dataDirectory, []);

	/// <summary>
	/// Loads the file from the data folder, with the changes from the mod folders applied in order.
	/// </summary>
	public static DataLoadResult<FamilySettings> Load(string dataDirectory, IReadOnlyList<string> modDirectories) =>
		JsonDataLoader.Load<FamilySettings>(Path.Combine(dataDirectory, FileName), modDirectories, []);

	/// <summary>
	/// The character properties that generating households depends on.
	/// </summary>
	public required FamilyProperties Properties { get; init; }

	/// <summary>
	/// The id of the gender property's value for men.
	/// </summary>
	public required string Male { get; init; }

	/// <summary>
	/// The id of the gender property's value for women.
	/// </summary>
	public required string Female { get; init; }

	/// <summary>
	/// The id of the profession property's value for a household elder who keeps house instead of working a trade.
	/// It isn't a business, so it has no entry in Professions.json.
	/// </summary>
	public required string Homemaker { get; init; }

	/// <summary>
	/// The id of the profession property's value for a household elder who is too frail to work. It isn't a business,
	/// so it has no entry in Professions.json.
	/// </summary>
	public required string Infirm { get; init; }

	/// <summary>
	/// The id of the work role property's value for someone who isn't helping in someone else's business.
	/// </summary>
	public required string Independent { get; init; }

	/// <summary>
	/// The id of the work role property's value for someone who helps in a household member's business.
	/// </summary>
	public required string Helper { get; init; }

	/// <summary>
	/// The id of the household role property's value for the head of a household.
	/// </summary>
	public required string Head { get; init; }

	/// <summary>
	/// The id of the household role property's value for everyone else in a household.
	/// </summary>
	public required string Member { get; init; }

	/// <summary>
	/// How many households the town has, other than the player's.
	/// </summary>
	public required IntegerRange FamilyCount { get; init; }

	/// <summary>
	/// The chance that the head of a household is a widow rather than a man.
	/// </summary>
	public required double FemaleHeadChance { get; init; }

	/// <summary>
	/// The lowest wealth a woman at the head of a household can have from her own profession.
	/// </summary>
	public required WealthLevel FemaleHeadMinWealth { get; init; }

	/// <summary>
	/// The age of the head of a non-player household. It must be within the age property's range for non-player characters.
	/// </summary>
	public required IntegerRange HeadAge { get; init; }

	/// <summary>
	/// The chance that a man at the head of a household has a living wife.
	/// </summary>
	public required double WifeChance { get; init; }

	/// <summary>
	/// The wife's age minus her husband's age.
	/// </summary>
	public required IntegerRange WifeAgeDifference { get; init; }

	/// <summary>
	/// The chance that a household elder (a parent of the head or the wife) lives with the household.
	/// </summary>
	public required double HouseholdElderChance { get; init; }

	/// <summary>
	/// The chance that a household elder is a woman, when a man is allowed.
	/// </summary>
	public required double FemaleHouseholdElderChance { get; init; }

	/// <summary>
	/// Weights how much older a household elder is than their child, over the ages at which the elder could have had the child.
	/// </summary>
	public required WeightShape HouseholdElderAgeGap { get; init; }

	/// <summary>
	/// The youngest age at which a man or a woman can be a household elder.
	/// </summary>
	public required GenderAges HouseholdElderMinAge { get; init; }

	/// <summary>
	/// The chance that a woman who is a household elder is infirm, by the id of the age property's group her age is in.
	/// Groups that aren't listed have no chance. A man who is a household elder is always infirm.
	/// </summary>
	public IReadOnlyDictionary<string, double> InfirmChance { get; init; } = new Dictionary<string, double>();

	/// <summary>
	/// The chance that a woman who is a household elder, and doesn't help in a household business, keeps house rather than
	/// taking up a profession for household elders.
	/// </summary>
	public required double HouseholdElderHomemakerChance { get; init; }

	/// <summary>
	/// The youngest age at which someone can have a child.
	/// </summary>
	public required int MinParentAge { get; init; }

	/// <summary>
	/// The oldest age at which a woman can have a child.
	/// </summary>
	public required int MaxMotherAge { get; init; }

	/// <summary>
	/// The oldest age at which a man can have a child.
	/// </summary>
	public required int MaxFatherAge { get; init; }

	/// <summary>
	/// How many adult children live with the household, if their parents' ages allow it.
	/// </summary>
	public required IntegerRange ChildCount { get; init; }

	/// <summary>
	/// Weights the age of the first child chosen, over the ages the children could be, from youngest to oldest.
	/// </summary>
	public required WeightShape ChildAge { get; init; }

	/// <summary>
	/// How close in age the other children tend to be to the first child chosen: about two-thirds are within this
	/// many years.
	/// </summary>
	public required double SiblingAgeSpread { get; init; }

	/// <summary>
	/// The chance that a child living with the household is a son, when a man is allowed.
	/// </summary>
	public required double SonChance { get; init; }

	/// <summary>
	/// The chance that a daughter who doesn't help in a household business keeps house, rather than working from home.
	/// </summary>
	public required double ChildHomemakerChance { get; init; }

	/// <summary>
	/// The chance that a member of the household helps in a household business (the head's, or else the wife's), when
	/// the business has room for them and they're allowed to.
	/// </summary>
	public required double HelperChance { get; init; }

	/// <summary>
	/// The part of a business's wealth that each helper adds to the household's wealth, from 0 to 1.
	/// </summary>
	public required double HelperWealth { get; init; }

	/// <summary>
	/// Each helper after the first in a business adds this fraction of what the helper before them added, from 0 to 1.
	/// </summary>
	public required double HelperWealthFalloff { get; init; }

	/// <summary>
	/// What a homemaker adds to the household's wealth, by making the household's money go further.
	/// </summary>
	public required double HomemakerWealth { get; init; }

	/// <summary>
	/// What an infirm household member adds to the household's wealth. Usually negative, for the cost of their care.
	/// </summary>
	public required double InfirmWealth { get; init; }

	/// <summary>
	/// A percentage that each household's wealth is multiplied by, for its good or bad fortune: debts, savings, good
	/// years and bad.
	/// </summary>
	public required IntegerRange HouseholdFortune { get; init; }

	/// <summary>
	/// A percentage that a household's wealth is multiplied by, by the id of the age property's group the head's age is
	/// in. Groups that aren't listed are 100.
	/// </summary>
	public IReadOnlyDictionary<string, double> HeadAgeWealth { get; init; } = new Dictionary<string, double>();

	/// <summary>
	/// The household wealth needed to be counted as medium or high wealth.
	/// </summary>
	public required WealthThresholds WealthThresholds { get; init; }

	/// <summary>
	/// Gets the wealth level of a household with the given total wealth.
	/// </summary>
	public WealthLevel GetHouseholdWealthLevel(double wealth) =>
		wealth >= WealthThresholds.High ? WealthLevel.High : wealth >= WealthThresholds.Medium ? WealthLevel.Medium : WealthLevel.Low;

	/// <summary>
	/// Gets the oldest age at which someone of the given gender can have a child.
	/// </summary>
	public int GetMaxParentAge(bool isFemale) => isFemale ? MaxMotherAge : MaxFatherAge;

	/// <summary>
	/// Gets the youngest age at which someone of the given gender can be a household elder.
	/// </summary>
	public int GetHouseholdElderMinAge(bool isFemale) => isFemale ? HouseholdElderMinAge.Female : HouseholdElderMinAge.Male;

	public IEnumerable<string> Validate()
	{
		var errors = new List<string>();
		errors.AddRange(Properties.Validate());
		if (string.IsNullOrWhiteSpace(Male))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture("male"));
		if (string.IsNullOrWhiteSpace(Female))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture("female"));
		if (string.IsNullOrWhiteSpace(Homemaker))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture("homemaker"));
		if (string.IsNullOrWhiteSpace(Infirm))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture("infirm"));
		if (string.IsNullOrWhiteSpace(Independent))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture("independent"));
		if (string.IsNullOrWhiteSpace(Helper))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture("helper"));
		if (string.IsNullOrWhiteSpace(Head))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture("head"));
		if (string.IsNullOrWhiteSpace(Member))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture("member"));

		errors.AddRange(CharacterData.ValidateRange("familyCount", FamilyCount));
		if (FamilyCount.Minimum < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("familyCount.minimum", FamilyCount.Minimum, 0));
		errors.AddRange(CharacterData.ValidateRange("wifeAgeDifference", WifeAgeDifference));
		errors.AddRange(CharacterData.ValidateRange("headAge", HeadAge));
		errors.AddRange(HouseholdElderAgeGap.Validate("householdElderAgeGap"));
		if (HouseholdElderMinAge.Male < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("householdElderMinAge.male", HouseholdElderMinAge.Male, 0));
		if (HouseholdElderMinAge.Female < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("householdElderMinAge.female", HouseholdElderMinAge.Female, 0));
		errors.AddRange(CharacterData.ValidateRange("childCount", ChildCount));
		if (ChildCount.Minimum < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("childCount.minimum", ChildCount.Minimum, 0));
		errors.AddRange(ChildAge.Validate("childAge"));
		if (!(SiblingAgeSpread > 0) || !double.IsFinite(SiblingAgeSpread))
			errors.Add(OurResources.DataNumberNotPositive.FormatCurrentCulture("siblingAgeSpread", SiblingAgeSpread));

		if (MinParentAge < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("minParentAge", MinParentAge, 0));
		if (MaxMotherAge < MinParentAge)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("maxMotherAge", MaxMotherAge, MinParentAge));
		if (MaxFatherAge < MinParentAge)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("maxFatherAge", MaxFatherAge, MinParentAge));

		errors.AddRange(DataValidation.ValidateChance("femaleHeadChance", FemaleHeadChance));
		errors.AddRange(DataValidation.ValidateChance("wifeChance", WifeChance));
		errors.AddRange(DataValidation.ValidateChance("householdElderChance", HouseholdElderChance));
		errors.AddRange(DataValidation.ValidateChance("femaleHouseholdElderChance", FemaleHouseholdElderChance));
		errors.AddRange(DataValidation.ValidateChance("householdElderHomemakerChance", HouseholdElderHomemakerChance));
		foreach (var (group, chance) in InfirmChance)
			errors.AddRange(DataValidation.ValidateChance($"infirmChance.{group}", chance));
		errors.AddRange(DataValidation.ValidateChance("sonChance", SonChance));
		errors.AddRange(DataValidation.ValidateChance("childHomemakerChance", ChildHomemakerChance));
		errors.AddRange(DataValidation.ValidateChance("helperChance", HelperChance));
		if (!(HelperWealth >= 0 && HelperWealth <= 1))
			errors.Add(OurResources.DataFractionInvalid.FormatCurrentCulture("helperWealth", HelperWealth));
		if (!(HelperWealthFalloff >= 0 && HelperWealthFalloff <= 1))
			errors.Add(OurResources.DataFractionInvalid.FormatCurrentCulture("helperWealthFalloff", HelperWealthFalloff));
		errors.AddRange(CharacterData.ValidateRange("householdFortune", HouseholdFortune));
		if (HouseholdFortune.Minimum < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("householdFortune.minimum", HouseholdFortune.Minimum, 0));
		foreach (var (group, percent) in HeadAgeWealth.Where(x => !(x.Value >= 0)))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"headAgeWealth.{group}", percent, 0));

		if (!(WealthThresholds.Medium >= 0) || !double.IsFinite(WealthThresholds.Medium))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("wealthThresholds.medium", WealthThresholds.Medium, 0));
		if (!(WealthThresholds.High >= WealthThresholds.Medium) || !double.IsFinite(WealthThresholds.High))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture("wealthThresholds.high", WealthThresholds.High, WealthThresholds.Medium));

		return errors;
	}
}

/// <summary>
/// The ids of the character properties that generating households depends on.
/// </summary>
public sealed class FamilyProperties
{
	/// <summary>
	/// A value list property with values for men and women.
	/// </summary>
	public required string Gender { get; init; }

	/// <summary>
	/// An integer property holding the character's age in years.
	/// </summary>
	public required string Age { get; init; }

	/// <summary>
	/// A value list property whose values are the ids of the professions.
	/// </summary>
	public required string Profession { get; init; }

	/// <summary>
	/// A string property shared by the members of a household. Its non-player defaults must have other values,
	/// which are used when a household's usual last name is taken, and for in-laws.
	/// </summary>
	public required string LastName { get; init; }

	/// <summary>
	/// A value list property, set by the game, that says whether someone helps in a household member's business.
	/// </summary>
	public required string WorkRole { get; init; }

	/// <summary>
	/// A value list property, set by the game, that says whether someone heads their household.
	/// </summary>
	public required string HouseholdRole { get; init; }

	internal IEnumerable<string> Validate()
	{
		if (string.IsNullOrWhiteSpace(Gender))
			yield return OurResources.DataValueMissing.FormatCurrentCulture("properties.gender");
		if (string.IsNullOrWhiteSpace(Age))
			yield return OurResources.DataValueMissing.FormatCurrentCulture("properties.age");
		if (string.IsNullOrWhiteSpace(Profession))
			yield return OurResources.DataValueMissing.FormatCurrentCulture("properties.profession");
		if (string.IsNullOrWhiteSpace(LastName))
			yield return OurResources.DataValueMissing.FormatCurrentCulture("properties.lastName");
		if (string.IsNullOrWhiteSpace(WorkRole))
			yield return OurResources.DataValueMissing.FormatCurrentCulture("properties.workRole");
		if (string.IsNullOrWhiteSpace(HouseholdRole))
			yield return OurResources.DataValueMissing.FormatCurrentCulture("properties.householdRole");
	}
}

/// <summary>
/// An age for men and an age for women.
/// </summary>
public sealed class GenderAges
{
	public required int Male { get; init; }

	public required int Female { get; init; }
}

/// <summary>
/// The household wealth needed to reach each wealth level above low.
/// </summary>
public sealed class WealthThresholds
{
	public required double Medium { get; init; }

	public required double High { get; init; }
}
