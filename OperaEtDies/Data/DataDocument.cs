using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// The data folder's JSON files merged into one document, with the mod folders' files applied in order. Each top-level
/// section of a file is either a list of identified items, which are joined across files, or a settings object, whose
/// properties are joined across files. The document remembers which file each item and setting came from, so problems
/// can be reported against the file that has them.
/// </summary>
public sealed partial class DataDocument
{
	/// <summary>
	/// The settings mods can't change, as paths from the top of the document.
	/// </summary>
	public static IReadOnlyList<string> ProtectedPaths { get; } = ["game.title", "game.icon"];

	/// <summary>
	/// Reads and merges every JSON file in the data folder (including its subfolders), in order of their paths, then
	/// applies the files of each mod folder in order. A mod file holds either operations or data, which is merged in the
	/// same way as a data file. Files' comments are left out (see <see cref="DataFileParts"/>).
	/// </summary>
	public static DataLoadResult<DataDocument> Read(string dataDirectory, IReadOnlyList<string> modDirectories)
	{
		var document = new DataDocument(dataDirectory);
		var errors = new List<DataLoadError>();
		if (Directory.Exists(dataDirectory))
		{
			foreach (var filePath in GetJsonFiles(dataDirectory))
			{
				if (ReadFile(filePath, null, errors) is { } file)
				{
					var (parts, fileErrors) = DataFileParts.Split(file, allowOperations: false);
					errors.AddRange(fileErrors.Select(x => new DataLoadError(filePath, x)));
					if (parts?.Sections is { } data)
						document.Merge(data, filePath, null, errors);
				}
			}
		}
		else
		{
			errors.Add(new DataLoadError(dataDirectory, OurResources.DataFolderNotFound.FormatCurrentCulture(dataDirectory)));
		}

		foreach (var modDirectory in modDirectories)
		{
			var source = OurResources.DataLoadErrorModSource.FormatCurrentCulture(Path.GetFileName(modDirectory));
			foreach (var filePath in GetJsonFiles(modDirectory))
			{
				document.m_hasMods = true;
				if (ReadFile(filePath, source, errors) is { } file)
				{
					var (parts, fileErrors) = DataFileParts.Split(file, allowOperations: true);
					errors.AddRange(fileErrors.Select(x => new DataLoadError(filePath, x, source)));
					if (parts?.Sections is { } data)
						document.Merge(data, filePath, source, errors);
					else if (parts?.Operations is { } operations)
						errors.AddRange(ModPatcher.Apply(document.Root, operations, ProtectedPaths).Select(x => new DataLoadError(filePath, x, source)));
				}
			}
		}

		return errors.Count != 0 ? DataLoadResult<DataDocument>.Failure(errors) : DataLoadResult<DataDocument>.Success(document);
	}

	private DataDocument(string dataDirectory)
	{
		DataDirectory = dataDirectory;
		Root = new JsonObject(new JsonNodeOptions { PropertyNameCaseInsensitive = true });
	}

	/// <summary>
	/// The data folder the document was read from.
	/// </summary>
	public string DataDirectory { get; }

	/// <summary>
	/// The merged data, with the mods applied.
	/// </summary>
	public JsonObject Root { get; }

	/// <summary>
	/// Reads one top-level section of the document as the given type, or returns the default value if the document
	/// doesn't have it. Problems are added to the errors, reported against the files the section's data came from.
	/// </summary>
	public T? ReadSection<T>(string name, ICollection<DataLoadError> errors)
		where T : class
	{
		if (GetSection(name) is not { } node)
			return null;

		try
		{
			return node.Deserialize<T>(JsonDataLoader.Options);
		}
		catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
		{
			var path = e is JsonException { Path: { } jsonPath } ? name + jsonPath.TrimStart('$') : name;
			errors.Add(CreateError(path, OurResources.DataFileInvalidJson.FormatCurrentCulture(e.Message)));
			return null;
		}
	}

	/// <summary>
	/// Gets a top-level section of the document, or null if it doesn't have one.
	/// </summary>
	public JsonNode? GetSection(string name) => Root.TryGetPropertyValue(name, out var node) ? node : null;

	/// <summary>
	/// Creates an error for a problem found in the document. The error is reported against the file holding the item
	/// or setting the message refers to (by a path such as "needs[sleep].hours", or by a quoted id), or against the data
	/// folder if that isn't known.
	/// </summary>
	public DataLoadError CreateError(string message) => CreateError(null, message);

