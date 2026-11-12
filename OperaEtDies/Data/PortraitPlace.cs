namespace OperaEtDies.Data;

/// <summary>
/// Where a character is, for choosing the layers of their portrait: in a home, in the workplace of a profession, or
/// in a kind of quarter.
/// </summary>
public sealed record PortraitPlace
{
	/// <summary>
	/// In a home that isn't a workplace right now.
	/// </summary>
	public static PortraitPlace Home { get; } = new(PortraitPlaceKind.Home, null);

	/// <summary>
	/// In a quarter of the given kind.
	/// </summary>
	public static PortraitPlace InQuarter(string quarterKind) => new(PortraitPlaceKind.Quarter, quarterKind);

	/// <summary>
	/// In a workplace of the given profession: a business, or the home of someone working from home.
	/// </summary>
	public static PortraitPlace InWorkplace(string profession) => new(PortraitPlaceKind.Workplace, profession);

	public PortraitPlaceKind Kind { get; }

	/// <summary>
	/// The quarter kind or profession id, or null for a home.
	/// </summary>
	public string? Id { get; }

	public override string ToString() => Kind switch
	{
		PortraitPlaceKind.Home => "home",
		PortraitPlaceKind.Quarter => $"quarter:{Id}",
		_ => $"workplace:{Id}",
	};

	private PortraitPlace(PortraitPlaceKind kind, string? id)
	{
		Kind = kind;
		Id = id;
	}
}

/// <summary>
/// The kinds of place a character can be in, for drawing their portrait.
/// </summary>
public enum PortraitPlaceKind
{
	Home,
	Quarter,
	Workplace,
}

/// <summary>
/// The places a portrait layer is drawn in. It's met when the character is in any of them.
/// </summary>
public sealed class PortraitPlaceCondition
{
	/// <summary>
	/// Whether the layer is drawn in a home that isn't a workplace right now.
	/// </summary>
	public bool Home { get; init; }

	/// <summary>
	/// The ids of the kinds of quarter the layer is drawn in.
	/// </summary>
	public IReadOnlyList<string>? QuarterKinds { get; init; }

	/// <summary>
	/// The ids of the professions whose workplaces the layer is drawn in.
	/// </summary>
	public IReadOnlyList<string>? Workplaces { get; init; }

	/// <summary>
	/// Checks whether a character at a place meets the condition. A null place meets none.
	/// </summary>
	public bool IsMet(PortraitPlace? place) => place?.Kind switch
	{
		PortraitPlaceKind.Home => Home,
		PortraitPlaceKind.Quarter => QuarterKinds?.Contains(place.Id, StringComparer.Ordinal) ?? false,
		PortraitPlaceKind.Workplace => Workplaces?.Contains(place.Id, StringComparer.Ordinal) ?? false,
		_ => false,
	};
}
