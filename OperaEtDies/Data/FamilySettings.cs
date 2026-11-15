using System.Text.Json.Serialization;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// How the households of the village are generated when a new game is created, from the families section of the data.
/// Chances are percentages.
/// </summary>
public sealed class FamilySettings
{
	/// <summary>
	/// The name of the data section the settings are read from.
	/// </summary>
	public const string SectionName = "families";

	/// <summary>
	/// The character properties and values that generating households depends on, found by their roles. They're filled
	/// in when the data loads, rather than read from the families section.
	/// </summary>
	[JsonIgnore]
	public FamilyRoles Roles { get; internal set; } = FamilyRoles.Empty;

	/// <summary>
	/// The head of each household.
	/// </summary>
	public required FamilyHeadSettings Head { get; init; }

	/// <summary>
	/// The wife of a man at the head of a household.
	/// </summary>
	public required FamilyWifeSettings Wife { get; init; }

	/// <summary>
	/// The adult children living with a household.
	/// </summary>
	public required FamilyChildSettings Children { get; init; }

	/// <summary>
	/// The household elder: a parent of the head or the wife, living with the household.
	/// </summary>
	public required FamilyElderSettings Elder { get; init; }

	/// <summary>
	/// The ages at which someone can have a child.
	/// </summary>
	public required ParentAgeSettings ParentAge { get; init; }

	/// <summary>
	/// The chance that a member of the household helps in a household business (the head's, or else the wife's), when
	/// the business has room for them and they're allowed to.
	/// </summary>
	public required double HelperChance { get; init; }

	/// <summary>
	/// How a household's wealth is worked out.
	/// </summary>
	public required FamilyWealthSettings Wealth { get; init; }

	/// <summary>
	/// The value list properties that family members share: a household elder has the same value as their child, and a
	/// child takes after their parents.
	/// </summary>
	public IReadOnlyList<InheritedProperty> InheritedProperties { get; init; } = [];

	/// <summary>
	/// Gets the wealth level of a household with the given total wealth.
	/// </summary>
	public WealthLevel GetHouseholdWealthLevel(double wealth) =>
		wealth >= Wealth.Thresholds.High ? WealthLevel.High : wealth >= Wealth.Thresholds.Medium ? WealthLevel.Medium : WealthLevel.Low;

	/// <summary>
	/// Gets the oldest age at which someone of the given gender can have a child.
	/// </summary>
	public int GetMaxParentAge(bool isFemale) => isFemale ? ParentAge.MaxMother : ParentAge.MaxFather;

	/// <summary>
	/// Gets the youngest age at which someone of the given gender can be a household elder.
	/// </summary>
	public int GetHouseholdElderMinAge(bool isFemale) => isFemale ? Elder.MinAge.Female : Elder.MinAge.Male;

