using System.Text.Json.Nodes;
using OperaEtDies.Data;

namespace ModEditor.Model;

public enum ChangeKind
{
	Added,
	Removed,
	Changed,
}

/// <summary>
/// One difference the mod makes to the game data: what was added, removed, or changed at a path, with the values
/// before and after. <see cref="Section"/> and <see cref="Item"/> name the top-level piece it's in: a section, and the
/// id of a list item or the name of a setting in it (null for a whole section).
/// </summary>
public sealed record DataChange(string Path, ChangeKind Kind, JsonNode? Before, JsonNode? After, string Section, string? Item);

/// <summary>
/// What a mod changes in the game data: the differences between the game data with and without it, the data with it,
/// and which of the mod's files and operations made each difference.
/// </summary>
public sealed class ModChanges
{
	/// <summary>
	/// Applies the mod's files to the game data one at a time, in the order the game applies them: a data file is
	/// merged as a whole, and an operations file one operation at a time. The differences each makes are kept for it,
	/// and the differences the whole mod makes are worked out from the game data and the result. Can be run on any
	/// thread, given its own copy of the game data.
	/// </summary>
	public static ModChanges Compute(JsonObject gameData, IEnumerable<(string Path, string Text)> files)
	{
		var data = (JsonObject) gameData.DeepClone();
		var bySource = new Dictionary<ModLocation, IReadOnlyList<DataChange>>();
		var failed = new HashSet<ModLocation>();
		foreach (var (path, text) in files)
		{
			var parsed = ModJson.Parse(text);
			var parts = parsed.Error is null ? DataFileParts.Split(parsed.Node, allowOperations: true).Parts : null;
			switch ((JsonNode?) parts?.Sections ?? parts?.Operations)
			{
			case JsonObject sections:
				// Only the sections the file has can change.
				var before = Snapshot(data, sections.Select(x => x.Key));
				DataDocument.MergeSections(data, sections);
				bySource[new FileLocation(path)] = Compare(before, data);
				break;

			case JsonArray operations:
				foreach (var (location, operation) in ModOperations.GetOperations(path, operations))
				{
					var section = operation is JsonObject obj && ModOperations.GetString(obj, "path") is { } operationPath && DataSchema.ParsePath(operationPath) is [var first, ..]
						? first.Text
						: null;
					var touched = Snapshot(data, section is null ? data.Select(x => x.Key).ToList() : [section]);
					if (ModPatcher.Apply(data, new JsonArray(operation?.DeepClone()), DataDocument.ProtectedPaths).Count != 0)
						failed.Add(location);
					bySource[location] = Compare(touched, data);
				}

				break;
			}
		}

		var net = Compare(Snapshot(gameData, gameData.Select(x => x.Key).Concat(data.Select(x => x.Key))), data);
		return new ModChanges(net, data, bySource, failed);
	}

	private ModChanges(IReadOnlyList<DataChange> changes, JsonObject data, IReadOnlyDictionary<ModLocation, IReadOnlyList<DataChange>> bySource, IReadOnlySet<ModLocation> failed)
	{
		Changes = changes;
		Data = data;
		BySource = bySource;
		m_failed = failed;
	}

	/// <summary>
	/// An empty set of changes, for a mod with nothing in it.
	/// </summary>
	public static ModChanges None { get; } = new([], [], new Dictionary<ModLocation, IReadOnlyList<DataChange>>(), new HashSet<ModLocation>());

	/// <summary>
	/// The differences the whole mod makes, in the order they're found.
	/// </summary>
	public IReadOnlyList<DataChange> Changes { get; }

	/// <summary>
	/// The game data with the mod applied, as the game sees it (unless the mod has problems).
	/// </summary>
	public JsonObject Data { get; }

	/// <summary>
	/// The differences each data file and each operation made when it was applied, whether or not they last.
	/// </summary>
	public IReadOnlyDictionary<ModLocation, IReadOnlyList<DataChange>> BySource { get; }

