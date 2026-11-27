using System.Windows.Input;

namespace OperaEtDies.Test;

[TestFixture]
public sealed class HotkeysTests
{
	[Test]
	public void KeysMapToWhatTheyDo()
	{
		Assert.That(Hotkeys.Find(Key.C, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Character)));
		Assert.That(Hotkeys.Find(Key.V, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Villagers)));
		Assert.That(Hotkeys.Find(Key.M, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Map)));
		Assert.That(Hotkeys.Find(Key.W, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Wardrobe)));
		Assert.That(Hotkeys.Find(Key.I, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Inventory)));
		Assert.That(Hotkeys.Find(Key.T, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Status)));
		Assert.That(Hotkeys.Find(Key.S, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Start)));
		Assert.That(Hotkeys.Find(Key.J, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Concerns)));
		Assert.That(Hotkeys.Find(Key.Escape, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Escape)));
		Assert.That(Hotkeys.Find(Key.F5, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.QuickSave)));
		Assert.That(Hotkeys.Find(Key.F9, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.QuickLoad)));
		Assert.That(Hotkeys.Find(Key.F11, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.FullScreen)));
		Assert.That(Hotkeys.Find(Key.Return, ModifierKeys.Alt), Is.EqualTo(new Hotkey(HotkeyCommand.FullScreen)));

		// Space and Enter press the button with keyboard focus, and Backspace does nothing, so none of them are hotkeys.
		Assert.That(Hotkeys.Find(Key.Space, ModifierKeys.None), Is.Null);
		Assert.That(Hotkeys.Find(Key.Return, ModifierKeys.None), Is.Null);
		Assert.That(Hotkeys.Find(Key.Back, ModifierKeys.None), Is.Null);

		// Keys with other modifiers are left alone.
		Assert.That(Hotkeys.Find(Key.C, ModifierKeys.Control), Is.Null);
		Assert.That(Hotkeys.Find(Key.C, ModifierKeys.Shift), Is.Null);
		Assert.That(Hotkeys.Find(Key.D1, ModifierKeys.Control), Is.Null);
	}

	[Test]
	public void ShiftWithANumberOpensItsMenu()
	{
		Assert.That(Hotkeys.Find(Key.D1, ModifierKeys.Shift), Is.EqualTo(new Hotkey(HotkeyCommand.ChoiceMenu, 1)));
		Assert.That(Hotkeys.Find(Key.D0, ModifierKeys.Shift), Is.EqualTo(new Hotkey(HotkeyCommand.ChoiceMenu, 10)));
		Assert.That(Hotkeys.IsTypingKey(new Hotkey(HotkeyCommand.ChoiceMenu, 1)), Is.True);
		Assert.That(Hotkeys.FormatShiftChoiceKey("1"), Is.EqualTo("Shift+1"));
	}

	[Test]
	public void NumberKeysAreTheChoicesWithZeroTheTenth()
	{
		Assert.That(Hotkeys.Find(Key.D1, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Choice, 1)));
		Assert.That(Hotkeys.Find(Key.D9, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Choice, 9)));
		Assert.That(Hotkeys.Find(Key.D0, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Choice, 10)));
		Assert.That(Hotkeys.Find(Key.NumPad3, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Choice, 3)));
		Assert.That(Hotkeys.Find(Key.NumPad0, ModifierKeys.None), Is.EqualTo(new Hotkey(HotkeyCommand.Choice, 10)));

		Assert.That(Enumerable.Range(0, 12).Select(Hotkeys.GetChoiceKeyText), Is.EqualTo(new[] { null, "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", null }));
	}

	[Test]
	public void TypingKeysAreOffInTextBoxesButNotEscOrFunctionKeys()
	{
		Assert.That(Hotkeys.IsTypingKey(new Hotkey(HotkeyCommand.Choice, 1)), Is.True);
		Assert.That(Hotkeys.IsTypingKey(new Hotkey(HotkeyCommand.Villagers)), Is.True);
		Assert.That(Hotkeys.IsTypingKey(new Hotkey(HotkeyCommand.Escape)), Is.False);
		Assert.That(Hotkeys.IsTypingKey(new Hotkey(HotkeyCommand.QuickSave)), Is.False);
		Assert.That(Hotkeys.IsTypingKey(new Hotkey(HotkeyCommand.QuickLoad)), Is.False);
		Assert.That(Hotkeys.IsTypingKey(new Hotkey(HotkeyCommand.FullScreen)), Is.False);
	}

	[Test]
	public void ToolTipsShowTheirKey()
	{
		Assert.That(Hotkeys.FormatToolTip("Villagers", HotkeyCommand.Villagers), Is.EqualTo("Villagers (V)"));
		Assert.That(Hotkeys.FormatToolTip("Status", HotkeyCommand.Status), Is.EqualTo("Status (T)"));
		Assert.That(Hotkeys.FormatToolTip("Main menu", HotkeyCommand.Escape), Is.EqualTo("Main menu (Esc)"));
		Assert.That(new HotkeyToolTipExtension(HotkeyCommand.Map) { Text = "Map" }.ProvideValue(null!), Is.EqualTo("Map (M)"));
	}
}
