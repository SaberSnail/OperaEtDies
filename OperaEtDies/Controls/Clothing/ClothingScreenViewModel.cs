using System.Globalization;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Clothing;

/// <summary>
/// The player's clothes: their inventory, with what they're wearing and carrying, or their wardrobe at home, which also
/// has the outfits they can change into and the clothes they keep there. Changing clothes takes time once, when the player
/// leaves the page having changed what they're wearing, however many things they tried. They can't leave it while they
/// aren't dressed enough to be seen where they are.
/// </summary>
public sealed class ClothingScreenViewModel : ViewModelBase
{
	public ClothingScreenViewModel(GameWorld world, bool isWardrobe, bool isDeveloper = false)
	{
		m_world = world;
		IsWardrobe = isWardrobe;
		IsDeveloper = isDeveloper;
		m_wornBefore = world.Player.Clothing.Pieces.ToList();
		Title = isWardrobe ? OurResources.WardrobeHeader : OurResources.InventoryHeader;
		m_wearing = [];
		m_carrying = [];
		m_keptAtHome = [];
		m_outfits = [];
		Refresh();
	}

	public event EventHandler? CloseRequested;

	public string Title { get; }

	/// <summary>
	/// Whether this is the wardrobe at home, rather than the inventory.
	/// </summary>
	public bool IsWardrobe { get; }

	/// <summary>
	/// Whether to show how modest the player is, and how much clothing covers, for developing the game.
	/// </summary>
	public bool IsDeveloper { get; }

	/// <summary>
	/// What the player is wearing.
	/// </summary>
	public IReadOnlyList<ClothingPieceViewModel> Wearing
	{
		get => VerifyAccess(m_wearing);
		private set => SetPropertyField([nameof(Wearing), nameof(IsNothingWorn)], value, ref m_wearing);
	}

	/// <summary>
	/// What the player is carrying.
	/// </summary>
	public IReadOnlyList<ClothingPieceViewModel> Carrying
	{
		get => VerifyAccess(m_carrying);
		private set => SetPropertyField([nameof(Carrying), nameof(IsNothingCarried)], value, ref m_carrying);
	}

	/// <summary>
	/// In the wardrobe, the player's own clothes kept at home.
	/// </summary>
	public IReadOnlyList<ClothingPieceViewModel> KeptAtHome
	{
		get => VerifyAccess(m_keptAtHome);
		private set => SetPropertyField([nameof(KeptAtHome), nameof(IsNothingKeptAtHome)], value, ref m_keptAtHome);
	}

	public bool IsNothingWorn => Wearing.Count == 0;

	public bool IsNothingCarried => Carrying.Count == 0;

	public bool IsNothingKeptAtHome => KeptAtHome.Count == 0;

	/// <summary>
	/// In the wardrobe, the player's outfits, which they can change into when they have every piece at hand.
	/// </summary>
	public IReadOnlyList<OutfitViewModel> Outfits
	{
		get => VerifyAccess(m_outfits);
		private set => SetPropertyField(value, ref m_outfits);
	}

	/// <summary>
	/// Says which parts of the body the player must cover more before they can leave, or null if they're dressed enough to
	/// be seen where they are.
	/// </summary>
	public string? Warning
	{
		get => VerifyAccess(m_warning);
		private set => SetPropertyField([nameof(Warning), nameof(HasWarning)], value, ref m_warning);
	}

	public bool HasWarning => Warning is not null;

	/// <summary>
	/// For developers, how modest the player is in each region their modesty rule checks, and the least it calls for where
	/// they are.
	/// </summary>
	public string? DeveloperModesty
	{
		get => VerifyAccess(m_developerModesty);
		private set => SetPropertyField(value, ref m_developerModesty);
	}

	/// <summary>
	/// The page can only be closed while the player is dressed enough to be seen where they are.
	/// </summary>
	public bool CanClose() => Warning is null;

	public void Close()
	{
		VerifyAccess();
		if (CanClose())
			CloseRequested.Raise(this);
	}

