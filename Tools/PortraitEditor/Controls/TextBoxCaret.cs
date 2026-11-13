using System.Windows;
using System.Windows.Controls;

namespace PortraitEditor.Controls;

/// <summary>
/// Makes a text box's caret position bindable: set IsTracked on the text box, and bind Index. Setting the index from
/// the binding moves the caret.
/// </summary>
public static class TextBoxCaret
{
	public static readonly DependencyProperty IsTrackedProperty = DependencyProperty.RegisterAttached(
		"IsTracked", typeof(bool), typeof(TextBoxCaret), new PropertyMetadata(false, OnIsTrackedChanged));

	public static readonly DependencyProperty IndexProperty = DependencyProperty.RegisterAttached(
		"Index", typeof(int), typeof(TextBoxCaret), new FrameworkPropertyMetadata(0, OnIndexChanged));

	public static bool GetIsTracked(DependencyObject element) => (bool) element.GetValue(IsTrackedProperty);

	public static void SetIsTracked(DependencyObject element, bool value) => element.SetValue(IsTrackedProperty, value);

	public static int GetIndex(DependencyObject element) => (int) element.GetValue(IndexProperty);

	public static void SetIndex(DependencyObject element, int value) => element.SetValue(IndexProperty, value);

	private static void OnIsTrackedChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
	{
		if (element is TextBox textBox)
		{
			textBox.SelectionChanged -= OnSelectionChanged;
			if ((bool) e.NewValue)
				textBox.SelectionChanged += OnSelectionChanged;
		}
	}

	/// <summary>
	/// Moves the caret when the index is set from the binding.
	/// </summary>
	private static void OnIndexChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
	{
		if (element is TextBox textBox && GetIsTracked(textBox))
		{
			var index = Math.Clamp((int) e.NewValue, 0, textBox.Text.Length);
			if (index != textBox.CaretIndex)
				textBox.CaretIndex = index;
		}
	}

	private static void OnSelectionChanged(object sender, RoutedEventArgs e)
	{
		if (sender is TextBox textBox)
			textBox.SetCurrentValue(IndexProperty, textBox.CaretIndex);
	}
}
