using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
	/// are errors, since they're probably misspelled. Every object read into a class can have "comments" (see
	/// <see cref="AllowComments"/>).
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
		TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { AllowComments } },
	};

	/// <summary>
	/// Lets an object read into a class have a "comments" property, which must be text and is ignored. Comments are for
	/// the people editing the data. Maps (objects whose property names are keys) and classes with converters of their
	/// own aren't changed.
	/// </summary>
	private static void AllowComments(JsonTypeInfo typeInfo)
	{
		if (typeInfo.Kind != JsonTypeInfoKind.Object || typeInfo.Properties.Any(x => string.Equals(x.Name, DataFileParts.CommentsName, StringComparison.OrdinalIgnoreCase)))
			return;

		var comments = typeInfo.CreateJsonPropertyInfo(typeof(string), DataFileParts.CommentsName);
		comments.Set = (_, _) => { };
		comments.IsSetNullable = false;
		typeInfo.Properties.Add(comments);
	}
}
