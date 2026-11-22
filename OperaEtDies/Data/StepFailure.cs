using System.Text.Json;
using System.Text.Json.Serialization;

namespace OperaEtDies.Data;

/// <summary>
/// What happens when a step fails: one result for any cause, or a result for each cause. A cause it has no result for is
/// left to whatever the step is in (see <see cref="StepLayout.GetFailure"/>).
/// </summary>
[JsonConverter(typeof(StepFailureConverter))]
public sealed class StepFailure
{
	/// <summary>
	/// The result for any cause without one of its own, or null if there's none.
	/// </summary>
	public StepFailureResult? Any { get; init; }

	/// <summary>
	/// The results for particular causes.
	/// </summary>
	public IReadOnlyDictionary<FailCause, StepFailureResult> ByCause { get; init; } = new Dictionary<FailCause, StepFailureResult>();

	/// <summary>
	/// Gets the result for a cause, or null if there's none.
	/// </summary>
	public StepFailureResult? Get(FailCause cause) => ByCause.GetValueOrDefault(cause) ?? Any;

	/// <summary>
	/// Every result it has.
	/// </summary>
	internal IEnumerable<StepFailureResult> Results => Any is { } any ? ByCause.Values.Prepend(any) : ByCause.Values;
}

/// <summary>
/// What happens after a step fails.
/// </summary>
public sealed record StepFailureResult(StepFailureKind Kind, string? SkipTo = null);

/// <summary>
/// What happens after a step fails.
/// </summary>
public enum StepFailureKind
{
	/// <summary>
	/// Carry on with the step after it.
	/// </summary>
	Next,

	/// <summary>
	/// Give up the steps.
	/// </summary>
	Stop,

	/// <summary>
	/// Carry on from the step with an id.
	/// </summary>
	SkipTo,
}

/// <summary>
/// Why a step failed.
/// </summary>
public enum FailCause
{
	/// <summary>
	/// The one asked said no.
	/// </summary>
	No,

	/// <summary>
	/// There's no one to talk with, ask, lead, or find.
	/// </summary>
	Nobody,

	/// <summary>
	/// There's nowhere to go, or no way to get there.
	/// </summary>
	Unreachable,

	/// <summary>
	/// What the step needs isn't at hand.
	/// </summary>
	Missing,

	/// <summary>
	/// The character left the place partway through the steps there.
	/// </summary>
	Left,

	/// <summary>
	/// Something else took over, such as an appointment for another need.
	/// </summary>
	Interrupted,

	/// <summary>
	/// The step's hours ended, or it was reached outside them.
	/// </summary>
	Hours,

	/// <summary>
	/// The other didn't come to an appointment.
	/// </summary>
	NoShow,

	/// <summary>
	/// None of a "choose" step's options can be chosen.
	/// </summary>
	NoOption,
}

/// <summary>
/// Reads what happens when a step fails: "next" or "stop", an object with "skipTo" and a step's id, or an object with a
/// result for each cause (and, like any object in the data, comments).
/// </summary>
public sealed class StepFailureConverter : JsonConverter<StepFailure>
{
	public override StepFailure? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
			return new StepFailure { Any = ReadResult(ref reader) };

		var byCause = new Dictionary<FailCause, StepFailureResult>();
		StepFailureResult? any = null;
		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			var name = reader.GetString();
			reader.Read();
			if (string.Equals(name, SkipToName, StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.String)
				any = new StepFailureResult(StepFailureKind.SkipTo, reader.GetString());
			else if (string.Equals(name, DataFileParts.CommentsName, StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.String)
				continue;
			else if (Enum.TryParse<FailCause>(name, ignoreCase: true, out var cause) && !int.TryParse(name, out _))
				byCause[cause] = ReadResult(ref reader);
			else
				throw new JsonException(OurResources.StepFailureInvalid);
		}

		// An object is either a result to skip to a step, or results for causes, but not both.
		if (any is not null && byCause.Count != 0 || any is null && byCause.Count == 0)
			throw new JsonException(OurResources.StepFailureInvalid);
		return new StepFailure { Any = any, ByCause = byCause };
	}

