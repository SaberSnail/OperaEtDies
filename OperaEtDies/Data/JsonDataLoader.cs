using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;

namespace OperaEtDies.Data;

/// <summary>
/// Loads data files. Problems are returned as errors rather than thrown, since data files may come from outside the game.
/// </summary>
public static class JsonDataLoader
{
	/// <summary>
	/// The folder (next to the executable) that holds the game's data files.
	/// </summary>
	public static string DefaultDataDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Data");

	public static DataLoadResult<T> Load<T>(string filePath)
		where T : class => Load<T>(filePath, [], []);

	/// <summary>
	/// Loads a data file after applying the mod file with the same name from each mod folder that has one, in order.
	/// Mods can't change the protected top level properties. The value is prepared, if needed, before it's checked.
	/// </summary>
	public static DataLoadResult<T> Load<T>(string filePath, IReadOnlyList<string> modDirectories, IReadOnlyCollection<string> protectedProperties, Func<T, T>? prepare = null)
		where T : class
	{
		var result = LoadCore(filePath, modDirectories, protectedProperties, prepare);
		foreach (var error in result.Errors)
			Log.Error(error.ToString());
		return result;
	}

	private static DataLoadResult<T> LoadCore<T>(string filePath, IReadOnlyList<string> modDirectories, IReadOnlyCollection<string> protectedProperties, Func<T, T>? prepare)
		where T : class
	{
		var readResult = DataFileReader.Read(filePath, File.ReadAllText);
		if (readResult.Value is not { } json)
			return DataLoadResult<T>.Failure(readResult.Errors);

		// Without mods, the file is read directly, so problems are reported with their line numbers.
		var fileName = Path.GetFileName(filePath);
		var modFilePaths = modDirectories.Select(x => Path.Combine(x, fileName)).Where(File.Exists).ToList();
		return modFilePaths.Count == 0
			? Deserialize(filePath, null, options => JsonSerializer.Deserialize<T>(json, options), prepare)
			: LoadWithMods(filePath, json, modFilePaths, protectedProperties, prepare);
	}

	/// <summary>
	/// Reads a data file as JSON, after applying the mod file with the same name from each mod folder that has one, in
	/// order, without reading it as any type. Mods can't change the protected top level properties.
	/// </summary>
	public static DataLoadResult<JsonNode> ReadJson(string filePath, IReadOnlyList<string> modDirectories, IReadOnlyCollection<string> protectedProperties)
	{
		var readResult = DataFileReader.Read(filePath, File.ReadAllText);
		if (readResult.Value is not { } json)
			return DataLoadResult<JsonNode>.Failure(readResult.Errors);

		var fileName = Path.GetFileName(filePath);
		var modFilePaths = modDirectories.Select(x => Path.Combine(x, fileName)).Where(File.Exists).ToList();
		return ApplyMods(filePath, json, modFilePaths, protectedProperties);
	}

	/// <summary>
	/// Reads JSON (such as from <see cref="ReadJson"/>) as the given type and checks that it's valid. Problems are
	/// reported as coming from the given file, and from the source, if any.
	/// </summary>
	public static DataLoadResult<T> Deserialize<T>(string filePath, JsonNode root, string? source)
		where T : class => Deserialize(filePath, source, options => root.Deserialize<T>(options), null);

	private static DataLoadResult<T> LoadWithMods<T>(string filePath, string json, IReadOnlyList<string> modFilePaths, IReadOnlyCollection<string> protectedProperties, Func<T, T>? prepare)
		where T : class
	{
		var result = ApplyMods(filePath, json, modFilePaths, protectedProperties);
		return result.Value is { } root
			? Deserialize(filePath, OurResources.DataLoadErrorWithMods, options => root.Deserialize<T>(options), prepare)
			: DataLoadResult<T>.Failure(result.Errors);
	}

	private static DataLoadResult<JsonNode> ApplyMods(string filePath, string json, IReadOnlyList<string> modFilePaths, IReadOnlyCollection<string> protectedProperties)
	{
		JsonNode? root;
		try
		{
			root = JsonNode.Parse(json, documentOptions: s_documentOptions);
		}
		catch (JsonException e)
		{
			return DataLoadResult<JsonNode>.Failure(new DataLoadError(filePath, OurResources.DataFileInvalidJson.FormatCurrentCulture(e.Message)));
		}

		if (root is null)
			return DataLoadResult<JsonNode>.Failure(new DataLoadError(filePath, OurResources.DataFileEmpty));

		var errors = new List<DataLoadError>();
		foreach (var modFilePath in modFilePaths)
		{
			var source = OurResources.DataLoadErrorModSource.FormatCurrentCulture(Path.GetFileName(Path.GetDirectoryName(modFilePath)));
			var modReadResult = DataFileReader.Read(modFilePath, File.ReadAllText);
			if (modReadResult.Value is { } modJson)
			{
				try
				{
					var modFile = JsonNode.Parse(modJson, documentOptions: s_documentOptions);
					errors.AddRange(ModPatcher.Apply(root, modFile, protectedProperties).Select(x => new DataLoadError(filePath, x, source)));
				}
				// Objects with the same property twice are only found once their properties are used.
				catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
				{
					errors.Add(new DataLoadError(filePath, OurResources.DataFileInvalidJson.FormatCurrentCulture(e.Message), source));
				}
			}
			else
			{
				errors.AddRange(modReadResult.Errors.Select(x => x with { FilePath = filePath, Source = source }));
			}
		}

		return errors.Count != 0 ? DataLoadResult<JsonNode>.Failure(errors) : DataLoadResult<JsonNode>.Success(root);
	}

	/// <summary>
	/// Reads the contents of a data file as the given type, prepares it, and checks that it's valid.
	/// </summary>
	private static DataLoadResult<T> Deserialize<T>(string filePath, string? source, Func<JsonSerializerOptions, T?> deserialize, Func<T, T>? prepare)
		where T : class
	{
		T? value;
		try
		{
			value = deserialize(s_options);
		}
		catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
		{
			return DataLoadResult<T>.Failure(new DataLoadError(filePath, OurResources.DataFileInvalidJson.FormatCurrentCulture(e.Message), source));
		}

		if (value is null)
			return DataLoadResult<T>.Failure(new DataLoadError(filePath, OurResources.DataFileEmpty, source));

		if (prepare is not null)
			value = prepare(value);

		if (value is IValidatedData validatedData)
		{
			var errors = validatedData.Validate().Select(x => new DataLoadError(filePath, x, source)).ToList();
			if (errors.Count != 0)
				return DataLoadResult<T>.Failure(errors);
		}

		return DataLoadResult<T>.Success(value);
	}

	// Data files are hand edited, so be forgiving about comments and trailing commas.
	private static readonly JsonSerializerOptions s_options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		RespectNullableAnnotations = true,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
	};

	private static readonly JsonDocumentOptions s_documentOptions = new()
	{
		CommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(JsonDataLoader));
}
