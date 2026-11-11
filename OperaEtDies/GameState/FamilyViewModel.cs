using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// The people who live together in one house.
/// </summary>
public sealed class FamilyViewModel : ViewModelBase
{
	public FamilyViewModel(string lastName, IReadOnlyList<PersonViewModel> members, LocationViewModel home, WealthLevel wealthLevel)
	{
		LastName = lastName;
		Members = members;
		Home = home;
		WealthLevel = wealthLevel;
	}

	/// <summary>
	/// The last name of the head of the family.
	/// </summary>
	public string LastName { get; }

	/// <summary>
	/// The members of the family, starting with the head.
	/// </summary>
	public IReadOnlyList<PersonViewModel> Members { get; }

	/// <summary>
	/// The house the family lives in.
	/// </summary>
	public LocationViewModel Home { get; }

	public WealthLevel WealthLevel { get; }
}
