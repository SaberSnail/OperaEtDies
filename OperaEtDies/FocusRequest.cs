using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace OperaEtDies;

/// <summary>
/// Lets a view model move keyboard focus into part of the view: bind <c>FocusRequest.Request</c> on that part to a view
/// model property, and each time the property changes to a new value, keyboard focus moves to the first control in it
/// that can take focus, if there is one.
/// </summary>
public static class FocusRequest
{
	public static readonly DependencyProperty RequestProperty =
		DependencyProperty.RegisterAttached("Request", typeof(object), typeof(FocusRequest), new PropertyMetadata(null, OnRequestChanged));

	public static object? GetRequest(DependencyObject d) => d.GetValue(RequestProperty);

	public static void SetRequest(DependencyObject d, object? value) => d.SetValue(RequestProperty, value);

	private static void OnRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue is not null && d is UIElement element && FindFocusable(element) is { } target)
			Keyboard.Focus(target);
	}

	/// <summary>
	/// Finds the first control in an element, in the order of the visual tree, that can take keyboard focus now.
	/// </summary>
	private static IInputElement? FindFocusable(DependencyObject parent)
	{
		var count = VisualTreeHelper.GetChildrenCount(parent);
		for (var index = 0; index < count; index++)
		{
			var child = VisualTreeHelper.GetChild(parent, index);
			if (child is UIElement { Focusable: true, IsEnabled: true, IsVisible: true } element && KeyboardNavigation.GetIsTabStop(element))
				return element;
			if (child is UIElement { IsVisible: false })
				continue;
			if (FindFocusable(child) is { } found)
				return found;
		}

		return null;
	}
}
