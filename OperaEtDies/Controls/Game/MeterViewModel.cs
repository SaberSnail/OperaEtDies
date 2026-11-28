using System.Windows.Media;
using GoldenAnvil.Utility;
using OperaEtDies.Controls.Start;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Game;

/// <summary>
/// A need shown as its icon over a short bar, such as how rested the player is in the left column, or how much of a
/// household chore is done in Concerns, with what it shows in words in its tooltip. It can be a button that starts the
/// need.
/// </summary>
public sealed class MeterViewModel : ViewModelBase
{
	public MeterViewModel(Need need, Geometry icon, Action<Need>? start = null)
	{
		Need = need;
		Icon = icon;
		m_start = start;
	}

	public Need Need { get; }

	/// <summary>
	/// What the bar measures, such as "Fatigue".
	/// </summary>
	public string Title => Need.Title;

	/// <summary>
	/// The need's icon, on a 24 by 24 grid.
	/// </summary>
	public Geometry Icon { get; }

	/// <summary>
	/// How full the bar is, from 0 to 1.
	/// </summary>
	public double Fraction
	{
		get => VerifyAccess(m_fraction);
		private set => SetPropertyField(value, ref m_fraction);
	}

	/// <summary>
	/// What the bar shows, in words.
	/// </summary>
	public string ToolTip
	{
		get => VerifyAccess(m_toolTip);
		private set => SetPropertyField(value, ref m_toolTip);
	}

	/// <summary>
	/// Whether what it measures can be seen to now. A chore that can't be done today is shown muted.
	/// </summary>
	public bool IsActive
	{
		get => VerifyAccess(m_isActive);
		private set => SetPropertyField(value, ref m_isActive);
	}

	/// <summary>
	/// Whether a need is at its worst level, which is shown as a warning.
	/// </summary>
	public bool IsWarning
	{
		get => VerifyAccess(m_isWarning);
		private set => SetPropertyField(value, ref m_isWarning);
	}

	/// <summary>
	/// Whether a chore is done for the day or week, which is shown differently from one that's only partly done.
	/// </summary>
	public bool IsComplete
	{
		get => VerifyAccess(m_isComplete);
		private set => SetPropertyField(value, ref m_isComplete);
	}

	/// <summary>
	/// Whether pressing it starts the need's steps now.
	/// </summary>
	public bool IsStartable
	{
		get => VerifyAccess(m_isStartable);
		private set => SetPropertyField(value, ref m_isStartable);
	}

	/// <summary>
	/// Starts the need's steps, or carries on with them, if it can be started now.
	/// </summary>
	public void Start()
	{
		VerifyAccess();
		if (IsStartable)
			m_start?.Invoke(Need);
	}

	/// <summary>
	/// Shows how the player is doing with a level need: a full bar is a need that's fully met, and one at its worst level is
	/// a warning. The tooltip has the word for its level (or the level, without words), and what pressing it does, or why
	/// it can't be started now.
	/// </summary>
	internal void UpdateLevel(GameWorld world, PlayerStartChoice? choice)
	{
		VerifyAccess();
		var level = world.Player.GetLevel(Need);
		var status = OurResources.NeedToolTip.FormatCurrentCulture(Need.Title,
			Need.GetLevelTitle(level) ?? OurResources.NeedStatusLevel.FormatCurrentCulture(Math.Round(level), Need.MaximumLevel));
		Update(1 - (level / Need.MaximumLevel), AddAction(status, choice, world), isWarning: Need.IsWorstLevel(level), isStartable: choice?.CanStart ?? false);
	}

	/// <summary>
	/// Shows how far along a quota need is, such as a chore, and whether it can be started now, with both in its tooltip.
	/// One that can't be worked on today is muted, and one that's done for the day or week is shown complete.
	/// </summary>
	internal void UpdateQuota(GameWorld world, PlayerQuotaProgress quota, PlayerStartChoice? choice)
	{
		VerifyAccess();
		var status = quota.IsOpenToday ?
			OurResources.QuotaProgressToolTip.FormatCurrentCulture(Need.Title, ConcernsViewModel.FormatQuotaAmount(Need, quota.Done), ConcernsViewModel.FormatQuotaAmount(Need, quota.Total),
				Need.Period == NeedPeriod.Week ? OurResources.QuotaThisWeek : OurResources.QuotaToday) :
			OurResources.QuotaClosedToolTip.FormatCurrentCulture(Need.Title);
		Update(quota.Fraction, AddAction(status, choice, world), isActive: quota.IsOpenToday, isComplete: quota.Done >= quota.Total, isStartable: choice?.CanStart ?? false);
	}

	/// <summary>
	/// Shows how the need is going now.
	/// </summary>
	internal void Update(double fraction, string toolTip, bool isActive = true, bool isWarning = false, bool isComplete = false, bool isStartable = false)
	{
		VerifyAccess();
		Fraction = Math.Clamp(fraction, 0, 1);
		ToolTip = toolTip;
		IsActive = isActive;
		IsWarning = isWarning;
		IsComplete = isComplete;
		IsStartable = isStartable && m_start is not null;
	}

	/// <summary>
	/// Adds what pressing it does, or why it can't be started now, to a tooltip, for a need that can be started from here.
	/// </summary>
	private string AddAction(string status, PlayerStartChoice? choice, GameWorld world)
	{
		var action = choice is null || m_start is null ? null :
			choice.CanStart ? (choice.IsInProgress ? OurResources.ConcernsCarryOnToolTip : OurResources.ConcernsStartToolTip) :
			StartChoiceViewModel.GetNote(choice, world);
		return action is null ? status : OurResources.ConcernsChoreToolTip.FormatCurrentCulture(status, action);
	}

	private readonly Action<Need>? m_start;
	private double m_fraction;
	private string m_toolTip = "";
	private bool m_isActive = true;
	private bool m_isWarning;
	private bool m_isComplete;
	private bool m_isStartable;
}
