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
	/// Mods can't change the protected top level properties.
	/// </summary>
	public static DataLoadResult<T> Load<T>(string filePath, IReadOnlyList<string> modDirectories, IReadOnlyCollection<string> protectedProperties)
		where T : class
	{
		var result = LoadCore<T>(filePath, modDirectories, protectedProperties);
		foreach (var error in result.Errors)
			Log.Error(error.ToString());
		return result;
	}

	private static DataLoadResult<T> LoadCore<T>(string filePath, IReadOnlyList<string> modDirectories, IReadOnlyCollection<string> protectedProperties)
		where T : class
	{
		var readResult = DataFileReader.Read(filePath, File.ReadAllText);
		if (readResult.Value is not { } json)
			return DataLoadResult<T>.Failure(readResult.Errors);

		// Without mods, the file is read directly, so problems are reported with their line numbers.
		var fileName = Path.GetFileName(filePath);
		var modFilePaths = modDirectories.Select(x => Path.Combine(x, fileName)).Where(File.Exists).ToList();
		return modFilePaths.Count == 0
			? Deserialize(filePath, null, options => JsonSerializer.Deserialize<T>(json, options))
			: LoadWithMods<T>(filePath, json, modFilePaths, protectedProperties);
	}

	private static DataLoadResult<T> LoadWithMods<T>(string filePath, string json, IReadOnlyList<string> modFilePaths, IReadOnlyCollection<string> protectedProperties)
		where T : class
	{
		JsonNode? root;
		try
		{
			root = JsonNode.Parse(json, documentOptions: s_documentOptions);
		}
		catch (JsonException e)
		{
			return DataLoadResult<T>.Failure(new DataLoadError(filePath, OurResources.DataFileInvalidJson.FormatCurrentCulture(e.Message)));
		}

		if (root is null)
			return DataLoadResult<T>.Failure(new DataLoadError(filePath, OurResources.DataFileEmpty));

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

		return errors.Count != 0
			? DataLoadResult<T>.Failure(errors)
			: Deserialize(filePath, OurResources.DataLoadErrorWithMods, options => root.Deserialize<T>(options));
	}

	/// <summary>
	/// Reads the contents of a data file as the given type, and checks that it's valid.
	/// </summary>
	private static DataLoadResult<T> Deserialize<T>(string filePath, string? source, Func<JsonSerializerOptions, T?> deserialize)
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
