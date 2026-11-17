using System.Text.Json.Serialization;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// A profession, from the professions section of the data: a value of the property with the profession role, and how its
/// businesses work. A profession without <see cref="Business"/>, such as keeping house, isn't a business.
/// </summary>
public sealed class Profession : IIdentified
{
	public required string Id { get; init; }

	public required string Title { get; init; }

	/// <summary>
	/// What the engine uses the profession for, such as the homemaker role, or null for none.
	/// </summary>
	public string? Role { get; init; }

	/// <summary>
	/// The groups the profession is in, which conditions can name in place of listing professions.
	/// </summary>
	public IReadOnlyList<string>? Groups { get; init; }

	/// <summary>
	/// Whether the player can choose the profession.
	/// </summary>
	public bool Player { get; init; } = true;

	/// <summary>
	/// How common the profession is when chosen at random.
	/// </summary>
	public FrequencyLevel Frequency { get; init; } = new(Utility.Random.Frequency.Common);

	/// <summary>
	/// Frequencies that replace <see cref="Frequency"/> when their conditions are met.
	/// </summary>
	public IReadOnlyList<FrequencyOverride>? FrequencyWhen { get; init; }

	/// <summary>
	/// Named pieces of text that character templates can use, such as a description.
	/// </summary>
	public IReadOnlyDictionary<string, string> Text { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

	/// <summary>
	/// A condition on character properties that must be met for a character to have the profession.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// Default values for string character properties whose defaults are keyed by the profession, by property id, such
	/// as the usual last names of people in the profession.
	/// </summary>
	public IReadOnlyDictionary<string, IReadOnlyList<WeightedString>>? Defaults { get; init; }

	/// <summary>
	/// How the profession's businesses work, or null if it isn't a business.
	/// </summary>
	public ProfessionBusiness? Business { get; init; }

	/// <summary>
	/// How much more (or, when negative, less) modesty the profession calls for while working, keyed by the id of the
	/// modesty rule someone follows, then by slot id, such as the extra covering of someone whose work calls for it.
	/// </summary>
	public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Modesty { get; init; } = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal);

	/// <summary>
	/// Whether the profession is a business.
	/// </summary>
	[JsonIgnore]
	public bool IsBusiness => Business is not null;

	/// <summary>
	/// How much a business in this profession earns, or null if it isn't a business.
	/// </summary>
	[JsonIgnore]
	public ProfessionWealth? Wealth => Business?.Wealth;

	/// <summary>
	/// Gets the amount a business in this profession earns, or 0 if it isn't a business.
	/// </summary>
	public double GetWealthAmount() => Wealth?.GetAmount() ?? 0;

	/// <summary>
	/// The most businesses in this profession the village can have, or null for no limit.
	/// </summary>
	[JsonIgnore]
	public int? MaxBusinesses => Business?.MaxBusinesses;

	/// <summary>
	/// The most members of the owner's household that can help in each business, or null for no limit.
	/// </summary>
	[JsonIgnore]
	public int? Helpers => Business?.Helpers;

	/// <summary>
	/// A condition a character must meet to help in a business of this profession, or null if anyone can help.
	/// </summary>
	[JsonIgnore]
	public CharacterCondition? HelperWhen => Business?.HelperWhen;

	/// <summary>
	/// Whether the business has no place of its own, because the work is done at home or in other people's homes.
	/// </summary>
	[JsonIgnore]
	public bool WorksFromHome => Business?.WorksFromHome ?? false;

	/// <summary>
	/// Whether a household elder can take up this profession as their own business.
	/// </summary>
	[JsonIgnore]
	public bool ForHouseholdElders => Business?.ForHouseholdElders ?? false;

	/// <summary>
	/// The hours people in this profession work, in place of the hours of the needs met at the workplace, or null to use
	/// those needs' hours.
	/// </summary>
	[JsonIgnore]
	public Hours? WorkHours => Business?.Hours;

	/// <summary>
	/// Whether a business in this profession can have another helper, given how many it has.
	/// </summary>
	public bool HasRoomForHelper(int helperCount) => Helpers is not { } max || helperCount < max;

	/// <summary>
	/// Whether the village can have another business in this profession, given how many it has.
	/// </summary>
	public bool HasRoomForBusiness(int businessCount) => MaxBusinesses is not { } max || businessCount < max;

	/// <summary>
	/// Creates the character property value for the profession.
	/// </summary>
	public CharacterPropertyOption ToOption() => new()
	{
		Id = Id,
		Title = Title,
		Role = Role,
		Groups = Groups,
		Frequency = Frequency,
		FrequencyWhen = FrequencyWhen,
		Text = Text,
		When = When,
		IsPlayerAllowed = Player,
	};
}

/// <summary>
/// How the businesses of a profession work.
/// </summary>
public sealed class ProfessionBusiness
{
	/// <summary>
	/// How much a business in this profession earns.
	/// </summary>
	public required ProfessionWealth Wealth { get; init; }

	/// <summary>
	/// The most businesses in this profession the village can have, or null for no limit.
	/// </summary>
	public int? MaxBusinesses { get; init; }

	/// <summary>
	/// The most members of the owner's household that can help in each business, or null for no limit.
	/// </summary>
	public int? Helpers { get; init; }

	/// <summary>
	/// A condition a character must meet to help in a business of this profession. Without it, anyone can help.
	/// </summary>
	public CharacterCondition? HelperWhen { get; init; }

	/// <summary>
	/// Whether the business has no place of its own, because the work is done at home or in other people's homes.
	/// Otherwise, the places a business can be run from are the business templates for this profession.
	/// </summary>
	public bool WorksFromHome { get; init; }

	/// <summary>
	/// Whether a household elder can take up this profession as their own business. Only allowed for professions that
	/// work from home.
	/// </summary>
	public bool ForHouseholdElders { get; init; }

	/// <summary>
	/// The hours people in this profession work, in place of the hours of the needs met at the workplace. Only the start
	/// and end are used.
	/// </summary>
	public Hours? Hours { get; init; }
}