	/// <summary>
	/// Gets the differences the whole mod makes to a section, or to a list item or setting in it.
	/// </summary>
	public IReadOnlyList<DataChange> GetChanges(string section, string? item) =>
		Changes.Where(x => string.Equals(x.Section, section, StringComparison.OrdinalIgnoreCase) && (item is null || x.Item == item)).ToList();

	/// <summary>
	/// Gets the files and operations whose changes touch a path (the path itself, or anything in or around it), in the
	/// order they're applied, so the last is the one whose value it has.
	/// </summary>
	public IReadOnlyList<ModLocation> GetSources(string path) =>
		BySource.Where(x => x.Value.Any(change => Overlaps(change.Path, path))).Select(x => x.Key).ToList();

	/// <summary>
	/// Describes what an operation does to the game data, for its tooltip and warning: null if its changes last, or why
	/// they don't (it changes nothing, it fails, or what it changes is changed back or again later).
	/// </summary>
	public string? GetOperationWarning(OperationLocation location)
	{
		if (m_failed.Contains(location))
			return "This operation fails, so it changes nothing (see Problems).";
		if (!BySource.TryGetValue(location, out var changes))
			return null;
		if (changes.Count == 0)
			return "This operation changes nothing in the data.";
		return changes.Any(change => Changes.Any(x => Overlaps(x.Path, change.Path)))
			? null
			: "What this operation changes is changed back later, so the mod doesn't change it in the end.";
	}

	/// <summary>
	/// Gets what the mod does to the data at a path: what happens to it, if a change is at the path; changed, if a
	/// change is inside it; added, if it's inside something added; or else null.
	/// </summary>
	public ChangeKind? GetChangeAt(string path)
	{
		if (Changes.LastOrDefault(x => x.Path == path) is { } exact)
			return exact.Kind;
		if (Changes.Any(x => IsWithin(x.Path, path)))
			return ChangeKind.Changed;
		return Changes.Any(x => x.Kind == ChangeKind.Added && IsWithin(path, x.Path)) ? ChangeKind.Added : null;
	}

	/// <summary>
	/// Gets the last of the mod's changes at, in, or around a path, whose part of the mod is the one to go to for it,
	/// or null if nothing there is changed.
	/// </summary>
	public DataChange? GetLastChangeAt(string path) => Changes.LastOrDefault(x => Overlaps(x.Path, path));

	/// <summary>
	/// Whether two paths are the same, or one is inside the other.
	/// </summary>
	public static bool Overlaps(string a, string b) =>
		a == b || IsWithin(a, b) || IsWithin(b, a);

	private static bool IsWithin(string path, string parent) =>
		path.Length > parent.Length && path.StartsWith(parent, StringComparison.Ordinal) && path[parent.Length] is '.' or '[';

	/// <summary>
	/// Copies the named sections, so what a step changes in them can be found afterwards.
	/// </summary>
	private static JsonObject Snapshot(JsonObject data, IEnumerable<string> sections)
	{
		var snapshot = new JsonObject();
		foreach (var section in sections.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			if (ModJson.FindName(data, section) is { } name)
				snapshot[name] = data[name]?.DeepClone();
			else
				snapshot[section] = null;
		}

		return snapshot;
	}

	/// <summary>
	/// Finds the differences between sections before a step and the same sections after it.
	/// </summary>
	private static IReadOnlyList<DataChange> Compare(JsonObject before, JsonObject after)
	{
		var changes = new List<DataChange>();
		foreach (var (section, old) in before)
		{
			var now = ModJson.FindName(after, section) is { } name ? after[name] : null;
			var sectionName = ModJson.FindName(after, section) ?? section;
			switch (old, now)
			{
			case (null, null):
				break;
			case (null, _):
				changes.Add(new DataChange(sectionName, ChangeKind.Added, null, now.DeepClone(), sectionName, null));
				break;
			case (_, null):
				changes.Add(new DataChange(sectionName, ChangeKind.Removed, old.DeepClone(), null, sectionName, null));
				break;
			case (JsonArray oldItems, JsonArray newItems):
				CompareLists(oldItems, newItems, sectionName, sectionName, isTop: true, changes);
				break;
			case (JsonObject oldSettings, JsonObject newSettings):
				foreach (var key in Keys(oldSettings, newSettings))
				{
					var path = ModEditing.AppendName(sectionName, key)!;
					CompareValues(Get(oldSettings, key), Get(newSettings, key), path, sectionName, key, changes);
				}

				break;
			default:
				CompareValues(old, now, sectionName, sectionName, null, changes);
				break;
			}
		}

		return changes;
	}

