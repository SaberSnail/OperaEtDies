using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// A profession, loaded from Professions.json: the value a character can have for the property whose values come from
/// the professions, and how its businesses work. A profession without business settings, such as keeping house, isn't
/// a business.
/// </summary>
public sealed class Profession : IIdentified
{
	public required string Id { get; init; }

	public required string Title { get; init; }

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
	/// How hard the profession is to hide from obscurers.
	/// </summary>
	public int? Prominence { get; init; }

	/// <summary>
	/// Default values for string character properties whose defaults are keyed by the profession, by property id, such
	/// as the usual last names of people in the profession.
	/// </summary>
	public IReadOnlyDictionary<string, IReadOnlyList<WeightedString>>? Defaults { get; init; }

	/// <summary>
	/// How much a business in this profession earns. Every business has one.
	/// </summary>
	public ProfessionWealth? Wealth { get; init; }

	/// <summary>
	/// Gets the amount a business in this profession earns, or 0 if it isn't a business.
	/// </summary>
	public double GetWealthAmount() => Wealth?.GetAmount() ?? 0;

	/// <summary>
	/// Whether any business setting is given, which only businesses may have.
	/// </summary>
	public bool HasBusinessSettings => Wealth is not null || MaxBusinesses is not null || Helpers is not null || HelperWhen is not null || WorksFromHome || ForHouseholdElders;

	/// <summary>
	/// Creates the character property value for the profession.
	/// </summary>
	public CharacterPropertyOption ToOption() => new()
	{
		Id = Id,
		Title = Title,
		Frequency = Frequency,
		FrequencyWhen = FrequencyWhen,
		Text = Text,
		When = When,
		Prominence = Prominence,
	};

	/// <summary>
	/// The most businesses in this profession the town can have, or null for no limit.
	/// </summary>
	public int? MaxBusinesses { get; init; }

	/// <summary>
	/// The most members of the owner's household that can help in each business, or null for no limit.
	/// </summary>
	public int? Helpers { get; init; }

	/// <summary>
	/// Whether a business in this profession can have another helper, given how many it has.
	/// </summary>
	public bool HasRoomForHelper(int helperCount) => Helpers is not { } max || helperCount < max;

	/// <summary>
	/// A condition a character must meet to help in a business of this profession. Without it, a helper must be
	/// allowed to have the profession.
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
	/// Whether the town can have another business in this profession, given how many it has.
	/// </summary>
	public bool HasRoomForBusiness(int businessCount) => MaxBusinesses is not { } max || businessCount < max;
}
