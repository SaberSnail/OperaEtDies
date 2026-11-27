using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using GoldenAnvil.Utility;

namespace OperaEtDies;

/// <summary>
/// What a hotkey does. The window decides what that means where the player is (see
/// <see cref="Windows.Main.MainWindowViewModel.HandleHotkey"/>).
/// </summary>
public enum HotkeyCommand
{
	/// <summary>
	/// One of the numbered choices in the center.
	/// </summary>
	Choice,

	/// <summary>
	/// Opens the menu of other ways of one of the numbered choices in the center, when it's a split button.
	/// </summary>
	ChoiceMenu,
	Character,
	Villagers,
	Map,
	Wardrobe,
	Inventory,
	Status,
	Start,
	Concerns,
	Escape,
	QuickSave,
	QuickLoad,
	FullScreen,
}

/// <summary>
/// A hotkey pressed: what it does, and for a choice, its number, from 1 to <see cref="Hotkeys.ChoiceCount"/>.
/// </summary>
public readonly record struct Hotkey(HotkeyCommand Command, int Number = 0);

/// <summary>
/// The game's one map of keys to what they do, and the text that shows them, so tooltips and badges always match the
/// keys. Space and Enter aren't hotkeys: they press the button with keyboard focus.
/// </summary>
public static class Hotkeys
{
	/// <summary>
	/// How many choices in the center can have a number: 1 to 9, and 0 for the tenth.
	/// </summary>
	public const int ChoiceCount = 10;

	/// <summary>
	/// Finds the hotkey for a key pressed with the given modifiers, or null if it isn't one.
	/// </summary>
	public static Hotkey? Find(Key key, ModifierKeys modifiers)
	{
		if (modifiers == ModifierKeys.Alt)
			return key == Key.Return ? new Hotkey(HotkeyCommand.FullScreen) : null;
		if (modifiers == ModifierKeys.Shift)
		{
			return key switch
			{
				>= Key.D1 and <= Key.D9 => new Hotkey(HotkeyCommand.ChoiceMenu, key - Key.D0),
				Key.D0 => new Hotkey(HotkeyCommand.ChoiceMenu, ChoiceCount),
				_ => null,
			};
		}
		if (modifiers != ModifierKeys.None)
			return null;

		return key switch
		{
			>= Key.D1 and <= Key.D9 => new Hotkey(HotkeyCommand.Choice, key - Key.D0),
			>= Key.NumPad1 and <= Key.NumPad9 => new Hotkey(HotkeyCommand.Choice, key - Key.NumPad0),
			Key.D0 or Key.NumPad0 => new Hotkey(HotkeyCommand.Choice, ChoiceCount),
			Key.C => new Hotkey(HotkeyCommand.Character),
			Key.V => new Hotkey(HotkeyCommand.Villagers),
			Key.M => new Hotkey(HotkeyCommand.Map),
			Key.W => new Hotkey(HotkeyCommand.Wardrobe),
			Key.I => new Hotkey(HotkeyCommand.Inventory),
			Key.T => new Hotkey(HotkeyCommand.Status),
			Key.S => new Hotkey(HotkeyCommand.Start),
			Key.J => new Hotkey(HotkeyCommand.Concerns),
			Key.Escape => new Hotkey(HotkeyCommand.Escape),
			Key.F5 => new Hotkey(HotkeyCommand.QuickSave),
			Key.F9 => new Hotkey(HotkeyCommand.QuickLoad),
			Key.F11 => new Hotkey(HotkeyCommand.FullScreen),
			_ => null,
		};
	}

	/// <summary>
	/// Whether a hotkey is a key that types something, so it's off while a text box has focus. Esc and the function keys
	/// still work there.
	/// </summary>
	public static bool IsTypingKey(Hotkey hotkey) => hotkey.Command is not (HotkeyCommand.Escape or HotkeyCommand.QuickSave or HotkeyCommand.QuickLoad or HotkeyCommand.FullScreen);

	/// <summary>
	/// Gets the key for a command as it's shown, such as "V" or "F5". A choice's key comes from <see cref="GetChoiceKeyText"/>.
	/// </summary>
	public static string GetKeyText(HotkeyCommand command) => command switch
	{
		HotkeyCommand.Character => "C",
		HotkeyCommand.Villagers => "V",
		HotkeyCommand.Map => "M",
		HotkeyCommand.Wardrobe => "W",
		HotkeyCommand.Inventory => "I",
		HotkeyCommand.Status => "T",
		HotkeyCommand.Start => "S",
		HotkeyCommand.Concerns => "J",
		HotkeyCommand.Escape => OurResources.HotkeyEscape,
		HotkeyCommand.QuickSave => "F5",
		HotkeyCommand.QuickLoad => "F9",
		HotkeyCommand.FullScreen => "F11",
		_ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
	};

	/// <summary>
	/// Gets the key of a numbered choice as it's shown on its badge ("1" to "9", and "0" for the tenth), or null if the
	/// number has no key.
	/// </summary>
	public static string? GetChoiceKeyText(int number) =>
		number is >= 1 and < ChoiceCount ? number.ToString(CultureInfo.InvariantCulture) :
		number == ChoiceCount ? "0" :
		null;

	/// <summary>
	/// Gets the keys that open the menu of a numbered choice that's a split button, as they're shown, such as "Shift+1",
	/// from the key shown on its badge.
	/// </summary>
	public static string FormatShiftChoiceKey(string badge) => OurResources.HotkeyShiftChoice.FormatCurrentCulture(badge);

	/// <summary>
	/// Adds a command's key to a tooltip, such as "Villagers (V)".
	/// </summary>
	public static string FormatToolTip(string text, HotkeyCommand command) => OurResources.ToolTipWithHotkey.FormatCurrentCulture(text, GetKeyText(command));

	/// <summary>
	/// The number badge drawn on a button for the key that presses it, such as "1", or null for none. The button styles
	/// draw it at the right end of the button.
	/// </summary>
	public static readonly DependencyProperty BadgeProperty =
		DependencyProperty.RegisterAttached("Badge", typeof(string), typeof(Hotkeys));

	public static string? GetBadge(DependencyObject d) => (string?) d.GetValue(BadgeProperty);

	public static void SetBadge(DependencyObject d, string? value) => d.SetValue(BadgeProperty, value);
}

/// <summary>
/// A tooltip with its hotkey, such as "Villagers (V)":
/// <c>ToolTip="{operaEtDies:HotkeyToolTip Villagers, Text={x:Static operaEtDies:OurResources.ViewVillagers}}"</c>.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class HotkeyToolTipExtension : MarkupExtension
{
	public HotkeyToolTipExtension()
	{
	}

	public HotkeyToolTipExtension(HotkeyCommand command)
	{
		Command = command;
	}

	[ConstructorArgument("command")]
	public HotkeyCommand Command { get; set; }

	public string Text { get; set; } = "";

	public override object ProvideValue(IServiceProvider serviceProvider) => Hotkeys.FormatToolTip(Text, Command);
}
