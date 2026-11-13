using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PortraitEditor.Controls;
using PortraitEditor.ViewModels;

namespace PortraitEditor.Views;

public partial class MainWindowView : Window
{
	public MainWindowView(MainWindowViewModel viewModel)
	{
		InitializeComponent();
		DataContext = viewModel;
		m_viewModel = viewModel;
		RestoreLayout();
		ContentRendered += (_, _) => ZoomToFit();
		Closing += (_, _) => SaveLayout();
	}

	private void RestoreLayout()
	{
		if (WindowLayout.Load() is { } layout)
		{
			if (layout.IsOnScreen())
			{
				WindowStartupLocation = WindowStartupLocation.Manual;
				Left = layout.Bounds.Left;
				Top = layout.Bounds.Top;
				Width = Math.Max(layout.Bounds.Width, MinWidth);
				Height = Math.Max(layout.Bounds.Height, MinHeight);
			}

			if (layout.IsMaximized)
				WindowState = WindowState.Maximized;
			RestoreWidth(TreeColumn, layout.TreeWidth);
			RestoreWidth(EditorColumn, layout.EditorWidth);
			RestoreWidth(PreviewsColumn, layout.PreviewsWidth);
		}
	}

	private static void RestoreWidth(ColumnDefinition column, double width)
	{
		if (width > 0)
			column.Width = new GridLength(Math.Max(width, column.MinWidth));
	}

	/// <summary>
	/// Saves the layout. A maximized window keeps the place it goes back to when it's no longer maximized.
	/// </summary>
	private void SaveLayout()
	{
		var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
		new WindowLayout(bounds, WindowState == WindowState.Maximized, TreeColumn.ActualWidth, EditorColumn.ActualWidth, PreviewsColumn.ActualWidth).Save();
	}

	/// <summary>
	/// Scrolls a selected item's header into view, including when it's selected by clicking a shape in the portrait.
	/// Only the header is scrolled to, so the tree doesn't jump to show the item's children.
	/// </summary>
	private void OnTreeViewItemSelected(object sender, RoutedEventArgs e)
	{
		if (e.OriginalSource is TreeViewItem item)
		{
			var header = item.Template?.FindName("PART_Header", item) as FrameworkElement;
			m_isBringingIntoView = true;
			try
			{
				if (header is not null)
					header.BringIntoView();
				else
					item.BringIntoView();
			}
			finally
			{
				m_isBringingIntoView = false;
			}
		}
	}

	/// <summary>
	/// Right-clicking a layer, shape, or other item selects it, so its menu acts on the item the editor shows.
	/// </summary>
	private void OnTreePreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
	{
		var element = e.OriginalSource as DependencyObject;
		while (element is not null and not TreeViewItem)
			element = element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
		if (element is TreeViewItem { DataContext: DataItemViewModel item })
			item.IsSelected = true;
	}

	/// <summary>
	/// Keeps the tree from scrolling when items ask to be shown on their own, such as when they're expanded or clicked,
	/// so the item at the top stays at the top.
	/// </summary>
	private void OnTreeViewItemRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
	{
		if (!m_isBringingIntoView)
			e.Handled = true;
	}

	/// <summary>
	/// Clicking a layer's or shape's canvas state button goes to the next state, and Alt+click shows only that item.
	/// </summary>
	private void OnCanvasStateButtonClick(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is DataItemViewModel item)
		{
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
				item.ShowOnly();
			else
				item.CycleCanvasState();
		}
	}

	private void OnShortcutsClick(object sender, RoutedEventArgs e) => ShowShortcuts();

	private void OnHelpExecuted(object sender, ExecutedRoutedEventArgs e) => ShowShortcuts();

	private void ShowShortcuts() => new ShortcutsWindowView { Owner = this }.ShowDialog();

	private void OnZoomToFitClick(object sender, RoutedEventArgs e) => ZoomToFit();

	private void ZoomToFit() => m_viewModel.Canvas.ZoomToFit(new Size(CanvasScrollViewer.ActualWidth, CanvasScrollViewer.ActualHeight), ShapeCanvas.CanvasMargin);

	/// <summary>
	/// Shift+wheel scrolls the canvas sideways. Ctrl+wheel is left to the canvas, which zooms.
	/// </summary>
	private void OnCanvasPreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (Keyboard.Modifiers == ModifierKeys.Shift)
		{
			CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset - e.Delta);
			e.Handled = true;
		}
	}

	private readonly MainWindowViewModel m_viewModel;
	private bool m_isBringingIntoView;
}
