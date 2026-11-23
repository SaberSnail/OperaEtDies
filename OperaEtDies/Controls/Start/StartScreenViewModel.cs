using System.Globalization;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Controls.Status;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Start;

/// <summary>
/// What the player can start, as anyone else choosing a goal would: each need they have, with how it's going, and its
/// choices of how long to spend on it (or just a button to start it), or why it can't be started now. The player can
/// choose to have it all done for them.
/// </summary>
public sealed class StartScreenViewModel : ViewModelBase
{
	public StartScreenViewModel(GameWorld world)
	{
		var player = world.Player;
		InProgress = player.Current is { } current ? OurResources.StartInProgress.FormatCurrentCulture(current.Need.Title) : null;
		Choices = world.GetPlayerStartChoices().Select(x => new StartChoiceViewModel(x, world, Start)).ToList();
	}

	/// <summary>
	/// Raised when the screen should close without starting anything.
	/// </summary>
	public event EventHandler? CloseRequested;

	/// <summary>
	/// Raised when the player chooses something to start, or carry on with, and whether it's done for them.
	/// </summary>
	public event EventHandler<GenericEventArgs<StartRequest>>? StartRequested;

	public string Title { get; } = OurResources.StartTitle;

	/// <summary>
	/// What the player is in the middle of, which starting something else puts on hold, or null if nothing.
	/// </summary>
	public string? InProgress { get; }

	public IReadOnlyList<StartChoiceViewModel> Choices { get; }

	/// <summary>
	/// Whether what's started is done for the player, walking included, as anyone else would.
	/// </summary>
	public bool IsAutomatic
	{
		get => VerifyAccess(m_isAutomatic);
		set => SetPropertyField(value, ref m_isAutomatic);
	}

	public void Close()
	{
		VerifyAccess();
		CloseRequested.Raise(this);
	}

	/// <summary>
	/// Asks for something to be started, or carried on with.
	/// </summary>
	private void Start(PlayerStartChoice choice) => StartRequested.Raise(this, new GenericEventArgs<StartRequest>(new StartRequest(choice.Need, IsAutomatic)));

	private bool m_isAutomatic;
}

/// <summary>
/// Something the player chose to start, or carry on with, and whether it's done for them.
/// </summary>
public sealed record StartRequest(Need Need, bool IsAutomatic);

/// <summary>
/// Something the player can start: the need, how it's going, and a button to start it (or carry on with it), or why it
/// can't be started now.
/// </summary>
public sealed class StartChoiceViewModel : ViewModelBase
{
	internal StartChoiceViewModel(PlayerStartChoice choice, GameWorld world, Action<PlayerStartChoice> start)
	{
		Choice = choice;
		var need = choice.Need;
		Title = need.Title;
		Status = new NeedStatusViewModel(world, world.Player, need, isDeveloper: false).Value;
		Note = choice.Block switch
		{
			PlayerStartBlock.Closed => PlayerActionText.FormatOpeningHours(need, world.GameData.Calendar),
			PlayerStartBlock.Done => need.Period == NeedPeriod.Week ? OurResources.StartDoneWeek : OurResources.StartDoneDay,
			PlayerStartBlock.NoPlace => OurResources.StartNoPlace,
			_ => null,
		};

		Options = choice.CanStart ? [new StartOptionViewModel(choice.IsInProgress ? OurResources.StartCarryOn : OurResources.StartButton, () => start(choice))] : [];
	}

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
	/// The button that starts it, or carries on with it, or none if it can't be started now.
	/// </summary>
	public IReadOnlyList<StartOptionViewModel> Options { get; }
}

/// <summary>
/// A button that starts something, such as "Until done for the day (8 hours)".
/// </summary>
public sealed class StartOptionViewModel(string text, Action start) : ViewModelBase
{
	public string Text { get; } = text;

	public void Start()
	{
		VerifyAccess();
		start();
	}
}
