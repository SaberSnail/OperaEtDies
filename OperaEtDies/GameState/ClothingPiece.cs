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
	/// Whether the piece has been taken to be washed and hasn't been hung out to dry yet.
	/// </summary>
	public bool IsInWash
	{
		get => m_isInWash;
		internal set => SetPropertyField(value, ref m_isInWash);
	}

	/// <summary>
	/// When the piece, hung out to dry after it was washed, is dry, or null if it hasn't been washed since it was last
	/// taken to be.
	/// </summary>
	public TimePoint? DryAt
	{
		get => m_dryAt;
		internal set => SetPropertyField(value, ref m_dryAt);
	}

	/// <summary>
	/// Whether the piece can be put on or carried at the given time: it isn't in the wash or still drying.
	/// </summary>
	public bool IsReady(TimePoint time) => !m_isInWash && (m_dryAt is not { } dry || dry <= time);

	internal override bool IsPlaced => base.IsPlaced || m_wearer is not null;

	public override WorldObjectRecord CreateRecord() =>
		new ClothingPieceRecord(Id, Clothing.Id, Colors.ToDictionary(x => x.Key, x => x.Value.Id, StringComparer.Ordinal), Owner?.Id, m_isInWash, m_dryAt?.SaveToString());

	private readonly string m_description;
	private Person? m_wearer;
	private bool m_isInWash;
	private TimePoint? m_dryAt;
}

/// <summary>
/// The saved form of a piece of clothing: the id of its article of clothing in the data, the id of the clothing color of
/// each of its colors (by color name), its owner, and whether it's in the wash or when it's dry, if it was washed. Where it is comes from the container or person holding it.
/// </summary>
public sealed record ClothingPieceRecord(WorldId Id, string Clothing, IReadOnlyDictionary<string, string> Colors, WorldId? Owner, bool InWash = false, string? DryAt = null) : WorldObjectRecord(Id)
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
			IsInWash = InWash,
			DryAt = DryAt is null ? null : loader.LoadTime(DryAt),
		};
	}
}
