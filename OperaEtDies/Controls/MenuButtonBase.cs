using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace OperaEtDies.Controls;

/// <summary>
/// A button that opens a themed menu below it of an action's other ways (see <see cref="SplitActionViewModel"/>), shared
/// by <see cref="SplitButton"/> and <see cref="MenuButton"/>. Alt+Down opens the menu while the button has keyboard focus,
/// and while it's open, the number keys choose from it. The menu opens below the template part named by
/// <see cref="MenuTargetPartName"/>, or the whole button without one.
/// </summary>
public abstract class MenuButtonBase : Control
{
	protected MenuButtonBase()
	{
		m_menu = new ContextMenu { Placement = PlacementMode.Bottom };
		m_menu.SetResourceReference(StyleProperty, "SplitButtonMenuStyle");
		m_menu.Closed += OnMenuClosed;
		m_menu.PreviewKeyDown += OnMenuPreviewKeyDown;
		Unloaded += (_, _) => m_menu.IsOpen = false;
	}

	public static readonly DependencyProperty ActionProperty =
		DependencyProperty.Register(nameof(Action), typeof(SplitActionViewModel), typeof(MenuButtonBase), new PropertyMetadata(null, OnActionChanged));

	/// <summary>
	/// The action: its default, and the other ways listed in the menu.
	/// </summary>
	public SplitActionViewModel? Action
	{
		get => (SplitActionViewModel?) GetValue(ActionProperty);
		set => SetValue(ActionProperty, value);
	}

	/// <summary>
	/// The name of the template part the menu opens below.
	/// </summary>
	protected abstract string MenuTargetPartName { get; }

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		m_menu.PlacementTarget = GetTemplateChild(MenuTargetPartName) as UIElement ?? this;
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.System && e.SystemKey == Key.Down && Action is { } action)
		{
			action.OpenMenu();
			e.Handled = true;
		}
	}

	/// <summary>
	/// Called when the action changes, after the menu is set up for it.
	/// </summary>
	protected virtual void OnActionChanged(SplitActionViewModel? action)
	{
	}

	private static void OnActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var button = (MenuButtonBase) d;
		if (e.OldValue is SplitActionViewModel oldAction)
		{
			PropertyChangedEventManager.RemoveHandler(oldAction, button.OnActionPropertyChanged, nameof(SplitActionViewModel.IsMenuOpen));
			oldAction.IsMenuOpen = false;
		}

		var action = e.NewValue as SplitActionViewModel;
		button.m_menu.IsOpen = false;
		button.m_menu.DataContext = action;
		button.m_menu.ItemsSource = action?.Alternatives;
		button.OnActionChanged(action);
		if (action is not null)
		{
			PropertyChangedEventManager.AddHandler(action, button.OnActionPropertyChanged, nameof(SplitActionViewModel.IsMenuOpen));
			button.UpdateMenu();
		}
	}

	private void OnActionPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateMenu();

	/// <summary>
	/// Opens or closes the menu with the action. Opened from the keyboard, its first item has keyboard focus.
	/// </summary>
	private void UpdateMenu()
	{
		var isOpen = Action?.IsMenuOpen ?? false;
		if (m_menu.IsOpen != isOpen)
		{
			m_menu.IsOpen = isOpen;
			if (isOpen && InputManager.Current.MostRecentInputDevice is KeyboardDevice && m_menu.ItemContainerGenerator.ContainerFromIndex(0) is MenuItem first)
				first.Focus();
		}
	}

	private void OnMenuClosed(object sender, RoutedEventArgs e)
	{
		if (Action is { } action)
			action.IsMenuOpen = false;
	}

	/// <summary>
	/// Chooses from the open menu by number.
	/// </summary>
	private void OnMenuPreviewKeyDown(object sender, KeyEventArgs e)
	{
		var number = e.Key switch
		{
			>= Key.D1 and <= Key.D9 => e.Key - Key.D0,
			>= Key.NumPad1 and <= Key.NumPad9 => e.Key - Key.NumPad0,
			_ => 0,
		};
		if (number != 0 && Keyboard.Modifiers == ModifierKeys.None && Action is { } action)
		{
			action.ChooseAlternative(number);
			e.Handled = true;
		}
	}

	private readonly ContextMenu m_menu;
}
