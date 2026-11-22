using OperaEtDies.Data;

namespace ModEditor.Model;

/// <summary>
/// The settings a place in the data has when it's written as an object rather than named (see
/// <see cref="SchedulePlace"/>): a quarter of some kinds, or a place a "remember" step remembered. The game reads places
/// with a converter, so the editor uses this to make fields for them.
/// </summary>
public sealed class PlaceObject
{
	public IReadOnlyList<QuarterChoice>? QuarterKinds { get; init; }

	public bool Nearest { get; init; }

	public string? Remembered { get; init; }
}
