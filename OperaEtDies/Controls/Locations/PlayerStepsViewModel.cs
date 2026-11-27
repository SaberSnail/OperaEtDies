using System.Globalization;
using GoldenAnvil.Utility;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// The steps of what the player is in the middle of, such as carrying water home and putting it away: the step they're
/// on, highlighted, with the steps after it a click away, a split button that presses the step's own button if it has one,
/// or otherwise has the step done for them, with having it (or all of it) done for them in its menu, the options when
/// it's a choice, a button to stop, and the other things they're in the middle of, to switch to.
/// </summary>
public sealed class PlayerStepsViewModel : ViewModelBase, INumberedChoices
{
	/// <summary>
	/// Creates the steps of a sequence, shown expanded or not, with the step's button, the options to choose from, and the
	/// other sequences in progress. Having the step or all of it done for the player, stopping, and switching call the
	/// given actions. The split button and the options are numbered from the given number, as far as the number keys go.
	/// </summary>
	public PlayerStepsViewModel(StepSequence sequence, IReadOnlyList<PlayerStep> steps, Person player, bool isExpanded, PlayerStepActionViewModel? action,
		IReadOnlyList<PlayerStepActionViewModel> choices, IReadOnlyList<PlayerStepActionViewModel> others, Action doStep, Action doAll, Action stop, bool canDoStep, string? carryWarning = null,
		int firstNumber = 1)
	{
		// An appointment's steps are named for who it's with, since they're part of the need's steps, which may be in progress too.
		Title = sequence.IsFinishingUp ? OurResources.PlayerStepsHeaderFinishing.FormatCurrentCulture(sequence.Need.Title) :
			sequence is { Role: SequenceRole.Steps, Appointment: null } ? OurResources.PlayerStepsHeader.FormatCurrentCulture(sequence.Need.Title) :
			OurResources.PlayerStepsHeaderWith.FormatCurrentCulture(GetOther(sequence, player)?.FullName ?? "");
		var current = steps.FirstOrDefault(x => x.IsCurrent);
		Steps = steps.Select(x => new PlayerStepItemViewModel(Describe(x, player), x.IsCurrent, IsReached: false)).ToList();
		Next = current is null ? null : OurResources.PlayerStepsNext.FormatCurrentCulture(current.Kind == PlayerStepKind.Option ? OurResources.PlayerStepChoose : Describe(current, player));
		Action = action;
		Choices = choices;
		Others = others;
		m_isExpanded = isExpanded;
		m_doStep = doStep;
		m_doAll = doAll;
		m_stop = stop;
		m_canDoStep = canDoStep;
		CarryWarning = carryWarning;

		m_firstNumber = firstNumber;
		var doStepItem = new MenuActionViewModel(OurResources.PlayerStepsAutomatic, DoStep, CanDoStep, OurResources.PlayerStepsAutomaticToolTip);
		var doAllItem = new MenuActionViewModel(OurResources.PlayerStepsDoAll, DoAll, toolTip: OurResources.PlayerStepsDoAllToolTip);
		Primary = action is null
			? new SplitActionViewModel(OurResources.PlayerStepsAutomatic, DoStep, CanDoStep, [doAllItem], OurResources.PlayerStepsAutomaticToolTip, isTextStable: false)
			: new SplitActionViewModel(action.Text, action.Do, action.CanDo, [doStepItem, doAllItem], isTextStable: false);
		Primary.Badge = Hotkeys.GetChoiceKeyText(firstNumber);
		for (var index = 0; index < choices.Count; index++)
			choices[index].Badge = Hotkeys.GetChoiceKeyText(firstNumber + 1 + index);
	}

	/// <summary>
	/// The split button for the step: the step's own button, if it has one, or else having the step done for the player,
	/// with having it (or all of it) done for them in its menu.
	/// </summary>
	public SplitActionViewModel Primary { get; }

	/// <summary>
	/// Presses the numbered button: the split button's default, then the options.
	/// </summary>
	public bool ChooseNumber(int number)
	{
		VerifyAccess();
		var isChosen = false;
		var index = number - m_firstNumber - 1;
		if (number == m_firstNumber && Primary.CanDo())
		{
			Primary.Do();
			isChosen = true;
		}
		else if (index >= 0 && index < Choices.Count && Choices[index].CanDo())
		{
			Choices[index].Do();
			isChosen = true;
		}
		return isChosen;
	}

	/// <summary>
	/// Opens the split button's menu, for its number.
	/// </summary>
	public bool OpenChoiceMenu(int number)
	{
		VerifyAccess();
		return number == m_firstNumber && Primary.OpenMenu();
	}

	/// <summary>
	/// A reminder of the bundles the player kept to finish these steps later, once they've carried them for a while, or
	/// null.
	/// </summary>
	public string? CarryWarning { get; }

	/// <summary>
	/// The button for the step the player is on, such as "Fetch water until 8:05 (5 minutes)", or null if it has none. It's
	/// the split button's default when there is one.
	/// </summary>
	public PlayerStepActionViewModel? Action { get; }