	private static void CompareValues(JsonNode? old, JsonNode? now, string path, string section, string? item, ICollection<DataChange> changes)
	{
		switch (old, now)
		{
		case (null, null):
			return;
		case (null, _):
			changes.Add(new DataChange(path, ChangeKind.Added, null, now.DeepClone(), section, item));
			return;
		case (_, null):
			changes.Add(new DataChange(path, ChangeKind.Removed, old.DeepClone(), null, section, item));
			return;
		case (JsonObject oldObject, JsonObject newObject):
			foreach (var key in Keys(oldObject, newObject))
				CompareValues(Get(oldObject, key), Get(newObject, key), ModEditing.AppendName(path, key)!, section, item, changes);
			return;
		case (JsonArray oldList, JsonArray newList):
			CompareLists(oldList, newList, path, section, isTop: false, changes, item);
			return;
		default:
			if (!JsonNode.DeepEquals(old, now))
				changes.Add(new DataChange(path, ChangeKind.Changed, old.DeepClone(), now.DeepClone(), section, item));
			return;
		}
	}

	/// <summary>
	/// Compares lists, matching their items by id when every item has a different one, or else by position. Items that
	/// only moved aren't differences. In a section, each item is its own top-level piece.
	/// </summary>
	private static void CompareLists(JsonArray old, JsonArray now, string path, string section, bool isTop, ICollection<DataChange> changes, string? item = null)
	{
		if (HaveIds(old) && HaveIds(now))
		{
			var oldById = old.ToDictionary(x => ModJson.GetId(x)!, StringComparer.Ordinal);
			var newById = now.ToDictionary(x => ModJson.GetId(x)!, StringComparer.Ordinal);
			foreach (var id in oldById.Keys.Concat(newById.Keys.Where(x => !oldById.ContainsKey(x))))
			{
				var itemPath = ModEditing.AppendItem(path, new JsonObject { ["id"] = id }, 0)!;
				CompareValues(oldById.GetValueOrDefault(id), newById.GetValueOrDefault(id), itemPath, section, isTop ? id : item, changes);
			}
		}
		else
		{
			for (var index = 0; index < Math.Max(old.Count, now.Count); index++)
			{
				var itemPath = ModEditing.AppendItem(path, null, index)!;
				CompareValues(index < old.Count ? old[index] : null, index < now.Count ? now[index] : null, itemPath, section, item, changes);
			}
		}
	}

	private static bool HaveIds(JsonArray list)
	{
		var ids = list.Select(ModJson.GetId).ToList();
		return ids.All(x => x is not null) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Count;
	}

	/// <summary>
	/// Gets the names of the properties of two objects, leaving out their comments, since changing them doesn't change
	/// the data.
	/// </summary>
	private static IEnumerable<string> Keys(JsonObject a, JsonObject b) =>
		a.Select(x => x.Key).Concat(b.Select(x => x.Key).Where(x => ModJson.FindName(a, x) is null)).Where(x => !IsComments(a, b, x));

	/// <summary>
	/// Whether a property is comments in both objects (or comments in one and missing from the other).
	/// </summary>
	private static bool IsComments(JsonObject a, JsonObject b, string key) =>
		(Get(a, key) is not { } before || ModJson.IsComments(key, before)) && (Get(b, key) is not { } after || ModJson.IsComments(key, after)) &&
		(Get(a, key) is not null || Get(b, key) is not null);

	private static JsonNode? Get(JsonObject obj, string key) => ModJson.FindName(obj, key) is { } name ? obj[name] : null;

	private readonly IReadOnlySet<ModLocation> m_failed;
}
