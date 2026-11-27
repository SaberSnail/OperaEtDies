using System.Text.Json;
using System.Text.Json.Serialization;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// Clothing that's worn together, from the outfits section of the data, which people are given when they're created.
/// </summary>
public sealed class Outfit : IIdentified, IHasFrequency
{
	public required string Id { get; init; }

	public required OutfitKind Kind { get; init; }

	/// <summary>
	/// A condition on character properties that must be met for a character to be given the outfit.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// How common the outfit is when chosen at random among the outfits of its kind.
	/// </summary>
	public FrequencyLevel Frequency { get; init; } = new(Utility.Random.Frequency.Common);

	/// <summary>
	/// The clothing in the outfit: each entry a piece of clothing, or a choice among options, one of which is chosen for
	/// each person given the outfit. No two entries can hold clothing that takes the same slot in the same layer.
	/// </summary>
	[JsonConverter(typeof(OutfitClothingListConverter))]
	public required IReadOnlyList<OutfitClothing> Clothing { get; init; }

	/// <summary>
	/// Gets the options of each entry of the outfit that a character whose values have the given ids in conditions can be
	/// given, in the order of the entries. An entry none of whose options they can be given is an option of nothing.
	/// </summary>
	public IReadOnlyList<IReadOnlyList<OutfitClothingOption>> GetOptions(Func<string, string?> getValueId) =>
		Clothing.OfType<OutfitClothing>()
			.Select(entry => entry.GetOptions().Where(x => CharacterCondition.IsMet(x.When, getValueId)).ToList() is { Count: > 0 } options ? options : (IReadOnlyList<OutfitClothingOption>) [OutfitClothingOption.None])
			.ToList();

	/// <summary>
	/// Gets the ids of every piece of clothing any entry of the outfit can hold.
	/// </summary>
	public IEnumerable<string> GetAllClothingIds() =>
		Clothing.OfType<OutfitClothing>().SelectMany(x => x.GetOptions()).Select(x => x.Clothing).OfType<string>();

	/// <summary>
	/// For work clothes, whether they're a complete set of clothes worn on their own, such as a uniform, rather than going
	/// over what someone is already wearing.
	/// </summary>
	[AllowedFor(nameof(Kind), OutfitKind.Work)]
	public bool Complete { get; init; }
}

/// <summary>
/// When an outfit is worn.
/// </summary>
public enum OutfitKind
{
	/// <summary>
	/// Worn most of the time.
	/// </summary>
	Everyday,

	/// <summary>
	/// Worn while working. It only fills its own slots, so it can be worn over everyday clothing.
	/// </summary>
	Work,

	/// <summary>
	/// Worn while sleeping at home.
	/// </summary>
	Night,

	/// <summary>
	/// What someone has on when they'd otherwise wear nothing, such as for swimming or bathing: nothing at all, or only
	/// a little, such as a loincloth. It's always complete, and everyone has one.
	/// </summary>
	Nothing,
}

/// <summary>
/// An entry in an outfit's clothing: a piece of clothing, written as its id, or a choice among options, written as an
/// object with its options (see <see cref="OutfitClothingChoice"/>).
/// </summary>
[JsonConverter(typeof(OutfitClothingConverter))]
public sealed class OutfitClothing
{
	/// <summary>
	/// The id of the clothing, for an entry that's a piece of clothing rather than a choice.
	/// </summary>
	public string? Clothing { get; init; }

	/// <summary>
	/// The options, for an entry that's a choice.
	/// </summary>
	public IReadOnlyList<OutfitClothingOption>? Options { get; init; }

	/// <summary>
	/// Creates an entry that's a piece of clothing.
	/// </summary>
	public static OutfitClothing FromId(string clothing) => new() { Clothing = clothing };

	public static implicit operator OutfitClothing(string clothing) => FromId(clothing);

	/// <summary>
	/// Gets the entry's options: its own, or for a piece of clothing, that piece as the only option.
	/// </summary>
	public IReadOnlyList<OutfitClothingOption> GetOptions() => Options ?? [new OutfitClothingOption { Clothing = Clothing }];
}

/// <summary>
/// An entry in an outfit's clothing that's a choice among options.
/// </summary>
public sealed class OutfitClothingChoice
{
	/// <summary>
	/// The options, one of which is chosen for each person given the outfit.
	/// </summary>
	public required IReadOnlyList<OutfitClothingOption> Options { get; init; }
}

/// <summary>
/// One of the options of an entry in an outfit's clothing: a piece of clothing, or nothing.
/// </summary>
public sealed class OutfitClothingOption : IHasFrequency
{
	/// <summary>
	/// An option of nothing, for an entry none of whose options someone can be given.
	/// </summary>
	public static OutfitClothingOption None { get; } = new();

	/// <summary>
	/// The id of the clothing, or null for nothing.
	/// </summary>
	public string? Clothing { get; init; }

	/// <summary>
	/// A condition on character properties that must be met for a character to be given the option.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// How common the option is when chosen at random among the entry's options.
	/// </summary>
	public FrequencyLevel Frequency { get; init; } = new(Utility.Random.Frequency.Common);
}

/// <summary>
/// Reads an outfit's clothing: a list of entries, or a single entry as a list of one.
/// </summary>
public sealed class OutfitClothingListConverter : JsonConverter<IReadOnlyList<OutfitClothing>>
{
	public override IReadOnlyList<OutfitClothing>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartArray)
			return [JsonSerializer.Deserialize<OutfitClothing>(ref reader, options)!];

		var entries = new List<OutfitClothing>();
		while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
			entries.Add(JsonSerializer.Deserialize<OutfitClothing>(ref reader, options)!);
		return entries;
	}

	public override void Write(Utf8JsonWriter writer, IReadOnlyList<OutfitClothing> value, JsonSerializerOptions options)
	{
		writer.WriteStartArray();
		foreach (var entry in value)
			JsonSerializer.Serialize(writer, entry, options);
		writer.WriteEndArray();
	}
}

/// <summary>
/// Reads an entry in an outfit's clothing: a clothing id, or an object with its options (and, like any object in the
/// data, comments).
/// </summary>
public sealed class OutfitClothingConverter : JsonConverter<OutfitClothing>
{
	public override OutfitClothing? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
	{
		JsonTokenType.String => new OutfitClothing { Clothing = reader.GetString() },
		JsonTokenType.Null => null,
		JsonTokenType.StartObject => new OutfitClothing { Options = JsonSerializer.Deserialize<OutfitClothingChoice>(ref reader, options)!.Options },
		_ => throw new JsonException(OurResources.OutfitClothingInvalid),
	};

	public override void Write(Utf8JsonWriter writer, OutfitClothing value, JsonSerializerOptions options)
	{
		if (value.Options is { } entryOptions)
			JsonSerializer.Serialize(writer, new OutfitClothingChoice { Options = entryOptions }, options);
		else
			writer.WriteStringValue(value.Clothing);
	}
}