	/// <summary>
	/// Finishes changing clothes as the page closes, which takes time if the player is wearing something different from
	/// when it opened. Returns how long it took, to show the player, or null if nothing changed.
	/// </summary>
	internal string? Finish()
	{
		var start = m_world.Time;
		var passed = m_world.FinishPlayerChanging(m_wornBefore);
		m_wornBefore = m_world.Player.Clothing.Pieces.ToList();
		return passed is { } offset ? OurResources.ClothingChanged.FormatCurrentCulture(m_world.GameData.Calendar.FormatOffsetFrom(start, offset, TimeFormat.Long)) : null;
	}

	/// <summary>
	/// Has the player change clothes, which takes no time until they're done, then shows their clothes as they are now.
	/// </summary>
	internal void Change(Action change)
	{
		change();
		Refresh();
	}

	/// <summary>
	/// Lists the player's clothes as they are now.
	/// </summary>
	internal void Refresh()
	{
		var player = m_world.Player;
		var gameData = m_world.GameData;
		IReadOnlyList<ClothingPieceViewModel> List(IEnumerable<ClothingPiece> pieces) =>
			WornClothing.Order(pieces, gameData.ClothingSlots, gameData.ClothingLayers).Select(x => new ClothingPieceViewModel(x, m_world, this)).ToList();

		Wearing = List(player.Clothing.Pieces);
		Carrying = List(player.Inventory.Clothing);
		KeptAtHome = IsWardrobe ? List(player.Home.Items.Clothing.Where(x => x.Owner == player)) : [];
		Outfits = IsWardrobe ? [.. player.Outfits.Select(x => new OutfitViewModel(x, m_world, this)), new OutfitViewModel(null, m_world, this)] : [];

		Warning = ModestyWarning.FormatHere(m_world.GetPlayerUncoveredSlots());
		DeveloperModesty = IsDeveloper ? ModestyWarning.FormatDeveloper(m_world.GetPlayerModesty()) : null;
	}

	private readonly GameWorld m_world;
	private IReadOnlyList<ClothingPieceViewModel> m_wearing;
	private IReadOnlyList<ClothingPieceViewModel> m_carrying;
	private IReadOnlyList<ClothingPieceViewModel> m_keptAtHome;
	private IReadOnlyList<OutfitViewModel> m_outfits;
	private string? m_warning;
	private string? m_developerModesty;
	private IReadOnlyList<ClothingPiece> m_wornBefore;
}

/// <summary>
/// A piece of the player's clothing, which they can examine to see what it looks like, and put on, take off, pick up, or
/// put away, where they can.
/// </summary>
public sealed class ClothingPieceViewModel : ViewModelBase
{
	internal ClothingPieceViewModel(ClothingPiece piece, GameWorld world, ClothingScreenViewModel screen)
	{
		Piece = piece;
		m_world = world;
		m_screen = screen;
		Description = FirstUpper(piece.Description);
		DeveloperCoverage = screen.IsDeveloper ? ModestyWarning.FormatCoverage(piece.Clothing, world.GameData) : null;
		Note = piece.IsInWash ? OurResources.ClothingInWash :
			piece.DryAt is { } dryAt && dryAt > world.Time ? OurResources.ClothingDrying.FormatCurrentCulture(world.GameData.Calendar.FormatClock(dryAt)) :
			null;
	}

	public ClothingPiece Piece { get; }

	/// <summary>
	/// A few words describing the piece, such as "A russet kirtle".
	/// </summary>
	public string Description { get; }

	/// <summary>
	/// For developers, how much of each region the piece covers and its bulk, shown while it's being examined.
	/// </summary>
	public string? DeveloperCoverage { get; }

	/// <summary>
	/// What the piece looks like, shown while it's being examined.
	/// </summary>
	public string LongDescription => Piece.LongDescription;

	/// <summary>
	/// Why the piece can't be worn right now, such as being in the wash, or null if nothing keeps it from being worn.
	/// </summary>
	public string? Note { get; }

	/// <summary>
	/// Whether the player is examining the piece, which shows what it looks like.
	/// </summary>
	public bool IsExamined
	{
		get => VerifyAccess(m_isExamined);
		private set => SetPropertyField(value, ref m_isExamined);
	}

