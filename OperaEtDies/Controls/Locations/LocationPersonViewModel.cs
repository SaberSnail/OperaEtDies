using System.ComponentModel;
using System.Windows.Media;
using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Time;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// A person who can be examined to open a view focused on them, such as someone in the player's location, with what
/// they're doing, and how they're related to the player if they're in the player's household. In the player's location,
/// the player can talk with them and follow them. In the list of villagers, the player is labeled, and developers can go to where anyone
/// else is.
/// </summary>
public sealed class LocationPersonViewModel : ViewModelBase
{
	/// <summary>
	/// Creates the view model. With the family settings, the player is labeled as the player, and members of their household
	/// with how they're related to the player. If there's a way to go to people or
	/// to follow them, it's offered for everyone but the player. If there's a way to examine people, it's offered for
	/// everyone. With the developer calendar, developers are shown where
	/// everyone but the player is going and why.
	/// </summary>
	public LocationPersonViewModel(Person person, PortraitRenderer? portraitRenderer, ScheduleSettings? schedules = null, FamilySettings? labelRelations = null,
		Action<Person>? goTo = null, Action<Person>? toggleFollow = null, GameCalendar? developerCalendar = null, Action<Person>? examine = null, Action<Person>? talk = null)
	{
		Person = person;
		m_portraitRenderer = portraitRenderer;
		m_schedules = schedules;
		m_developerCalendar = person.IsPlayer ? null : developerCalendar;
		m_goTo = person.IsPlayer ? null : goTo;
		m_toggleFollow = person.IsPlayer ? null : toggleFollow;
		m_examine = examine;
		m_talk = person.IsPlayer ? null : talk;
		IsYou = labelRelations is not null && person.IsPlayer;
		PlayerLabel = labelRelations is null ? null : IsYou ? OurResources.ThisIsYou :
			person.Family.Members.Any(x => x.IsPlayer) && labelRelations.GetPlayerRelation(person.Traits.GetValueId) is { } relation ? OurResources.ThisIsYourRelation.FormatCurrentCulture(relation) : null;

		// The person outlives the view model, so it's listened to weakly.
		PropertyChangedEventManager.AddHandler(person, OnPersonPropertyChanged, "");
	}

	/// <summary>
	/// The person. Their description is bound to directly, so it updates when the person's work changes.
	/// </summary>
	public Person Person { get; }

	public string Name => Person.FullName;

	/// <summary>
	/// What the person is doing right now, such as "Sleeping" or "Walking to The Square", or null if nothing is shown.
	/// </summary>
	public string? Activity => VerifyAccess(GetActivity());

	/// <summary>
	/// For developers, where the person is going and what for, such as "Laundry at The Ford, then home (walking to The
	/// Ford)", or null if it isn't shown. Everyone else only sees the next place on the way, in <see cref="Activity"/>.
	/// </summary>
	public string? DeveloperGoal => VerifyAccess(m_developerCalendar is null ? null : GetDeveloperGoal());

	/// <summary>
	/// For developers, why the person chose their goal: when they chose it, how it went, and how urgent each need was then
	/// with its chance of being chosen, or null if it isn't shown.
	/// </summary>
	public string? DeveloperGoalReason => VerifyAccess(m_developerCalendar is { } calendar && m_schedules is { } schedules ? GetDeveloperGoalReason(calendar, schedules) : null);

	/// <summary>
	/// A picture of the person where they are, or null if there's no portrait data.
	/// </summary>
	public ImageSource? Portrait => VerifyAccess(m_portraitRenderer?.GetPortrait(Person.Traits, Person.PortraitPlace));

	/// <summary>
	/// Whether to label the person as the player.
	/// </summary>
	public bool IsYou { get; }

	/// <summary>
	/// How the person is related to the player, such as "This is you" or "This is your son", or null if it isn't shown.
	/// </summary>
	public string? PlayerLabel { get; }

	/// <summary>
	/// Whether the player can go straight to where the person is, for developers.
	/// </summary>
	public bool CanGoTo => m_goTo is not null;

	/// <summary>
	/// Whether the player can follow the person.
	/// </summary>
	public bool CanFollow => m_toggleFollow is not null;

	/// <summary>
	/// Whether the player is following the person.
	/// </summary>
	public bool IsFollowed
	{
		get => VerifyAccess(m_isFollowed);
		set => SetPropertyField([nameof(IsFollowed), nameof(FollowText)], value, ref m_isFollowed);
	}

	/// <summary>
	/// The text of the button that starts or stops following the person.
	/// </summary>
	public string FollowText => IsFollowed ? OurResources.StopFollowing : OurResources.FollowPerson;

	/// <summary>
	/// Whether the player can talk with the person.
	/// </summary>
	public bool CanTalk => m_talk is not null;

	/// <summary>
	/// Starts a conversation with the person.
	/// </summary>
	public void Talk()
	{
		VerifyAccess();
		m_talk?.Invoke(Person);
	}

	/// <summary>
	/// Whether the person can be examined, which opens a view focused on them.
	/// </summary>
	public bool CanExamine() => m_examine is not null;

	/// <summary>
	/// Opens a view focused on the person.
	/// </summary>
	public void Examine()
	{
		VerifyAccess();
		m_examine?.Invoke(Person);
	}

	/// <summary>
	/// Goes straight to where the person is, for developers.
	/// </summary>
	public void GoTo()
	{
		VerifyAccess();
		m_goTo?.Invoke(Person);
	}

