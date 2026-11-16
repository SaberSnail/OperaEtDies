using System.Text.Json;
using System.Text.Json.Nodes;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// What a data or mod file holds: either sections, with their comments (for the people editing the file, ignored by
/// the game), or a mod file's list of operations. A file of sections is an object, with an optional "comments"
/// property beside its sections. A list of operations has comments in its groups instead (see <see cref="ModPatcher"/>).
/// </summary>
public sealed record DataFileParts(string? Comments, JsonObject? Sections, JsonArray? Operations)
{
	public const string CommentsName = "comments";

	/// <summary>
	/// Splits a file's JSON into its parts. A file of sections has its comments taken out of it, so the sections are
	/// what's left of the file's object. A list of operations is only allowed when <paramref name="allowOperations"/> is
	/// true (for mod files). Returns the parts, or the problems that stop them from being read.
	/// </summary>
	public static (DataFileParts? Parts, IReadOnlyList<string> Errors) Split(JsonNode? file, bool allowOperations)
	{
		if (file is JsonArray operations && allowOperations)
			return (new DataFileParts(null, null, operations), []);
		if (file is not JsonObject sections)
			return (null, [allowOperations ? OurResources.ModFileInvalid : OurResources.DataFileNotObject]);

		string? comments = null;
		if (FindKey(sections, CommentsName) is { } commentsKey)
		{
			var value = sections[commentsKey];
			sections.Remove(commentsKey);
			if (value is not JsonValue text || text.GetValueKind() != JsonValueKind.String)
				return (null, [OurResources.DataFileCommentsInvalid.FormatCurrentCulture(commentsKey)]);
			comments = text.GetValue<string>();
		}

		return (new DataFileParts(comments, sections, null), []);
	}

	private static string? FindKey(JsonObject obj, string name) =>
		obj.ContainsKey(name) ? name : obj.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
}
