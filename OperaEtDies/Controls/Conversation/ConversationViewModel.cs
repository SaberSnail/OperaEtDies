using System.Collections.ObjectModel;
using System.Globalization;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Conversation;

/// <summary>
/// Talking with someone in the player's location: asking what they're doing, asking whether they've seen someone,
/// chatting with them, or asking them the question of the next "ask" step of the player's pending steps, if it asks
/// them. Each takes time, shown on its button, and what was said is kept as a list of lines. It's shown on top of the
/// player's location, which is shown again when it closes.
/// </summary>
public sealed class ConversationViewModel : ViewModelBase
{
	public ConversationViewModel(Person person, GameWorld world)
	{
		var gameData = world.GameData;
		m_world = world;
		m_person = person;
		Person = new LocationPersonViewModel(person, gameData.PortraitRenderer, gameData.Schedules, gameData.Relationships);
		Subjects = world.People
			.Where(x => !x.IsPlayer && x != person)
			.OrderBy(x => x.FullName, StringComparer.CurrentCulture)
			.ToList();
		m_selectedSubject = Subjects.Count == 0 ? null : Subjects[0];
		Lines = [];

		var talking = gameData.Schedules.Talking;
		AskWhatDoingText = OurResources.ConversationAskDoing.FormatCurrentCulture(FormatMinutes(talking.AskDoingMinutes));
		AskAboutText = OurResources.ConversationAskAbout.FormatCurrentCulture(FormatMinutes(talking.AskAboutMinutes));
		SocializeText = OurResources.ConversationSocialize.FormatCurrentCulture(FormatMinutes(gameData.Schedules.Socializing.ChatMinutes));
	}

	public event EventHandler? CloseRequested;

	/// <summary>
	/// The person being talked with, with their name, portrait, and what they're doing.
	/// </summary>
	public LocationPersonViewModel Person { get; }

	public string Title => OurResources.ConversationHeader.FormatCurrentCulture(m_person.FullName);

	/// <summary>
	/// What was said, in order.
	/// </summary>
	public ObservableCollection<string> Lines { get; }

	/// <summary>
	/// Why the player can't talk with the person right now, such as that they've left, or null if they can.
	/// </summary>
	public string? Unavailable
	{
		get
		{
			VerifyAccess();
			return m_person.Location != m_world.Player.Location ? OurResources.ConversationNotHere.FormatCurrentCulture(m_person.FullName) :
				m_world.IsTooBusyToTalk(m_person) ? OurResources.ConversationTooBusy.FormatCurrentCulture(m_person.FullName) :
				null;
		}
	}

	/// <summary>
	/// The people the player can ask about: everyone but the player and the person being talked with, by name.
	/// </summary>
	public IReadOnlyList<Person> Subjects { get; }

	/// <summary>
	/// The person to ask about.
	/// </summary>
	public Person? SelectedSubject
	{
		get => VerifyAccess(m_selectedSubject);
		set => SetPropertyField(value, ref m_selectedSubject);
	}

	public string AskWhatDoingText { get; }

	public string AskAboutText { get; }

	public string SocializeText { get; }

	/// <summary>
	/// The text of the button that asks the question of the player's next "ask" step, with how long it takes, or null if
	/// they have none to ask the person now.
	/// </summary>
	public string? AskStepText
	{
		get
		{
			VerifyAccess();
			return m_world.GetPlayerAsk(m_person) is { } ask ? OurResources.ConversationAskStep.FormatCurrentCulture(ask.Step.Player!.Title, FormatMinutes(GetMinutes(ask))) : null;
		}
	}

	public bool CanAskStep() => m_world.GetPlayerAsk(m_person) is not null;

	/// <summary>
	/// Asks the person the question of the player's next "ask" step, saying what its player text says (or its title), and
	/// tells how they answered, and when and where they agreed to meet, if they did.
	/// </summary>
	public void AskStep()
	{
		VerifyAccess();
		if (m_world.GetPlayerAsk(m_person) is { } ask)
		{
			Say(ask.Step.Player!.Text?.Text ?? ask.Step.Player.Title!);
			var answer = m_world.AskPlayerQuestion(m_person);
			Say(answer.Appointment is { } appointment ?
				OurResources.ConversationAgreedToMeet.FormatCurrentCulture(m_person.FullName, AppointmentText.FormatPlace(m_world, appointment), AppointmentText.FormatWhen(m_world, appointment.Time)) :
				(answer.IsYes ? OurResources.ConversationAgreed : OurResources.ConversationSaidNo).FormatCurrentCulture(m_person.FullName));
		}
		Refresh();
	}

