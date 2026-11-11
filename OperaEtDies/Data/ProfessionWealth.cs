using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OperaEtDies.Data;

/// <summary>
/// How much a profession earns: a wealth level, nudged up or down by up to <see cref="MaxSteps"/> steps. Each step
/// moves <see cref="StepSize"/> of the way toward the next level up or down, so steps never cross into another level.
/// Data files write it as the level followed by "+" or "-" for each step, such as "high-" or "low++".
/// </summary>
[JsonConverter(typeof(ProfessionWealthJsonConverter))]
public readonly record struct ProfessionWealth(WealthLevel Level, int Steps = 0)
{
	public const int MaxSteps = 2;

	/// <summary>
	/// How far each step moves, as a fraction of the way to the next level up or down.
	/// </summary>
	public const double StepSize = 0.2;

	public static implicit operator ProfessionWealth(WealthLevel level) => new(level);

	/// <summary>
	/// Gets the amount of wealth the profession earns. Each level is worth twice the one below it, so a step up adds a
	/// fifth of the level's amount and a step down takes away a tenth.
	/// </summary>
	public double GetAmount() => Level.GetAmount() * (1 + Steps * StepSize * (Steps < 0 ? 0.5 : 1));

	/// <summary>
	/// Parses a profession's wealth, ignoring case. Returns false if the text isn't a wealth level followed by up to
	/// <see cref="MaxSteps"/> of either "+" or "-".
	/// </summary>
	public static bool TryParse(string? text, [NotNullWhen(true)] out ProfessionWealth? wealth)
	{
		wealth = null;
		if (text is not null)
		{
			var name = text.TrimEnd('+', '-');
			var suffix = text[name.Length..];
			var isValidSuffix = suffix.Length <= MaxSteps && (suffix.All(x => x == '+') || suffix.All(x => x == '-'));
			var level = Enum.GetValues<WealthLevel>().Cast<WealthLevel?>().FirstOrDefault(x => string.Equals(x!.Value.GetDataName(), name, StringComparison.OrdinalIgnoreCase));
			if (isValidSuffix && level is { } found)
				wealth = new ProfessionWealth(found, suffix.StartsWith('-') ? -suffix.Length : suffix.Length);
		}

		return wealth is not null;
	}

	public override string ToString() => Level.GetDataName() + new string(Steps < 0 ? '-' : '+', Math.Abs(Steps));
}

/// <summary>
/// Reads and writes a <see cref="ProfessionWealth"/> as a string, such as "medium+".
/// </summary>
public sealed class ProfessionWealthJsonConverter : JsonConverter<ProfessionWealth>
{
	public override ProfessionWealth Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
		return ProfessionWealth.TryParse(text, out var wealth)
			? wealth.Value
			: throw new JsonException(OurResources.ProfessionWealthInvalid);
	}

	public override void Write(Utf8JsonWriter writer, ProfessionWealth value, JsonSerializerOptions options) =>
		writer.WriteStringValue(value.ToString());
}
