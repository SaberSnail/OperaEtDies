using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Controls.NewGame;

/// <summary>
/// Lets the player edit one of their character's properties when starting a new game.
/// </summary>
public abstract class CharacterPropertyEditorViewModel : ViewModelBase
{
	protected CharacterPropertyEditorViewModel(CharacterRandomizer character, CharacterProperty property)
	{
		Character = character;
		Property = property;
		Trait = character[property.Id];

		// Values can also change from outside the editor, such as when randomizing everything or when a value
		// depends on another property.
		Trait.ValueChanged += (_, _) =>
		{
			m_wasNotified = true;
			RaiseValueChanged();
		};
	}

	public CharacterProperty Property { get; }

	public string Id => Property.Id;

	public string Title => Property.Title;

	/// <summary>
	/// Whether the player has a choice to make. Properties with only one possible value aren't shown.
	/// </summary>
	public abstract bool IsEditable { get; }

	public virtual bool CanRandomize() => true;

	public void Randomize()
	{
		VerifyAccess();
		if (!CanRandomize())
			return;

		ChangeValue(() =>
		{
			Trait.IsLocked = false;
			Trait.Randomize();
		});
	}

	protected CharacterRandomizer Character { get; }

	protected RandomizedValue<object> Trait { get; }

	/// <summary>
	/// Raises change notifications for the properties that show the value.
	/// </summary>
	protected abstract void RaiseValueChanged();

	/// <summary>
	/// Changes the value, and raises change notifications even if it didn't change, since the view may need to
	/// replace text that was edited to something else.
	/// </summary>
	protected void ChangeValue(Action change)
	{
		m_wasNotified = false;
		change();
		if (!m_wasNotified)
			RaiseValueChanged();
	}

	private bool m_wasNotified;
}