	private static StepFailureResult ReadResult(ref Utf8JsonReader reader)
	{
		if (reader.TokenType == JsonTokenType.String)
		{
			var text = reader.GetString();
			return string.Equals(text, "next", StringComparison.OrdinalIgnoreCase) ? new StepFailureResult(StepFailureKind.Next) :
				string.Equals(text, "stop", StringComparison.OrdinalIgnoreCase) ? new StepFailureResult(StepFailureKind.Stop) :
				throw new JsonException(OurResources.StepFailureInvalid);
		}

		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException(OurResources.StepFailureInvalid);
		string? skipTo = null;
		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			var name = reader.GetString();
			reader.Read();
			if (string.Equals(name, SkipToName, StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.String)
				skipTo = reader.GetString();
			else if (!string.Equals(name, DataFileParts.CommentsName, StringComparison.OrdinalIgnoreCase) || reader.TokenType != JsonTokenType.String)
				throw new JsonException(OurResources.StepFailureInvalid);
		}

		return new StepFailureResult(StepFailureKind.SkipTo, skipTo ?? throw new JsonException(OurResources.StepFailureInvalid));
	}

	public override void Write(Utf8JsonWriter writer, StepFailure value, JsonSerializerOptions options)
	{
		if (value.ByCause.Count == 0 && value.Any is { Kind: not StepFailureKind.SkipTo } simple)
		{
			WriteResult(writer, simple);
			return;
		}

		writer.WriteStartObject();
		if (value.Any is { } any)
			writer.WriteString(SkipToName, any.SkipTo);
		foreach (var (cause, result) in value.ByCause)
		{
			writer.WritePropertyName(JsonNamingPolicy.CamelCase.ConvertName(cause.ToString()));
			WriteResult(writer, result);
		}

		writer.WriteEndObject();
	}

	private static void WriteResult(Utf8JsonWriter writer, StepFailureResult result)
	{
		if (result.Kind == StepFailureKind.SkipTo)
		{
			writer.WriteStartObject();
			writer.WriteString(SkipToName, result.SkipTo);
			writer.WriteEndObject();
		}
		else
		{
			writer.WriteStringValue(result.Kind == StepFailureKind.Next ? "next" : "stop");
		}
	}

	private const string SkipToName = "skipTo";
}

/// <summary>
/// A bundle a step takes or makes, which the character carries until a "put" step puts it down: its name, and for the
/// step that takes or makes it, what it's called.
/// </summary>
[JsonConverter(typeof(StepBundleConverter))]
public sealed class StepBundle
{
	public required string Id { get; init; }

	/// <summary>
	/// What the bundle is called, such as "Washing", or null where it's only referred to.
	/// </summary>
	public string? Title { get; init; }
}

/// <summary>
/// Reads a <see cref="StepBundle"/>: its id, or an object with its id and title (and, like any object in the data,
/// comments).
/// </summary>
public sealed class StepBundleConverter : JsonConverter<StepBundle>
{
	public override StepBundle? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.String)
			return new StepBundle { Id = reader.GetString()! };
		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException(OurResources.StepBundleInvalid);

		string? id = null;
		string? title = null;
		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			var name = reader.GetString();
			reader.Read();
			if (string.Equals(name, "id", StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.String)
				id = reader.GetString();
			else if (string.Equals(name, "title", StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.String)
				title = reader.GetString();
			else if (!string.Equals(name, DataFileParts.CommentsName, StringComparison.OrdinalIgnoreCase) || reader.TokenType != JsonTokenType.String)
				throw new JsonException(OurResources.StepBundleInvalid);
		}

		return new StepBundle { Id = id ?? throw new JsonException(OurResources.StepBundleInvalid), Title = title };
	}

	public override void Write(Utf8JsonWriter writer, StepBundle value, JsonSerializerOptions options)
	{
		if (value.Title is null)
		{
			writer.WriteStringValue(value.Id);
			return;
		}

		writer.WriteStartObject();
		writer.WriteString("id", value.Id);
		writer.WriteString("title", value.Title);
		writer.WriteEndObject();
	}
}
