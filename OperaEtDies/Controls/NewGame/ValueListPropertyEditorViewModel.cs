using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Controls.NewGame;

/// <summary>
/// Edits a value list property by choosing one of the values allowed for the player. The values to choose from can
/// change when the properties their conditions use change.
/// </summary>
public sealed class ValueListPropertyEditorViewModel : CharacterPropertyEditorViewModel
{
	public ValueListPropertyEditorViewModel(CharacterRandomizer character, CharacterProperty property)
		: base(character, property)
	{
		m_options = character.GetOptions(property);
	}

	/// <summary>
	/// The values that can currently be chosen.
	/// </summary>
	public IReadOnlyList<CharacterPropertyOption> Options => VerifyAccess(m_options);

	/// <summary>
	/// The chosen value, or null if the property currently has no value.
	/// </summary>
	public CharacterPropertyOption? SelectedOption
	{
		get => VerifyAccess(Trait.Value as CharacterPropertyOption);
		set
		{
			// The combo box can push null, or a value that's no longer allowed, when its items change.
			if (value is not null && m_options.Contains(value))
				Trait.Value = value;
		}
	}

	protected override bool HasChoice => m_options.Count > 1;

	protected override void RaiseValueChanged() => RaisePropertyChanged(nameof(SelectedOption));

	protected override void OnConditionsChanged()
	{
		var options = Character.GetOptions(Property);
		if (!options.SequenceEqual(m_options))
		{
			m_options = options;
			RaisePropertyChanged(nameof(Options));
		}

		RaisePropertyChanged(nameof(SelectedOption));
		base.OnConditionsChanged();
	}

	private IReadOnlyList<CharacterPropertyOption> m_options;
}
