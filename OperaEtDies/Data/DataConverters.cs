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
		var (baseAmount, perMember) = ReadParts(ref reader, isDuration: false);
		return new FamilyAmount { Base = baseAmount, PerMember = perMember };
	}

	public override void Write(Utf8JsonWriter writer, FamilyAmount value, JsonSerializerOptions options) => WriteParts(writer, value);

	/// <summary>
	/// Reads the base amount and the amount for each family member, either of which can be a duration's text if the
	/// amount is a length of time.
	/// </summary>
	internal static (double Base, double? PerMember) ReadParts(ref Utf8JsonReader reader, bool isDuration)
	{
		var invalid = isDuration ? OurResources.FamilyDurationInvalid : OurResources.FamilyAmountInvalid;
		if (ReadNumber(ref reader, isDuration) is { } amount)
			return (amount, null);
		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException(invalid);

		double? baseAmount = null;
		double? perMember = null;
		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			var name = reader.GetString();
			reader.Read();
			if (string.Equals(name, "base", StringComparison.OrdinalIgnoreCase) && ReadNumber(ref reader, isDuration) is { } readBase)
				baseAmount = readBase;
			else if (string.Equals(name, "perMember", StringComparison.OrdinalIgnoreCase) && ReadNumber(ref reader, isDuration) is { } readPerMember)
				perMember = readPerMember;
			else if (string.Equals(name, DataFileParts.CommentsName, StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.String)
				continue;
			else
				throw new JsonException(invalid);
		}

		if (baseAmount is null && perMember is null)
			throw new JsonException(invalid);
		return (baseAmount ?? 0, perMember);
	}

	internal static void WriteParts(Utf8JsonWriter writer, FamilyAmount value)
	{
		writer.WriteStartObject();
		writer.WriteNumber("base", value.Base);
		if (value.PerMember is { } perMember)
			writer.WriteNumber("perMember", perMember);
		writer.WriteEndObject();
	}

	/// <summary>
	/// Reads a number, or for a length of time, a duration's text, or returns null if the value isn't one.
	/// </summary>
	private static double? ReadNumber(ref Utf8JsonReader reader, bool isDuration) =>
		reader.TokenType == JsonTokenType.Number ? reader.GetDouble() :
		isDuration && reader.TokenType == JsonTokenType.String && DurationConverter.Parse(reader.GetString()) is { } duration ? duration.Minutes :
		null;
}

/// <summary>
/// Reads a length of time that can grow with the size of the family: like a <see cref="FamilyAmount"/>, except that the
/// base and the amount for each family member can also be a duration's text.
/// </summary>
public sealed class FamilyDurationConverter : JsonConverter<FamilyDuration>
{
	public override FamilyDuration? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var (baseAmount, perMember) = FamilyAmountConverter.ReadParts(ref reader, isDuration: true);
		return new FamilyDuration { Base = baseAmount, PerMember = perMember };
	}

	public override void Write(Utf8JsonWriter writer, FamilyDuration value, JsonSerializerOptions options) => FamilyAmountConverter.WriteParts(writer, value);
}

/// <summary>
/// Reads a place: "home", "workplace", "otherHome", or an object with the kinds of quarter or a remembered place's name.
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
				: string.Equals(text, "otherHome", StringComparison.OrdinalIgnoreCase) ? new SchedulePlace { Kind = NeedPlace.OtherHome }
				: string.Equals(text, "partnerHome", StringComparison.OrdinalIgnoreCase) ? new SchedulePlace { Kind = NeedPlace.PartnerHome }
				: string.Equals(text, "jobSite", StringComparison.OrdinalIgnoreCase) ? new SchedulePlace { Kind = NeedPlace.JobSite }
				: throw new JsonException(OurResources.SchedulePlaceInvalid);
		}

		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException(OurResources.SchedulePlaceInvalid);

		// An object is a quarter, with its kinds, or a remembered place, with its name, but not both.
		var place = JsonSerializer.Deserialize<ObjectPlace>(ref reader, options) ?? throw new JsonException(OurResources.SchedulePlaceInvalid);
		return place switch
		{
			{ Remembered: { } name, QuarterKinds: null, Nearest: false } => new SchedulePlace { Kind = NeedPlace.Remembered, Remembered = name },
			{ Remembered: null, QuarterKinds: { } kinds } => new SchedulePlace { Kind = NeedPlace.Quarter, QuarterKinds = kinds, Nearest = place.Nearest },
			_ => throw new JsonException(OurResources.SchedulePlaceInvalid),
		};
	}

	public override void Write(Utf8JsonWriter writer, SchedulePlace value, JsonSerializerOptions options)
	{
		if (value.Kind == NeedPlace.Quarter)
			JsonSerializer.Serialize(writer, new ObjectPlace { QuarterKinds = value.QuarterKinds ?? [], Nearest = value.Nearest }, options);
		else if (value.Kind == NeedPlace.Remembered)
			JsonSerializer.Serialize(writer, new ObjectPlace { Remembered = value.Remembered }, options);
		else
			writer.WriteStringValue(JsonNamingPolicy.CamelCase.ConvertName(value.Kind.ToString()));
	}

	private sealed class ObjectPlace
	{
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public IReadOnlyList<QuarterChoice>? QuarterKinds { get; init; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
		public bool Nearest { get; init; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Remembered { get; init; }
	}
}
