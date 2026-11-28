using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Controls.Status;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Start;

/// <summary>
/// What the player can start, as anyone else choosing a goal would: each need they have, with how it's going, and a split
/// button that starts it (or carries on with it), with shorter shifts and having it all done for them in its menu, or why
/// it can't be started now.
/// </summary>
public sealed class StartScreenViewModel : ViewModelBase, ISheet, INumberedChoices
{
	public StartScreenViewModel(GameWorld world)
	{
		var player = world.Player;
		InProgress = player.Current is { } current ? OurResources.StartInProgress.FormatCurrentCulture(current.Need.Title) : null;
		Choices = world.GetPlayerStartChoices().Select(x => new StartChoiceViewModel(x, world, Start)).ToList();
		m_actions = Choices.Select(x => x.Action).OfType<SplitActionViewModel>().ToList();
		for (var index = 0; index < m_actions.Count; index++)
			m_actions[index].Badge = Hotkeys.GetChoiceKeyText(index + 1);
	}


	/// <summary>
	/// Raised when the player chooses something to start, or carry on with, with how.
	/// </summary>
	public event EventHandler<GenericEventArgs<StartRequest>>? StartRequested;

	public string Title { get; } = OurResources.StartTitle;

	public bool IsFullWidth => false;

	/// <summary>
	/// What the player is in the middle of, which starting something else puts on hold, or null if nothing.
	/// </summary>
	public string? InProgress { get; }

	public IReadOnlyList<StartChoiceViewModel> Choices { get; }

	/// <summary>
	/// Presses the numbered button: the buttons that start something are numbered in the order they're listed.
	/// </summary>
	public bool ChooseNumber(int number)
	{
		VerifyAccess();
		var isChosen = number >= 1 && number <= m_actions.Count;
		if (isChosen)
			m_actions[number - 1].Do();
		return isChosen;
	}

	/// <summary>
	/// Opens the menu of the numbered button.
	/// </summary>
	public bool OpenChoiceMenu(int number)
	{
		VerifyAccess();
		return number >= 1 && number <= m_actions.Count && m_actions[number - 1].OpenMenu();
	}

	/// <summary>
	/// Asks for something to be started, or carried on with.
	/// </summary>
	private void Start(StartRequest request) => StartRequested.Raise(this, new GenericEventArgs<StartRequest>(request));

	private readonly IReadOnlyList<SplitActionViewModel> m_actions;
}

/// <summary>
/// Something the player chose to start, or carry on with, whether it's done for them, and for a need measured in time,
/// how many minutes they chose to spend on it, if fewer than anyone else would, or null.
/// </summary>
public sealed record StartRequest(Need Need, bool IsAutomatic, int? Minutes = null);

/// <summary>
/// Something the player can start: the need, how it's going, and a split button to start it (or carry on with it), or why
/// it can't be started now.
/// </summary>
public sealed class StartChoiceViewModel : ViewModelBase
{
	internal StartChoiceViewModel(PlayerStartChoice choice, GameWorld world, Action<StartRequest> start)
	{
		Choice = choice;
		var need = choice.Need;
		Title = need.Title;
		Status = new NeedStatusViewModel(world, world.Player, need, isDeveloper: false).Value;
		Note = GetNote(choice, world);
		Action = choice.CanStart ? CreateAction(choice, world, start) : null;
	}

	/// <summary>
	/// Gets why something can't be started now, such as when it can be done, or null if it can.
	/// </summary>
	internal static string? GetNote(PlayerStartChoice choice, GameWorld world) => choice.Block switch
	{
		PlayerStartBlock.Closed => PlayerActionText.FormatOpeningHours(choice.Need, world.GameData.Calendar),
		PlayerStartBlock.Done => choice.Need.Period == NeedPeriod.Week ? OurResources.StartDoneWeek : OurResources.StartDoneDay,
		PlayerStartBlock.NoPlace => OurResources.StartNoPlace,
		_ => null,
	};

	public PlayerStartChoice Choice { get; }

	public string Title { get; }

	/// <summary>
	/// How the need is going, such as "Tired (65/100)" or "2 hours of 8 today".
	/// </summary>
	public string Status { get; }

	/// <summary>
	/// Why it can't be started now, or null.
	/// </summary>
	public string? Note { get; }

	/// <summary>
	/// The split button that starts it, or carries on with it, or null if it can't be started now.
	/// </summary>
	public SplitActionViewModel? Action { get; }

	/// <summary>
	/// Creates the split button that starts something: carrying on with it, if the player is in the middle of it, for a need
	/// measured in time, the shift anyone else would choose, with when it would end, or otherwise just starting it. Its
	/// menu has the shorter shifts, then having it all done for them.
	/// </summary>
	private static SplitActionViewModel CreateAction(PlayerStartChoice choice, GameWorld world, Action<StartRequest> start)
	{
		var need = choice.Need;
		var shift = choice.IsInProgress ? null : world.GetPlayerShiftMinutes(need);
		var text = choice.IsInProgress ? OurResources.StartCarryOn :
			shift is { } minutes ? FormatShift(world, minutes, world.GetPlayerShiftEnd(need, minutes), dayPart: null, isDone: minutes >= world.GetPlayerRemainingMinutes(need)) :
			OurResources.StartButton;
		var shorter = world.GetPlayerShiftChoices(need)
			.Select(x => new MenuActionViewModel(FormatShift(world, x.Minutes, x.Until, x.DayPart, isDone: false), () => start(new StartRequest(need, IsAutomatic: false, x.Minutes))));
		var automatic = new MenuActionViewModel(choice.IsInProgress ? OurResources.StartCarryOnAutomatic : OurResources.StartAutomatic, () => start(new StartRequest(need, IsAutomatic: true)),
			toolTip: OurResources.StartAutomaticToolTip);
		return new SplitActionViewModel(text, () => start(new StartRequest(need, IsAutomatic: false)), alternatives: [.. shorter, automatic], isTextStable: false);
	}

	/// <summary>
	/// Describes a shift by when it ends and how long it is, such as "Until done (8 hours)", "Until afternoon (2 hours)", or
	/// "Until 15:00 (1 hour)".
	/// </summary>
	private static string FormatShift(GameWorld world, int minutes, TimePoint end, CalendarDayPart? dayPart, bool isDone)
	{
		var duration = PlayerActionText.FormatMinutes(world, minutes);
		return isDone ? OurResources.StartUntilDone.FormatCurrentCulture(duration) :
			dayPart is not null ? OurResources.StartUntilDayPart.FormatCurrentCulture(PlayerStepsViewModel.LowerFirst(dayPart.Title), duration) :
			OurResources.StartUntilTime.FormatCurrentCulture(world.GameData.Calendar.FormatClock(end), duration);
	}
}
