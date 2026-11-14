using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Status;

/// <summary>
/// Shows how the player is doing: each of their needs, as of when it was opened.
/// </summary>
public sealed class StatusViewModel : ViewModelBase
{
	public StatusViewModel(GameWorld world)
	{
		var player = world.Player;
		var calendar = world.GameData.Calendar;
		string FormatMinutes(long minutes) => calendar.FormatOffsetFrom(world.Time, calendar.CreateTimeOffset(0, 0, (int) minutes), TimeFormat.Long);

		Needs = world.GameData.Schedules.Needs
			.Where(x => Scheduler.HasNeed(player, x))
			.Select(need =>
			{
				if (need.Kind == NeedKind.Level)
				{
					var level = player.GetLevel(need);
					var amount = OurResources.NeedStatusLevel.FormatCurrentCulture(Math.Round(level), Need.MaximumLevel);
					return new NeedStatusViewModel(need.Title, need.GetLevelTitle(level) is { } title ? OurResources.NeedStatusLevelTitled.FormatCurrentCulture(title, amount) : amount, null);
				}

				var people = need.SharedByFamily ? player.Family.Members : [player];
				var done = people.Sum(x => x.GetProgress(need));
				var quota = need.GetQuotaMinutes(people.Count);
				var isDay = need.Period == NeedPeriod.Day;
				string text;
				if (need.IsTrips)
				{
					// Trips are counted once they're finished, with how long each one takes at the place and at home.
					var tripMinutes = need.GetShiftMinutes(people.Count);
					var trips = quota / tripMinutes;
					text = (isDay ? OurResources.NeedStatusTripsDay : OurResources.NeedStatusTripsWeek).FormatCurrentCulture(Math.Min(done / tripMinutes, trips), trips, FormatMinutes(tripMinutes));
				}
				else
				{
					text = (isDay ? OurResources.NeedStatusQuotaDay : OurResources.NeedStatusQuotaWeek).FormatCurrentCulture(FormatMinutes(Math.Min(done, quota)), FormatMinutes(quota));
				}

				var notes = new[] { need.SharedByFamily ? OurResources.NeedStatusShared : null, player.PendingTrip == need ? need.PlayerAction?.Return : null }.OfType<string>().ToList();
				return new NeedStatusViewModel(need.Title, text, notes.Count == 0 ? null : string.Join(OurResources.NeedStatusNoteSeparator, notes)) { IsDone = done >= quota };
			})
			.ToList();
	}

	public event EventHandler? CloseRequested;

	public IReadOnlyList<NeedStatusViewModel> Needs { get; }

	public void Close()
	{
		VerifyAccess();
		CloseRequested.Raise(this);
	}
}

/// <summary>
/// How the player is doing with one need, such as "Fatigue: Tired (62 of 100)", "Work: 2 hours of 8 hours today", or
/// "Fetching water: 1 of 3 visits today (5 minutes each)", whether a quota need is done, and
/// notes such as whether the household shares it, and whether the player still has to finish a trip for it at home.
/// </summary>
public sealed record NeedStatusViewModel(string Title, string Value, string? Note)
{
	/// <summary>
	/// Whether the need's quota is met for this day or week.
	/// </summary>
	public bool IsDone { get; init; }
}
