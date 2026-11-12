using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Controls.NewGame;

/// <summary>
/// Edits a value list property by choosing one of the values allowed for the player. The values to choose from can
/// change when the properties their conditions use change. Developers can also be allowed to choose values the role
/// doesn't allow, which randomizing then leaves alone.
/// </summary>
public sealed class ValueListPropertyEditorViewModel : CharacterPropertyEditorViewModel
{
	public ValueListPropertyEditorViewModel(CharacterRandomizer character, CharacterProperty property, bool hasDeveloperOptions = false)
		: base(character, property)
	{
		HasDeveloperOptions = hasDeveloperOptions;
		m_options = Sort(character.GetOptions(property));
		m_developerOptions = hasDeveloperOptions ? Sort(character.GetAllOptions(property)) : [];
	}

	/// <summary>
	/// The values that can currently be chosen, in alphabetical order if the property asks for it.
	/// </summary>
	public IReadOnlyList<CharacterPropertyOption> Options => VerifyAccess(m_options);

	/// <summary>
	/// Whether developers can choose from <see cref="DeveloperOptions"/>.
	/// </summary>
	public bool HasDeveloperOptions { get; }

	/// <summary>
	/// For developers, every value whose conditions are met, including the ones the role doesn't allow. Empty unless
	/// <see cref="HasDeveloperOptions"/>.
	/// </summary>
	public IReadOnlyList<CharacterPropertyOption> DeveloperOptions => VerifyAccess(m_developerOptions);

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

	/// <summary>
	/// The chosen value, for the developer's choice of any value. Choosing one locks it, so that randomizing doesn't
	/// replace it with a value the role allows.
	/// </summary>
	public CharacterPropertyOption? DeveloperSelectedOption
	{
		get => VerifyAccess(Trait.Value as CharacterPropertyOption);
		set
		{
			if (value is not null && m_developerOptions.Contains(value) && value != Trait.Value)
				ChangeValue(() => Trait.Lock(value));
		}
	}

	protected override bool HasChoice => m_options.Count > 1;

	protected override void RaiseValueChanged() => RaisePropertyChanged(nameof(SelectedOption), nameof(DeveloperSelectedOption));

	protected override void OnConditionsChanged()
	{
		var options = Sort(Character.GetOptions(Property));
		if (!options.SequenceEqual(m_options))
		{
			m_options = options;
			RaisePropertyChanged(nameof(Options));
		}

		if (HasDeveloperOptions)
		{
			var developerOptions = Sort(Character.GetAllOptions(Property));
			if (!developerOptions.SequenceEqual(m_developerOptions))
			{
				m_developerOptions = developerOptions;
				RaisePropertyChanged(nameof(DeveloperOptions));
			}
		}

		RaisePropertyChanged(nameof(SelectedOption), nameof(DeveloperSelectedOption));
		base.OnConditionsChanged();
	}

	/// <summary>
	/// Puts values in the order they're listed in: alphabetical by title if the property asks for it, or else the order
	/// they're defined in.
	/// </summary>
	private IReadOnlyList<CharacterPropertyOption> Sort(IReadOnlyList<CharacterPropertyOption> options) =>
		Property.SortByTitle ? options.OrderBy(x => x.Title, StringComparer.CurrentCulture).ToList() : options;

	private IReadOnlyList<CharacterPropertyOption> m_options;
	private IReadOnlyList<CharacterPropertyOption> m_developerOptions;
}
