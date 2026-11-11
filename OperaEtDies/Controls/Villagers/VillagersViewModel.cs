using GoldenAnvil.Utility;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Villagers;

/// <summary>
/// Shows everyone in the village, grouped by family, with the families sorted by last name. Each person can be
/// examined to see a description of them. For developers, each family also shows its wealth level and a way to go
/// straight to its house.
/// </summary>
public sealed class VillagersViewModel : ViewModelBase
{
	public VillagersViewModel(IEnumerable<FamilyViewModel> families, bool isDeveloper)
	{
		IsDeveloper = isDeveloper;
		Families = families
			.OrderBy(x => x.LastName, StringComparer.CurrentCulture)
			.Select(x => new VillagerFamilyViewModel(x, isDeveloper, VisitHome))
			.ToList();
	}

	public event EventHandler? CloseRequested;

	/// <summary>
	/// Raised when a developer asks to go to a family's house.
	/// </summary>
	public event EventHandler<GenericEventArgs<LocationViewModel>>? TravelRequested;

	public IReadOnlyList<VillagerFamilyViewModel> Families { get; }

	/// <summary>
	/// Whether to show the developer information for each family.
	/// </summary>
	public bool IsDeveloper { get; }

	public void Close()
	{
		VerifyAccess();
		CloseRequested.Raise(this);
	}

	private void VisitHome(FamilyViewModel family) => TravelRequested.Raise(this, new GenericEventArgs<LocationViewModel>(family.Home));
}

/// <summary>
/// One family in the list of villagers.
/// </summary>
public sealed class VillagerFamilyViewModel : ViewModelBase
{
	public VillagerFamilyViewModel(FamilyViewModel family, bool isDeveloper, Action<FamilyViewModel> visitHome)
	{
		Family = family;
		IsDeveloper = isDeveloper;
		m_visitHome = visitHome;
		Title = OurResources.FamilyTitle.FormatCurrentCulture(family.LastName);
		Wealth = OurResources.VillagerFamilyWealth.FormatCurrentCulture(family.WealthLevel.GetDataName());
		People = family.Members.Select(x => new LocationPersonViewModel(x)).ToList();
	}

	public FamilyViewModel Family { get; }

	public string Title { get; }

	public IReadOnlyList<LocationPersonViewModel> People { get; }

	/// <summary>
	/// Whether to show the developer information for the family.
	/// </summary>
	public bool IsDeveloper { get; }

	/// <summary>
	/// The family's wealth level, for developers.
	/// </summary>
	public string Wealth { get; }

	/// <summary>
	/// The title of the family's house.
	/// </summary>
	public string HomeTitle => Family.Home.Title;

	/// <summary>
	/// Goes to the family's house, for developers.
	/// </summary>
	public void VisitHome()
	{
		VerifyAccess();
		m_visitHome(Family);
	}

	private readonly Action<FamilyViewModel> m_visitHome;
}