	/// <summary>
	/// Whether the player can tell the person they're calling off what they're doing together, such as an appointment, or
	/// the player following them.
	/// </summary>
	public bool HasCallOff => m_world.CanCallOffWith(m_person);

	/// <summary>
	/// Tells the person the player is calling off what they're doing together. They carry on without the player.
	/// </summary>
	public void CallOff()
	{
		VerifyAccess();
		m_world.CallOffWith(m_person);
		Say(OurResources.ConversationCalledOff.FormatCurrentCulture(m_person.FullName));
		Refresh();
	}

	public bool CanAskWhatDoing() => m_world.CanTalkTo(m_person);

	/// <summary>
	/// Asks the person where they're going and why.
	/// </summary>
	public void AskWhatDoing()
	{
		VerifyAccess();
		if (CanAskWhatDoing())
		{
			Say(OurResources.ConversationAskedDoing.FormatCurrentCulture(m_person.FullName));
			var answer = m_world.AskWhatDoing(m_person);
			Say(DescribePlan(m_person, answer.Sighting?.Location ?? m_person.Location, answer.Sighting?.Plan));
		}
		Refresh();
	}

	public bool CanAskAbout() => m_selectedSubject is not null && m_world.CanTalkTo(m_person);

	/// <summary>
	/// Asks the person whether they've seen the selected person.
	/// </summary>
	public void AskAbout()
	{
		VerifyAccess();
		if (CanAskAbout() && m_selectedSubject is { } subject)
		{
			Say(OurResources.ConversationAskedAbout.FormatCurrentCulture(m_person.FullName, subject.FullName));
			var answer = m_world.AskAbout(m_person, subject);
			Say(answer.Sighting is { } sighting ? DescribeSighting(subject, sighting) : OurResources.TalkNotSeen.FormatCurrentCulture(subject.FullName));
		}
		Refresh();
	}

	public bool CanSocialize() => m_world.CanTalkTo(m_person);

	/// <summary>
	/// Chats with the person.
	/// </summary>
	public void Socialize()
	{
		VerifyAccess();
		if (CanSocialize())
		{
			var start = m_world.Time;
			var time = m_world.Socialize(m_person);
			Say(OurResources.ConversationSocialized.FormatCurrentCulture(m_person.FullName, m_world.GameData.Calendar.FormatOffsetFrom(start, time, TimeFormat.Long)));
		}
		Refresh();
	}

	/// <summary>
	/// Updates what can be said, once time has passed.
	/// </summary>
	private void Refresh()
	{
		RaisePropertyChanged(nameof(Unavailable));
		RaisePropertyChanged(nameof(AskStepText));
		RaisePropertyChanged(nameof(HasCallOff));
	}

	/// <summary>
	/// Gets how many minutes asking an "ask" step's question takes.
	/// </summary>
	private int GetMinutes(PlayerAsk ask) =>
		Math.Max((int) ask.Step.Minutes!.GetAmount(ask.Sequence.Need.SharedByFamily ? m_world.Player.Family.Members.Count : 0), 1);

	public void Close()
	{
		VerifyAccess();
		CloseRequested.Raise(this);
	}

	/// <summary>
	/// Describes what someone said they're doing, in their own words, such as "I'm eating" or "I'm on my way home, where
	/// I'll be eating". They were at the given place when they said it.
	/// </summary>
	internal static string DescribePlan(Person person, Location location, SightingPlan? plan) => plan switch
	{
		{ Goal: { } goal, Destination: { } end } when end == person.Home => OurResources.TalkPlanWalkingHome.FormatCurrentCulture(GetActivity(person, goal, end, plan.Doing)),
		{ Goal: { } goal, Destination: { } end } when GetBuildingQuarter(end) is { } quarter => OurResources.TalkPlanWalkingAt.FormatCurrentCulture(quarter.Title, GetActivity(person, goal, end, plan.Doing), end.Title),
		{ Goal: { } goal, Destination: { } end } => OurResources.TalkPlanWalking.FormatCurrentCulture(end.Title, GetActivity(person, goal, end, plan.Doing)),
		{ Goal: null, Destination: { } end } when end == person.Home => OurResources.TalkPlanWalkingIdleHome,
		{ Goal: null, Destination: { } end } => OurResources.TalkPlanWalkingIdle.FormatCurrentCulture(end.Title),
		{ Goal: { } goal } => OurResources.TalkPlanBusy.FormatCurrentCulture(GetActivity(person, goal, location, plan.Doing)),
		_ => OurResources.TalkPlanIdle,
	};

