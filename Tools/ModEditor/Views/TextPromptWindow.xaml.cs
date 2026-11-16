using System.Windows;
using System.Windows.Controls;

namespace ModEditor.Views;

/// <summary>
/// Asks for a line of text, such as a file name, with an optional list of suggestions.
/// </summary>
public partial class TextPromptWindow : Window
{
	public TextPromptWindow(string title, string message, string initialText, IReadOnlyList<string>? suggestions)
	{
		InitializeComponent();
		Title = title;
		MessageText.Text = message;
		InputBox.ItemsSource = suggestions ?? [];
		InputBox.Text = initialText;
		Loaded += (_, _) =>
		{
			InputBox.Focus();
			if (InputBox.Template.FindName("PART_EditableTextBox", InputBox) is TextBox textBox)
			{
				// File names are selected without their extension, so typing replaces just the name.
				var length = initialText.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? initialText.Length - ".json".Length : initialText.Length;
				textBox.Select(0, length);
			}
		};
	}

	public string Text => InputBox.Text;

	private void OnOkClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
