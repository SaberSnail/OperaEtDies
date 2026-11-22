using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace ModEditor.Model;

/// <summary>
/// What kind of editor a value in the data needs.
/// </summary>
public enum DataKind
{
	Text,
	Integer,
	Number,
	Boolean,

	/// <summary>
	/// One of a fixed set of names.
	/// </summary>
	Choice,

	/// <summary>
	/// An object with known properties.
	/// </summary>
	Object,

	List,

	/// <summary>
	/// An object whose property names are keys, each with a value of the same type.
	/// </summary>
	Map,

	/// <summary>
	/// A value the editor doesn't know the shape of, edited as JSON.
	/// </summary>
	Json,
}

/// <summary>
/// The shape of a value in the data, worked out from the class the game reads it into.
/// </summary>
public sealed record DataType(DataKind Kind, Type ClrType, DataType? Element = null, IReadOnlyList<string>? Choices = null)
{
	/// <summary>
	/// The properties of an object, in the order the class declares them.
	/// </summary>
	public IReadOnlyList<DataField> Fields => Kind == DataKind.Object ? DataSchema.GetFields(ClrType) : [];
}

/// <summary>
/// A property of an object in the data: its name in the JSON, its shape, and whether it must be there, with the class
/// that declares it, and the class of the object it's a field of, which may inherit it.
/// </summary>
public sealed record DataField(string Name, DataType Type, bool IsRequired, Type DeclaringType, string PropertyName, DataRule? AllowedFor = null, DataRule? RequiredFor = null,
	Type? Owner = null);

/// <summary>
/// The shapes of the data's sections and of everything in them, read by reflection from the classes the game reads
/// the data into (see Docs/DataFiles.md), so they follow the game as it changes.
/// </summary>
public static class DataSchema
{
	/// <summary>
	/// The sections that are lists, with the class of their items.
	/// </summary>
	public static IReadOnlyDictionary<string, Type> ListSections { get; } = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
	{
		["properties"] = typeof(CharacterProperty),
		["templates"] = typeof(CharacterTemplate),
		["clothingSlots"] = typeof(ClothingSlot),
		["clothingLayers"] = typeof(ClothingLayer),
		["clothingBulks"] = typeof(ClothingBulk),
		["modestyRules"] = typeof(ModestyRule),
		["clothingColors"] = typeof(ClothingColor),
		["clothing"] = typeof(ClothingItem),
		["outfits"] = typeof(Outfit),
		["professions"] = typeof(Profession),
		["quarterKinds"] = typeof(QuarterKind),
		["layouts"] = typeof(TownLayoutChoice),
		["features"] = typeof(MapFeature),
		["quarters"] = typeof(QuarterTemplate),
		["houses"] = typeof(HouseTemplate),
		["businesses"] = typeof(BusinessTemplate),
		["days"] = typeof(CalendarDay),
		["weeks"] = typeof(CalendarWeek),
		["seasons"] = typeof(CalendarSeason),
		["dayParts"] = typeof(CalendarDayPart),
		["specialDays"] = typeof(CalendarSpecialDay),
		["needs"] = typeof(Need),
		["urgencyFactors"] = typeof(UrgencyFactor),
		["travelTimeFactors"] = typeof(TravelTimeFactor),
		["stepLists"] = typeof(StepList),
		["portraitColors"] = typeof(PortraitColor),
		["portraitScales"] = typeof(PortraitScale),
		["portraitOffsets"] = typeof(PortraitOffset),
		["portraitShapeSets"] = typeof(PortraitShapeSet),
		["portraitLayers"] = typeof(PortraitLayer),
	};

	/// <summary>
	/// The sections that are settings objects, with the class they're read into.
	/// </summary>
	public static IReadOnlyDictionary<string, Type> SettingsSections { get; } = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
	{
		[GameInfo.SectionName] = typeof(GameInfo),
		[FamilySettings.SectionName] = typeof(FamilySettings),
		[RelationshipSettings.SectionName] = typeof(RelationshipSettings),
		[VillageSettings.SectionName] = typeof(VillageSettings),
		["calendar"] = typeof(CalendarSection),
		[ScheduleSettings.SectionName] = typeof(ScheduleSettings),
		[ClothingSettings.SectionName] = typeof(ClothingSettings),
		["portraits"] = typeof(PortraitSection),
	};

	/// <summary>
	/// Gets the shape of a piece of a mod file, or null if it isn't known (such as an operation, or an unknown section).
	/// </summary>
	public static DataType? GetPieceType(ModPiece piece) => piece.Kind switch
	{
		ModPieceKind.Item => ListSections.TryGetValue(piece.Section!, out var type) ? GetType(type) : null,
		ModPieceKind.Section when SettingsSections.TryGetValue(piece.Section!, out var type) => GetType(type),
		ModPieceKind.Section when ListSections.TryGetValue(piece.Section!, out var type) => new DataType(DataKind.List, typeof(List<>).MakeGenericType(type), GetType(type)),
		ModPieceKind.Setting when SettingsSections.TryGetValue(piece.Section!, out var type) => GetFields(type).FirstOrDefault(x => string.Equals(x.Name, piece.Name, StringComparison.OrdinalIgnoreCase))?.Type,
		_ => null,
	};