	/// <summary>
	/// Gets the quarter a building (a house or business) is on, or null if the place is a quarter itself.
	/// </summary>
	private static Location? GetBuildingQuarter(Location place) => place.Template is QuarterTemplate ? null : place.Quarter;

	/// <summary>
	/// Gets what someone does for a need at a place, to go in a sentence, such as "eating": what they were seen or said to
	/// be doing, if it's known, or otherwise the activity of the need's first step done at a place like it, or its activity
	/// (or title, without one).
	/// </summary>
	private static string GetActivity(Person person, Need need, Location place, string? doing) =>
		LowerFirst(doing ?? need.GetActivityAt(x => Scheduler.IsPlaceFor(person, null, x, place), person.Business?.Profession.Id));

	/// <summary>
	/// Describes a sighting of someone in the words of the person who saw them (or was told about it), with what that
	/// person said they were doing, if they were asked.
	/// </summary>
	internal string DescribeSighting(Person subject, Sighting sighting)
	{
		var calendar = m_world.GameData.Calendar;
		var clock = calendar.FormatClock(sighting.Time);
		var time = calendar.GetStartOfDay(sighting.Time) == calendar.GetStartOfDay(m_world.Time) ? clock : OurResources.TalkSeenYesterday.FormatCurrentCulture(clock);
		var (place, destination) = (sighting.Location.Title, sighting.Destination?.Title);
		var where = sighting.Activity switch
		{
			SightingActivity.Busy when sighting.Need is { } need => OurResources.TalkSeenBusy.FormatCurrentCulture(place, GetActivity(subject, need, sighting.Location, sighting.Doing)),
			SightingActivity.Walking => OurResources.TalkSeenWalking.FormatCurrentCulture(place, destination),
			SightingActivity.Left => OurResources.TalkSeenLeaving.FormatCurrentCulture(place, destination),
			SightingActivity.Talking => OurResources.TalkSeenTalking.FormatCurrentCulture(place),
			_ => OurResources.TalkSeenAt.FormatCurrentCulture(place),
		};

		var text = sighting.Source is { } source
			? OurResources.TalkHeard.FormatCurrentCulture(subject.FullName, where, time, source == m_world.Player ? OurResources.TalkSourceYou : source.FullName)
			: OurResources.TalkSeen.FormatCurrentCulture(subject.FullName, where, time);
		if (sighting.Plan is { } plan)
		{
			text += " " + plan switch
			{
				{ Goal: { } goal, Destination: { } end } when end == subject.Home => OurResources.TalkToldWalkingHome.FormatCurrentCulture(subject.FullName, GetActivity(subject, goal, end, plan.Doing)),
				{ Goal: { } goal, Destination: { } end } when GetBuildingQuarter(end) is { } quarter =>
					OurResources.TalkToldWalkingAt.FormatCurrentCulture(subject.FullName, quarter.Title, GetActivity(subject, goal, end, plan.Doing), end.Title),
				{ Goal: { } goal, Destination: { } end } => OurResources.TalkToldWalking.FormatCurrentCulture(subject.FullName, end.Title, GetActivity(subject, goal, end, plan.Doing)),
				{ Goal: null, Destination: { } end } when end == subject.Home => OurResources.TalkToldWalkingIdleHome.FormatCurrentCulture(subject.FullName),
				{ Goal: null, Destination: { } end } => OurResources.TalkToldWalkingIdle.FormatCurrentCulture(subject.FullName, end.Title),
				{ Goal: { } goal } => OurResources.TalkToldBusy.FormatCurrentCulture(subject.FullName, GetActivity(subject, goal, sighting.Location, plan.Doing)),
				_ => OurResources.TalkToldIdle.FormatCurrentCulture(subject.FullName),
			};
		}

		return OurResources.TalkQuote.FormatCurrentCulture(text);
	}

	/// <summary>
	/// Makes the first letter of a title lowercase, so it reads as part of a sentence, such as "laundry".
	/// </summary>
	private static string LowerFirst(string text) => text.Length == 0 ? text : char.ToLower(text[0], CultureInfo.CurrentCulture) + text[1..];

	private void Say(string line) => Lines.Add(line);

	private string FormatMinutes(int minutes)
	{
		var calendar = m_world.GameData.Calendar;
		return calendar.FormatOffsetFrom(m_world.Time, calendar.CreateTimeOffset(0, 0, minutes), TimeFormat.Long);
	}

	private readonly GameWorld m_world;
	private readonly Person m_person;
	private Person? m_selectedSubject;
}
