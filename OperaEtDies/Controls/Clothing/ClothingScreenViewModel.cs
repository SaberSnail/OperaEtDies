using System.Globalization;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Controls.Locations;
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
	public ClothingScreenViewModel(GameWorld world, TimeRunner timeRunner, bool isWardrobe, bool isDeveloper = false)
	{
		m_world = world;
		m_timeRunner = timeRunner;
		IsWardrobe = isWardrobe;
		IsDeveloper = isDeveloper;
		m_wornBefore = world.Player.Clothing.Pieces.ToList();
		Title = isWardrobe ? OurResources.WardrobeHeader : OurResources.InventoryHeader;
		m_wearing = [];
		m_carrying = [];
		m_bundles = [];
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
	/// The bundles the player is carrying for their steps, such as the household's washing, which only their steps can
	/// put down.
	/// </summary>
	public IReadOnlyList<BundleViewModel> Bundles
	{
		get => VerifyAccess(m_bundles);
		private set => SetPropertyField([nameof(Bundles), nameof(IsNothingCarried)], value, ref m_bundles);
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

	public bool IsNothingCarried => Carrying.Count == 0 && Bundles.Count == 0;

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
	internal async Task<string?> FinishAsync()
	{
		var start = m_world.Time;
		var passed = await m_world.FinishPlayerChangingAsync(m_wornBefore).ConfigureAwait(true);
		m_wornBefore = m_world.Player.Clothing.Pieces.ToList();
		return passed is { } offset ? OurResources.ClothingChanged.FormatCurrentCulture(m_world.GameData.Calendar.FormatOffsetFrom(start, offset, TimeFormat.Long)) : null;
	}

	/// <summary>
	/// Runs something the player does here that can pass time, such as picking up clothes, which can carry them on through
	/// their steps, then shows their clothes as they are now.
	/// </summary>
	internal void Run(Func<Task> action) =>
		m_timeRunner.Run(async () =>
		{
			await action().ConfigureAwait(true);
			Refresh();
		});

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
		Bundles = [.. player.Inventory.Items.OfType<Bundle>().Select(x => new BundleViewModel(x, player))];
		KeptAtHome = IsWardrobe ? List(player.Home.Items.Clothing.Where(x => x.Owner == player)) : [];
		Outfits = IsWardrobe ? [.. player.Outfits.OrderBy(x => x.Kind == OutfitKind.Nothing).Select(x => new OutfitViewModel(x, m_world, this))] : [];

		Warning = ModestyWarning.FormatHere(m_world.GetPlayerUncoveredSlots());
		DeveloperModesty = IsDeveloper ? ModestyWarning.FormatDeveloper(m_world.GetPlayerModesty()) : null;
	}

	private readonly GameWorld m_world;
	private readonly TimeRunner m_timeRunner;
	private IReadOnlyList<ClothingPieceViewModel> m_wearing;
	private IReadOnlyList<ClothingPieceViewModel> m_carrying;
	private IReadOnlyList<BundleViewModel> m_bundles;
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
		Note = GetCarrier(piece, world) is { } carrier ? OurResources.ClothingCarriedBy.FormatCurrentCulture(carrier.FullName) :
			world.Player.Inventory.Items.OfType<Bundle>().FirstOrDefault(x => x.Items.Contains(piece)) is { } bundle ? OurResources.ClothingInBundle.FormatCurrentCulture(bundle.Title.ToLower(CultureInfo.CurrentCulture)) :
			piece.ReadyAt is { } readyAt && readyAt > world.Time ? OurResources.ClothingNotReady.FormatCurrentCulture(piece.DelayActivity ?? OurResources.ClothingNotReadyActivity, world.GameData.Calendar.FormatClock(readyAt)) :
			null;
	}

	/// <summary>
	/// Gets who other than the player is carrying a piece of clothing, such as the household's washing, or null if no one
	/// else is.
	/// </summary>
	internal static Person? GetCarrier(ClothingPiece piece, GameWorld world) => world.Player.Family.Members.FirstOrDefault(x => !x.IsPlayer && piece.Container == x.Inventory);

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
		m_screen.Change(() => m_world.PlayerWear(Piece, isCarried: !m_screen.IsWardrobe));
	}

	public bool CanTakeOff() => Piece.Wearer == m_world.Player;

	public void TakeOff()
	{
		VerifyAccess();
		m_screen.Change(() => m_world.PlayerTakeOff(Piece, isCarried: !m_screen.IsWardrobe));
	}

	public bool CanTake() => m_world.CanPlayerTake(Piece);

	public void Take()
	{
		VerifyAccess();
		m_screen.Run(() => m_world.PlayerTakeAsync(Piece));
	}

	public bool CanPutAway() => m_world.CanPlayerPutAway(Piece);

	public void PutAway()
	{
		VerifyAccess();
		m_screen.Run(() => m_world.PlayerPutAwayAsync(Piece));
	}

	internal static string FirstUpper(string text) => text.Length == 0 ? text : text[..1].ToUpper(CultureInfo.CurrentCulture) + text[1..];

	private readonly GameWorld m_world;
	private readonly ClothingScreenViewModel m_screen;
	private bool m_isExamined;
}

