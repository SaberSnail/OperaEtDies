using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// A piece of clothing: an article of clothing from the data, in its own colors.
/// </summary>
public sealed class ClothingPiece : Item
{
	/// <summary>
	/// Creates a piece of clothing with a color for each of the clothing's colors, by color name.
	/// </summary>
	internal ClothingPiece(WorldId id, ClothingItem clothing, IReadOnlyDictionary<string, ClothingColor> colors, Person? owner)
		: base(id, owner)
	{
		if (clothing.Colors.Count != colors.Count || clothing.Colors.Any(x => !colors.ContainsKey(x)))
			throw new ArgumentException($"A piece of clothing needs exactly one color for each of the colors of {clothing.Id}.", nameof(colors));

		Clothing = clothing;
		Colors = colors;
		m_description = clothing.FormatDescription(x => colors[x]);
	}

	/// <summary>
	/// The article of clothing from the data.
	/// </summary>
	public ClothingItem Clothing { get; }

	/// <summary>
	/// The color of each of the clothing's colors, by color name.
	/// </summary>
	public IReadOnlyDictionary<string, ClothingColor> Colors { get; }

	public override string Title => Clothing.Title;

	public override string Description => m_description;

	public override string LongDescription => Clothing.Text.Text;

	/// <summary>
	/// The person wearing the piece, or null if nobody is. Worn clothing keeps it up to date.
	/// </summary>
	public Person? Wearer
	{
		get => m_wearer;
		internal set => SetPropertyField(value, ref m_wearer);
	}

	/// <summary>
	/// When the piece can be worn again, such as once washing hung out to dry is dry, or null if it was never put away to be
	/// ready later.
	/// </summary>
	public TimePoint? ReadyAt
	{
		get => m_readyAt;
		internal set => SetPropertyField(value, ref m_readyAt);
	}

	/// <summary>
	/// What the piece is shown to be doing until it's ready, such as "Drying", from the step that put it away, or null if
	/// it was never put away to be ready later.
	/// </summary>
	public string? DelayActivity
	{
		get => m_delayActivity;
		internal set => SetPropertyField(value, ref m_delayActivity);
	}

	/// <summary>
	/// Whether the piece can be put on or carried at the given time: it isn't still being made ready, such as drying.
	/// </summary>
	public bool IsReady(TimePoint time) => m_readyAt is not { } ready || ready <= time;

	internal override bool IsPlaced => base.IsPlaced || m_wearer is not null;

	public override WorldObjectRecord CreateRecord() =>
		new ClothingPieceRecord(Id, Clothing.Id, Colors.ToDictionary(x => x.Key, x => x.Value.Id, StringComparer.Ordinal), Owner?.Id, m_readyAt?.SaveToString(), m_delayActivity);

	private readonly string m_description;
	private Person? m_wearer;
	private TimePoint? m_readyAt;
	private string? m_delayActivity;
}

/// <summary>
/// The saved form of a piece of clothing: the id of its article of clothing in the data, the id of the clothing color of
/// each of its colors (by color name), its owner, and when it can be worn again, if it was put away to be ready later. Where it is comes from the container or person holding it.
/// </summary>
public sealed record ClothingPieceRecord(WorldId Id, string Clothing, IReadOnlyDictionary<string, string> Colors, WorldId? Owner, string? ReadyAt = null, string? DelayActivity = null) : WorldObjectRecord(Id)
{
	internal override IWorldObject Create(WorldLoader loader)
	{
		var gameData = loader.GameData;
		var clothing = WorldLoader.GetData(gameData.Clothing, Clothing, "clothing");
		var colors = (Colors ?? new Dictionary<string, string>()).ToDictionary(x => x.Key, x => WorldLoader.GetData(gameData.ClothingColors, x.Value, "clothing color"), StringComparer.Ordinal);
		if (clothing.Colors.Count != colors.Count || clothing.Colors.Any(x => !colors.ContainsKey(x)))
			throw new SaveLoadException(OurResources.SaveClothingColorsInvalid.FormatCurrentCulture(Id, Clothing, string.Join(", ", clothing.Colors)));

		return new ClothingPiece(Id, clothing, colors, Owner is { } owner ? loader.Get<Person>(owner) : null)
		{
			ReadyAt = ReadyAt is null ? null : loader.LoadTime(ReadyAt),
			DelayActivity = DelayActivity,
		};
	}
}
