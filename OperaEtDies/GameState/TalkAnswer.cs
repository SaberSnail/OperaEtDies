using GoldenAnvil.Utility.Calendar;

namespace OperaEtDies.GameState;

/// <summary>
/// What came of asking someone something.
/// </summary>
/// <param name="Time">How long it took.</param>
/// <param name="Sighting">What the player learned: what they saw and were told about the person they asked, or the person
/// they asked about, or null if the person asked hadn't seen them.</param>
public sealed record TalkAnswer(TimeOffset Time, Sighting? Sighting);
