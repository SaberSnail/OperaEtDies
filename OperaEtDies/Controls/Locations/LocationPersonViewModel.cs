using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// A person who can be examined to see a description of them, such as someone in the player's location.
/// </summary>
public sealed class LocationPersonViewModel : ViewModelBase
{
	public LocationPersonViewModel(PersonViewModel person)
	{
		Person = person;
	}

	/// <summary>
	/// The person, whose description and portrait are only shown once they've been examined. They're bound to
	/// directly, so they update when the person's place or work changes.
	/// </summary>
	public PersonViewModel Person { get; }

	public string Name => Person.FullName;

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
