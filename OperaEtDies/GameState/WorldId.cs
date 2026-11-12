using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OperaEtDies.GameState;

/// <summary>
/// Identifies an object in the game world, unique among every object in one game. It's a black box: nothing should
/// depend on its value. Ids are only handed out by <see cref="WorldObjects"/>.
/// </summary>
[JsonConverter(typeof(WorldIdJsonConverter))]
public readonly record struct WorldId
{
	internal WorldId(int value)
	{
		Value = value;
	}

	/// <summary>
	/// Whether this is a real id, rather than the default value.
	/// </summary>
	public bool IsValid => Value > 0;

	/// <summary>
	/// For logs and developer reports only.
	/// </summary>
	public override string ToString() => "#" + Value.ToString(CultureInfo.InvariantCulture);

	internal int Value { get; }
}

/// <summary>
/// Writes a world id as a number in save files.
/// </summary>
public sealed class WorldIdJsonConverter : JsonConverter<WorldId>
{
	public override WorldId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var value = reader.GetInt32();
		if (value <= 0)
			throw new JsonException($"{value} isn't a valid world object id.");
		return new WorldId(value);
	}

	public override void Write(Utf8JsonWriter writer, WorldId value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}
