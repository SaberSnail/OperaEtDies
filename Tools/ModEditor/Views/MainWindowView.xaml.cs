using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ModEditor.Model;
using ModEditor.ViewModels;
using ModEditor.ViewModels.Form;
using ModEditor.Views.Help;

namespace ModEditor.Views;

public partial class MainWindowView : Window
{
	public MainWindowView()
	{
		InitializeComponent();
	}

	public void SetViewModel(MainWindowViewModel viewModel)
	{
		m_viewModel = viewModel;
		DataContext = viewModel;
		RestoreLayout(viewModel.Settings);
		viewModel.HelpRequested += (_, e) => ShowHelp(e.Value);
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		base.OnClosing(e);
		if (m_viewModel is null)
			return;
		if (!m_viewModel.CanClose())
		{
			e.Cancel = true;
			return;
		}

		SaveLayout();
		m_helpWindow?.CloseForGood();
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None && m_viewModel is not null)
		{
			// F1 shows the help about the form field that has the focus, or else about the selected item.
			if (FindFocusedField() is { Help: not null } field)
				field.ShowHelp();
			else
				m_viewModel.ShowSelectionHelp();
			e.Handled = true;
			return;
		}

		if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
		{
			FilterBox.Focus();
			FilterBox.SelectAll();
			e.Handled = true;
		}
	}

	private void RestoreLayout(EditorSettings settings)
	{
		if (settings.IsWindowOnScreen() && settings.WindowBounds is { } bounds)
		{
			WindowStartupLocation = WindowStartupLocation.Manual;
			Left = bounds.Left;
			Top = bounds.Top;
			Width = Math.Max(bounds.Width, MinWidth);
			Height = Math.Max(bounds.Height, MinHeight);
		}

		if (settings.IsMaximized)
			WindowState = WindowState.Maximized;
		if (settings.TreeWidth > 0)
			TreeColumn.Width = new GridLength(Math.Max(settings.TreeWidth, TreeColumn.MinWidth));
	}

	/// <summary>
	/// Saves the layout. A maximized window keeps the place it goes back to when it's no longer maximized.
	/// </summary>
	private void SaveLayout()
	{
		var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
		var treeWidth = TreeColumn.ActualWidth > 0 ? TreeColumn.ActualWidth : m_viewModel!.Settings.TreeWidth;
		m_viewModel!.UpdateSettings(m_viewModel.Settings with { WindowBounds = bounds, IsMaximized = WindowState == WindowState.Maximized, TreeWidth = treeWidth });
	}

	private void OnProblemDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (ProblemList.SelectedItem is ModProblem problem)
		{
			m_viewModel?.ShowProblem(problem);
			ModTree.Focus();
		}
	}

	/// <summary>
	/// Scrolls an item selected in the mod's tree into view, including one selected by the editor (such as a new
	/// operation), once its parents have been expanded and laid out. Only its own line is scrolled to, not its children.
	/// </summary>
	private void OnModTreeItemSelected(object sender, RoutedEventArgs e)
	{
		if (e.OriginalSource is TreeViewItem item)
		{
			void OnLayoutUpdated(object? sender, EventArgs e)
			{
				item.LayoutUpdated -= OnLayoutUpdated;
				if (item.IsSelected && item.Template?.FindName("PART_Header", item) is FrameworkElement header)
					header.BringIntoView();
				else if (item.IsSelected)
					item.BringIntoView();
			}

			item.LayoutUpdated += OnLayoutUpdated;
			item.InvalidateMeasure();
		}
	}

	private void OnCopyProblemsExecuted(object sender, ExecutedRoutedEventArgs e) => CopyProblems(ProblemList.SelectedItems.Cast<ModProblem>());

	private void OnCopyProblemsClick(object sender, RoutedEventArgs e) => CopyProblems(ProblemList.SelectedItems.Cast<ModProblem>());

	private void OnCopyAllProblemsClick(object sender, RoutedEventArgs e) => CopyProblems(ProblemList.Items.Cast<ModProblem>());

	/// <summary>
	/// Copies problems as text, one per line, in the order they're listed.
	/// </summary>
	private void CopyProblems(IEnumerable<ModProblem> problems)
	{
		var selected = problems.ToHashSet();
		var lines = ProblemList.Items.Cast<ModProblem>().Where(selected.Contains).Select(x => x.Location.Length == 0 ? x.Message : $"{x.Location}: {x.Message}").ToList();
		if (lines.Count == 0)
			return;
		try
		{
			Clipboard.SetText(string.Join(Environment.NewLine, lines));
		}
		catch (System.Runtime.InteropServices.COMException)
		{
			// The clipboard can be held by another program for a moment.
		}
	}

	/// <summary>
	/// Right-clicking a problem selects it, unless it's already one of those selected, so its menu copies what was
	/// clicked.
	/// </summary>
	private void OnProblemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
	{
		var element = e.OriginalSource as DependencyObject;
		while (element is not null and not ListBoxItem)
			element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
		if (element is ListBoxItem { IsSelected: false } item)
		{
			ProblemList.SelectedItems.Clear();
			item.IsSelected = true;
		}
	}

	/// <summary>
	/// Right-clicking an item selects it, so its menu acts on the item the editor shows.
	/// </summary>
	private void OnTreePreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (FindTreeViewItem(e.OriginalSource) is { DataContext: TreeNodeViewModel node })
			node.IsSelected = true;
	}

	private void OnTreePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		var item = FindTreeViewItem(e.OriginalSource);
		m_dragSource = item?.DataContext as TreeNodeViewModel;
		m_dragStart = e.GetPosition(this);
	}

	/// <summary>
	/// Dragging an item far enough starts moving it (or copying it, with Ctrl or from the game data).
	/// </summary>
	private void OnTreePreviewMouseMove(object sender, MouseEventArgs e)
	{
		if (e.LeftButton != MouseButtonState.Pressed || m_dragSource is not { } source)
			return;
		var offset = e.GetPosition(this) - m_dragStart;
		if (Math.Abs(offset.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(offset.Y) < SystemParameters.MinimumVerticalDragDistance)
			return;
		if (source is ModNodeViewModel { Location: FolderLocation { Path.Length: 0 } } || (source is GameDataNodeViewModel && source.GetPiece() is null))
			return;

		m_dragSource = null;
		var data = new DataObject(DragFormat, source);
		DragDrop.DoDragDrop((DependencyObject) sender, data, DragDropEffects.Move | DragDropEffects.Copy);
		ClearDropAdorner();
	}

	private void OnTreeDragOver(object sender, DragEventArgs e)
	{
		e.Effects = DragDropEffects.None;
		if (GetDrop(e) is { } drop)
		{
			e.Effects = drop.IsCopy ? DragDropEffects.Copy : DragDropEffects.Move;
			ShowDropAdorner(drop.Header, drop.Placement);
		}
		else
		{
			ClearDropAdorner();
		}

		e.Handled = true;
	}

	private void OnTreeDragLeave(object sender, DragEventArgs e) => ClearDropAdorner();

	private void OnTreeDrop(object sender, DragEventArgs e)
	{
		ClearDropAdorner();
		if (GetDrop(e) is { } drop && m_viewModel is not null)
		{
			m_viewModel.Drop(drop.Source, drop.Target, drop.Placement == DropPlacement.After, drop.IsCopy);
			ModTree.Focus();
		}

		e.Handled = true;
	}

	/// <summary>
	/// Works out what a drag would do where the mouse is: the item it's over, and whether the dragged item would go
	/// into it (a folder, file, or section) or before or after it (by which half of it the mouse is over).
	/// </summary>
	private static DropInfo? GetDrop(DragEventArgs e)
	{
		if (e.Data.GetData(DragFormat) is not TreeNodeViewModel source || FindTreeViewItem(e.OriginalSource) is not { DataContext: TreeNodeViewModel target } item)
			return null;
		if (!MainWindowViewModel.CanDrop(source, target))
			return null;

		var header = item.Template?.FindName("PART_Header", item) as FrameworkElement ?? item;
		var isInto = target is ModNodeViewModel { Location: FolderLocation or FileLocation or SectionLocation };
		var placement = isInto ? DropPlacement.Into : e.GetPosition(header).Y > header.ActualHeight / 2 ? DropPlacement.After : DropPlacement.Before;
		var isCopy = source is GameDataNodeViewModel || e.KeyStates.HasFlag(DragDropKeyStates.ControlKey);
		return new DropInfo(source, target, header, placement, isCopy);
	}

	private void ShowDropAdorner(FrameworkElement header, DropPlacement placement)
	{
		if (m_dropAdorner is { } existing && existing.AdornedElement == header && existing.Placement == placement)
			return;
		ClearDropAdorner();
		if (AdornerLayer.GetAdornerLayer(header) is { } layer)
		{
			m_dropAdorner = new DropAdorner(header, placement);
			layer.Add(m_dropAdorner);
		}
	}

	private void ClearDropAdorner()
	{
		if (m_dropAdorner is { } adorner)
		{
			AdornerLayer.GetAdornerLayer(adorner.AdornedElement)?.Remove(adorner);
			m_dropAdorner = null;
		}
	}

	private static TreeViewItem? FindTreeViewItem(object source)
	{
		var element = source as DependencyObject;
		while (element is not null and not TreeViewItem)
			element = element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
		return element as TreeViewItem;
	}

	private const string DragFormat = "OperaEtDies.ModEditor.Node";

	private enum DropPlacement
	{
		Into,
		Before,
		After,
	}

	private sealed record DropInfo(TreeNodeViewModel Source, TreeNodeViewModel Target, FrameworkElement Header, DropPlacement Placement, bool IsCopy);

	/// <summary>
	/// Shows where a dragged item would go: a box around the item it would go into, or a line where it would go
	/// between items.
	/// </summary>
	private sealed class DropAdorner(UIElement adornedElement, DropPlacement placement) : Adorner(adornedElement)
	{
		public DropPlacement Placement { get; } = placement;

		protected override void OnRender(DrawingContext drawingContext)
		{
			var size = AdornedElement.RenderSize;
			var pen = new Pen(s_brush, 2);
			switch (Placement)
			{
			case DropPlacement.Into:
				drawingContext.DrawRectangle(null, pen, new Rect(new Point(0, 0), size));
				break;
			case DropPlacement.Before:
				drawingContext.DrawLine(pen, new Point(0, 0), new Point(size.Width, 0));
				break;
			case DropPlacement.After:
				drawingContext.DrawLine(pen, new Point(0, size.Height), new Point(size.Width, size.Height));
				break;
			}
		}

		private static readonly Brush s_brush = CreateBrush();

		private static SolidColorBrush CreateBrush()
		{
			var brush = new SolidColorBrush(Color.FromRgb(0x30, 0x70, 0xD0));
			brush.Freeze();
			return brush;
		}
	}

	/// <summary>
	/// Shows the help window (made the first time it's needed) at a place in the help.
	/// </summary>
	private void ShowHelp(HelpTarget target)
	{
		m_helpWindow ??= new HelpWindow { Owner = this };
		m_helpWindow.Navigate(target);
	}

	/// <summary>
	/// Finds the form field the focused control is part of, or null if the focus isn't in the form.
	/// </summary>
	private static FieldViewModel? FindFocusedField()
	{
		for (var element = Keyboard.FocusedElement as DependencyObject; element is not null; element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
		{
			switch ((element as FrameworkElement)?.DataContext)
			{
			case FieldViewModel field:
				return field;
			case MapEntryViewModel entry:
				return entry.Field;
			case MainWindowViewModel:
				return null;
			}
		}

		return null;
	}

	private MainWindowViewModel? m_viewModel;
	private HelpWindow? m_helpWindow;
	private TreeNodeViewModel? m_dragSource;
	private Point m_dragStart;
	private DropAdorner? m_dropAdorner;
}