	/// <summary>
	/// Gets the file that the item with the given id, in a top-level list, or the setting with the given name, in a
	/// top-level settings object, came from, or null if it isn't known.
	/// </summary>
	public string? FindFile(string section, string key) =>
		m_origins.TryGetValue(OriginKey(section, key), out var origin) ? origin.FilePath : null;

	/// <summary>
	/// Gets the first file that any data in a top-level section came from, or null if it isn't known.
	/// </summary>
	public string? FindSectionFile(string section) =>
		m_origins.Where(x => x.Key.StartsWith(OriginKey(section, ""), StringComparison.Ordinal)).Select(x => x.Value.FilePath).FirstOrDefault();

	private DataLoadError CreateError(string? path, string message)
	{
		var origin = (path is null ? null : FindOrigin(path)) ?? FindOrigin(message);
		var source = origin?.Source ?? (m_hasMods ? OurResources.DataLoadErrorWithMods : null);
		return new DataLoadError(origin?.FilePath ?? DataDirectory, message, source);
	}

	/// <summary>
	/// Finds where the text refers to: the first path whose section and id or setting are known, or else the first
	/// quoted id of an item in any section.
	/// </summary>
	private Origin? FindOrigin(string text)
	{
		foreach (Match match in PathRegex().Matches(text))
		{
			var section = match.Groups["section"].Value;
			var key = match.Groups["index"].Success ? GetItemId(section, match.Groups["index"].Value) : match.Groups["key"].Value;
			if (key is not null && m_origins.TryGetValue(OriginKey(section, key), out var origin))
				return origin;
		}

		foreach (Match match in QuotedRegex().Matches(text))
		{
			if (m_idOrigins.TryGetValue(match.Groups[1].Value, out var origin))
				return origin;
		}

		return null;
	}

	/// <summary>
	/// Gets the id of the item a list step refers to, which is either its id or its index.
	/// </summary>
	private string? GetItemId(string section, string step)
	{
		if (!int.TryParse(step, out var index))
			return step;
		return GetSection(section) is JsonArray list && index >= 0 && index < list.Count ? GetId(list[index]) : null;
	}

	private static JsonNode? ReadFile(string filePath, string? source, ICollection<DataLoadError> errors)
	{
		var readResult = DataFileReader.Read(filePath, File.ReadAllText);
		if (readResult.Value is not { } json)
		{
			foreach (var error in readResult.Errors)
				errors.Add(error with { Source = source });
			return null;
		}

		try
		{
			var node = JsonNode.Parse(json, new JsonNodeOptions { PropertyNameCaseInsensitive = true }, s_documentOptions);
			if (node is null)
				errors.Add(new DataLoadError(filePath, OurResources.DataFileEmpty, source));
			return node;
		}
		catch (JsonException e)
		{
			errors.Add(new DataLoadError(filePath, OurResources.DataFileInvalidJson.FormatCurrentCulture(e.Message), source));
			return null;
		}
	}

	/// <summary>
	/// Merges a data file's sections into merged data, by the same rules as <see cref="Read"/>, but without checking the
	/// file or remembering where its data came from. A list section's items are added to the list, except an item whose
	/// id the list already has; a settings section's settings are added, except one that's already set; and a section of
	/// a different kind than the one already there is left out. (Read reports each of those as an error.) The file's
	/// data is copied, so the file is left as it was. Tools use it to apply a mod one file at a time.
	/// </summary>
	public static void MergeSections(JsonObject root, JsonObject file)
	{
		foreach (var (name, value) in file)
		{
			var sectionName = FindKey(root, name);
			if (value is JsonArray items)
			{
				if (sectionName is null)
					root[sectionName = name] = new JsonArray();
				if (root[sectionName] is JsonArray list)
				{
					var ids = list.Select(GetId).OfType<string>().ToHashSet(StringComparer.Ordinal);
					foreach (var item in items)
					{
						if (GetId(item) is not { } id || ids.Add(id))
							list.Add(item?.DeepClone());
					}
				}
			}
			else if (value is JsonObject settings)
			{
				if (sectionName is null)
					root[sectionName = name] = new JsonObject(new JsonNodeOptions { PropertyNameCaseInsensitive = true });
				if (root[sectionName] is JsonObject target)
				{
					foreach (var (key, setting) in settings)
					{
						if (FindKey(target, key) is null)
							target[key] = setting?.DeepClone();
					}
				}
			}
		}
	}

