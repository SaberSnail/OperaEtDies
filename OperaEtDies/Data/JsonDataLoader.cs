using System.IO;
using System.Text.Json;
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
		where T : class
	{
		var result = LoadCore<T>(filePath);
		foreach (var error in result.Errors)
			Log.Error(error.ToString());
		return result;
	}

	private static DataLoadResult<T> LoadCore<T>(string filePath)
		where T : class
	{
		var readResult = DataFileReader.Read(filePath, File.ReadAllText);
		if (readResult.Value is not { } json)
			return DataLoadResult<T>.Failure(readResult.Errors);

		T? value;
		try
		{
			value = JsonSerializer.Deserialize<T>(json, s_options);
		}
		catch (JsonException e)
		{
			return DataLoadResult<T>.Failure(new DataLoadError(filePath, OurResources.DataFileInvalidJson.FormatCurrentCulture(e.Message)));
		}

		if (value is null)
			return DataLoadResult<T>.Failure(new DataLoadError(filePath, OurResources.DataFileEmpty));

		if (value is IValidatedData validatedData)
		{
			var errors = validatedData.Validate().Select(x => new DataLoadError(filePath, x)).ToList();
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
	};

	private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(JsonDataLoader));
}