	/// <summary>
	/// The options the player can choose, when the step they're on is a choice.
	/// </summary>
	public IReadOnlyList<PlayerStepActionViewModel> Choices { get; }

	public bool HasChoices => Choices.Count != 0;

	/// <summary>
	/// The other things the player is in the middle of, which they can switch to.
	/// </summary>
	public IReadOnlyList<PlayerStepActionViewModel> Others { get; }

	public bool HasOthers => Others.Count != 0;

	/// <summary>
	/// Raised when the steps are expanded or collapsed, so it's kept when they're shown again.
	/// </summary>
	public event EventHandler? ExpandedChanged;

	/// <summary>
	/// What the player is in the middle of, such as "In progress: Fetching water", or for an appointment, or taking part in
	/// someone else's steps, who with, such as "In progress: with Mary Smith", or for steps given up that are taking care
	/// of what's left, "Finishing up: Laundry".
	/// </summary>
	public string Title { get; }

	/// <summary>
	/// The step the player is on, such as "Next: Carry it home to finish.", or null if there's nothing left that the player
	/// takes themselves.
	/// </summary>
	public string? Next { get; }

	/// <summary>
	/// The steps from the one the player is on, with it marked, shown while expanded.
	/// </summary>
	public IReadOnlyList<PlayerStepItemViewModel> Steps { get; }

	public bool IsExpanded
	{
		get => VerifyAccess(m_isExpanded);
		set
		{
			if (SetPropertyField([nameof(IsExpanded), nameof(ExpandText)], value, ref m_isExpanded))
				ExpandedChanged.Raise(this);
		}
	}

	public string ExpandText => IsExpanded ? OurResources.PlayerStepsHide : OurResources.PlayerStepsShow;

	public void ToggleExpanded() => IsExpanded = !IsExpanded;

	public bool CanDoStep() => m_canDoStep;

	/// <summary>
	/// Has the step the player is on carried out for them.
	/// </summary>
	public void DoStep()
	{
		VerifyAccess();
		m_doStep();
	}

	/// <summary>
	/// Has all of the steps carried out for the player.
	/// </summary>
	public void DoAll()
	{
		VerifyAccess();
		m_doAll();
	}

	/// <summary>
	/// Gives up the steps.
	/// </summary>
	public void Stop()
	{
		VerifyAccess();
		m_stop();
	}

	/// <summary>
	/// Describes a step the player is shown: a step with a button by its title, going somewhere by the step's text for the
	/// player, if it has one, or where it would take them, changing clothes by the kind of clothes, which the player does in
	/// their inventory or wardrobe, asking by the title they ask it by in conversation, an option by its title, and the
	/// steps of whichever option is chosen with an ellipsis.
	/// </summary>
	internal static string Describe(PlayerStep step, Person player) => step.Kind switch
	{
		PlayerStepKind.Option => OurResources.PlayerStepOption.FormatCurrentCulture(step.Option?.Player?.Title ?? ""),
		PlayerStepKind.OptionSteps => OurResources.PlayerStepOptionSteps,
		PlayerStepKind.GoToAppointment or PlayerStepKind.Meet when step.Sequence.Appointment is { } appointment => DescribeMeeting(step.Kind, appointment, player),
		PlayerStepKind.Follow => OurResources.PlayerStepFollow.FormatCurrentCulture(GetOther(step.Sequence, player)?.FullName ?? ""),
		_ => Describe(step.Sequence.Need, step.StepIndex, step.Place, player, step.Sequence.Partner),
	};

	/// <summary>
	/// Describes a step of a need, with where it would take the player, for going somewhere, and the partner its activity
	/// names, if any.
	/// </summary>
	internal static string Describe(Need need, int index, Location? place, Person player, Person? partner = null) => need.Steps[index] switch
	{
		{ IsPlayerAction: true } and var step => step.Player!.Title!,
		{ Kind: StepKind.GoTo, Player.Text: { } text } => ActivityText.FillOther(text.Text, partner),
		{ Kind: StepKind.GoTo } when place is null => OurResources.PlayerStepGoElsewhere,
		{ Kind: StepKind.GoTo } when place == player.Home => OurResources.PlayerStepGoHome,
		{ Kind: StepKind.GoTo } => OurResources.PlayerStepGoTo.FormatCurrentCulture(place!.Title),
		{ Kind: StepKind.Take, Outfit: { } kind } => OurResources.PlayerStepTake.FormatCurrentCulture(DescribeKind(kind)),
		{ Kind: StepKind.Take } => OurResources.PlayerStepTakeClothes,
		{ Kind: StepKind.Put, Bundle: { } bundle } => OurResources.PlayerStepPut.FormatCurrentCulture(LowerFirst(FindBundleTitle(need, bundle.Id) ?? bundle.Id)),
		{ Kind: StepKind.Dress, Outfit: OutfitKind.Nothing } => OurResources.PlayerStepTakeOffEverything,
		{ Kind: StepKind.Dress, Outfit: { } kind } => OurResources.PlayerStepDress.FormatCurrentCulture(DescribeKind(kind)),
		{ Kind: StepKind.Undress, Outfit: OutfitKind.Nothing } => OurResources.PlayerStepDressAgain,
		{ Kind: StepKind.Undress, Outfit: { } kind } => OurResources.PlayerStepUndress.FormatCurrentCulture(DescribeKind(kind)),
		{ Kind: StepKind.Do } => ActivityText.Fill(need.GetActivity(index, player.Business?.Profession.Id), partner),
		{ Kind: StepKind.Wait, UntilHour: { } hour } => OurResources.PlayerStepWaitUntil.FormatCurrentCulture(OurResources.GameClock.FormatCurrentCulture(hour, 0)),
		{ Kind: StepKind.Wait } => OurResources.PlayerStepWait,
		{ Kind: StepKind.Wander } => OurResources.PlayerStepWander,
		{ Kind: StepKind.Seek, Who.Friends: true } => OurResources.PlayerStepSeekFriend,
		{ Kind: StepKind.Seek } => OurResources.PlayerStepSeek,
		{ Kind: StepKind.Talk, With: StepTarget.Anyone } => OurResources.PlayerStepTalkAnyone,
		{ Kind: StepKind.Talk } => OurResources.PlayerStepTalk,
		{ Kind: StepKind.Ask, Player.Title: { } title } => title,
		{ Kind: StepKind.Ask, Appointment: not null } => OurResources.PlayerStepAskAppointment,
		{ Kind: StepKind.Ask, With: StepTarget.Anyone } => OurResources.PlayerStepAskAnyone,
		{ Kind: StepKind.Ask } => OurResources.PlayerStepAsk,
		{ Kind: StepKind.Lead } => OurResources.PlayerStepLead,
		_ => ActivityText.Fill(need.GetActivity(index, player.Business?.Profession.Id), partner),
	};

