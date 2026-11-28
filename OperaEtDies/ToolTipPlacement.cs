using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace OperaEtDies;

/// <summary>
/// Places tooltips with their top left corner just below the mouse pointer, or above it when there's no room below. WPF's
/// own placement at the mouse opens them to the left of the pointer instead when Windows is set to open menus to the left
/// (its handedness setting, which tablets often have), so the tooltip style uses this:
/// <c>&lt;Setter Property="local:ToolTipPlacement.IsAtMouse" Value="True" /&gt;</c>.
/// </summary>
public static class ToolTipPlacement
{
	public static readonly DependencyProperty IsAtMouseProperty =
		DependencyProperty.RegisterAttached("IsAtMouse", typeof(bool), typeof(ToolTipPlacement), new PropertyMetadata(false, OnIsAtMouseChanged));

	public static bool GetIsAtMouse(DependencyObject d) => (bool) d.GetValue(IsAtMouseProperty);

	public static void SetIsAtMouse(DependencyObject d, bool value) => d.SetValue(IsAtMouseProperty, value);

	/// <summary>
	/// How far below the pointer's hot spot a tooltip starts, in device-independent pixels, so it clears the arrow.
	/// </summary>
	private const double c_pointerHeight = 20;

	private static void OnIsAtMouseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not ToolTip toolTip)
			throw new InvalidOperationException($"{nameof(IsAtMouseProperty)} can only be set on a {nameof(ToolTip)}.");

		if ((bool) e.NewValue)
		{
			toolTip.Placement = PlacementMode.Custom;
			toolTip.CustomPopupPlacementCallback = (popupSize, _, _) => PlaceAtMouse(toolTip, popupSize);
		}
		else
		{
			toolTip.ClearValue(ToolTip.PlacementProperty);
			toolTip.ClearValue(ToolTip.CustomPopupPlacementCallbackProperty);
		}
	}

	/// <summary>
	/// Gets where the tooltip goes, relative to its target: below the pointer, or failing that, above it. The popup works in
	/// device pixels, so the pointer's position is turned into them.
	/// </summary>
	private static CustomPopupPlacement[] PlaceAtMouse(ToolTip toolTip, Size popupSize)
	{
		if (toolTip.PlacementTarget is not UIElement target)
			return [];

		var toDevice = PresentationSource.FromVisual(target)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
		var pointer = toDevice.Transform(Mouse.GetPosition(target));
		var below = toDevice.Transform(new Vector(0, c_pointerHeight)).Y;
		return
		[
			new CustomPopupPlacement(new Point(pointer.X, pointer.Y + below), PopupPrimaryAxis.Horizontal),
			new CustomPopupPlacement(new Point(pointer.X, pointer.Y - popupSize.Height), PopupPrimaryAxis.Horizontal),
		];
	}
}
