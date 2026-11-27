using System.Windows;
using System.Windows.Controls;

namespace OperaEtDies.Controls;

/// <summary>
/// The window's layout of a column on each side of the center, such as the player's character on the left and the state
/// of the game during play. The center (the content) gets whatever room the side columns leave. A side column without
/// content isn't shown.
/// </summary>
/// <remarks>
/// The side columns have widths in ems, so they scale with the text size. When the window is too narrow for the center to
/// keep a comfortable line length, the left column switches to a narrower compact mode, and if that isn't enough, the
/// right column does too. What's in a column sees whether it's compact through <see cref="IsCompactProperty"/>.
/// </remarks>
public sealed class ColumnLayout : ContentControl
{
	/// <summary>
	/// The widths of the side columns, in ems, normally and in compact mode.
	/// </summary>
	public const double LeftEms = 15;
	public const double LeftCompactEms = 8;
	public const double RightEms = 19;
	public const double RightCompactEms = 12;

	/// <summary>
	/// The narrowest the center's text can be, in ems, before the side columns switch to compact mode: about 45 characters.
	/// </summary>
	public const double MinimumCenterEms = 25;

	/// <summary>
	/// How much wider than the narrowest it can be, in ems, the center's text must be able to get before a compact column
	/// switches back, so the layout doesn't flicker back and forth while the window is resized around the threshold.
	/// </summary>
	public const double HysteresisEms = 1;

	/// <summary>
	/// The room around the center's text that isn't in ems, in device-independent pixels: its margin on each side, and the
	/// side columns' borders.
	/// </summary>
	public const double CenterPadding = 50;

	public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register(nameof(LeftContent), typeof(object), typeof(ColumnLayout),
		new PropertyMetadata(null, (d, _) => ((ColumnLayout) d).UpdateCompact()));

	public static readonly DependencyProperty RightContentProperty = DependencyProperty.Register(nameof(RightContent), typeof(object), typeof(ColumnLayout),
		new PropertyMetadata(null, (d, _) => ((ColumnLayout) d).UpdateCompact()));

	private static readonly DependencyPropertyKey IsLeftCompactPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsLeftCompact), typeof(bool), typeof(ColumnLayout), new PropertyMetadata(false));

	public static readonly DependencyProperty IsLeftCompactProperty = IsLeftCompactPropertyKey.DependencyProperty;

	private static readonly DependencyPropertyKey IsRightCompactPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsRightCompact), typeof(bool), typeof(ColumnLayout), new PropertyMetadata(false));

	public static readonly DependencyProperty IsRightCompactProperty = IsRightCompactPropertyKey.DependencyProperty;

	/// <summary>
	/// Whether the side column an element is in is in compact mode, inherited by everything in the column, so its templates
	/// can show a smaller portrait or mini-map.
	/// </summary>
	public static readonly DependencyProperty IsCompactProperty = DependencyProperty.RegisterAttached("IsCompact", typeof(bool), typeof(ColumnLayout),
		new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

	public static bool GetIsCompact(DependencyObject element) => (bool) element.GetValue(IsCompactProperty);

	public static void SetIsCompact(DependencyObject element, bool value) => element.SetValue(IsCompactProperty, value);

	/// <summary>
	/// What's in the left column.
	/// </summary>
	public object? LeftContent
	{
		get => GetValue(LeftContentProperty);
		set => SetValue(LeftContentProperty, value);
	}

	/// <summary>
	/// What's in the right column.
	/// </summary>
	public object? RightContent
	{
		get => GetValue(RightContentProperty);
		set => SetValue(RightContentProperty, value);
	}

	/// <summary>
	/// Whether the left column is in compact mode.
	/// </summary>
	public bool IsLeftCompact
	{
		get => (bool) GetValue(IsLeftCompactProperty);
		private set => SetValue(IsLeftCompactPropertyKey, value);
	}

	/// <summary>
	/// Whether the right column is in compact mode.
	/// </summary>
	public bool IsRightCompact
	{
		get => (bool) GetValue(IsRightCompactProperty);
		private set => SetValue(IsRightCompactPropertyKey, value);
	}

	/// <summary>
	/// Decides which side columns are in compact mode for the layout's width and font size, given which are now, so the
	/// center's text keeps at least <see cref="MinimumCenterEms"/>. The left column goes compact first, and the right column
	/// only if that isn't enough. A compact column only switches back once the center would have
	/// <see cref="HysteresisEms"/> more than that to spare. A column that isn't there is never compact.
	/// </summary>
	public static (bool Left, bool Right) GetCompactColumns(double width, double fontSize, bool hasLeft, bool hasRight, bool isLeftCompact, bool isRightCompact)
	{
		// The center's text, in ems, with the side columns at the given widths.
		double GetCenterEms(double leftEms, double rightEms) => ((width - CenterPadding) / fontSize) - (hasLeft ? leftEms : 0) - (hasRight ? rightEms : 0);

		// A column is compact if the center would be too narrow without it, or it already is and switching back would leave
		// the center within the hysteresis of too narrow.
		bool IsTooNarrow(double centerEms, bool isCompact) => centerEms < MinimumCenterEms + (isCompact ? HysteresisEms : 0);

		if (!(width > 0) || !(fontSize > 0))
			return (isLeftCompact, isRightCompact);

		var left = hasLeft && IsTooNarrow(GetCenterEms(LeftEms, RightEms), isLeftCompact);
		var right = hasRight && (left || !hasLeft) && IsTooNarrow(GetCenterEms(LeftCompactEms, RightEms), isRightCompact);
		return (left, right);
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		base.OnRenderSizeChanged(sizeInfo);
		if (sizeInfo.WidthChanged)
			UpdateCompact();
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		if (e.Property == FontSizeProperty)
			UpdateCompact();
	}

	private void UpdateCompact()
	{
		var (left, right) = GetCompactColumns(ActualWidth, FontSize, LeftContent is not null, RightContent is not null, IsLeftCompact, IsRightCompact);
		IsLeftCompact = left;
		IsRightCompact = right;
	}
}
