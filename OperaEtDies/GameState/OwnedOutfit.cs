using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// Clothing a person owns that's worn together, such as their work clothes or one of their everyday outfits.
/// </summary>
public sealed class OwnedOutfit : NotifyPropertyChangedBase, IWorldObject
{
	internal OwnedOutfit(WorldId id, Person owner, OutfitKind kind, Outfit? template, IReadOnlyList<ClothingPiece> pieces)
	{
		Id = id;
		Owner = owner;
		Kind = kind;
		Template = template;
		Pieces = pieces;
		owner.AddOutfit(this);
	}

	public WorldId Id { get; }

	public Person Owner { get; }

	/// <summary>
	/// When the outfit is worn.
	/// </summary>
	public OutfitKind Kind { get; }

	/// <summary>
	/// The outfit from the data it was made from, or null if it wasn't made from one.
	/// </summary>
	public Outfit? Template { get; }

	/// <summary>
	/// The pieces of clothing in the outfit, no two of which take the same slot.
	/// </summary>
	public IReadOnlyList<ClothingPiece> Pieces { get; }

	/// <summary>
	/// Whether the outfit is worn on its own, rather than over other clothes: everyday and night clothes always are, and
	/// work clothes are when the outfit they were made from is complete.
	/// </summary>
	public bool IsComplete => Kind != OutfitKind.Work || Template?.Complete == true;

	public WorldObjectRecord CreateRecord() => new OwnedOutfitRecord(Id, Owner.Id, Kind, Template?.Id, Pieces.Select(x => x.Id).ToList());
}

/// <summary>
/// The saved form of an outfit: its owner, its kind, the id of the outfit in the data it was made from (if any), and its
/// pieces of clothing.
/// </summary>
public sealed record OwnedOutfitRecord(WorldId Id, WorldId Owner, OutfitKind OutfitKind, string? Template, IReadOnlyList<WorldId> Pieces) : WorldObjectRecord(Id)
{
	internal override IWorldObject Create(WorldLoader loader)
	{
		var pieces = (Pieces ?? []).Select(loader.Get<ClothingPiece>).ToList();
		var overlaps = pieces.Select((x, i) => pieces.Skip(i + 1).Any(y => y.Clothing.Overlaps(x.Clothing))).Any(x => x);
		if (pieces.Distinct().Count() != pieces.Count || overlaps)
			throw new SaveLoadException(OurResources.SaveOutfitInvalid.FormatCurrentCulture(Id));

		return new OwnedOutfit(Id, loader.Get<Person>(Owner), OutfitKind, Template is null ? null : WorldLoader.GetData(loader.GameData.Outfits, Template, "outfit"), pieces);
	}
}
