using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Controls.NewGame;

/// <summary>
/// Edits a string property. A blank value is replaced with a random default. A value that isn't one of the
/// defaults is kept when the defaults change or everything is randomized.
/// </summary>
public sealed class StringPropertyEditorViewModel : CharacterPropertyEditorViewModel
{
	public StringPropertyEditorViewModel(CharacterRandomizer character, CharacterProperty property)
		: base(character, property)
	{
	}

	public override bool IsEditable => true;

	public bool HasDefaults => Character.GetDefaultValues(Property) is not null;

	public string Text
	{
		get => VerifyAccess((string) Trait.Value);
		set => ChangeValue(() =>
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				// With no defaults, randomizing leaves the value blank.
				Trait.IsLocked = false;
				Trait.Randomize();
			}
			else
			{
				Trait.IsLocked = !IsDefault(value);
				Trait.Value = value;
			}
		});
	}

	public override bool CanRandomize() => HasDefaults;

	protected override void RaiseValueChanged() => RaisePropertyChanged(nameof(Text));

	private bool IsDefault(string value)
	{
		var trimmed = value.Trim();
		return Character.GetDefaultValues(Property)?.Any(x => x.Value == trimmed) ?? false;
	}
}
