using System.Globalization;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Controls.NewGame;

/// <summary>
/// Lets the player edit one of their character's properties when starting a new game. A property that has no value
/// (because the values of the properties it depends on rule it out) is unavailable, and a property with only one
/// possible value is fixed.
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
			NotifyValueChanged();
		};

		// The randomizer updates this value when the properties its conditions use change, before this runs.
		foreach (var conditionId in property.GetConditionPropertyIds())
			character[conditionId].ValueChanged += (_, _) => OnConditionsChanged();
	}

	public CharacterProperty Property { get; }

	public string Id => Property.Id;

	public string Title => Property.Title;

	/// <summary>
	/// Whether the property currently has a value. An unavailable property is shown disabled and empty.
	/// </summary>
	public bool IsAvailable => VerifyAccess(Character.HasValue(Property));

	/// <summary>
	/// Whether the property has a value but no choice to make, so it's shown as a label instead of an editor.
	/// </summary>
	public bool IsFixed => VerifyAccess(Character.HasValue(Property) && !HasChoice);

	/// <summary>
	/// The current value as text, for showing a fixed value. Empty if the property has no value.
	/// </summary>
	public string ValueText => VerifyAccess(Trait.Value switch
	{
		CharacterPropertyOption option => option.Title,
		int number => number.ToString(CultureInfo.CurrentCulture),
		string text => text,
		_ => "",
	});

	public virtual bool CanRandomize() => IsAvailable && HasChoice;

	public void Randomize()
	{
		VerifyAccess();
		if (!CanRandomize())
			return;

		ChangeValue(() => Character.RandomizeAlone(Property));
	}

	protected CharacterRandomizer Character { get; }

	protected RandomizedValue<object?> Trait { get; }

	/// <summary>
	/// Whether there's a choice to make when the property has a value.
	/// </summary>
	protected abstract bool HasChoice { get; }

	/// <summary>
	/// Raises change notifications for the properties that show the value.
	/// </summary>
	protected abstract void RaiseValueChanged();

	/// <summary>
	/// Called when a property used by the conditions changes, which can change whether the property is available
	/// and whether it has a choice.
	/// </summary>
	protected virtual void OnConditionsChanged() => RaisePropertyChanged(nameof(IsAvailable), nameof(IsFixed));

	/// <summary>
	/// Changes the value, and raises change notifications even if it didn't change, since the view may need to
	/// replace text that was edited to something else.
	/// </summary>
	protected void ChangeValue(Action change)
	{
		m_wasNotified = false;
		change();
		if (!m_wasNotified)
			NotifyValueChanged();
	}

	private void NotifyValueChanged()
	{
		RaiseValueChanged();
		RaisePropertyChanged(nameof(ValueText));
	}

	private bool m_wasNotified;
}
