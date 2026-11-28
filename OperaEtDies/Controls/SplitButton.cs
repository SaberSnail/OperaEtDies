using System.Windows;
using System.Windows.Controls.Primitives;

namespace OperaEtDies.Controls;

/// <summary>
/// A button with a reasonable default and other ways of doing it (see <see cref="SplitActionViewModel"/>): the main part
/// does the default, and the chevron opens a themed menu of the others below it (see <see cref="MenuButtonBase"/>). Its
/// template is in <c>Themes/Shared/SplitButtons.xaml</c>, with the main part as <c>PART_Main</c> and the chevron as
/// <c>PART_Chevron</c>.
/// </summary>
[TemplatePart(Name = "PART_Chevron", Type = typeof(ToggleButton))]
public sealed class SplitButton : MenuButtonBase
{
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

	protected override string MenuTargetPartName => "PART_Chevron";

	protected override void OnActionChanged(SplitActionViewModel? action) => SetValue(HasMenuPropertyKey, action?.HasMenu ?? false);
}