	/// <summary>
	/// Starts following the person, or stops if the player already is.
	/// </summary>
	public void ToggleFollow()
	{
		VerifyAccess();
		m_toggleFollow?.Invoke(Person);
	}

	/// <summary>
	/// Describes what the person is doing: walking to the next place on their way, talking with someone, or the activity of the need they're
	/// spending time on (or of being idle). Time at home after a trip has the need's home activity, if it has one.
	/// </summary>
	private string? GetActivity()
	{
		var activity = (string?) null;
		if (Person.CurrentTask is TravelTask travel)
		{
			activity = OurResources.PersonTraveling.FormatCurrentCulture(travel.Destination.Title);
		}
		else if (Person.CurrentTask is TalkTask talk)
		{
			activity = OurResources.PersonTalking.FormatCurrentCulture(talk.With.FullName);
		}
		else if (Person.CurrentTask is WaitTask wait)
		{
			activity = OurResources.PersonWaitingFor.FormatCurrentCulture(wait.For.FullName);
		}
		else if (Person.CurrentTask is BusyTask)
		{
			var goal = Person.Goal;
			activity = goal is null ? m_schedules?.Idle.Activity :
				goal.HomeActivity is { } homeActivity && goal.ReturnHome && Person.Location == Person.Home ? homeActivity :
				goal.Activity ?? goal.Title;
		}

		return activity;
	}

	/// <summary>
	/// Describes the person's plan: their goal (or being idle) and where it's met, whether they go home afterward, and
	/// the end of the walk they're on, if they're walking.
	/// </summary>
	private string? GetDeveloperGoal()
	{
		var tasks = Person.Tasks;
		var text = (string?) null;
		if (tasks.Count != 0)
		{
			// The goal is met where the first busy task is, after walking there.
			var place = Person.Location;
			foreach (var task in tasks.TakeWhile(x => x is not BusyTask))
			{
				if (task is TravelTask step)
					place = step.Destination;
			}

			var goal = Person.Goal;
			text = goal is null ? OurResources.DeveloperIdle.FormatCurrentCulture(place.Title) :
				(goal.ReturnHome && place != Person.Home ? OurResources.DeveloperGoalThenHome : OurResources.DeveloperGoal).FormatCurrentCulture(goal.Title, place.Title);
			if (Person.CurrentTask is TravelTask)
			{
				var destination = tasks.TakeWhile(x => x is TravelTask).OfType<TravelTask>().Last().Destination;
				text = OurResources.DeveloperWalking.FormatCurrentCulture(text, destination.Title);
			}
		}

		return text;
	}

	/// <summary>
	/// Describes how the person last chose their goal.
	/// </summary>
	private string GetDeveloperGoalReason(GameCalendar calendar, ScheduleSettings schedules)
	{
		if (Person.GoalChoice is not { } choice)
			return OurResources.DeveloperNoGoalChoice;

		var time = calendar.FormatClock(choice.Time);
		var title = choice.Need?.Title;
		var result = choice.Result switch
		{
			GoalChoiceResult.Chosen => OurResources.DeveloperGoalChosen.FormatCurrentCulture(time, title),
			GoalChoiceResult.NoPlace => OurResources.DeveloperGoalNoPlace.FormatCurrentCulture(time, title),
			GoalChoiceResult.NoTime => OurResources.DeveloperGoalNoTime.FormatCurrentCulture(time, title),
			_ => OurResources.DeveloperGoalNothingUrgent.FormatCurrentCulture(time),
		};

		// Someone seeking a friend chose who and where from a clue.
		if (choice.Seeking is { } seeking)
		{
			var clue = seeking.Clue switch
			{
				SeekClue.Workplace => OurResources.DeveloperSeekWorkplace,
				SeekClue.Home => OurResources.DeveloperSeekHome,
				SeekClue.Sighting => OurResources.DeveloperSeekSighting,
				_ => OurResources.DeveloperSeekGathering,
			};
			result = OurResources.DeveloperGoalSeeking.FormatCurrentCulture(result, seeking.Friend.FullName, seeking.Place.Title, clue);
		}

		var urgencies = choice.Urgencies.Count == 0 ? OurResources.DeveloperNoUrgencies :
			string.Join(OurResources.DeveloperUrgencySeparator, choice.Urgencies.Select(x => schedules.IsGoalCandidate(x.Need, x.Urgency)
				? OurResources.DeveloperUrgency.FormatCurrentCulture(x.Need.Title, x.Urgency, choice.GetChance(x, schedules))
				: OurResources.DeveloperUrgencyTooLow.FormatCurrentCulture(x.Need.Title, x.Urgency, schedules.MinimumUrgency)));
		return OurResources.DeveloperGoalReason.FormatCurrentCulture(result, urgencies);
	}

	private void OnPersonPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(Person.Traits) or nameof(Person.PortraitPlace) or null or "")
			RaisePropertyChanged(nameof(Portrait));
		if (e.PropertyName is nameof(Person.CurrentTask) or nameof(Person.Location) or null or "")
			RaisePropertyChanged(nameof(Activity), nameof(DeveloperGoal));
		if (e.PropertyName is nameof(Person.GoalChoice) or null or "")
			RaisePropertyChanged(nameof(DeveloperGoalReason));
	}

	private readonly PortraitRenderer? m_portraitRenderer;
	private readonly ScheduleSettings? m_schedules;
	private readonly GameCalendar? m_developerCalendar;
	private readonly Action<Person>? m_goTo;
	private readonly Action<Person>? m_toggleFollow;
	private readonly Action<Person>? m_examine;
	private readonly Action<Person>? m_talk;
	private bool m_isFollowed;
}