/// <summary>
/// One of the player's outfits, such as their work clothes or their outfit for wearing nothing (which may be nothing at
/// all), which they can change into in their wardrobe when they have every piece at hand.
/// </summary>
public sealed class OutfitViewModel : ViewModelBase
{
	internal OutfitViewModel(OwnedOutfit outfit, GameWorld world, ClothingScreenViewModel screen)
	{
		Outfit = outfit;
		m_world = world;
		m_screen = screen;
		Title = outfit.Kind switch
		{
			OutfitKind.Work => OurResources.OutfitWork,
			OutfitKind.Night => OurResources.OutfitNight,
			OutfitKind.Nothing => OurResources.OutfitNothing,
			_ => OurResources.OutfitEveryday,
		};
		var gameData = world.GameData;
		var pieces = TextList.Join(WornClothing.Order(outfit.Pieces, gameData.ClothingSlots, gameData.ClothingLayers).Select(x => x.Description).ToList());
		Description = outfit.Pieces.Count == 0 ? OurResources.OutfitNothingDescription :
			ClothingPieceViewModel.FirstUpper(!outfit.IsComplete ? OurResources.OutfitOverOtherClothes.FormatCurrentCulture(pieces) : pieces);
		Note = outfit.Pieces.Any(x => ClothingPieceViewModel.GetCarrier(x, world) is not null) ? OurResources.OutfitCarriedByOthers :
			outfit.Pieces.Any(x => !x.IsReady(world.Time)) ? OurResources.OutfitNotReady.FormatCurrentCulture(TextList.Join(outfit.Pieces.Where(x => !x.IsReady(world.Time))
				.Select(x => PlayerStepsViewModel.LowerFirst(x.DelayActivity ?? OurResources.ClothingNotReadyActivity)).Distinct(StringComparer.CurrentCulture).ToList())) :
			ClothingRules.IsDressedIn(world.Player, outfit) ? OurResources.OutfitWearing :
			null;
	}

	public OwnedOutfit Outfit { get; }

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

	public bool CanChangeInto() => m_world.CanPlayerChangeInto(Outfit);

	public void ChangeInto()
	{
		VerifyAccess();
		var outfit = Outfit;
		m_screen.Change(() => m_world.PlayerChangeInto(outfit, isCarried: !m_screen.IsWardrobe));
	}

	private readonly GameWorld m_world;
	private readonly ClothingScreenViewModel m_screen;
}

/// <summary>
/// A bundle the player carries for their steps, such as the household's washing or a bucket of water, which only the step
/// that puts it down can.
/// </summary>
public sealed class BundleViewModel
{
	internal BundleViewModel(Bundle bundle, Person player)
	{
		Description = ClothingPieceViewModel.FirstUpper(bundle.Description);
		Note = player.Sequences.FirstOrDefault(x => x.Bundles.Values.Contains(bundle)) is { } sequence ? OurResources.BundleFor.FormatCurrentCulture(sequence.Need.Title.ToLower(CultureInfo.CurrentCulture)) : null;
	}

	/// <summary>
	/// What the bundle is, such as "Washing (6 things)".
	/// </summary>
	public string Description { get; }

	/// <summary>
	/// What it's for, such as "For laundry, until your steps put it down", or null if no steps of the player's have it.
	/// </summary>
	public string? Note { get; }
}
