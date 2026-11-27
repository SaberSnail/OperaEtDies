using System.Windows;

namespace OperaEtDies.Settings;

/// <summary>
/// The player's preferences for how the game looks and runs, chosen on the settings screen: the text size, whether to
/// reduce motion, how fast time passes, and full screen. They aren't game state, so they aren't saved with games. Each
/// change is kept in the store at once, so the settings are the same the next time the game starts.
/// </summary>
public sealed class AppSettings : ViewModelBase
{
	/// <summary>
	/// Creates the settings from those kept in the store, or the defaults without one. Values out of range are brought
	/// into it.
	/// </summary>
	public AppSettings(IAppSettingsStore? store = null)
	{
		m_store = store;
		var values = store?.Load() ?? new AppSettingsValues();
		m_textSize = NormalizeTextSize(values.TextSize);
		m_reduceMotion = Enum.IsDefined(values.ReduceMotion) ? values.ReduceMotion : ReduceMotion.FollowWindows;
		m_timeSpeed = Enum.IsDefined(values.TimeSpeed) ? values.TimeSpeed : TimeSpeed.Normal;
		m_isFullScreen = values.IsFullScreen;
	}

	/// <summary>
	/// The smallest text size, as a percentage of the theme's.
	/// </summary>
	public const int MinTextSize = 90;

	/// <summary>
	/// The largest text size, as a percentage of the theme's.
	/// </summary>
	public const int MaxTextSize = 150;

	/// <summary>
	/// The steps between text sizes.
	/// </summary>
	public const int TextSizeStep = 10;

	public const int DefaultTextSize = 100;

	/// <summary>
	/// The size of text as a percentage of the theme's sizes, from <see cref="MinTextSize"/> to <see cref="MaxTextSize"/>
	/// in steps of <see cref="TextSizeStep"/>.
	/// </summary>
	public int TextSize
	{
		get => VerifyAccess(m_textSize);
		set
		{
			if (SetPropertyField(NormalizeTextSize(value), ref m_textSize))
				Save();
		}
	}

	/// <summary>
	/// Whether to reduce motion, or to follow Windows' own setting for showing animations.
	/// </summary>
	public ReduceMotion ReduceMotion
	{
		get => VerifyAccess(m_reduceMotion);
		set
		{
			if (SetPropertyField([nameof(ReduceMotion), nameof(IsMotionReduced)], value, ref m_reduceMotion))
				Save();
		}
	}

	/// <summary>
	/// Whether animations are turned off, from <see cref="ReduceMotion"/>, or from Windows' own setting when it follows
	/// that.
	/// </summary>
	public bool IsMotionReduced => ReduceMotion switch
	{
		ReduceMotion.On => true,
		ReduceMotion.Off => false,
		_ => !SystemParameters.ClientAreaAnimation,
	};

	/// <summary>
	/// How fast time passes when the player does something that takes time.
	/// </summary>
	public TimeSpeed TimeSpeed
	{
		get => VerifyAccess(m_timeSpeed);
		set
		{
			if (SetPropertyField(value, ref m_timeSpeed))
				Save();
		}
	}

	/// <summary>
	/// Whether the window fills the whole screen, without a frame. It's kept, so the game starts the way it was left.
	/// </summary>
	public bool IsFullScreen
	{
		get => VerifyAccess(m_isFullScreen);
		set
		{
			if (SetPropertyField(value, ref m_isFullScreen))
				Save();
		}
	}

	/// <summary>
	/// Rounds a text size to the nearest step, within the range.
	/// </summary>
	internal static int NormalizeTextSize(int textSize)
	{
		var clamped = Math.Clamp(textSize, MinTextSize, MaxTextSize);
		return MinTextSize + ((int) Math.Round((clamped - MinTextSize) / (double) TextSizeStep, MidpointRounding.AwayFromZero) * TextSizeStep);
	}

	private void Save() => m_store?.Save(new AppSettingsValues
	{
		TextSize = m_textSize,
		ReduceMotion = m_reduceMotion,
		TimeSpeed = m_timeSpeed,
		IsFullScreen = m_isFullScreen,
	});

	private readonly IAppSettingsStore? m_store;
	private int m_textSize;
	private ReduceMotion m_reduceMotion;
	private TimeSpeed m_timeSpeed;
	private bool m_isFullScreen;
}

/// <summary>
/// Whether to turn animations off.
/// </summary>
public enum ReduceMotion
{
	/// <summary>
	/// Follow Windows' own setting for showing animations.
	/// </summary>
	FollowWindows,

	/// <summary>
	/// Turn animations off.
	/// </summary>
	On,

	/// <summary>
	/// Show animations.
	/// </summary>
	Off,
}

/// <summary>
/// How fast time passes when the player does something that takes time.
/// </summary>
public enum TimeSpeed
{
	Slow,
	Normal,
	Fast,
	VeryFast,

	/// <summary>
	/// Time passes at once, so there's nothing to watch or stop.
	/// </summary>
	Instant,
}

public static class TimeSpeedUtility
{
	/// <summary>
	/// Gets how much longer than usual time takes to pass at this speed, or null if it passes at once.
	/// </summary>
	public static double? GetPaceScale(this TimeSpeed speed) => speed switch
	{
		TimeSpeed.Slow => 2,
		TimeSpeed.Fast => 0.5,
		TimeSpeed.VeryFast => 0.25,
		TimeSpeed.Instant => null,
		_ => 1,
	};
}
