using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// How comfortable people are with how covered they are, from the modesty settings section of the data. Comfort goes from
/// 0 to 100: it starts from the company someone is in, or with no one there, how likely someone is to come by, and adds
/// the comfort of the place, of the step they're on, and of their boldness. Their modesty rule turns it into the least
/// modesty each slot needs.
/// </summary>
public sealed class ModestySettings
{
	/// <summary>
	/// The name of the data section the settings are read from.
	/// </summary>
	public const string SectionName = "modestySettings";

	/// <summary>
	/// The most comfort there can be.
	/// </summary>
	public const int MaxComfort = 100;

	/// <summary>
	/// The settings used when the data has none.
	/// </summary>
	public static ModestySettings None { get; } = new();

	/// <summary>
	/// The comfort of being seen by each kind of person.
	/// </summary>
	public ModestyCompany Company { get; init; } = new();

	/// <summary>
	/// The comfort added when everyone who can see someone is of the same gender.
	/// </summary>
	public int SameGender { get; init; } = 10;

	/// <summary>
	/// How much, from 0 to 100 percent, someone who's asleep counts compared to being awake: their comfort is this far
	/// from the comfort of no one being there to the comfort of their company.
	/// </summary>
	public int Asleep { get; init; } = 50;

	/// <summary>
	/// The comfort of being where no one is, by how likely someone is to come by.
	/// </summary>
	public ModestyTraffic Traffic { get; init; } = new();

	/// <summary>
	/// How someone's boldness changes their comfort, or null if it doesn't.
	/// </summary>
	public ModestyBoldness? Boldness { get; init; }

	/// <summary>
	/// How people react when they find themselves less covered than they're comfortable with, such as when someone comes in.
	/// </summary>
	public ModestyCoverUp CoverUp { get; init; } = new();

	/// <summary>
	/// Gets the comfort of being where no one is: the comfort of a level of traffic, less <see cref="ModestyTraffic.Step"/>
	/// for each step it's more common, including the steps the part of the day adds.
	/// </summary>
	public int GetTrafficComfort(FrequencyLevel traffic, int dayPartSteps)
	{
		var comfort = traffic.Tier switch
		{
			Frequency.Common => Traffic.Common,
			Frequency.Uncommon => Traffic.Uncommon,
			Frequency.Rare => Traffic.Rare,
			_ => Traffic.VeryRare,
		};
		return Math.Clamp(comfort - ((traffic.Steps + dayPartSteps) * Traffic.Step), 0, MaxComfort);
	}

	/// <summary>
	/// Gets how much a character's boldness adds to their comfort, or takes away from it.
	/// </summary>
	public double GetBoldnessComfort(CharacterTraits traits) =>
		Boldness is { } boldness && traits.Values.GetValueOrDefault(boldness.Property) is int value ? (value - boldness.Neutral) * boldness.PerPoint : 0;

	internal IEnumerable<string> Validate()
	{
		var path = SectionName;
		var values = new[]
		{
			("company.spouse", Company.Spouse), ("company.household", Company.Household), ("company.friend", Company.Friend), ("company.anyone", Company.Anyone),
			("traffic.common", Traffic.Common), ("traffic.uncommon", Traffic.Uncommon), ("traffic.rare", Traffic.Rare), ("traffic.veryRare", Traffic.VeryRare),
			("asleep", Asleep),
		};
		var errors = values.SelectMany(x => ValidateRange($"{path}.{x.Item1}", x.Item2, 0))
			.Concat(new[] { ("sameGender", SameGender), ("traffic.step", Traffic.Step) }.SelectMany(x => ValidateRange($"{path}.{x.Item1}", x.Item2, -MaxComfort)));
		foreach (var error in errors)
			yield return error;

		if (Boldness is { } boldness && string.IsNullOrWhiteSpace(boldness.Property))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.boldness.property");

		var coverUp = CoverUp ?? new ModestyCoverUp();
		foreach (var (name, text) in new[] { ("activity", coverUp.Activity), ("retreatActivity", coverUp.RetreatActivity), ("embarrassedActivity", coverUp.EmbarrassedActivity), ("waitActivity", coverUp.WaitActivity) }.Where(x => string.IsNullOrWhiteSpace(x.Item2)))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.coverUp.{name}");
		if (coverUp.Exposed is { } exposed && string.IsNullOrWhiteSpace(exposed.Text))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.coverUp.exposed");
		if (coverUp.WaitMinutes < 0)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.coverUp.waitMinutes", coverUp.WaitMinutes, 0);
		if (coverUp.CheckMinutes < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.coverUp.checkMinutes", coverUp.CheckMinutes, 1);
	}

	/// <summary>
	/// Checks that a number is from the given minimum to <see cref="MaxComfort"/>.
	/// </summary>
	internal static IEnumerable<string> ValidateRange(string path, int value, int minimum)
	{
		if (value < minimum)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture(path, value, minimum);
		else if (value > MaxComfort)
			yield return OurResources.DataNumberTooLarge.FormatCurrentCulture(path, value, MaxComfort);
	}
}

/// <summary>
/// The comfort of being seen by each kind of person, from 0 to 100.
/// </summary>
public sealed class ModestyCompany
{
	/// <summary>
	/// One's husband or wife.
	/// </summary>
	public int Spouse { get; init; } = 75;

