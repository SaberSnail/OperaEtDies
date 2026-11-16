using System.Text.Json;
using System.Text.Json.Serialization;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// Reads a list of strings, or a single string as a list of one.
/// </summary>
public sealed class StringListConverter : JsonConverter<IReadOnlyList<string>>
{
	public override IReadOnlyList<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.String)
			return [reader.GetString()!];
		if (reader.TokenType != JsonTokenType.StartArray)
			throw new JsonException(OurResources.DataStringListInvalid);

		var items = new List<string>();
		while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
		{
			if (reader.TokenType == JsonTokenType.String)
				items.Add(reader.GetString()!);
			else if (reader.TokenType == JsonTokenType.Null)
				items.Add(null!);
			else
				throw new JsonException(OurResources.DataStringListInvalid);
		}

		return items;
	}

	public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
	{
		writer.WriteStartArray();
		foreach (var item in value)
			writer.WriteStringValue(item);
		writer.WriteEndArray();
	}
}

/// <summary>
/// Reads an amount that can grow with the size of the family: a number, or an object with a base and an amount for each
/// family member (and, like any object in the data, comments).
/// </summary>
public sealed class FamilyAmountConverter : JsonConverter<FamilyAmount>
{
	public override FamilyAmount? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Number)
			return new FamilyAmount { Base = reader.GetDouble() };
		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException(OurResources.FamilyAmountInvalid);

		double? baseAmount = null;
		double? perMember = null;
		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			var name = reader.GetString();
			reader.Read();
			if (string.Equals(name, "base", StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.Number)
				baseAmount = reader.GetDouble();
			else if (string.Equals(name, "perMember", StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.Number)
				perMember = reader.GetDouble();
			else if (string.Equals(name, DataFileParts.CommentsName, StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.String)
				continue;
			else
				throw new JsonException(OurResources.FamilyAmountInvalid);
		}

		if (baseAmount is null && perMember is null)
			throw new JsonException(OurResources.FamilyAmountInvalid);
		return new FamilyAmount { Base = baseAmount ?? 0, PerMember = perMember };
	}

	public override void Write(Utf8JsonWriter writer, FamilyAmount value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WriteNumber("base", value.Base);
		if (value.PerMember is { } perMember)
			writer.WriteNumber("perMember", perMember);
		writer.WriteEndObject();
	}
}

/// <summary>
/// Reads where a need is met: "home", "workplace", or an object with the kinds of quarter.
/// </summary>
public sealed class SchedulePlaceConverter : JsonConverter<SchedulePlace>
{
	public override SchedulePlace? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.String)
		{
			var text = reader.GetString();
			return string.Equals(text, "home", StringComparison.OrdinalIgnoreCase) ? new SchedulePlace { Kind = NeedPlace.Home }
				: string.Equals(text, "workplace", StringComparison.OrdinalIgnoreCase) ? new SchedulePlace { Kind = NeedPlace.Workplace }
				: throw new JsonException(OurResources.SchedulePlaceInvalid);
		}

		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException(OurResources.SchedulePlaceInvalid);

		var quarters = JsonSerializer.Deserialize<QuarterPlace>(ref reader, options) ?? throw new JsonException(OurResources.SchedulePlaceInvalid);
		return new SchedulePlace { Kind = NeedPlace.Quarter, QuarterKinds = quarters.QuarterKinds, Nearest = quarters.Nearest };
	}

	public override void Write(Utf8JsonWriter writer, SchedulePlace value, JsonSerializerOptions options)
	{
		if (value.Kind == NeedPlace.Quarter)
			JsonSerializer.Serialize(writer, new QuarterPlace { QuarterKinds = value.QuarterKinds ?? [], Nearest = value.Nearest }, options);
		else
			writer.WriteStringValue(JsonNamingPolicy.CamelCase.ConvertName(value.Kind.ToString()));
	}

	private sealed class QuarterPlace
	{
		public required IReadOnlyList<QuarterChoice> QuarterKinds { get; init; }

		public bool Nearest { get; init; }
	}
}
