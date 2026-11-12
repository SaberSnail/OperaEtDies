namespace OperaEtDies.Time;

/// <summary>
/// The date and time of a <see cref="GoldenAnvil.Utility.Calendar.TimePoint"/> in a <see cref="GameCalendar"/>. The
/// season, the week within the season, and the day within the week count from 1.
/// </summary>
public readonly record struct GameTimePointParts(int Year, int Season, int Week, int Day, int Hour, int Minute);

/// <summary>
/// The length of a <see cref="GoldenAnvil.Utility.Calendar.TimeOffset"/> in a <see cref="GameCalendar"/>. Every part
/// of a negative offset is zero or negative.
/// </summary>
public readonly record struct GameTimeOffsetParts(long Days, int Hours, int Minutes);
