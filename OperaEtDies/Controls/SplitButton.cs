using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace OperaEtDies.Controls;

/// <summary>
/// A button with a reasonable default and other ways of doing it (see <see cref="SplitActionViewModel"/>): the main part
/// does the default, and the chevron opens a themed menu of the others below it. Alt+Down opens the menu while the button
/// has keyboard focus, and while it's open, the number keys choose from it. Its template is in
/// <c>Themes/Shared/SplitButtons.xaml</c>, with the main part as <c>PART_Main</c> and the chevron as <c>PART_Chevron</c>.
/// </summary>
[TemplatePart(Name = "PART_Chevron", Type = typeof(ToggleButton))]
public sealed class SplitButton : Control
{
	public SplitButton()
	{
		m_menu = new ContextMenu { Placement = PlacementMode.Bottom };
		m_menu.SetResourceReference(StyleProperty, "SplitButtonMenuStyle");
		m_menu.Closed += OnMenuClosed;
		m_menu.PreviewKeyDown += OnMenuPreviewKeyDown;
		Unloaded += (_, _) => m_menu.IsOpen = false;
	}

	public static readonly DependencyProperty ActionProperty =
		DependencyProperty.Register(nameof(Action), typeof(SplitActionViewModel), typeof(SplitButton), new PropertyMetadata(null, OnActionChanged));

	/// <summary>
	/// The action: its default, and the other ways listed in the menu.
	/// </summary>
	public SplitActionViewModel? Action
	{
		get => (SplitActionViewModel?) GetValue(ActionProperty);
		set => SetValue(ActionProperty, value);
	}

	public static readonly DependencyProperty LeadingProperty =
		DependencyProperty.Register(nameof(Leading), typeof(object), typeof(SplitButton), new PropertyMetadata(null));

	/// <summary>
	/// Shown at the left of the main part, before its text, such as icons marking an exit, or null.
	/// </summary>
	public object? Leading
	{
		get => GetValue(LeadingProperty);
		set => SetValue(LeadingProperty, value);
	}

	private static readonly DependencyPropertyKey HasMenuPropertyKey =
		DependencyProperty.RegisterReadOnly(nameof(HasMenu), typeof(bool), typeof(SplitButton), new PropertyMetadata(false));

	public static readonly DependencyProperty HasMenuProperty = HasMenuPropertyKey.DependencyProperty;

	/// <summary>
	/// Whether the chevron is shown (see <see cref="SplitActionViewModel.HasMenu"/>).
	/// </summary>
	public bool HasMenu => (bool) GetValue(HasMenuProperty);

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		m_chevron = GetTemplateChild("PART_Chevron") as ToggleButton;
		m_menu.PlacementTarget = m_chevron ?? (UIElement) this;
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

	private static void OnActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var button = (SplitButton) d;
		if (e.OldValue is SplitActionViewModel oldAction)
		{
			PropertyChangedEventManager.RemoveHandler(oldAction, button.OnActionPropertyChanged, nameof(SplitActionViewModel.IsMenuOpen));
			oldAction.IsMenuOpen = false;
		}

		var action = e.NewValue as SplitActionViewModel;
		button.m_menu.IsOpen = false;
		button.m_menu.DataContext = action;
		button.m_menu.ItemsSource = action?.Alternatives;
		button.SetValue(HasMenuPropertyKey, action?.HasMenu ?? false);
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
	private ToggleButton? m_chevron;
}
