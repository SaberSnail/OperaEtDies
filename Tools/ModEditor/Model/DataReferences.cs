using System.Text.Json;
using System.Text.Json.Nodes;
using OperaEtDies.Data;

namespace ModEditor.Model;

/// <summary>
/// Where the ids a field can hold come from.
/// </summary>
public abstract record IdSource;

/// <summary>
/// The ids of the items in a list section, such as "quarterKinds".
/// </summary>
public sealed record SectionIds(string Section) : IdSource;

/// <summary>
/// The values (and value groups) of a property. The property is the map key the field is the value of, the property
/// named by another field of the same object, or the property the field is part of. Keys that aren't properties, such
/// as <c>family</c> in a step's <c>who</c>, can have values of their own in <see cref="Others"/>.
/// </summary>
public sealed record PropertyValueIds(string? SiblingField = null, bool FromMapKey = false, IReadOnlyDictionary<string, IReadOnlyList<string>>? Others = null) : IdSource;

/// <summary>
/// The ids of several sources together, such as the character properties and <c>family</c>.
/// </summary>
public sealed record CombinedIds(IReadOnlyList<IdSource> Sources) : IdSource;

/// <summary>
/// Portrait colors, by id, and in a portrait layer or shape set, the colors of its own and of the clothing it draws. A
/// field holding one can also hold a color written out, such as #FFAA00.
/// </summary>
public sealed record ColorIds : IdSource;

/// <summary>
/// A fixed set of names, such as the roles the game knows.
/// </summary>
public sealed record FixedIds(IReadOnlyList<string> Names) : IdSource;

/// <summary>
/// The ids a field refers to: those its value can be (or that its list or map values can be), and those a map's keys
/// can be. A map whose values are maps of their own gives what those maps refer to as its entries.
/// </summary>
public sealed record FieldReference(IdSource? Value, IdSource? Key = null, FieldReference? Entries = null);

/// <summary>
/// Which fields of the data refer to other things by id (see Docs/DataFiles.md), so the form can offer them. Fields
/// are named by the class the game reads them into and the C# property. A class can refer to things of its own in a
/// field it inherits, such as a step's <c>who</c>, whose values can also say whether someone is family.
/// </summary>
public static class DataReferences
{
	public static IReadOnlyDictionary<(Type Type, string Property), FieldReference> Fields { get; } = CreateFields();

	/// <summary>
	/// Gets what a field refers to, or null if it doesn't refer to anything.
	/// </summary>
	public static FieldReference? Find(DataField field) =>
		(field.Owner is { } owner ? Fields.GetValueOrDefault((owner, field.PropertyName)) : null) ?? Fields.GetValueOrDefault((field.DeclaringType, field.PropertyName));

	/// <summary>
	/// Gets the suggestions for a field whose text has no id source of its own, such as a frequency.
	/// </summary>
	public static IReadOnlyList<string>? GetTextChoices(Type type) => type switch
	{
		_ when type == typeof(OperaEtDies.Utility.Random.FrequencyLevel) => Frequencies,
		_ when type == typeof(PeakLevel) => ["low", "middle", "high"],
		_ when type == typeof(ProfessionWealth) => Wealths,
		_ => null,
	};

	private static IReadOnlyList<string> Frequencies { get; } = WithSteps(["common", "uncommon", "rare", "veryRare"], OperaEtDies.Utility.Random.FrequencyLevel.MaxSteps);

	private static IReadOnlyList<string> Wealths { get; } = WithSteps(Enum.GetValues<WealthLevel>().Select(x => x.GetDataName()), ProfessionWealth.MaxSteps);

	/// <summary>
	/// Gets names with each of the steps up and down they can take, such as "high", "high+", "high++", "high-", and "high--".
	/// </summary>
	private static IReadOnlyList<string> WithSteps(IEnumerable<string> names, int maxSteps) =>
		names.SelectMany(x => Enumerable.Range(0, maxSteps + 1).Select(steps => x + new string('+', steps))
			.Concat(Enumerable.Range(1, maxSteps).Select(steps => x + new string('-', steps)))).ToList();

