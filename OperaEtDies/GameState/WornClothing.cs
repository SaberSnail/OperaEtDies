using System.Collections.ObjectModel;
using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// The clothing a person is wearing, with at most one piece in each clothing slot. It's part of the person, who saves the
/// ids of the pieces.
/// </summary>
public sealed class WornClothing
{
	internal WornClothing(Person wearer)
	{
		m_wearer = wearer;
		m_pieces = [];
		Pieces = new ReadOnlyObservableCollection<ClothingPiece>(m_pieces);
	}

	/// <summary>
	/// The pieces being worn, in the order they were put on.
	/// </summary>
	public ReadOnlyObservableCollection<ClothingPiece> Pieces { get; }

	/// <summary>
	/// Whether a piece can be put on: nothing worn takes any of its slots in its layer.
	/// </summary>
	public bool AreSlotsFree(ClothingPiece piece) => !m_pieces.Any(x => x.Clothing.Overlaps(piece.Clothing));

	/// <summary>
	/// Describes the worn clothing that can be seen as a list of the pieces' short descriptions, in the order of the
	/// first slot each takes and then from the outermost layer in, or empty text if nothing is worn. A piece can't be
	/// seen when each of its slots is covered at least as much as the given coverage by pieces in layers outside it.
	/// </summary>
	public string Describe(IReadOnlyList<ClothingSlot> slots, IReadOnlyList<ClothingLayer> layers, int hidesClothingAt)
	{
		var layerIndexes = GetIndexes(layers);
		int GetLayerIndex(ClothingPiece piece) => layerIndexes.GetValueOrDefault(piece.Clothing.Layer, -1);
		bool IsCovered(ClothingPiece piece, string slot) =>
			m_pieces.Where(x => GetLayerIndex(x) > GetLayerIndex(piece)).Select(x => x.Clothing.GetCoverage(slot)).DefaultIfEmpty(0).Max() >= hidesClothingAt;

		return TextList.Join(Order(m_pieces.Where(piece => piece.Clothing.Slots.Any(slot => !IsCovered(piece, slot))), slots, layers).Select(x => x.Description).ToList());
	}

	/// <summary>
	/// Puts pieces of clothing in the order they're described: by the first slot each takes, and then from the outermost
	/// layer in.
	/// </summary>
	public static IReadOnlyList<ClothingPiece> Order(IEnumerable<ClothingPiece> pieces, IReadOnlyList<ClothingSlot> slots, IReadOnlyList<ClothingLayer> layers)
	{
		var slotIndexes = GetIndexes(slots);
		var layerIndexes = GetIndexes(layers);
		return pieces
			.OrderBy(x => x.Clothing.Slots.Select(y => slotIndexes.GetValueOrDefault(y, slots.Count)).DefaultIfEmpty(slots.Count).Min())
			.ThenByDescending(x => layerIndexes.GetValueOrDefault(x.Clothing.Layer, -1))
			.ToList();
	}

	private static Dictionary<string, int> GetIndexes(IEnumerable<IIdentified> items)
	{
		var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var (item, index) in items.Select((x, i) => (x, i)))
			indexes.TryAdd(item.Id, index);
		return indexes;
	}

	/// <summary>
	/// Gets the worn clothing as a portrait draws it.
	/// </summary>
	public PortraitClothing GetPortraitClothing() =>
		new(m_pieces.Select(x => new PortraitClothingPiece(x.Clothing, x.Colors.ToDictionary(y => y.Key, y => y.Value.Color, StringComparer.Ordinal))));

	/// <summary>
	/// Puts on a piece of clothing. It can't be somewhere else, and its slots must be free in its layer.
	/// </summary>
	internal void Wear(ClothingPiece piece)
	{
		if (piece.IsPlaced)
			throw new InvalidOperationException($"Clothing {piece.Id} is already somewhere else.");
		if (!AreSlotsFree(piece))
			throw new InvalidOperationException($"Clothing {piece.Id} takes a slot that's already taken.");

		m_pieces.Add(piece);
		piece.Wearer = m_wearer;
		m_wearer.OnClothingChanged();
	}

	/// <summary>
	/// Takes off a piece of clothing.
	/// </summary>
	internal void TakeOff(ClothingPiece piece)
	{
		if (piece.Wearer != m_wearer || !m_pieces.Remove(piece))
			throw new InvalidOperationException($"Clothing {piece.Id} isn't being worn by {m_wearer.Id}.");

		piece.Wearer = null;
		m_wearer.OnClothingChanged();
	}

	/// <summary>
	/// Puts on an outfit, taking the pieces out of whatever container they're in. Pieces already worn that take the same
	/// slots in the same layers are taken off and put in the given container. Pieces of the outfit that are already worn stay on.
	/// </summary>
	internal void PutOn(OwnedOutfit outfit, ItemContainer takenOffTo)
	{
		foreach (var piece in outfit.Pieces.Where(x => x.Wearer != m_wearer))
		{
			piece.Container?.Remove(piece);
			foreach (var worn in m_pieces.Where(x => x.Clothing.Overlaps(piece.Clothing)).ToList())
			{
				TakeOff(worn);
				takenOffTo.Add(worn);
			}

			Wear(piece);
		}
	}

	/// <summary>
	/// Takes off everything and puts it in the given container.
	/// </summary>
	internal void TakeOffAll(ItemContainer takenOffTo)
	{
		foreach (var piece in m_pieces.ToList())
		{
			TakeOff(piece);
			takenOffTo.Add(piece);
		}
	}

	/// <summary>
	/// Gets the ids of the pieces for saving, or null if nothing is worn.
	/// </summary>
	internal IReadOnlyList<WorldId>? CreateRecord() => m_pieces.Count == 0 ? null : m_pieces.Select(x => x.Id).ToList();

	/// <summary>
	/// Puts on the pieces from a save, failing to load if any is already somewhere else or takes a slot that's taken.
	/// </summary>
	internal void Load(IEnumerable<ClothingPiece> pieces)
	{
		foreach (var piece in pieces)
		{
			if (piece.IsPlaced)
				throw new SaveLoadException(OurResources.SaveItemPlacedTwice.FormatCurrentCulture(piece.Id));
			if (!AreSlotsFree(piece))
				throw new SaveLoadException(OurResources.SaveClothingSlotTaken.FormatCurrentCulture(m_wearer.Id, piece.Id));
			Wear(piece);
		}
	}

	private readonly Person m_wearer;
	private readonly ObservableCollection<ClothingPiece> m_pieces;
}
