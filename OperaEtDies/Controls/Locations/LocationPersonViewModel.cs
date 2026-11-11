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

	public PersonViewModel Person { get; }

	public string Name => Person.FullName;

	/// <summary>
	/// The description of the person, which is only shown once they've been examined.
	/// </summary>
	public string Description => Person.Description;

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
