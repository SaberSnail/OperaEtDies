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
		m_world = world;
		var player = world.Player;
		InProgress = player.Current is { } current ? OurResources.StartInProgress.FormatCurrentCulture(current.Need.Title) : null;
		Choices = world.GetPlayerStartChoices().Select(x => new StartChoiceViewModel(x, world, Start)).ToList();
	}

	/// <summary>
	/// Raised when the screen should close without starting anything.
	/// </summary>
	public event EventHandler? CloseRequested;

	/// <summary>
	/// Raised when the player has started something, with how it went if it was done for them, or null.
	/// </summary>
	public event EventHandler<GenericEventArgs<string?>>? Started;

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
	/// Starts something, or carries on with it, and says how it went if it was done for the player.
	/// </summary>
	private void Start(PlayerStartChoice choice)
	{
		var isAutomatic = IsAutomatic;
		var activity = choice.Need.GetActivity(0, m_world.Player.Business?.Profession.Id);
		activity = char.ToLower(activity[0], CultureInfo.CurrentCulture) + activity[1..];
		var start = m_world.Time;
		m_world.StartPlayerSteps(choice.Need, isAutomatic);
		var spent = m_world.GameData.Calendar.FormatOffsetFrom(start, m_world.Time - start, TimeFormat.Long);
		var result = !isAutomatic ? null : (m_world.Player.GetSequence(choice.Need) is not null ? OurResources.PlayerStepsStopped : OurResources.PlayerStepsDone).FormatCurrentCulture(activity, spent);
		Started.Raise(this, new GenericEventArgs<string?>(result));
	}

	private readonly GameWorld m_world;
	private bool m_isAutomatic;
}

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
