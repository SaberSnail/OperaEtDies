using OperaEtDies.Characters;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// A character in the player's location, who can be examined to see a description of them.
/// </summary>
public sealed class CharacterViewModel : ViewModelBase
{
	public CharacterViewModel(Character character)
	{
		Name = character.Traits.FullName;
		Description = character.Traits.GetDescription();
	}

	public string Name { get; }

	/// <summary>
	/// The description of the character, which is only shown once they've been examined.
	/// </summary>
	public string Description { get; }

	public bool IsExamined
	{
		get => VerifyAccess(m_isExamined);
		private set => SetPropertyField(value, ref m_isExamined);
	}

	public bool CanExamine() => !m_isExamined;

	public void Examine()
	{
		VerifyAccess();
		IsExamined = true;
	}

	private bool m_isExamined;
}
