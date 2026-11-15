using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OperaEtDies.Data;

/// <summary>
/// Settings shared by everything that reads data files.
/// </summary>
public static class JsonDataLoader
{
	/// <summary>
	/// The folder (next to the executable) that holds the game's data files.
	/// </summary>
	public static string DefaultDataDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Data");

	/// <summary>
	/// The options data is read with. Data files are hand edited, so property names ignore case, but unknown properties
	/// are errors, since they're probably misspelled.
	/// </summary>
	public static JsonSerializerOptions Options { get; } = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		RespectNullableAnnotations = true,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false), new StringListConverter() },
	};
}
