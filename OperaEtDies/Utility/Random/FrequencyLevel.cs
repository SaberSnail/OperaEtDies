using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OperaEtDies.Utility.Random;

/// <summary>
/// A frequency, nudged up or down by up to <see cref="MaxSteps"/> steps. Data files write it as the frequency name
/// followed by "+" or "-" for each step, such as "common-" or "rare++".
/// </summary>
[JsonConverter(typeof(FrequencyLevelJsonConverter))]
public readonly record struct FrequencyLevel(Frequency Tier, int Steps = 0)
{
	public const int MaxSteps = 2;

	public static implicit operator FrequencyLevel(Frequency tier) => new(tier);

	/// <summary>
	/// Parses a frequency level, ignoring case. Returns false if the text isn't a frequency name followed by up to
	/// <see cref="MaxSteps"/> of either "+" or "-".
	/// </summary>
	public static bool TryParse(string? text, [NotNullWhen(true)] out FrequencyLevel? level)
	{
		level = null;
		if (text is not null)
		{
			var name = text.TrimEnd('+', '-');
			var suffix = text[name.Length..];
			var isValidSuffix = suffix.Length <= MaxSteps && (suffix.All(x => x == '+') || suffix.All(x => x == '-'));
			if (isValidSuffix && s_tiersByName.TryGetValue(name, out var tier))
				level = new FrequencyLevel(tier, suffix.StartsWith('-') ? -suffix.Length : suffix.Length);
		}

		return level is not null;
	}

	public override string ToString()
	{
		var tier = Tier;
		var name = s_tiersByName.First(x => x.Value == tier).Key;
		return name + new string(Steps < 0 ? '-' : '+', Math.Abs(Steps));
	}

	private static readonly Dictionary<string, Frequency> s_tiersByName = new(StringComparer.OrdinalIgnoreCase)
	{
		["common"] = Frequency.Common,
		["uncommon"] = Frequency.Uncommon,
		["rare"] = Frequency.Rare,
		["veryRare"] = Frequency.VeryRare,
	};
}

/// <summary>
/// Reads and writes a <see cref="FrequencyLevel"/> as a string, such as "uncommon+".
/// </summary>
public sealed class FrequencyLevelJsonConverter : JsonConverter<FrequencyLevel>
{
	public override FrequencyLevel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
		return FrequencyLevel.TryParse(text, out var level)
			? level.Value
			: throw new JsonException(OurResources.FrequencyLevelInvalid);
	}

	public override void Write(Utf8JsonWriter writer, FrequencyLevel value, JsonSerializerOptions options) =>
		writer.WriteStringValue(value.ToString());
}
