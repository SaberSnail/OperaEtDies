namespace OperaEtDies.Data;

/// <summary>
/// How well covered someone must be, from the modesty rules section of the data: for each slot, the least modesty the
/// clothing worn in it must have, at home, out of the house, and at formal occasions. Each person follows the first rule
/// whose condition they meet.
/// </summary>
public sealed class ModestyRule : IIdentified
{
	public required string Id { get; init; }

	/// <summary>
	/// A condition on character properties that must be met for a character to follow the rule.
	/// </summary>
	public CharacterCondition? When { get; init; }

	/// <summary>
	/// The least modesty in each slot at home, keyed by slot id.
	/// </summary>
	public IReadOnlyDictionary<string, int> Home { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

	/// <summary>
	/// The least modesty in each slot out of the house, keyed by slot id.
	/// </summary>
	public IReadOnlyDictionary<string, int> Outside { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

	/// <summary>
	/// The least modesty in each slot at formal occasions, such as church, keyed by slot id.
	/// </summary>
	public IReadOnlyDictionary<string, int> Formal { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the least modesty in each slot at a level of formality.
	/// </summary>
	public IReadOnlyDictionary<string, int> GetMinimums(ModestyLevel level) => level switch
	{
		ModestyLevel.Home => Home,
		ModestyLevel.Outside => Outside,
		_ => Formal,
	};
}

/// <summary>
/// How formal a place or occasion is, which decides how well covered someone must be.
/// </summary>
public enum ModestyLevel
{
	/// <summary>
	/// In someone's own home.
	/// </summary>
	Home,

	/// <summary>
	/// Out of the house.
	/// </summary>
	Outside,

	/// <summary>
	/// At a formal occasion, such as church.
	/// </summary>
	Formal,
}
