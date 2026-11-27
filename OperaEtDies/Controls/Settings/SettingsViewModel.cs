using System.ComponentModel;
using GoldenAnvil.Utility;
using OperaEtDies.Settings;

namespace OperaEtDies.Controls.Settings;

/// <summary>
/// The settings screen, reached from the main menu: the text size, full screen, reducing motion, and how fast time
/// passes. Each is a set of choices, and choosing one changes the setting at once, so it shows straight away.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
	public SettingsViewModel(AppSettings settings)
	{
		m_settings = settings;
		TextSizes = Enumerable.Range(0, ((AppSettings.MaxTextSize - AppSettings.MinTextSize) / AppSettings.TextSizeStep) + 1)
			.Select(x => AppSettings.MinTextSize + (x * AppSettings.TextSizeStep))
			.Select(size => new SettingsChoiceViewModel(nameof(TextSizes), OurResources.TextSizeChoice.FormatCurrentCulture(size), () => settings.TextSize == size, () => settings.TextSize = size))
			.ToList();
		DisplayModes =
		[
			new SettingsChoiceViewModel(nameof(DisplayModes), OurResources.DisplayWindowed, () => !settings.IsFullScreen, () => settings.IsFullScreen = false),
			new SettingsChoiceViewModel(nameof(DisplayModes), OurResources.DisplayFullScreen, () => settings.IsFullScreen, () => settings.IsFullScreen = true),
		];
		ReduceMotionChoices =
		[
			CreateChoice(OurResources.ReduceMotionFollowWindows, ReduceMotion.FollowWindows),
			CreateChoice(OurResources.ReduceMotionOn, ReduceMotion.On),
			CreateChoice(OurResources.ReduceMotionOff, ReduceMotion.Off),
		];
		TimeSpeeds =
		[
			CreateChoice(OurResources.TimeSpeedSlow, TimeSpeed.Slow),
			CreateChoice(OurResources.TimeSpeedNormal, TimeSpeed.Normal),
			CreateChoice(OurResources.TimeSpeedFast, TimeSpeed.Fast),
			CreateChoice(OurResources.TimeSpeedVeryFast, TimeSpeed.VeryFast),
			CreateChoice(OurResources.TimeSpeedInstant, TimeSpeed.Instant),
		];
		settings.PropertyChanged += OnSettingsPropertyChanged;
	}

	/// <summary>
	/// Raised when the player goes back to the main menu.
	/// </summary>
	public event EventHandler? BackRequested;

	public IReadOnlyList<SettingsChoiceViewModel> TextSizes { get; }

	/// <summary>
	/// In a window, or full screen.
	/// </summary>
	public IReadOnlyList<SettingsChoiceViewModel> DisplayModes { get; }

	public IReadOnlyList<SettingsChoiceViewModel> ReduceMotionChoices { get; }

	public IReadOnlyList<SettingsChoiceViewModel> TimeSpeeds { get; }

	public void Back()
	{
		VerifyAccess();
		BackRequested.Raise(this);
	}

	public void Dispose() => m_settings.PropertyChanged -= OnSettingsPropertyChanged;

	private SettingsChoiceViewModel CreateChoice(string label, ReduceMotion value) =>
		new(nameof(ReduceMotionChoices), label, () => m_settings.ReduceMotion == value, () => m_settings.ReduceMotion = value);

	private SettingsChoiceViewModel CreateChoice(string label, TimeSpeed value) =>
		new(nameof(TimeSpeeds), label, () => m_settings.TimeSpeed == value, () => m_settings.TimeSpeed = value);

	// The settings can change from elsewhere too, such as full screen from F11.
	private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		foreach (var choice in TextSizes.Concat(DisplayModes).Concat(ReduceMotionChoices).Concat(TimeSpeeds))
			choice.Refresh();
	}

	private readonly AppSettings m_settings;
}

/// <summary>
/// One of the choices for a setting, which is selected while the setting has its value.
/// </summary>
public sealed class SettingsChoiceViewModel : ViewModelBase
{
	internal SettingsChoiceViewModel(string group, string label, Func<bool> isSelected, Action select)
	{
		Group = group;
		Label = label;
		m_isSelected = isSelected;
		m_select = select;
	}

	/// <summary>
	/// The name of the setting the choice belongs to, shared by its other choices.
	/// </summary>
	public string Group { get; }

	public string Label { get; }

	/// <summary>
	/// Whether the setting has this choice's value. Selecting it changes the setting; a choice can't be unselected other
	/// than by selecting another.
	/// </summary>
	public bool IsSelected
	{
		get => VerifyAccess(m_isSelected());
		set
		{
			VerifyAccess();
			if (value)
				m_select();
			Refresh();
		}
	}

	internal void Refresh() => RaisePropertyChanged(nameof(IsSelected));

	private readonly Func<bool> m_isSelected;
	private readonly Action m_select;
}
