using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// How people react when who's around them changes and they find themselves less covered than they're comfortable with
/// (see <see cref="ModestyComfort"/> and <see cref="ModestyCoverUp"/>).
/// </summary>
internal sealed partial class Scheduler
{
	/// <summary>
	/// Notes that who can see the people at a location has changed, such as someone arriving, leaving, waking, or falling
	/// asleep, so they react to it (see <see cref="ReactToCompany(TimePoint)"/>).
	/// </summary>
	internal void OnCompanyChanged(Location location) => m_companyChanged.Add(location);

	/// <summary>
	/// Has everyone at the locations whose company changed react to it, until no more change. Someone the game decides for
	/// who's no longer covered enough covers up, and someone covering up who's comfortable again calls it off. The player
	/// is told they're exposed.
	/// </summary>
	internal void ReactToCompany(TimePoint time)
	{
		while (m_companyChanged.Count != 0)
		{
			var locations = m_companyChanged.ToList();
			m_companyChanged.Clear();
			foreach (var person in locations.SelectMany(x => x.People).ToList())
				ReactToCompany(person, time);
		}
	}

	/// <summary>
	/// Has someone react to the company they're in: someone covering up who's comfortable in what they have on calls it
	/// off, and someone awake who isn't covered enough, and isn't already changing, talking, or walking somewhere (such as
	/// heading home to cover up), covers up, unless they'd be covered enough but for people only passing through, when they
	/// only look embarrassed, and cover up if any of them stays. The player is told instead.
	/// </summary>
	private void ReactToCompany(Person person, TimePoint time)
	{
		var gameData = m_world.GameData;
		var isCovered = !person.IsAsleep && ClothingRules.GetUncoveredSlots(gameData, person, person.Clothing.Pieces.Select(x => x.Clothing), ModestyComfort.Get(gameData, person, time)).Count == 0;
		if (person.CurrentTask is ChangeClothesTask { IsCoverUp: true })
		{
			if (isCovered)
				CallOffCoverUp(person, time);
		}
		else if (!isCovered && !person.IsAsleep)
		{
			if (!m_world.IsAutomatic(person))
				m_world.OnPlayerExposed(time);
			else if (person.TaskEnd is not null && person.CurrentTask is not (ChangeClothesTask or TalkTask or TravelTask))
			{
				var staying = person.Location.People.Where(x => x != person && !IsPassingThrough(x)).Select(x => (x, x.IsAsleep));
				var stayingComfort = ModestyComfort.Get(gameData, person, person.Location, staying, ModestyComfort.GetCurrentStep(person), time);
				if (ClothingRules.GetUncoveredSlots(gameData, person, person.Clothing.Pieces.Select(x => x.Clothing), stayingComfort).Count == 0)
					m_world.OnCoverUpReaction(person, LogEntryKind.Embarrassed, time);
				else
					CoverUp(person, time);
			}
		}
	}

	/// <summary>
	/// Whether someone is only passing through where they are: walking on somewhere else, or following someone who is.
	/// </summary>
	private static bool IsPassingThrough(Person person) =>
		person.CurrentTask is TravelTask || (person.CurrentTask is FollowTask follow && follow.Leader.CurrentTask is TravelTask);

	/// <summary>
	/// Has someone cover up: change into the clothes at hand that make them comfortable, or with none, head home to cover
	/// up, unless everyone else there is only passing through, when they only look embarrassed.
	/// </summary>
	private void CoverUp(Person person, TimePoint time)
	{
		var comfort = ModestyComfort.Get(m_world.GameData, person, time);
		if (FindCoverUp(person, comfort, time) is { } change)
		{
			person.Interrupt(change, time, m_calendar.AddMinutes(time, GetMinutes(person, change)));
			m_world.OnTaskStarted(person, change, time);
		}
		else if (person.Location.People.Any(x => x != person && !x.IsAsleep && x.CurrentTask is not TravelTask) && person.Location != person.Home &&
			GetDistances(person.Location).ContainsKey(person.Home))
		{
			Retreat(person, time);
		}
		else
		{
			m_world.OnCoverUpReaction(person, LogEntryKind.Embarrassed, time);
		}
	}

	/// <summary>
	/// Finds the change of clothes that would make someone comfortable at a level of comfort, with every piece at hand, or
	/// null if there's none: out of their outfit for wearing nothing or their night clothes, back into what it replaced,
	/// or else into their everyday clothes or work clothes, in that order.
	/// </summary>
	private ChangeClothesTask? FindCoverUp(Person person, double comfort, TimePoint time)
	{
		var gameData = m_world.GameData;
		var takingOff = person.Outfits.Where(x => x.Kind is OutfitKind.Nothing or OutfitKind.Night && ClothingRules.IsChangedInto(person, x))
			.FirstOrDefault(x => ClothingRules.GetUncoveredSlots(gameData, person, ClothingRules.GetClothesOutOf(person, x, time), comfort).Count == 0);
		if (takingOff is not null)
			return new ChangeClothesTask(takingOff, IsTakingOff: true, IsCoverUp: true);

		var puttingOn = person.GetOutfits(OutfitKind.Everyday).Concat(person.GetOutfits(OutfitKind.Work))
			.FirstOrDefault(x => !ClothingRules.IsDressedIn(person, x) && ClothingRules.IsAtHand(x, time) && ClothingRules.IsComfortableIn(gameData, person, x, comfort));
		return puttingOn is null ? null : new ChangeClothesTask(puttingOn, IsTakingOff: false, IsCoverUp: true);
	}

	/// <summary>
	/// Has someone head home the quickest way to cover up, giving up the steps they were on, which fail as "shy".
	/// </summary>
	private void Retreat(Person person, TimePoint time)
	{
		person.UpdateNeeds(time);
		person.IsAtWork = false;
		if (person.Current is { } sequence)
		{
			m_world.OnStepFailed(person, sequence, sequence.Step, FailCause.Shy, time, person);
			EndSequence(person, sequence, time);
		}

		person.SetTasks(GetPath(person.Location, person.Home, GetDistances(person.Location)).Select(PersonTask (x) => new TravelTask(x, IsRetreat: true)).ToList());
		m_world.OnCoverUpReaction(person, LogEntryKind.Retreating, time);
		Continue(person, time);
	}

	/// <summary>
	/// Calls off someone covering up, once they're comfortable in what they have on, such as when whoever made them
	/// uncomfortable leaves before they finish changing. They go back to what they were doing.
	/// </summary>
	private void CallOffCoverUp(Person person, TimePoint time)
	{
		person.UpdateNeeds(time);
		person.FinishTask();
		Continue(person, time);
	}

	/// <summary>
	/// The locations whose company changed since everyone there last reacted to it.
	/// </summary>
	private readonly HashSet<Location> m_companyChanged = [];
}
