using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Time;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// Something the player can spend time on where they are, such as sleeping at home, with a choice of how long. A trip,
/// such as fetching water, has one choice: the time at the place, or the time at home that finishes it. For a quota
/// need, options for longer than the time left to spend on it aren't offered.
/// </summary>
public sealed class PlayerActionViewModel : ViewModelBase
{
	/// <summary>
	/// Creates the action, with how long each option would take from now. Doing an option is given the choice and how
	/// many minutes it takes.
	/// </summary>
	public PlayerActionViewModel(PlayerActionChoice choice, GameWorld world, Action<PlayerActionChoice, int> doAction)
	{
		Choice = choice;
		var need = choice.Need;
		var action = need.PlayerAction ?? throw new ArgumentException($"The need {need.Id} has no player action.", nameof(choice));
		var calendar = world.GameData.Calendar;
		string FormatMinutes(int minutes) => calendar.FormatOffsetFrom(world.Time, calendar.CreateTimeOffset(0, 0, minutes), TimeFormat.Long);

		if (!choice.IsHome && action.OnlyInHours && !world.IsPlayerActionOpen(need))
		{
			// While it's closed, the player is told when it's open instead of being shown options they can't choose.
			Title = action.Title;
			OpeningHours = FormatOpeningHours(need, calendar);
			Options = [];
		}
		else if (!need.IsTrips)
		{
			// For a quota need, options for longer than the time left aren't offered, nor ones for just the time left when
			// there's an option to finish it.
			var remaining = need.Kind == NeedKind.Quota ? world.GetPlayerRemainingMinutes(need) : long.MaxValue;
			var hasUntilMet = action.Options.Any(x => x.UntilMet);
			Title = action.Title;
			Options = action.Options.Where(x => x.Minutes is not { } optionMinutes || optionMinutes < remaining || (optionMinutes == remaining && !hasUntilMet)).Select(option =>
			{
				var minutes = world.GetPlayerActionMinutes(need, option);
				var text = option.Title is null ? FormatMinutes(option.Minutes ?? minutes) :
					minutes > 0 ? OurResources.PlayerActionOptionText.FormatCurrentCulture(option.Title, FormatMinutes(minutes)) : option.Title;
				return new PlayerActionOptionViewModel(text, minutes > 0 ? minutes : null, x => doAction(choice, x));
			}).ToList();
		}
		else
		{
			var minutes = world.GetPlayerTripMinutes(need, choice.IsHome);
			var members = world.Player.Family.Members.Count;
			Title = choice.IsHome ? action.Home!.Title : action.Title;
			var text = choice.IsHome
				? (minutes is null or 0 ? OurResources.PlayerActionFinish : FormatMinutes(minutes.Value))
				: FormatMinutes(minutes ?? need.GetVisitMinutes(members));
			Options = [new PlayerActionOptionViewModel(text, minutes, x => doAction(choice, x))];
		}
	}

	public PlayerActionChoice Choice { get; }

	public string Title { get; }

	public IReadOnlyList<PlayerActionOptionViewModel> Options { get; }

	/// <summary>
	/// When the action is open, such as "Open on Saturday from 7:00 to 14:00", shown in place of the options while it's
	/// closed, or null when it isn't closed.
	/// </summary>
	public string? OpeningHours { get; }

	/// <summary>
	/// Describes the days and hours a quota need can be met, in the calendar's order of the days.
	/// </summary>
	private static string FormatOpeningHours(Need need, GameCalendar calendar)
	{
		var days = need.Days is { } dayIds ? calendar.Days.Where(x => dayIds.Contains(x.Id, StringComparer.Ordinal)).Select(x => x.Title).ToList() : null;
		var dayText = days is null ? OurResources.OpeningHoursEveryDay :
			days.Count == 1 ? days[0] :
			days.Count == 2 ? OurResources.OpeningHoursTwoDays.FormatCurrentCulture(days[0], days[1]) :
			OurResources.OpeningHoursManyDays.FormatCurrentCulture(string.Join(OurResources.OpeningHoursDaySeparator, days.Take(days.Count - 1)), days[^1]);
		var start = OurResources.GameClock.FormatCurrentCulture(need.Start!.Value, 0);
		var end = OurResources.GameClock.FormatCurrentCulture(need.End!.Value, 0);
		return (need.OnSpecialDays ? OurResources.OpeningHours : OurResources.OpeningHoursNotSpecialDays).FormatCurrentCulture(dayText, start, end);
	}
}

/// <summary>
/// One choice of how long to spend on a player action, showing how long it takes before the player chooses it.
/// </summary>
public sealed class PlayerActionOptionViewModel : ViewModelBase
{
	/// <summary>
	/// Creates the option. Without minutes, it can't be chosen right now.
	/// </summary>
	public PlayerActionOptionViewModel(string text, int? minutes, Action<int> doAction)
	{
		Text = text;
		Minutes = minutes;
		m_doAction = doAction;
	}

	/// <summary>
	/// How many minutes the option takes from now, or null if it can't be chosen right now, such as sleeping until rested
	/// when already rested. Finishing a trip at home can take 0 minutes.
	/// </summary>
	public int? Minutes { get; }

	/// <summary>
	/// What the option is called, with how long it takes, such as "Until rested (7 hours, 30 minutes)".
	/// </summary>
	public string Text { get; }

	public bool CanDo() => Minutes is not null;

	public void Do()
	{
		VerifyAccess();
		if (Minutes is { } minutes)
			m_doAction(minutes);
	}

	private readonly Action<int> m_doAction;
}