	/// <summary>
	/// Gets the shape of the data a mod operation's path leads to, or null if it isn't known. Steps after the section
	/// follow the properties of objects and the items of lists and maps.
	/// </summary>
	public static DataType? GetPathType(string path)
	{
		var steps = ParsePath(path);
		if (steps.Count == 0 || steps[0].IsIndex)
			return null;

		DataType? type;
		var first = steps[0].Text;
		if (ListSections.TryGetValue(first, out var itemType))
			type = new DataType(DataKind.List, typeof(List<>).MakeGenericType(itemType), GetType(itemType));
		else if (SettingsSections.TryGetValue(first, out var settingsType))
			type = GetType(settingsType);
		else
			return null;

		foreach (var step in steps.Skip(1))
		{
			type = type switch
			{
				{ Kind: DataKind.List or DataKind.Map } when step.IsIndex => type.Element,
				{ Kind: DataKind.Map } => type.Element,
				{ Kind: DataKind.Object } => type.Fields.FirstOrDefault(x => string.Equals(x.Name, step.Text, StringComparison.OrdinalIgnoreCase))?.Type,
				_ => null,
			};
			if (type is null)
				return null;
		}

		return type;
	}

	/// <summary>
	/// Gets the property a path ends at, or whose items or values it ends at (such as "clothing[tunic].slots[0]"), or null if
	/// it ends somewhere else, such as at a whole item or section.
	/// </summary>
	public static DataField? GetPathField(string path)
	{
		var steps = ParsePath(path);
		if (steps.Count == 0 || steps[0].IsIndex)
			return null;

		DataType? type = ListSections.TryGetValue(steps[0].Text, out var itemType) ? new DataType(DataKind.List, typeof(List<>).MakeGenericType(itemType), GetType(itemType)) :
			SettingsSections.TryGetValue(steps[0].Text, out var settingsType) ? GetType(settingsType) : null;
		DataField? field = null;
		foreach (var step in steps.Skip(1))
		{
			if (type is { Kind: DataKind.Object } && !step.IsIndex)
			{
				field = type.Fields.FirstOrDefault(x => string.Equals(x.Name, step.Text, StringComparison.OrdinalIgnoreCase));
				type = field?.Type;
			}
			else
			{
				// An item of a list or a value of a map belongs to the property the list or map is, if there is one.
				field = type is { Kind: DataKind.List or DataKind.Map } && field?.Type == type ? field : null;
				type = type is { Kind: DataKind.List or DataKind.Map } ? type.Element : null;
			}

			if (type is null)
				return null;
		}

		return field;
	}

	/// <summary>
	/// Whether a path could lead to data the game reads: it starts with a known section, and each step after it is a
	/// property the data there has (or an item or key, in a list or map). Steps into data the editor doesn't know the
	/// shape of are taken on trust. Whether the items and keys exist isn't checked.
	/// </summary>
	public static bool IsKnownPath(string path)
	{
		var steps = ParsePath(path);
		if (steps.Count == 0 || steps[0].IsIndex)
			return false;

		DataType? type;
		if (ListSections.TryGetValue(steps[0].Text, out var itemType))
			type = new DataType(DataKind.List, typeof(List<>).MakeGenericType(itemType), GetType(itemType));
		else if (SettingsSections.TryGetValue(steps[0].Text, out var settingsType))
			type = GetType(settingsType);
		else
			return false;

		foreach (var step in steps.Skip(1))
		{
			if (type.Kind == DataKind.Json)
				return true;
			type = type switch
			{
				{ Kind: DataKind.List } when step.IsIndex => type.Element,
				{ Kind: DataKind.Map } => type.Element,
				{ Kind: DataKind.Object } => type.Fields.FirstOrDefault(x => string.Equals(x.Name, step.Text, StringComparison.OrdinalIgnoreCase))?.Type,
				_ => null,
			};
			if (type is null)
				return false;
		}

		return true;
	}

	/// <summary>
	/// Gets the id of the top-level item a path leads into, such as "hair" for "properties[hair].values", with its
	/// section, or nulls if it doesn't lead into one.
	/// </summary>
	public static (string? Section, string? ItemId) GetPathItem(string path)
	{
		var steps = ParsePath(path);
		return steps.Count >= 2 && !steps[0].IsIndex && steps[1].IsIndex && !int.TryParse(steps[1].Text, out _) ? (steps[0].Text, steps[1].Text) : (null, null);
	}

