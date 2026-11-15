using GoldenAnvil.Utility;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Time;

namespace OperaEtDies.Controls.Villagers;

/// <summary>
/// Shows everyone in the village, grouped by family, with the families sorted by last name. Each person can be
/// examined to open a view focused on them, and the player and their relatives are labeled. For developers, each family also shows its wealth
/// level and a way to go straight to its house, and each person other than the player a way to go straight to them, and
/// where they're going and why.
/// </summary>
public sealed class VillagersViewModel : ViewModelBase
{
	public VillagersViewModel(IEnumerable<Family> families, bool isDeveloper, PortraitRenderer? portraitRenderer = null, ScheduleSettings? schedules = null, FamilySettings? familySettings = null, GameCalendar? calendar = null)
	{
		IsDeveloper = isDeveloper;
		Families = families
			.OrderBy(x => x.LastName, StringComparer.CurrentCulture)
			.Select(x => new VillagerFamilyViewModel(x, isDeveloper, VisitHome, VisitPerson, ExaminePerson, portraitRenderer, schedules, familySettings, calendar))
			.ToList();
	}

	public event EventHandler? CloseRequested;

	/// <summary>
	/// Raised when a developer asks to go to a family's house, or to where someone is.
	/// </summary>
	public event EventHandler<GenericEventArgs<Location>>? TravelRequested;

	/// <summary>
	/// Raised when the player examines someone, to open a view focused on them.
	/// </summary>
	public event EventHandler<GenericEventArgs<Person>>? ExamineRequested;

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

	private void VisitHome(Family family) => TravelRequested.Raise(this, new GenericEventArgs<Location>(family.Home));

	private void VisitPerson(Person person) => TravelRequested.Raise(this, new GenericEventArgs<Location>(person.Location));

	private void ExaminePerson(Person person) => ExamineRequested.Raise(this, new GenericEventArgs<Person>(person));
}

/// <summary>
/// One family in the list of villagers.
/// </summary>
public sealed class VillagerFamilyViewModel : ViewModelBase
{
	public VillagerFamilyViewModel(Family family, bool isDeveloper, Action<Family> visitHome, Action<Person> visitPerson, Action<Person> examinePerson, PortraitRenderer? portraitRenderer = null, ScheduleSettings? schedules = null, FamilySettings? familySettings = null, GameCalendar? calendar = null)
	{
		Family = family;
		IsDeveloper = isDeveloper;
		m_visitHome = visitHome;
		Title = OurResources.FamilyTitle.FormatCurrentCulture(family.LastName);
		Wealth = OurResources.VillagerFamilyWealth.FormatCurrentCulture(family.WealthLevel.GetDataName());
		People = family.Members.Select(x => new LocationPersonViewModel(x, portraitRenderer, schedules, familySettings, isDeveloper ? visitPerson : null, developerCalendar: isDeveloper ? calendar : null, examine: examinePerson)).ToList();
	}

	public Family Family { get; }

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

	private readonly Action<Family> m_visitHome;
}
