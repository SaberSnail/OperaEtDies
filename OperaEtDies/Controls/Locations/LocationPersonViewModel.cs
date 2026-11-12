using System.ComponentModel;
using System.Windows.Media;
using OperaEtDies.Characters;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// A person who can be examined to see a description of them, such as someone in the player's location.
/// </summary>
public sealed class LocationPersonViewModel : ViewModelBase
{
	public LocationPersonViewModel(Person person, PortraitRenderer? portraitRenderer)
	{
		Person = person;
		m_portraitRenderer = portraitRenderer;

		// The person outlives the view model, so it's listened to weakly.
		PropertyChangedEventManager.AddHandler(person, OnPersonPropertyChanged, "");
	}

	/// <summary>
	/// The person, whose description and portrait are only shown once they've been examined. The description is bound
	/// to directly, so it updates when the person's work changes.
	/// </summary>
	public Person Person { get; }

	public string Name => Person.FullName;

	/// <summary>
	/// A picture of the person where they are, or null if there's no portrait data.
	/// </summary>
	public ImageSource? Portrait => VerifyAccess(m_portraitRenderer?.GetPortrait(Person.Traits, Person.PortraitPlace));

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

	private void OnPersonPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(Person.Traits) or nameof(Person.PortraitPlace) or null or "")
			RaisePropertyChanged(nameof(Portrait));
	}

	private readonly PortraitRenderer? m_portraitRenderer;
	private bool m_isExamined;
}