	/// <summary>
	/// Gets the shape of a class's values.
	/// </summary>
	public static DataType GetType(Type type) => s_types.GetOrAdd(type, CreateType);

	/// <summary>
	/// Gets the properties the game reads into a class: those with a public setter, without [JsonIgnore], named in camel
	/// case.
	/// </summary>
	public static IReadOnlyList<DataField> GetFields(Type type) => s_fields.GetOrAdd(type, CreateFields);

	private static DataType CreateType(Type type)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;
		if (type == typeof(string))
			return new DataType(DataKind.Text, type);
		if (type == typeof(bool))
			return new DataType(DataKind.Boolean, type);
		if (type == typeof(int) || type == typeof(long))
			return new DataType(DataKind.Integer, type);
		if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
			return new DataType(DataKind.Number, type);
		if (type.IsEnum)
			return new DataType(DataKind.Choice, type, Choices: Enum.GetNames(type).Select(x => JsonNamingPolicy.CamelCase.ConvertName(x)).ToList());

		// Lengths of time are whole numbers of minutes, or text with hours and minutes.
		if (type == typeof(Duration))
			return new DataType(DataKind.Integer, type);

		// Values the game reads from text of their own, such as frequencies, are edited as text.
		if (type == typeof(FrequencyLevel) || type == typeof(PeakLevel) || type == typeof(ProfessionWealth))
			return new DataType(DataKind.Text, type);
		if (type.GetCustomAttribute<JsonConverterAttribute>() is not null)
			return new DataType(DataKind.Json, type);

		if (type.IsGenericType)
		{
			var definition = type.GetGenericTypeDefinition();
			var arguments = type.GetGenericArguments();
			if (definition == typeof(IReadOnlyList<>) || definition == typeof(List<>) || definition == typeof(IList<>) || definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyCollection<>))
				return new DataType(DataKind.List, type, GetType(arguments[0]));
			if ((definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(Dictionary<,>) || definition == typeof(IDictionary<,>)) && arguments[0] == typeof(string))
				return new DataType(DataKind.Map, type, GetType(arguments[1]));
		}

		if (type.IsArray && type.GetElementType() is { } element)
			return new DataType(DataKind.List, type, GetType(element));
		if (type.IsClass && !type.IsAbstract && type.Namespace?.StartsWith("OperaEtDies", StringComparison.Ordinal) == true)
			return new DataType(DataKind.Object, type);
		return new DataType(DataKind.Json, type);
	}

	private static IReadOnlyList<DataField> CreateFields(Type type)
	{
		// Base classes' properties come first, as they're usually the shared ones like the id.
		var hierarchy = new List<Type>();
		for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
			hierarchy.Insert(0, current);

		return hierarchy
			.SelectMany(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
			.Where(x => x.GetSetMethod() is not null && x.GetIndexParameters().Length == 0 && x.GetCustomAttribute<JsonIgnoreAttribute>() is null && !ExcludedFields.Contains((type, x.Name)))
			.Select(x => new DataField(
				x.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(x.Name),
				GetType(x.PropertyType),
				x.GetCustomAttribute<RequiredMemberAttribute>() is not null,
				x.DeclaringType!,
				x.Name,
				DataRules.GetAllowedRule(x.DeclaringType!, x.Name),
				DataRules.GetRequiredRule(x.DeclaringType!, x.Name),
				type))
			.ToList();
	}

	/// <summary>
	/// Splits a path into its steps, as mod operations read them: names after dots, and bracketed ids or indexes.
	/// </summary>
	public static IReadOnlyList<(string Text, bool IsIndex)> ParsePath(string path)
	{
		var steps = new List<(string, bool)>();
		var index = 0;
		while (index < path.Length)
		{
			if (path[index] == '.')
			{
				index++;
			}
			else if (path[index] == '[')
			{
				var quoted = index + 1 < path.Length && path[index + 1] == '\'';
				var end = quoted ? path.IndexOf("']", index + 2, StringComparison.Ordinal) : path.IndexOf(']', index + 1);
				if (end < 0)
					break;
				steps.Add((quoted ? path[(index + 2)..end] : path[(index + 1)..end], true));
				index = end + (quoted ? 2 : 1);
			}
			else
			{
				var end = path.IndexOfAny(['.', '['], index);
				end = end < 0 ? path.Length : end;
				steps.Add((path[index..end], false));
				index = end;
			}
		}

		return steps;
	}

	/// <summary>
	/// Properties a class inherits but the game doesn't accept in it, named by the class and the C# property.
	/// </summary>
	private static readonly HashSet<(Type, string)> ExcludedFields =
	[
		// A house is titled by the village's houseTitle, and a title of its own is an error.
		(typeof(HouseTemplate), nameof(HouseTemplate.Title)),
	];

	private static readonly ConcurrentDictionary<Type, DataType> s_types = new();
	private static readonly ConcurrentDictionary<Type, IReadOnlyList<DataField>> s_fields = new();
}
