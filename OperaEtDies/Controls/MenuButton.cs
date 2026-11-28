using System.Windows;
using System.Windows.Controls.Primitives;

namespace OperaEtDies.Controls;

/// <summary>
/// A button whose only job is to open a themed menu of choices below it (see <see cref="MenuButtonBase"/>), such as
/// "Go to", with a chevron after its text. Its action is made with <see cref="SplitActionViewModel.CreateMenu"/>. Its
/// template is in <c>Themes/Shared/SplitButtons.xaml</c>, with the button as <c>PART_Toggle</c>.
/// </summary>
[TemplatePart(Name = "PART_Toggle", Type = typeof(ToggleButton))]
public sealed class MenuButton : MenuButtonBase
{
	protected override string MenuTargetPartName => "PART_Toggle";
}
