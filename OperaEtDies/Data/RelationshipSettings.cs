using GoldenAnvil.Utility;
using OperaEtDies.Town;

namespace OperaEtDies.Data;

/// <summary>
/// How the relationships between characters are chosen when a game starts, and what their scores mean, from Families.json.
/// Every two characters (including the player) share one score from 0 to <see cref="MaximumScore"/>.
/// </summary>
public sealed class RelationshipSettings
{
	/// <summary>
	/// The highest a relationship score can be.
	/// </summary>
	public const int MaximumScore = 100;

	/// <summary>
	/// The score every two characters start with, before the bonuses.
	/// </summary>
	public required IntegerRange Score { get; init; }

	/// <summary>
	/// Added to the score of two members of the same household who aren't one of the closer or more distant ties below,
	/// such as a parent and child, or siblings.
	/// </summary>
	public required int FamilyBonus { get; init; }

	/// <summary>
	/// Added to the score of a married couple in the same household, in place of <see cref="FamilyBonus"/>.
	/// </summary>
	public required int SpouseBonus { get; init; }

	/// <summary>
	/// Added to the score of a household elder and their child or grandchild, in place of <see cref="FamilyBonus"/>.
	/// </summary>
	public required int ElderBonus { get; init; }

	/// <summary>
	/// Added to the score of a household elder and their child's spouse, in place of <see cref="FamilyBonus"/>.
	/// </summary>
	public required int ElderInLawBonus { get; init; }

	/// <summary>
	/// Added to the score of two characters in different households whose homes are on the same quarter.
	/// </summary>
	public required int NeighborBonus { get; init; }

	/// <summary>
	/// The lowest score at which two characters are friends.
	/// </summary>
	public required int FriendScore { get; init; }

	/// <summary>
	/// What scores are called when shown to the player, in order of their minimum score. The first starts at 0.
	/// </summary>
	public required IReadOnlyList<RelationshipLevel> Levels { get; init; }

	/// <summary>
	/// Whether two characters with the given score are friends.
	/// </summary>
	public bool IsFriend(int score) => score >= FriendScore;

	/// <summary>
	/// Gets what a score is called: the last level whose minimum it reaches.
	/// </summary>
	public RelationshipLevel GetLevel(int score) => Levels.Last(x => x.Minimum <= score);

	/// <summary>
	/// Chooses the score of two characters, with the bonuses that apply to them, up to <see cref="MaximumScore"/>: one for
	/// how they're related if they're in the same household (null if they aren't), and one if they're neighbors in different households.
	/// </summary>
	public int ChooseScore(Random random, FamilyTie? tie, bool isNeighbor) =>
		Math.Min(random.NextInRange(Score) + GetFamilyBonus(tie) + (isNeighbor && tie is null ? NeighborBonus : 0), MaximumScore);

	/// <summary>
	/// Gets the bonus for how two members of the same household are related, or 0 if they aren't in the same household.
	/// </summary>
	public int GetFamilyBonus(FamilyTie? tie) => tie switch
	{
		null => 0,
		FamilyTie.Spouse => SpouseBonus,
		FamilyTie.Elder => ElderBonus,
		FamilyTie.ElderInLaw => ElderInLawBonus,
		_ => FamilyBonus,
	};

	internal IEnumerable<string> Validate(string path)
	{
		foreach (var error in CharacterData.ValidateRange($"{path}.score", Score))
			yield return error;
		if (Score.Minimum < 0)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.score.minimum", Score.Minimum, 0);
		if (Score.Maximum > MaximumScore)
			yield return OurResources.DataNumberTooLarge.FormatCurrentCulture($"{path}.score.maximum", Score.Maximum, MaximumScore);
		if (FamilyBonus < 0)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.familyBonus", FamilyBonus, 0);
		if (SpouseBonus < 0)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.spouseBonus", SpouseBonus, 0);
		if (ElderBonus < 0)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.elderBonus", ElderBonus, 0);
		if (ElderInLawBonus < 0)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.elderInLawBonus", ElderInLawBonus, 0);
		if (NeighborBonus < 0)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.neighborBonus", NeighborBonus, 0);
		if (FriendScore < 0)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.friendScore", FriendScore, 0);
		if (FriendScore > MaximumScore)
			yield return OurResources.DataNumberTooLarge.FormatCurrentCulture($"{path}.friendScore", FriendScore, MaximumScore);

		if (Levels.Count == 0)
			yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.levels");
		var previous = -1;
		for (var index = 0; index < Levels.Count; index++)
		{
			if (Levels[index] is not { } level)
			{
				yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.levels");
			}
			else
			{
				if (string.IsNullOrWhiteSpace(level.Title))
					yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.levels[{index}].title");
				if (index == 0 ? level.Minimum != 0 : level.Minimum <= previous || level.Minimum > MaximumScore)
					yield return OurResources.RelationshipLevelOutOfOrder.FormatCurrentCulture($"{path}.levels[{index}]", level.Minimum);
				previous = level.Minimum;
			}
		}
	}
}

/// <summary>
/// What relationship scores from a minimum up to the next level's minimum are called, such as "Friend".
/// </summary>
public sealed class RelationshipLevel
{
	public required int Minimum { get; init; }

	public required string Title { get; init; }
}