	private static Dictionary<(Type, string), FieldReference> CreateFields()
	{
		var properties = new SectionIds("properties");
		var quarterKinds = new SectionIds("quarterKinds");
		var features = new SectionIds("features");
		var professions = new SectionIds("professions");
		var needs = new SectionIds("needs");
		var stepLists = new SectionIds("stepLists");
		var icons = new SectionIds(GameIcon.SectionName);
		var whoKeys = new CombinedIds([properties, new FixedIds([StepWho.FamilyKey])]);
		var whoValues = new PropertyValueIds(FromMapKey: true, Others: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
		{
			[StepWho.FamilyKey] = [StepWho.FamilyMember, StepWho.FamilyOther],
		});
		var colors = new ColorIds();
		var fields = new Dictionary<(Type, string), FieldReference>
		{
			[(typeof(CharacterProperty), nameof(CharacterProperty.Follows))] = new(properties),
			[(typeof(CharacterProperty), nameof(CharacterProperty.Role))] = new(new FixedIds(["gender", "age", "profession", "lastName", "workRole", "householdRole", "headRelation", "headName", "atWork", "married"])),
			[(typeof(CharacterPropertyOption), nameof(CharacterPropertyOption.Role))] = new(new FixedIds(["male", "female", "independent", "helper", "head", "member", "self", "husband", "father", "mother", "son", "daughter", "sonInLaw", "daughterInLaw", "atWork", "notAtWork", "married", "unmarried"])),
			[(typeof(CharacterRoleSettings), nameof(CharacterRoleSettings.Values))] = new(new PropertyValueIds()),
			[(typeof(CharacterCondition), nameof(CharacterCondition.Values))] = new(new PropertyValueIds(FromMapKey: true), properties),
			[(typeof(CharacterCondition), nameof(CharacterCondition.NotValues))] = new(new PropertyValueIds(FromMapKey: true), properties),
			[(typeof(CharacterCondition), nameof(CharacterCondition.Hidden))] = new(properties),
			[(typeof(StepWho), nameof(StepWho.Values))] = new(whoValues, whoKeys),
			[(typeof(StepWho), nameof(StepWho.NotValues))] = new(whoValues, whoKeys),
			[(typeof(ClothingItem), nameof(ClothingItem.Slots))] = new(new SectionIds("clothingSlots")),
			[(typeof(ClothingItem), nameof(ClothingItem.Layer))] = new(new SectionIds("clothingLayers")),
			[(typeof(ClothingItem), nameof(ClothingItem.Coverage))] = new(null, new SectionIds("clothingSlots")),
			[(typeof(ClothingItem), nameof(ClothingItem.Bulk))] = new(new SectionIds("clothingBulks")),
			[(typeof(CharacterProperty), nameof(CharacterProperty.Regions))] = new(new SectionIds("clothingSlots")),
			[(typeof(PortraitHiddenBy), nameof(PortraitHiddenBy.Regions))] = new(new SectionIds("clothingSlots")),
			[(typeof(ModestyRule), nameof(ModestyRule.Slots))] = new(null, new SectionIds("clothingSlots")),
			[(typeof(ModestyBoldness), nameof(ModestyBoldness.Property))] = new(properties),
			[(typeof(Outfit), nameof(Outfit.Clothing))] = new(new SectionIds("clothing")),
			[(typeof(OutfitClothingOption), nameof(OutfitClothingOption.Clothing))] = new(new SectionIds("clothing")),
			[(typeof(StringDefaults), nameof(StringDefaults.KeyedBy))] = new(properties),
			[(typeof(StringDefaults), nameof(StringDefaults.ValuesByKey))] = new(null, new PropertyValueIds(SiblingField: "keyedBy")),
			[(typeof(Profession), nameof(Profession.Role))] = new(new FixedIds(["homemaker", "infirm"])),
			[(typeof(Profession), nameof(Profession.Defaults))] = new(null, properties),
			[(typeof(QuarterTemplate), nameof(QuarterTemplate.Kind))] = new(quarterKinds),
			[(typeof(QuarterTemplate), nameof(QuarterTemplate.Borders))] = new(features),
			[(typeof(QuarterTemplate), nameof(QuarterTemplate.FeatureText))] = new(null, features),
			[(typeof(QuarterKind), nameof(QuarterKind.Borders))] = new(features),
			[(typeof(QuarterKindOptional), nameof(QuarterKindOptional.With))] = new(features),
			[(typeof(TownLayoutChoice), nameof(TownLayoutChoice.Center))] = new(quarterKinds),
			[(typeof(BusinessTemplate), nameof(BusinessTemplate.Profession))] = new(professions),
			[(typeof(BusinessTemplate), nameof(BusinessTemplate.QuarterKinds))] = new(quarterKinds),
			[(typeof(InheritedProperty), nameof(InheritedProperty.Property))] = new(properties),
			[(typeof(InheritedProperty), nameof(InheritedProperty.Unblended))] = new(new PropertyValueIds(SiblingField: "property")),
			[(typeof(CalendarSpecialDay), nameof(CalendarSpecialDay.Season))] = new(new SectionIds("seasons")),
			[(typeof(CalendarSpecialDay), nameof(CalendarSpecialDay.Week))] = new(new SectionIds("weeks")),
			[(typeof(CalendarSpecialDay), nameof(CalendarSpecialDay.Day))] = new(new SectionIds("days")),
			[(typeof(Hours), nameof(Hours.Days))] = new(new SectionIds("days")),
			[(typeof(QuarterChoice), nameof(QuarterChoice.QuarterKind))] = new(quarterKinds),
			[(typeof(Need), nameof(Need.Icon))] = new(icons),
			[(typeof(Need), nameof(Need.Role))] = new(new FixedIds([ScheduleSettings.SleepRole, ScheduleSettings.SocializingRole])),
			[(typeof(LevelNeedSettings), nameof(LevelNeedSettings.PausedBy))] = new(needs),
			[(typeof(UrgencyFactor), nameof(UrgencyFactor.Needs))] = new(needs),
			[(typeof(Step), nameof(Step.List))] = new(stepLists),
			[(typeof(PortraitLayer), nameof(PortraitLayer.Scale))] = new(new SectionIds("portraitScales")),
			[(typeof(PortraitLayer), nameof(PortraitLayer.Offset))] = new(new SectionIds("portraitOffsets")),
			[(typeof(PortraitLayer), nameof(PortraitLayer.Clothing))] = new(new SectionIds("clothing")),
			[(typeof(PortraitShapeSet), nameof(PortraitShapeSet.Scale))] = new(new SectionIds("portraitScales")),
			[(typeof(PortraitShape), nameof(PortraitShape.Include))] = new(new SectionIds("portraitShapeSets")),
			[(typeof(PortraitShape), nameof(PortraitShape.Scale))] = new(new SectionIds("portraitScales")),
			[(typeof(PortraitShape), nameof(PortraitShape.Fill))] = new(colors),
			[(typeof(PortraitShape), nameof(PortraitShape.FillTo))] = new(colors),
			[(typeof(PortraitShape), nameof(PortraitShape.Stroke))] = new(colors),
			[(typeof(PortraitColor), nameof(PortraitColor.Color))] = new(colors),
			[(typeof(PortraitColor), nameof(PortraitColor.Mix))] = new(colors),
			[(typeof(PortraitColorVariant), nameof(PortraitColorVariant.Color))] = new(colors),
			[(typeof(PortraitPlaceCondition), nameof(PortraitPlaceCondition.QuarterKinds))] = new(quarterKinds),
			[(typeof(PortraitPlaceCondition), nameof(PortraitPlaceCondition.Workplaces))] = new(professions),
		};

		// The families' properties name the properties the game uses for each part of a household.
		foreach (var field in DataSchema.GetFields(typeof(FamilyProperties)).Where(x => x.Type.Kind == DataKind.Text))
			fields[(typeof(FamilyProperties), field.PropertyName)] = new(properties);

		return fields;
	}
}