	/// <summary>
	/// Shows what the piece looks like, or hides it again.
	/// </summary>
	public void Examine()
	{
		VerifyAccess();
		IsExamined = !IsExamined;
	}

	public bool CanWear() => m_world.CanPlayerWear(Piece);

	public void Wear()
	{
		VerifyAccess();
		m_screen.Change(() => m_world.PlayerWear(Piece));
	}

	public bool CanTakeOff() => Piece.Wearer == m_world.Player;

	public void TakeOff()
	{
		VerifyAccess();
		m_screen.Change(() => m_world.PlayerTakeOff(Piece));
	}

	public bool CanTake() => m_world.CanPlayerTake(Piece);

	public void Take()
	{
		VerifyAccess();
		m_world.PlayerTake(Piece);
		m_screen.Refresh();
	}

	public bool CanPutAway() => m_world.CanPlayerPutAway(Piece);

	public void PutAway()
	{
		VerifyAccess();
		m_world.PlayerPutAway(Piece);
		m_screen.Refresh();
	}

	internal static string FirstUpper(string text) => text.Length == 0 ? text : text[..1].ToUpper(CultureInfo.CurrentCulture) + text[1..];

	private readonly GameWorld m_world;
	private readonly ClothingScreenViewModel m_screen;
	private bool m_isExamined;
}

/// <summary>
/// One of the player's outfits, such as their work clothes, which they can change into in their wardrobe when they have
/// every piece at hand, or nothing at all, which takes off everything they're wearing.
/// </summary>
public sealed class OutfitViewModel : ViewModelBase
{
	internal OutfitViewModel(OwnedOutfit? outfit, GameWorld world, ClothingScreenViewModel screen)
	{
		Outfit = outfit;
		m_world = world;
		m_screen = screen;
		if (outfit is null)
		{
			Title = OurResources.OutfitNothing;
			Description = OurResources.OutfitNothingDescription;
			Note = world.Player.Clothing.Pieces.Count == 0 ? OurResources.OutfitWearing : null;
		}
		else
		{
			Title = outfit.Kind switch
			{
				OutfitKind.Work => OurResources.OutfitWork,
				OutfitKind.Night => OurResources.OutfitNight,
				_ => OurResources.OutfitEveryday,
			};
			var gameData = world.GameData;
			var pieces = TextList.Join(WornClothing.Order(outfit.Pieces, gameData.ClothingSlots, gameData.ClothingLayers).Select(x => x.Description).ToList());
			Description = outfit.Pieces.Count == 0 ? OurResources.OutfitNothingDescription :
				ClothingPieceViewModel.FirstUpper(!outfit.IsComplete ? OurResources.OutfitOverOtherClothes.FormatCurrentCulture(pieces) : pieces);
			Note = outfit.Pieces.Any(x => x.IsInWash) ? OurResources.OutfitInWash :
				outfit.Pieces.Any(x => !x.IsReady(world.Time)) ? OurResources.OutfitDrying :
				ClothingRules.IsDressedIn(world.Player, outfit) ? OurResources.OutfitWearing :
				null;
		}
	}

	/// <summary>
	/// The outfit, or null for nothing at all.
	/// </summary>
	public OwnedOutfit? Outfit { get; }

	/// <summary>
	/// What kind of clothes the outfit is, such as "Work clothes".
	/// </summary>
	public string Title { get; }

	/// <summary>
	/// The pieces of the outfit, such as "A linen shirt, a wool tunic, and leather shoes", and for work clothes, that
	/// they go over other clothes.
	/// </summary>
	public string Description { get; }

	/// <summary>
	/// Whether the player is wearing the outfit, or why they can't change into it, or null if neither.
	/// </summary>
	public string? Note { get; }

	public bool CanChangeInto() => Outfit is null ? m_world.Player.Clothing.Pieces.Count != 0 : m_world.CanPlayerChangeInto(Outfit);

	public void ChangeInto()
	{
		VerifyAccess();
		var outfit = Outfit;
		m_screen.Change(outfit is null ? m_world.PlayerTakeOffAll : () => m_world.PlayerChangeInto(outfit));
	}

	private readonly GameWorld m_world;
	private readonly ClothingScreenViewModel m_screen;
}