	/// <summary>
	/// Merges the top-level sections of a file into the document. Lists are joined, and an id already used in the list
	/// is an error. Settings objects are joined, and a setting already set is an error.
	/// </summary>
	private void Merge(JsonObject file, string filePath, string? source, ICollection<DataLoadError> errors)
	{
		var origin = new Origin(filePath, source);
		foreach (var (name, value) in file.ToList())
		{
			file.Remove(name);
			var existing = GetSection(name);
			var sectionName = existing is null ? name : FindKey(Root, name) ?? name;
			if (value is JsonArray items)
			{
				if (existing is null)
					Root[name] = new JsonArray();
				if (GetSection(sectionName) is JsonArray list)
				{
					foreach (var item in items.ToList())
					{
						items.Remove(item);
						if (GetId(item) is { } id)
						{
							if (m_origins.TryGetValue(OriginKey(sectionName, id), out var other))
							{
								errors.Add(new DataLoadError(filePath, OurResources.DataIdDefinedTwice.FormatCurrentCulture(id, sectionName, DisplayPath(other.FilePath)), source));
								continue;
							}

							m_origins[OriginKey(sectionName, id)] = origin;
							m_idOrigins.TryAdd(id, origin);
						}

						list.Add(item);
					}
				}
				else
				{
					errors.Add(new DataLoadError(filePath, OurResources.DataSectionKindMismatch.FormatCurrentCulture(sectionName), source));
				}
			}
			else if (value is JsonObject settings)
			{
				if (existing is null)
					Root[name] = new JsonObject(new JsonNodeOptions { PropertyNameCaseInsensitive = true });
				if (GetSection(sectionName) is JsonObject target)
				{
					foreach (var (key, setting) in settings.ToList())
					{
						settings.Remove(key);
						if (FindKey(target, key) is { } existingKey)
						{
							var other = m_origins.GetValueOrDefault(OriginKey(sectionName, existingKey));
							errors.Add(new DataLoadError(filePath, OurResources.DataSettingDefinedTwice.FormatCurrentCulture($"{sectionName}.{key}", DisplayPath(other?.FilePath ?? DataDirectory)), source));
						}
						else
						{
							target[key] = setting;
							m_origins[OriginKey(sectionName, key)] = origin;
						}
					}
				}
				else
				{
					errors.Add(new DataLoadError(filePath, OurResources.DataSectionKindMismatch.FormatCurrentCulture(sectionName), source));
				}
			}
			else
			{
				errors.Add(new DataLoadError(filePath, OurResources.DataSectionInvalid.FormatCurrentCulture(name), source));
			}
		}
	}

	/// <summary>
	/// Gets a file's path relative to the data folder, or its full path if it's somewhere else, such as in a mod folder.
	/// </summary>
	private string DisplayPath(string filePath)
	{
		var relative = Path.GetRelativePath(DataDirectory, filePath);
		return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? filePath : relative.Replace(Path.DirectorySeparatorChar, '/');
	}

	private static string? FindKey(JsonObject node, string name) =>
		node.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));

	private static string? GetId(JsonNode? item) =>
		item is JsonObject obj && obj.TryGetPropertyValue("id", out var id) && id is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

	private static string OriginKey(string section, string key) => $"{section.ToLowerInvariant()}\n{key}";

	private static IReadOnlyList<string> GetJsonFiles(string directory)
	{
		try
		{
			return Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
				.OrderBy(x => Path.GetRelativePath(directory, x).Replace('\\', '/'), StringComparer.Ordinal)
				.ToList();
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return [];
		}
	}

	[GeneratedRegex(@"(?<section>[A-Za-z]+)(?:\[(?<index>[^\]]+)\]|\.(?<key>[A-Za-z]+))")]
	private static partial Regex PathRegex();

	[GeneratedRegex("\"([^\"]+)\"")]
	private static partial Regex QuotedRegex();

	private sealed record Origin(string FilePath, string? Source);

	private static readonly JsonDocumentOptions s_documentOptions = new()
	{
		CommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};

	private readonly Dictionary<string, Origin> m_origins = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Origin> m_idOrigins = new(StringComparer.Ordinal);
	private bool m_hasMods;
}