	/// <summary>
	/// Gets who the player is taking part in the steps of: who they follow, or the other at their appointment.
	/// </summary>
	private static Person? GetOther(StepSequence sequence, Person player) => sequence.Leader ?? sequence.Appointment?.GetOther(player);

	/// <summary>
	/// Finds the title of a bundle a need's steps take or make, by its id.
	/// </summary>
	private static string? FindBundleTitle(Need need, string id) => need.Steps.FirstOrDefault(x => x.Kind is StepKind.Take or StepKind.Make && x.Bundle?.Id == id)?.Bundle?.Title;

	/// <summary>
	/// Describes a step toward an appointment the player set off for: going there, such as "Go to Mary Smith's home", or
	/// meeting the one they're meeting, such as "Meet Mary Smith".
	/// </summary>
	private static string DescribeMeeting(PlayerStepKind kind, Appointment appointment, Person player) => kind switch
	{
		PlayerStepKind.GoToAppointment when appointment.Place == player.Home => OurResources.PlayerStepGoHome,
		PlayerStepKind.GoToAppointment => OurResources.PlayerStepGoToAppointment.FormatCurrentCulture(AppointmentText.FormatPlace(appointment, player)),
		_ => OurResources.PlayerStepMeet.FormatCurrentCulture(appointment.GetOther(player).FullName),
	};

	/// <summary>
	/// Describes a kind of clothes within a sentence, such as "work clothes".
	/// </summary>
	private static string DescribeKind(OutfitKind kind) => LowerFirst(kind switch
	{
		OutfitKind.Work => OurResources.OutfitWork,
		OutfitKind.Night => OurResources.OutfitNight,
		_ => OurResources.OutfitEveryday,
	});

	internal static string LowerFirst(string text) => text.Length == 0 ? text : char.ToLower(text[0], CultureInfo.CurrentCulture) + text[1..];

	private readonly Action m_doStep;
	private readonly Action m_doAll;
	private readonly Action m_stop;
	private readonly bool m_canDoStep;
	private readonly int m_firstNumber;
	private bool m_isExpanded;
}

/// <summary>
/// A button in the player's steps, such as "Fetch water (5 minutes)" for the step they're on, an option they can choose,
/// or something else they're in the middle of. It can only be pressed while it's enabled, and the note may say why not.
/// </summary>
public sealed class PlayerStepActionViewModel : ViewModelBase
{
	public PlayerStepActionViewModel(string text, bool isEnabled, string? note, Action doAction)
	{
		Text = text;
		IsEnabled = isEnabled;
		Note = note;
		m_doAction = doAction;
	}

	public string Text { get; }

	public bool IsEnabled { get; }

	/// <summary>
	/// The key that presses it, such as "1", shown on it, or null if it has none.
	/// </summary>
	public string? Badge
	{
		get => VerifyAccess(m_badge);
		set => SetPropertyField(value, ref m_badge);
	}

	/// <summary>
	/// Why it can't be pressed right now, such as when it's open, or null.
	/// </summary>
	public string? Note { get; }

	public bool CanDo() => IsEnabled;

	public void Do()
	{
		VerifyAccess();
		if (IsEnabled)
			m_doAction();
	}

	private readonly Action m_doAction;
	private string? m_badge;
}

/// <summary>
/// One of the steps the player is shown, marked if it's the one they're on.
/// </summary>
public sealed record PlayerStepItemViewModel(string Text, bool IsNext, bool IsReached);