/// <summary>
/// The ids that can be referred to, gathered from the game data and the mod's data files, for suggesting values.
/// </summary>
public sealed class IdCatalog
{
	/// <summary>
	/// Gathers the ids from the game data (without mods) and the mod's data files, and the items its operations insert.
	/// </summary>
	public IdCatalog(JsonObject? gameData, IEnumerable<ModFile> modFiles)
	{
		m_gameData = gameData;
		void AddItems(string section, IEnumerable<JsonNode?> items)
		{
			if (!m_items.TryGetValue(section, out var list))
				m_items[section] = list = [];
			list.AddRange(items.OfType<JsonObject>());
		}

		foreach (var (name, value) in gameData ?? [])
		{
			if (value is JsonArray items)
				AddItems(name, items);
		}

		foreach (var file in modFiles)
		{
			if (file.Root is JsonObject sections)
			{
				foreach (var (name, value) in sections)
				{
					if (value is JsonArray items)
						AddItems(name, items);
				}
			}
			else if (file.Root is JsonArray operations)
			{
				foreach (var operation in ModOperations.GetOperations(file.Path, operations).Select(x => x.Operation).OfType<JsonObject>())
				{
					// Items inserted into a list section can be referred to like the others.
					if (ModOperations.GetString(operation, "op") is { } op && string.Equals(op, "insert", StringComparison.OrdinalIgnoreCase) &&
						ModOperations.GetString(operation, "path") is { } path && !path.Contains('.', StringComparison.Ordinal) && !path.Contains('[', StringComparison.Ordinal))
					{
						var values = ModJson.FindName(operation, "values") is { } valuesName && operation[valuesName] is JsonArray list ? list.ToList()
							: ModJson.FindName(operation, "value") is { } valueName ? [operation[valueName]] : [];
						AddItems(path, values);
					}
				}
			}
		}
	}

