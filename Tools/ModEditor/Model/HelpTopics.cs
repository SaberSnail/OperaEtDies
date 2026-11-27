using System.Text.RegularExpressions;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace ModEditor.Model;

/// <summary>
/// Where the help explains each part of the data: the heading in DataFiles.md for each class the game reads the data
/// into (named by splitting the class name into words, such as "Character Property", unless it's listed here), and
/// the table row for each of its properties.
/// </summary>
public static partial class HelpTopics
{
	/// <summary>
	/// The headings of the classes whose names don't give them.
	/// </summary>
	public static IReadOnlyDictionary<Type, string> Headings { get; } = new Dictionary<Type, string>
	{
		[typeof(GameInfo)] = "Game",
		[typeof(CharacterPropertyOption)] = "Character Value",
		[typeof(CharacterRoleSettings)] = "Role Settings",
		[typeof(CharacterTemplateVariant)] = "Template Variant",
		[typeof(CharacterCondition)] = "Condition",
		[typeof(WeightShape)] = "Shape",
		[typeof(WeightedString)] = "Weighted Value",
		[typeof(ProfessionBusiness)] = "Business",
		[typeof(FamilySettings)] = "Families",
		[typeof(FamilyHeadSettings)] = "Head",
		[typeof(FamilyWifeSettings)] = "Wife",
		[typeof(FamilyChildSettings)] = "Children",
		[typeof(FamilyElderSettings)] = "Elder",
		[typeof(ParentAgeSettings)] = "Parent Age",
		[typeof(FamilyWealthSettings)] = "Family Wealth",
		[typeof(RelationshipSettings)] = "Relationships",
		[typeof(TownLayoutChoice)] = "Layout",
		[typeof(QuarterTemplate)] = "Quarter",
		[typeof(HouseTemplate)] = "House",
		[typeof(BusinessTemplate)] = "Business Place",
		[typeof(CalendarSection)] = "Calendar Settings",
		[typeof(CalendarDay)] = "Calendar Item",
		[typeof(CalendarWeek)] = "Calendar Item",
		[typeof(CalendarSeason)] = "Calendar Item",
		[typeof(CalendarDayPart)] = "Day Part",
		[typeof(CalendarSpecialDay)] = "Special Day",
		[typeof(ScheduleSettings)] = "Schedules Settings",
		[typeof(SleepSettings)] = "Sleep",
		[typeof(TalkingSettings)] = "Talking",
		[typeof(SocializingSettings)] = "Socializing",
		[typeof(IdleSettings)] = "Idle",
		[typeof(ScheduleStart)] = "Start",
		[typeof(SchedulePlace)] = "Place",
		[typeof(PlaceObject)] = "Place",
		[typeof(OutfitClothing)] = "Outfit Clothing",
		[typeof(OutfitClothingChoice)] = "Outfit Clothing",
		[typeof(LevelNeedSettings)] = "Level Need",
		[typeof(NeedLevel)] = "Level",
		[typeof(QuotaNeedSettings)] = "Quota Need",
		[typeof(PortraitSection)] = "Portraits Settings",
		[typeof(PortraitPlaceCondition)] = "Portrait Place",
		[typeof(FrequencyLevel)] = "Frequency",
	};

	/// <summary>
	/// Gets the heading that explains a class.
	/// </summary>
	public static string GetHeading(Type type) =>
		Headings.TryGetValue(type, out var heading) ? heading : WordRegex().Replace(type.Name, " $1").Trim();

	/// <summary>
	/// Gets where the help explains a class, or null if it doesn't.
	/// </summary>
	public static HelpTarget? ForType(Type type) => HelpLibrary.Check(new HelpTarget(HelpLibrary.DataFiles, GetHeading(type)));

	/// <summary>
	/// Gets where the help explains a property of a class: its row in the class's table, or the class itself if the
	/// table doesn't have it, or null if the help doesn't explain the class.
	/// </summary>
	public static HelpTarget? ForProperty(Type type, string property) =>
		HelpLibrary.Check(new HelpTarget(HelpLibrary.DataFiles, GetHeading(type), property));

	/// <summary>
	/// Gets where the help explains a section: its row in the list of sections.
	/// </summary>
	public static HelpTarget? ForSection(string section) =>
		HelpLibrary.Check(new HelpTarget(HelpLibrary.DataFiles, "Files and Sections", section));

	/// <summary>
	/// Gets where the help explains a property of a mod operation, or operations themselves.
	/// </summary>
	public static HelpTarget? ForOperation(string? property = null) =>
		HelpLibrary.Check(new HelpTarget(HelpLibrary.Mods, "Operation", property));

	/// <summary>
	/// Gets where the help explains groups of mod operations.
	/// </summary>
	public static HelpTarget? ForGroup() => HelpLibrary.Check(new HelpTarget(HelpLibrary.Mods, "Group", null));

	/// <summary>
	/// Gets where the help explains a piece of a mod file or of the game data.
	/// </summary>
	public static HelpTarget? ForPiece(ModPiece piece) => piece.Kind switch
	{
		ModPieceKind.Operation => ForOperation(),
		ModPieceKind.Group => ForGroup(),
		ModPieceKind.Item when DataSchema.ListSections.TryGetValue(piece.Section!, out var type) => ForType(type),
		ModPieceKind.Setting when DataSchema.SettingsSections.TryGetValue(piece.Section!, out var type) => ForProperty(type, piece.Name!),
		_ => ForSection(piece.Section!),
	};

	[GeneratedRegex("(?<!^)([A-Z])")]
	private static partial Regex WordRegex();
}
