using System.Globalization;
using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Controls.NewGame;

/// <summary>
/// Edits an integer property, keeping it within the property's range.
/// </summary>
public sealed class IntegerPropertyEditorViewModel : CharacterPropertyEditorViewModel
{
	public IntegerPropertyEditorViewModel(CharacterRandomizer character, CharacterProperty property)
		: base(character, property)
	{
		var range = character.GetRange(property);
		Minimum = range.Minimum;
		Maximum = range.Maximum;
	}

	public int Minimum { get; }

	public int Maximum { get; }

	protected override bool HasChoice => Minimum < Maximum;

	public int Value
	{
		get => VerifyAccess(Trait.Value as int? ?? Minimum);
		set => ChangeValue(() => Trait.Value = Math.Clamp(value, Minimum, Maximum));
	}

	/// <summary>
	/// The value as text, for editing, or empty if the property has no value. Blank text is replaced with a random value, and text that isn't a number is undone.
	/// </summary>
	public string Text
	{
		get => VerifyAccess(Trait.Value is int number ? number.ToString(CultureInfo.CurrentCulture) : "");
		set => ChangeValue(() =>
		{
			if (string.IsNullOrWhiteSpace(value))
				Trait.Randomize();
			else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var number))
				Trait.Value = Math.Clamp(number, Minimum, Maximum);
		});
	}

	protected override void RaiseValueChanged() => RaisePropertyChanged(nameof(Value), nameof(Text));
}