	/// <summary>
	/// Gets the ids of a list section's items, without repeats, in the order they're found.
	/// </summary>
	public IReadOnlyList<string> GetIds(string section) =>
		m_items.FirstOrDefault(x => string.Equals(x.Key, section, StringComparison.OrdinalIgnoreCase)).Value?
			.Select(ModJson.GetId).OfType<string>().Distinct(StringComparer.Ordinal).ToList() ?? [];

	/// <summary>
	/// Gets a property's values and value groups (the professions, for the property with the profession role), or its
	/// integer groups.
	/// </summary>
	public IReadOnlyList<string> GetPropertyValues(string? propertyId)
	{
		var property = propertyId is null ? null : FindItem("properties", propertyId);
		if (property is null)
			return [];

		var values = GetString(property, "role") == "profession"
			? FindSection("professions")
			: (property.TryGetPropertyValue("values", out var list) && list is JsonArray items ? items.OfType<JsonObject>() : []);
		var ids = new List<string>();
		foreach (var value in values)
		{
			if (ModJson.GetId(value) is { } id)
				ids.Add(id);
			if (value.TryGetPropertyValue("groups", out var groups))
				ids.AddRange(groups is JsonArray groupList ? groupList.Select(x => x is JsonValue v && v.TryGetValue<string>(out var g) ? g : null).OfType<string>() : groups is JsonValue single && single.TryGetValue<string>(out var g2) ? [g2] : []);
		}

		// Integer properties name groups of numbers.
		if (property.TryGetPropertyValue("groups", out var integerGroups) && integerGroups is JsonArray integerGroupList)
			ids.AddRange(integerGroupList.Select(ModJson.GetId).OfType<string>());
		return ids.Distinct(StringComparer.Ordinal).ToList();
	}

	/// <summary>
	/// Gets the ids a source offers, with the property a value belongs to where the source needs one.
	/// </summary>
	public IReadOnlyList<string> GetIds(IdSource source, string? propertyId) => source switch
	{
		SectionIds x => GetIds(x.Section),
		PropertyValueIds { Others: { } others } when propertyId is not null && others.TryGetValue(propertyId, out var values) => values,
		PropertyValueIds => GetPropertyValues(propertyId),
		CombinedIds x => x.Sources.SelectMany(source => GetIds(source, propertyId)).Distinct(StringComparer.Ordinal).ToList(),
		ColorIds => GetIds("portraitColors"),
		FixedIds x => x.Names,
		_ => [],
	};

	/// <summary>
	/// Gets the colors a portrait shape or a layer's own color can use, given the object the field is in: the portrait
	/// colors, and in a layer, the layer's own colors and the color names of the clothing it draws. A shape set's shapes can
	/// use those of every layer that includes it. A layer or shape set that a form is editing on its own (or in an operation's
	/// value) isn't in its section, so the form gives the section, and for an operation, the id of the item it changes,
	/// which is used when the field isn't in a layer or shape set of its own, such as a shape or color set on its own.
	/// </summary>
	public IReadOnlyList<string> GetColorIds(JsonObject? owner, string? section = null, string? itemId = null)
	{
		var ids = new List<string>(GetIds("portraitColors"));
		var container = owner?.Parent is JsonArray shapes && shapes.Parent is JsonObject parent && shapes.GetPropertyName() is "shapes" or "colors" ? parent : null;
		var containerSection = container?.Parent is JsonArray items && items.Parent is JsonObject ? items.GetPropertyName() : section;
		if (container is null && itemId is not null && containerSection is "portraitLayers" or "portraitShapeSets")
			container = FindItem(containerSection, itemId);

		if (container is not null)
		{
			var isShapeSet = string.Equals(containerSection, "portraitShapeSets", StringComparison.OrdinalIgnoreCase) ||
				FindSection("portraitShapeSets").Contains(container);
			var layers = isShapeSet && (ModJson.GetId(container) ?? itemId) is { } setId
				? FindSection("portraitLayers").Where(layer => GetObjects(layer, "shapes").Any(shape => GetString(shape, "include") == setId))
				: [container];
			foreach (var layer in layers)
			{
				ids.AddRange(GetObjects(layer, "colors").Select(ModJson.GetId).OfType<string>());
				foreach (var clothingId in GetStrings(layer, "clothing"))
					ids.AddRange(FindItem("clothing", clothingId) is { } clothing ? GetStrings(clothing, "colors") : []);
			}
		}

		return ids.Distinct(StringComparer.Ordinal).ToList();
	}