	/// <summary>
	/// Anyone else in one's household.
	/// </summary>
	public int Household { get; init; } = 55;

	/// <summary>
	/// A friend.
	/// </summary>
	public int Friend { get; init; } = 40;

	/// <summary>
	/// Anyone else.
	/// </summary>
	public int Anyone { get; init; } = 30;
}

/// <summary>
/// The comfort, from 0 to 100, of being where no one is, by how likely someone is to come by, and the traffic of places
/// that don't give their own.
/// </summary>
public sealed class ModestyTraffic
{
	public int Common { get; init; } = 40;

	public int Uncommon { get; init; } = 60;

	public int Rare { get; init; } = 80;

	public int VeryRare { get; init; } = 90;

	/// <summary>
	/// The comfort taken away for each step of traffic more common than its frequency, such as "common+", or added for each
	/// step less common.
	/// </summary>
	public int Step { get; init; } = 5;

	/// <summary>
	/// The traffic of a quarter whose kind gives none.
	/// </summary>
	public FrequencyLevel Quarter { get; init; } = new(Frequency.Common);

	/// <summary>
	/// The traffic of a house that gives none.
	/// </summary>
	public FrequencyLevel House { get; init; } = new(Frequency.Rare);

	/// <summary>
	/// The traffic of a business's place that gives none.
	/// </summary>
	public FrequencyLevel Business { get; init; } = new(Frequency.Uncommon);
}

/// <summary>
/// How an integer character property, such as boldness, changes someone's comfort: by <see cref="PerPoint"/> for each
/// point it's above <see cref="Neutral"/>, or taken away for each point below.
/// </summary>
public sealed class ModestyBoldness
{
	/// <summary>
	/// The id of an integer character property.
	/// </summary>
	public required string Property { get; init; }

	/// <summary>
	/// The value that changes comfort by nothing.
	/// </summary>
	public int Neutral { get; init; } = 50;

	/// <summary>
	/// How much comfort each point above <see cref="Neutral"/> adds.
	/// </summary>
	public double PerPoint { get; init; } = 0.4;
}

/// <summary>
/// How people react when they find themselves less covered than they're comfortable with: when someone comes in or wakes,
/// they change into clothes at hand that are enough, or with none, head home, unless whoever made them uncomfortable is
/// only passing through. Before changing into less for a step, they wait a while for whoever makes them uncomfortable to
/// leave.
/// </summary>
public sealed class ModestyCoverUp
{
	/// <summary>
	/// What someone covering up is shown doing.
	/// </summary>
	public string Activity { get; init; } = "Covering up";

	/// <summary>
	/// What someone heading home to cover up is shown doing.
	/// </summary>
	public string RetreatActivity { get; init; } = "Hurrying home to cover up";

	/// <summary>
	/// What someone who can't cover up, or is only seen by people passing through, is shown doing.
	/// </summary>
	public string EmbarrassedActivity { get; init; } = "Looking embarrassed";

	/// <summary>
	/// What someone waiting for others to leave before they change is shown doing.
	/// </summary>
	public string WaitActivity { get; init; } = "Waiting for some privacy";

	/// <summary>
	/// What the player notices when they find themselves less covered than they're comfortable with, or null to be told only
	/// which parts of them they need to cover.
	/// </summary>
	public LongText? Exposed { get; init; }

	/// <summary>
	/// The most minutes someone waits for others to leave before changing into less for a step.
	/// </summary>
	public int WaitMinutes { get; init; } = 60;

	/// <summary>
	/// How many minutes someone waits at a time before checking again whether they can change.
	/// </summary>
	public int CheckMinutes { get; init; } = 5;
}
