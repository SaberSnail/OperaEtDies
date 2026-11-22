using System.Text.Json;
using System.Text.Json.Serialization;

namespace OperaEtDies.Data;

/// <summary>
/// A length of time: a number of minutes, or text with hours and minutes ("1:30"), or days, hours, and minutes
/// ("2:15:35"). It converts to and from a number of minutes.
/// </summary>
[JsonConverter(typeof(DurationConverter))]
public readonly record struct Duration(int Minutes)
{
	public static implicit operator int(Duration duration) => duration.Minutes;

	public static implicit operator Duration(int minutes) => new(minutes);
}

/// <summary>
/// Reads a <see cref="Duration"/>.
/// </summary>
public sealed class DurationConverter : JsonConverter<Duration>
{
	public override Duration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var minutes) && minutes >= 0)
			return new Duration(minutes);
		if (reader.TokenType == JsonTokenType.String && Parse(reader.GetString()) is { } parsed)
			return parsed;
		throw new JsonException(OurResources.DurationInvalid);
	}

	/// <summary>
	/// Reads a duration from text with hours and minutes, or days, hours, and minutes, or returns null if it isn't one.
	/// </summary>
	public static Duration? Parse(string? text)
	{
		var parts = text?.Split(':');
		if (parts is null || parts.Length is < 2 or > 3 || parts.Any(x => x.Length == 0 || !x.All(char.IsAsciiDigit) || x.Length > 6))
			return null;

		var numbers = parts.Select(int.Parse).ToList();
		var (days, hours, minutes) = numbers.Count == 3 ? (numbers[0], numbers[1], numbers[2]) : (0, numbers[0], numbers[1]);
		return minutes > 59 || (numbers.Count == 3 && hours > 23) || parts[^1].Length != 2 ? null : new Duration((((days * 24) + hours) * 60) + minutes);
	}

	public override void Write(Utf8JsonWriter writer, Duration value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Minutes);
}

/// <summary>
/// A range of lengths of time, and how likely each number of minutes in it is to be chosen. It converts to and from an
/// <see cref="IntegerRange"/> of minutes.
/// </summary>
public sealed class DurationRange
{
	public required Duration Minimum { get; init; }

	public required Duration Maximum { get; init; }

	/// <summary>
	/// Weights the numbers of minutes in the range by their order, or null for every number to be equally likely.
	/// </summary>
	public WeightShape? Shape { get; init; }

	public static implicit operator IntegerRange(DurationRange range) => new() { Minimum = range.Minimum, Maximum = range.Maximum, Shape = range.Shape };

	public static implicit operator DurationRange(IntegerRange range) => new() { Minimum = range.Minimum, Maximum = range.Maximum, Shape = range.Shape };
}
