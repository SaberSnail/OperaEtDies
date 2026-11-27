using System.Globalization;
using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.Controls.Clothing;

/// <summary>
/// Tells the player when they aren't dressed enough, and for developers, how modest they are and how much clothing covers.
/// </summary>
internal static class ModestyWarning
{
	/// <summary>
	/// Describes the clothing slots that aren't covered enough for something the player tried to do, or gets null if there
	/// are none.
	/// </summary>
	public static string? Format(IReadOnlyList<ClothingSlot> slots) =>
		slots.Count == 0 ? null : OurResources.ModestyNotCovered.FormatCurrentCulture(JoinSlots(slots));

	/// <summary>
	/// Describes the clothing slots that aren't covered enough for the player to be seen where they are, or gets null if
	/// there are none.
	/// </summary>
	public static string? FormatHere(IReadOnlyList<ClothingSlot> slots) =>
		slots.Count == 0 ? null : OurResources.ModestyNotCoveredHere.FormatCurrentCulture(JoinSlots(slots));

	/// <summary>
	/// For developers, describes how comfortable someone is with how covered they are, and how modest they are in each slot
	/// their modesty rule checks, and the least it calls for.
	/// </summary>
	public static string FormatDeveloper(IReadOnlyList<(ClothingSlot Slot, int Modesty, int Minimum)> modesty, double comfort) =>
		OurResources.DeveloperModesty.FormatCurrentCulture(modesty.Count == 0 ? OurResources.DeveloperModestyNoRule :
			string.Join(OurResources.ListJoinSeparator, modesty.Select(x => OurResources.DeveloperModestySlot.FormatCurrentCulture(x.Slot.Title, x.Modesty, x.Minimum))), comfort);

	/// <summary>
	/// For developers, describes how much of each region an article of clothing covers, and its bulk.
	/// </summary>
	public static string FormatCoverage(ClothingItem clothing, GameData gameData) =>
		OurResources.DeveloperClothingCoverage.FormatCurrentCulture(
			clothing.Coverage.Count == 0 ? OurResources.DeveloperNone :
				string.Join(OurResources.ListJoinSeparator, gameData.ClothingSlots.Where(x => clothing.Coverage.ContainsKey(x.Id)).Select(x => OurResources.DeveloperCoverageSlot.FormatCurrentCulture(x.Title, clothing.GetCoverage(x.Id)))),
			clothing.Bulk is { } bulk ? gameData.ClothingBulks.Find(bulk)?.Title ?? bulk : OurResources.DeveloperNone);

	private static string JoinSlots(IReadOnlyList<ClothingSlot> slots) => TextList.Join(slots.Select(x => x.Title.ToLower(CultureInfo.CurrentCulture)).ToList());
}
