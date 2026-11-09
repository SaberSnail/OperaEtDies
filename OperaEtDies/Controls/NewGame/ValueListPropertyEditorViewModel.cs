using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Controls.NewGame;

/// <summary>
/// Edits a value list property by choosing one of the values allowed for the player.
/// </summary>
public sealed class ValueListPropertyEditorViewModel : CharacterPropertyEditorViewModel
{
	public ValueListPropertyEditorViewModel(CharacterRandomizer character, CharacterProperty property)
		: base(character, property)
	{
		Options = character.GetOptions(property);
	}

	public IReadOnlyList<CharacterPropertyOption> Options { get; }

	public override bool IsEditable => Options.Count > 1;

	public CharacterPropertyOption SelectedOption
	{
		get => VerifyAccess((CharacterPropertyOption) Trait.Value);
		set
		{
			// The combo box can push null if its items change.
			if (value is not null)
				Trait.Value = value;
		}
	}

	protected override void RaiseValueChanged() => RaisePropertyChanged(nameof(SelectedOption));
}
