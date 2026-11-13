using System.Windows;
using System.Windows.Data;

namespace PortraitEditor.Views;

/// <summary>
/// Lists the editor's keyboard and mouse shortcuts.
/// </summary>
public partial class ShortcutsWindowView : Window
{
	public ShortcutsWindowView()
	{
		InitializeComponent();
		var view = new ListCollectionView(s_shortcuts.ToList());
		view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Shortcut.Group)));
		ShortcutList.ItemsSource = view;
	}

	private static readonly Shortcut[] s_shortcuts =
	[
		new("Editing", "F1", "Show this list"),
		new("Editing", "Ctrl+Z", "Undo (in a text box, undoes its typing instead)"),
		new("Editing", "Ctrl+Y", "Redo"),
		new("Editing", "F5", "Load the data files again, discarding any edits"),
		new("Tree", "Click the box beside a layer or shape", "Go to the next canvas state: normal, forced (📌), hidden (⊘)"),
		new("Tree", "Alt+click the box", "Show only that layer or shape on the canvas"),
		new("Tree", "Right-click a layer or shape", "Set the canvas state of it, its layer, the filtered layers, or all layers"),
		new("Canvas", "Click", "Select the shape at that point, passing over mostly see-through shapes"),
		new("Canvas", "Ctrl+click", "Select the shape under the selected one, cycling through every shape at that point"),
		new("Canvas", "Drag inside the bounds", "Move the selected shape"),
		new("Canvas", "Drag a bounds handle", "Stretch the selected shape"),
		new("Canvas", "Drag a point", "Move a point of the selected shape, with the curve handles attached to it"),
		new("Canvas", "Shift while dragging", "Keep the shape or point level or upright, or keep its proportions while stretching"),
		new("Canvas", "Ctrl+wheel", "Zoom in or out"),
		new("Canvas", "Shift+wheel", "Scroll sideways"),
		new("Fields", "Enter", "Apply a number field or the When field (they also apply when focus leaves them)"),
	];

	internal sealed record Shortcut(string Group, string Keys, string Action);
}