	/// <summary>
	/// Gets paths for an operation that go one step further than the path typed so far: the sections, while the first
	/// step is being typed, and then the items of a list (by id or index), the properties of an object (those it has in
	/// the game data, and those the game reads there), or the keys of a map.
	/// </summary>
	public IReadOnlyList<string> GetPathSuggestions(string typed)
	{
		var cut = Math.Max(typed.LastIndexOf('.'), typed.LastIndexOf('['));
		if (cut <= 0)
		{
			return m_items.Keys.Concat(m_gameData?.Select(x => x.Key) ?? [])
				.Concat(DataSchema.ListSections.Keys).Concat(DataSchema.SettingsSections.Keys)
				.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
		}

		var parent = typed[..cut];
		var steps = DataSchema.ParsePath(parent);
		var suggestions = new List<string>();
		if (steps.Count == 1 && m_items.ContainsKey(steps[0].Text))
		{
			// A list section's items include the mod's.
			suggestions.AddRange(GetIds(steps[0].Text).Select(x => ModEditing.AppendItem(parent, new JsonObject { ["id"] = x }, 0)!));
		}
		else
		{
			switch (Navigate(steps))
			{
			case JsonArray list:
				suggestions.AddRange(list.Select((x, index) => ModEditing.AppendItem(parent, x, index)!));
				break;
			case JsonObject obj:
				suggestions.AddRange(obj.Select(x => ModEditing.AppendName(parent, x.Key)!));
				break;
			}
		}

		if (DataSchema.GetPathType(parent) is { Kind: DataKind.Object } type)
			suggestions.AddRange(type.Fields.Select(x => ModEditing.AppendName(parent, x.Name)!));
		return suggestions.Distinct(StringComparer.Ordinal).ToList();
	}

	/// <summary>
	/// Gets the ids of the items in the list a path leads to, such as "properties" or "properties[hair].values", for an
	/// operation's before and after. Lists within items are looked for in the game data.
	/// </summary>
	public IReadOnlyList<string> GetListIds(string path)
	{
		var steps = DataSchema.ParsePath(path);
		if (steps.Count == 0)
			return [];
		if (steps.Count == 1)
			return GetIds(steps[0].Text);
		return Navigate(steps) is JsonArray items ? items.Select(ModJson.GetId).OfType<string>().ToList() : [];
	}

	/// <summary>
	/// Follows a path's steps through the game data, returning what it leads to, or null if it leads nowhere.
	/// </summary>
	private JsonNode? Navigate(IReadOnlyList<(string Text, bool IsIndex)> steps)
	{
		JsonNode? node = m_gameData;
		foreach (var (text, isIndex) in steps)
		{
			node = node switch
			{
				JsonArray list when isIndex && int.TryParse(text, out var index) => index >= 0 && index < list.Count ? list[index] : null,
				JsonArray list => list.FirstOrDefault(x => ModJson.GetId(x) == text),
				JsonObject obj => ModJson.FindName(obj, text) is { } name ? obj[name] : null,
				_ => null,
			};
		}

		return node;
	}

	private IEnumerable<JsonObject> FindSection(string section) =>
		m_items.FirstOrDefault(x => string.Equals(x.Key, section, StringComparison.OrdinalIgnoreCase)).Value ?? [];

	// The mod's items come after the game data's, so the last one with the id is the one the mod changed.
	private JsonObject? FindItem(string section, string id) => FindSection(section).LastOrDefault(x => ModJson.GetId(x) == id);

	/// <summary>
	/// Gets a property that's a string or a list of strings, as a list.
	/// </summary>
	private static IReadOnlyList<string> GetStrings(JsonObject obj, string name) => obj.TryGetPropertyValue(name, out var node) ? node switch
	{
		JsonArray list => list.OfType<JsonValue>().Where(x => x.GetValueKind() == JsonValueKind.String).Select(x => x.GetValue<string>()).ToList(),
		JsonValue value when value.GetValueKind() == JsonValueKind.String => [value.GetValue<string>()],
		_ => [],
	} : [];

	private static IEnumerable<JsonObject> GetObjects(JsonObject obj, string name) => obj.TryGetPropertyValue(name, out var node) && node is JsonArray list ? list.OfType<JsonObject>() : [];

	private static string? GetString(JsonObject obj, string name) =>
		obj.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

	private readonly Dictionary<string, List<JsonObject>> m_items = new(StringComparer.OrdinalIgnoreCase);
	private readonly JsonObject? m_gameData;
}
