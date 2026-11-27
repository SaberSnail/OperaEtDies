namespace OperaEtDies.Settings;

/// <summary>
/// Keeps the player's settings between runs of the game.
/// </summary>
public interface IAppSettingsStore
{
	/// <summary>
	/// Loads the settings, with the default for any that haven't been kept or can't be read.
	/// </summary>
	AppSettingsValues Load();

	void Save(AppSettingsValues values);

	/// <summary>
	/// Forgets the kept settings, so the defaults are used.
	/// </summary>
	void Reset();
}

/// <summary>
/// The values of the player's settings, as they're kept (see <see cref="AppSettings"/>).
/// </summary>
public sealed record AppSettingsValues
{
	public int TextSize { get; init; } = AppSettings.DefaultTextSize;

	public ReduceMotion ReduceMotion { get; init; } = ReduceMotion.FollowWindows;

	public TimeSpeed TimeSpeed { get; init; } = TimeSpeed.Normal;

	public bool IsFullScreen { get; init; }
}
