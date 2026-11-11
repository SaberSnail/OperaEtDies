using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OperaEtDies.Data;

/// <summary>
/// Where the peak of a <see cref="WeightShape"/> is: low, middle, or high, moved up or down by up to
/// <see cref="MaxSteps"/> steps of <see cref="StepSize"/> of the list. Data files write it as the position followed by
/// "+" or "-" for each step, such as "middle--" or "low+".
/// </summary>
[JsonConverter(typeof(PeakLevelJsonConverter))]
public readonly record struct PeakLevel(ShapePeak Position, int Steps = 0)
{
	public const int MaxSteps = 2;

	/// <summary>
	/// How far each step moves the peak, as a fraction of the distance from the first value to the last.
	/// </summary>
	public const double StepSize = 0.1;

	public static implicit operator PeakLevel(ShapePeak position) => new(position);

	/// <summary>
	/// Whether the peak is within the list: low can't move down, and high can't move up.
	/// </summary>
	public bool IsValid => !(Position == ShapePeak.Low && Steps < 0) && !(Position == ShapePeak.High && Steps > 0);

	/// <summary>
	/// Gets where the peak is, as a fraction of the distance from the first value to the last.
	/// </summary>
	public double GetFraction() => Position switch
	{
		ShapePeak.Low => 0,
		ShapePeak.Middle => 0.5,
		ShapePeak.High => 1,
		_ => throw new InvalidOperationException($"Unknown shape peak {Position}."),
	} + Steps * StepSize;

	/// <summary>
	/// Parses a peak level, ignoring case. Returns false if the text isn't a position followed by up to
	/// <see cref="MaxSteps"/> of either "+" or "-".
	/// </summary>
	public static bool TryParse(string? text, [NotNullWhen(true)] out PeakLevel? level)
	{
		level = null;
		if (text is not null)
		{
			var name = text.TrimEnd('+', '-');
			var suffix = text[name.Length..];
			var isValidSuffix = suffix.Length <= MaxSteps && (suffix.All(x => x == '+') || suffix.All(x => x == '-'));
			if (isValidSuffix && s_positionsByName.TryGetValue(name, out var position))
				level = new PeakLevel(position, suffix.StartsWith('-') ? -suffix.Length : suffix.Length);
		}

		return level is not null;
	}

	public override string ToString()
	{
		var position = Position;
		var name = s_positionsByName.First(x => x.Value == position).Key;
		return name + new string(Steps < 0 ? '-' : '+', Math.Abs(Steps));
	}

	private static readonly Dictionary<string, ShapePeak> s_positionsByName = new(StringComparer.OrdinalIgnoreCase)
	{
		["low"] = ShapePeak.Low,
		["middle"] = ShapePeak.Middle,
		["high"] = ShapePeak.High,
	};
}

/// <summary>
/// Reads and writes a <see cref="PeakLevel"/> as a string, such as "middle-".
/// </summary>
public sealed class PeakLevelJsonConverter : JsonConverter<PeakLevel>
{
	public override PeakLevel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
		return PeakLevel.TryParse(text, out var level)
			? level.Value
			: throw new JsonException(OurResources.ShapePeakInvalid);
	}

	public override void Write(Utf8JsonWriter writer, PeakLevel value, JsonSerializerOptions options) =>
		writer.WriteStringValue(value.ToString());
}