	public IEnumerable<string> Validate()
	{
		const string path = SectionName;
		var errors = new List<string>();
		errors.AddRange(DataValidation.ValidateChance($"{path}.head.femaleChance", Head.FemaleChance));
		errors.AddRange(CharacterData.ValidateRange($"{path}.head.age", Head.Age));

		errors.AddRange(DataValidation.ValidateChance($"{path}.wife.chance", Wife.Chance));
		errors.AddRange(CharacterData.ValidateRange($"{path}.wife.ageDifference", Wife.AgeDifference));

		errors.AddRange(CharacterData.ValidateRange($"{path}.children.count", Children.Count));
		if (Children.Count.Minimum < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.children.count.minimum", Children.Count.Minimum, 0));
		errors.AddRange(Children.Age.Validate($"{path}.children.age"));
		if (!(Children.SiblingAgeSpread > 0) || !double.IsFinite(Children.SiblingAgeSpread))
			errors.Add(OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.children.siblingAgeSpread", Children.SiblingAgeSpread));
		errors.AddRange(DataValidation.ValidateChance($"{path}.children.sonChance", Children.SonChance));
		errors.AddRange(DataValidation.ValidateChance($"{path}.children.homemakerChance", Children.HomemakerChance));

		errors.AddRange(DataValidation.ValidateChance($"{path}.elder.chance", Elder.Chance));
		errors.AddRange(DataValidation.ValidateChance($"{path}.elder.femaleChance", Elder.FemaleChance));
		errors.AddRange(Elder.AgeGap.Validate($"{path}.elder.ageGap"));
		if (Elder.MinAge.Male < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.elder.minAge.male", Elder.MinAge.Male, 0));
		if (Elder.MinAge.Female < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.elder.minAge.female", Elder.MinAge.Female, 0));
		foreach (var (group, chance) in Elder.InfirmChance)
			errors.AddRange(DataValidation.ValidateChance($"{path}.elder.infirmChance.{group}", chance));
		errors.AddRange(DataValidation.ValidateChance($"{path}.elder.homemakerChance", Elder.HomemakerChance));

		if (ParentAge.Minimum < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.parentAge.minimum", ParentAge.Minimum, 0));
		if (ParentAge.MaxMother < ParentAge.Minimum)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.parentAge.maxMother", ParentAge.MaxMother, ParentAge.Minimum));
		if (ParentAge.MaxFather < ParentAge.Minimum)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.parentAge.maxFather", ParentAge.MaxFather, ParentAge.Minimum));

		errors.AddRange(DataValidation.ValidateChance($"{path}.helperChance", HelperChance));

		if (!(Wealth.Helper >= 0 && Wealth.Helper <= 1))
			errors.Add(OurResources.DataFractionInvalid.FormatCurrentCulture($"{path}.wealth.helper", Wealth.Helper));
		if (!(Wealth.HelperFalloff >= 0 && Wealth.HelperFalloff <= 1))
			errors.Add(OurResources.DataFractionInvalid.FormatCurrentCulture($"{path}.wealth.helperFalloff", Wealth.HelperFalloff));
		errors.AddRange(CharacterData.ValidateRange($"{path}.wealth.fortune", Wealth.Fortune));
		if (Wealth.Fortune.Minimum < 0)
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.wealth.fortune.minimum", Wealth.Fortune.Minimum, 0));
		foreach (var (group, percent) in Wealth.HeadAge.Where(x => !(x.Value >= 0)))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.wealth.headAge.{group}", percent, 0));
		if (!(Wealth.Thresholds.Medium >= 0) || !double.IsFinite(Wealth.Thresholds.Medium))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.wealth.thresholds.medium", Wealth.Thresholds.Medium, 0));
		if (!(Wealth.Thresholds.High >= Wealth.Thresholds.Medium) || !double.IsFinite(Wealth.Thresholds.High))
			errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.wealth.thresholds.high", Wealth.Thresholds.High, Wealth.Thresholds.Medium));

		for (var index = 0; index < InheritedProperties.Count; index++)
		{
			if (InheritedProperties[index] is { } inherited)
				errors.AddRange(inherited.Validate($"{path}.inheritedProperties[{index}]"));
			else
				errors.Add(OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.inheritedProperties"));
		}

		return errors;
	}
}

/// <summary>
/// The head of each household.
/// </summary>
public sealed class FamilyHeadSettings
{
	/// <summary>
	/// The chance that the head of a household is a widow rather than a man.
	/// </summary>
	public required double FemaleChance { get; init; }

	/// <summary>
	/// The lowest wealth a woman at the head of a household can have from her own profession.
	/// </summary>
	public required WealthLevel FemaleMinWealth { get; init; }

	/// <summary>
	/// The age of the head of a non-player household. It must be within the age property's range for non-player characters.
	/// </summary>
	public required IntegerRange Age { get; init; }
}

/// <summary>
/// The wife of a man at the head of a household.
/// </summary>
public sealed class FamilyWifeSettings
{
	/// <summary>
	/// The chance that a man at the head of a household has a living wife.
	/// </summary>
	public required double Chance { get; init; }

	/// <summary>
	/// The wife's age minus her husband's age.
	/// </summary>
	public required IntegerRange AgeDifference { get; init; }
}

/// <summary>
/// The adult children living with a household.
/// </summary>
public sealed class FamilyChildSettings
{
	/// <summary>
	/// How many adult children live with the household, if their parents' ages allow it.
	/// </summary>
	public required IntegerRange Count { get; init; }

	/// <summary>
	/// Weights the age of the first child chosen, over the ages the children could be, from youngest to oldest.
	/// </summary>
	public required WeightShape Age { get; init; }

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
	public required double HomemakerChance { get; init; }
}

/// <summary>
/// The household elder: a parent of the head or the wife, living with the household.
/// </summary>
public sealed class FamilyElderSettings
{
	/// <summary>
	/// The chance that a household elder lives with the household.
	/// </summary>
	public required double Chance { get; init; }

	/// <summary>
	/// The chance that a household elder is a woman, when a man is allowed.
	/// </summary>
	public required double FemaleChance { get; init; }

	/// <summary>
	/// Weights how much older a household elder is than their child, over the ages at which the elder could have had the child.
	/// </summary>
	public required WeightShape AgeGap { get; init; }

	/// <summary>
	/// The youngest age at which a man or a woman can be a household elder.
	/// </summary>
	public required GenderAges MinAge { get; init; }

	/// <summary>
	/// The chance that a woman who is a household elder is infirm, by the id of the age property's group her age is in.
	/// Groups that aren't listed have no chance. A man who is a household elder is always infirm.
	/// </summary>
	public IReadOnlyDictionary<string, double> InfirmChance { get; init; } = new Dictionary<string, double>();

	/// <summary>
	/// The chance that a woman who is a household elder, and doesn't help in a household business, keeps house rather than
	/// taking up a profession for household elders.
	/// </summary>
	public required double HomemakerChance { get; init; }
}

/// <summary>
/// The ages at which someone can have a child.
/// </summary>
public sealed class ParentAgeSettings
{
	/// <summary>
	/// The youngest age at which someone can have a child.
	/// </summary>
	public required int Minimum { get; init; }

	/// <summary>
	/// The oldest age at which a woman can have a child.
	/// </summary>
	public required int MaxMother { get; init; }

	/// <summary>
	/// The oldest age at which a man can have a child.
	/// </summary>
	public required int MaxFather { get; init; }
}

/// <summary>
/// How a household's wealth is worked out.
/// </summary>
public sealed class FamilyWealthSettings
{
	/// <summary>
	/// The part of a business's wealth that its first helper adds to the household's wealth, from 0 to 1.
	/// </summary>
	public required double Helper { get; init; }

	/// <summary>
	/// Each helper after the first in a business adds this fraction of what the helper before them added, from 0 to 1.
	/// </summary>
	public required double HelperFalloff { get; init; }

	/// <summary>
	/// What a homemaker adds to the household's wealth, by making the household's money go further.
	/// </summary>
	public required double Homemaker { get; init; }

	/// <summary>
	/// What an infirm household member adds to the household's wealth. Usually negative, for the cost of their care.
	/// </summary>
	public required double Infirm { get; init; }

	/// <summary>
	/// A percentage that each household's wealth is multiplied by, for its good or bad fortune.
	/// </summary>
	public required IntegerRange Fortune { get; init; }

	/// <summary>
	/// A percentage that a household's wealth is multiplied by, by the id of the age property's group the head's age is
	/// in. Groups that aren't listed are 100.
	/// </summary>
	public IReadOnlyDictionary<string, double> HeadAge { get; init; } = new Dictionary<string, double>();

	/// <summary>
	/// The household wealth needed to be counted as medium or high wealth.
	/// </summary>
	public required WealthThresholds Thresholds { get; init; }
}

/// <summary>
/// The character properties and values that generating households depends on, found by their roles.
/// </summary>
public sealed class FamilyRoles
{
	/// <summary>
	/// Roles with no properties or values, before the data has loaded.
	/// </summary>
	public static FamilyRoles Empty { get; } = new()
	{
		Properties = new FamilyProperties { Gender = "", Age = "", Profession = "", LastName = "", WorkRole = "", HouseholdRole = "", HeadRelation = "", HeadName = "", AtWork = "", Married = "" },
		Male = "",
		Female = "",
		Homemaker = "",
		Infirm = "",
		Independent = "",
		Helper = "",
		Head = "",
		Member = "",
		HeadRelations = new HeadRelations { Self = "", Husband = "", Father = "", Mother = "", Son = "", Daughter = "", SonInLaw = "", DaughterInLaw = "" },
		AtWork = "",
		NotAtWork = "",
		Married = "",
		Unmarried = "",
	};

	/// <summary>
	/// The ids of the character properties that generating households depends on.
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
	/// The id of the profession for a household elder who keeps house instead of working a trade.
	/// </summary>
	public required string Homemaker { get; init; }

	/// <summary>
	/// The id of the profession for a household elder who is too frail to work.
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
	/// The ids of the head relation property's values, one for each way the head of a household can be related to a
	/// member of it.
	/// </summary>
	public required HeadRelations HeadRelations { get; init; }

	/// <summary>
	/// The id of the at work property's value for someone who is working.
	/// </summary>
	public required string AtWork { get; init; }

	/// <summary>
	/// The id of the at work property's value for someone who isn't working.
	/// </summary>
	public required string NotAtWork { get; init; }

	/// <summary>
	/// The id of the married property's value for someone who is married: the head of a household who has a wife, and
	/// the wife.
	/// </summary>
	public required string Married { get; init; }

	/// <summary>
	/// The id of the married property's value for someone who isn't married.
	/// </summary>
	public required string Unmarried { get; init; }
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
	/// A value list property whose values are the professions.
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

	/// <summary>
	/// A value list property, set by the game, that says how the head of someone's household is related to them.
	/// </summary>
	public required string HeadRelation { get; init; }

	/// <summary>
	/// A string property, set by the game, holding the full name of the head of someone's household.
	/// </summary>
	public required string HeadName { get; init; }

	/// <summary>
	/// A value list property, set by the game, that says whether someone is working right now.
	/// </summary>
	public required string AtWork { get; init; }

	/// <summary>
	/// A value list property, set by the game, that says whether someone is married. It's set before the rest of a
	/// character's values are chosen, so their conditions can use it.
	/// </summary>
	public required string Married { get; init; }
}

/// <summary>
/// A value list property that family members share. A household elder has the same value as their child. A child with
/// one parent in the household has that parent's value. A child with two parents has one of their values, or a value
/// listed between them.
/// </summary>
public sealed class InheritedProperty
{
	/// <summary>
	/// The id of a value list property.
	/// </summary>
	public required string Property { get; init; }

	/// <summary>
	/// Weights the values a child of two parents can have, from the earlier parent's value in the property's list to the
	/// later parent's, leaving out <see cref="Unblended"/> values. Without it, a child has one parent's value or the other's.
	/// </summary>
	public WeightShape? Blend { get; init; }

	/// <summary>
	/// The ids of values that don't blend. A child of a parent with one of them and a parent without has the first
	/// parent's value with <see cref="UnblendedChance"/>, and otherwise the other parent's. A child of two parents with
	/// one of them each has a random value.
	/// </summary>
	public IReadOnlyList<string> Unblended { get; init; } = [];

	/// <summary>
	/// The chance that a child with one parent with an <see cref="Unblended"/> value has that value.
	/// </summary>
	public double UnblendedChance { get; init; } = 50;

	internal IEnumerable<string> Validate(string path)
	{
		var errors = new List<string>();
		if (string.IsNullOrWhiteSpace(Property))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture($"{path}.property"));
		if (Blend is not null)
			errors.AddRange(Blend.Validate($"{path}.blend"));
		errors.AddRange(DataValidation.ValidateChance($"{path}.unblendedChance", UnblendedChance));
		return errors;
	}
}

/// <summary>
/// The ids of the head relation property's values: what the head of a household is to a member of it.
/// </summary>
public sealed class HeadRelations
{
	/// <summary>
	/// For the head of the household.
	/// </summary>
	public required string Self { get; init; }

	/// <summary>
	/// For the head's wife.
	/// </summary>
	public required string Husband { get; init; }

	/// <summary>
	/// For the children of a man at the head of a household.
	/// </summary>
	public required string Father { get; init; }

	/// <summary>
	/// For the children of a woman at the head of a household.
	/// </summary>
	public required string Mother { get; init; }

	/// <summary>
	/// For a household elder who is the parent of a man at the head of a household.
	/// </summary>
	public required string Son { get; init; }

	/// <summary>
	/// For a household elder who is the parent of a woman at the head of a household.
	/// </summary>
	public required string Daughter { get; init; }

	/// <summary>
	/// For a household elder who is the parent of the head's wife.
	/// </summary>
	public required string SonInLaw { get; init; }

	/// <summary>
	/// For a household elder who is the parent of the late husband of a woman at the head of a household.
	/// </summary>
	public required string DaughterInLaw { get; init; }

	/// <summary>
	/// Gets the setting names and value ids of every relation.
	/// </summary>
	internal IReadOnlyList<(string Name, string ValueId)> GetAll() =>
	[
		("headRelations.self", Self),
		("headRelations.husband", Husband),
		("headRelations.father", Father),
		("headRelations.mother", Mother),
		("headRelations.son", Son),
		("headRelations.daughter", Daughter),
		("headRelations.sonInLaw", SonInLaw),
		("headRelations.daughterInLaw", DaughterInLaw),
	];

	/// <summary>
	/// Gets how two members of the same household are related, from how each is related to its head.
	/// </summary>
	public FamilyTie GetFamilyTie(string? first, string? second)
	{
		var (a, b) = (GetPlace(first), GetPlace(second));
		return (a, b) switch
		{
			(HouseholdPlace.Head, HouseholdPlace.Wife) or (HouseholdPlace.Wife, HouseholdPlace.Head) => FamilyTie.Spouse,

			// Two elders on the same side of the family are a couple.
			(HouseholdPlace.HeadParent, HouseholdPlace.HeadParent) or (HouseholdPlace.SpouseParent, HouseholdPlace.SpouseParent) => FamilyTie.Spouse,

			// An elder is a parent of the head or of the head's spouse, and a grandparent of the children. They're an in-law to
			// the head's spouse or to the head.
			(HouseholdPlace.Head, HouseholdPlace.HeadParent) or (HouseholdPlace.HeadParent, HouseholdPlace.Head) => FamilyTie.Elder,
			(HouseholdPlace.Wife, HouseholdPlace.SpouseParent) or (HouseholdPlace.SpouseParent, HouseholdPlace.Wife) => FamilyTie.Elder,
			(HouseholdPlace.Child, HouseholdPlace.HeadParent or HouseholdPlace.SpouseParent) or (HouseholdPlace.HeadParent or HouseholdPlace.SpouseParent, HouseholdPlace.Child) => FamilyTie.Elder,
			(HouseholdPlace.Head, HouseholdPlace.SpouseParent) or (HouseholdPlace.SpouseParent, HouseholdPlace.Head) => FamilyTie.ElderInLaw,
			(HouseholdPlace.Wife, HouseholdPlace.HeadParent) or (HouseholdPlace.HeadParent, HouseholdPlace.Wife) => FamilyTie.ElderInLaw,
			_ => FamilyTie.Other,
		};
	}

	private HouseholdPlace GetPlace(string? relation) =>
		relation == Self ? HouseholdPlace.Head :
		relation == Husband ? HouseholdPlace.Wife :
		relation == Father || relation == Mother ? HouseholdPlace.Child :
		relation == Son || relation == Daughter ? HouseholdPlace.HeadParent :
		relation == SonInLaw || relation == DaughterInLaw ? HouseholdPlace.SpouseParent :
		HouseholdPlace.Unknown;

	internal IEnumerable<string> Validate() =>
		GetAll().Where(x => string.IsNullOrWhiteSpace(x.ValueId)).Select(x => OurResources.DataValueMissing.FormatCurrentCulture(x.Name));
}

/// <summary>
/// How two members of the same household are related.
/// </summary>
public enum FamilyTie
{
	/// <summary>
	/// Any other relation, such as a parent and child, or siblings.
	/// </summary>
	Other,

	/// <summary>
	/// A married couple.
	/// </summary>
	Spouse,

	/// <summary>
	/// A household elder and their child or grandchild.
	/// </summary>
	Elder,

	/// <summary>
	/// A household elder and their child's spouse.
	/// </summary>
	ElderInLaw,
}

/// <summary>
/// Where someone is in a household, from how they're related to its head.
/// </summary>
internal enum HouseholdPlace
{
	Unknown,
	Head,
	Wife,
	Child,
	HeadParent,
	SpouseParent,
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
