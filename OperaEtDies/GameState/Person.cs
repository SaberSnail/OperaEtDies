using System.Collections.ObjectModel;
using System.Text.Json;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.Town;

namespace OperaEtDies.GameState;

/// <summary>
/// A person in the game world, including the player.
/// </summary>
public sealed class Person : NotifyPropertyChangedBase, IWorldObject
{
	/// <summary>
	/// Creates a person in a location at the given time, with no goal and every need at 0. The traits must include the at
	/// work property.
	/// </summary>
	internal Person(WorldId id, GameData gameData, CharacterTraits traits, FamilyRole role, bool isPlayer, Location location, TimePoint time)
	{
		var families = gameData.Families;
		var atWorkProperty = gameData.CharacterProperties.Get(families.Roles.Properties.AtWork);
		m_gameData = gameData;
		m_atWorkPropertyId = atWorkProperty.Id;
		m_atWork = atWorkProperty.Values!.First(x => x.Id == families.Roles.AtWork);
		m_notAtWork = atWorkProperty.Values!.First(x => x.Id == families.Roles.NotAtWork);

		Id = id;
		Role = role;
		IsPlayer = isPlayer;
		m_traits = traits;
		m_fullName = traits.GetFullName(gameData.CharacterTemplates);
		m_location = location;
		m_location.AddPerson(this);

		m_tasks = [];
		m_levels = new Dictionary<string, long>(StringComparer.Ordinal);
		m_progress = new Dictionary<string, long>(StringComparer.Ordinal);
		m_needsTime = time;
		m_progressDay = gameData.Calendar.GetStartOfDay(time);
		m_progressWeek = gameData.Calendar.GetStartOfWeek(time);
		Inventory = new ItemContainer();
		Clothing = new WornClothing(this);
		m_outfits = [];
	}

	public WorldId Id { get; }

	/// <summary>
	/// What the person is carrying.
	/// </summary>
	public ItemContainer Inventory { get; }

	/// <summary>
	/// What the person is wearing.
	/// </summary>
	public WornClothing Clothing { get; }

	/// <summary>
	/// The outfits the person owns, in the order they were created.
	/// </summary>
	public IReadOnlyList<OwnedOutfit> Outfits => m_outfits;

	/// <summary>
	/// Gets the outfits of one kind the person owns.
	/// </summary>
	public IEnumerable<OwnedOutfit> GetOutfits(OutfitKind kind) => m_outfits.Where(x => x.Kind == kind);

	internal void AddOutfit(OwnedOutfit outfit) => m_outfits.Add(outfit);

	/// <summary>
	/// Whether the person is wearing any of their everyday or work clothes.
	/// </summary>
	public bool IsInDayClothes => Clothing.Pieces.Any(piece => m_outfits.Any(x => x.Kind is not (OutfitKind.Night or OutfitKind.Nothing) && x.Pieces.Contains(piece)));

	/// <summary>
	/// Gets how much a level need drops each hour while the person is meeting it. Sleep meets the sleep need more slowly
	/// in day clothes.
	/// </summary>
	public double GetSatisfyPerHour(Need need) =>
		need.SatisfyPerHour!.Value * (need.Id == m_gameData.Schedules.SleepNeed && IsInDayClothes ? m_gameData.Schedules.Sleep.DayClothesFactor : 1);

	public bool IsPlayer { get; }

	/// <summary>
	/// How the person is related to the head of their family.
	/// </summary>
	public FamilyRole Role { get; }

	/// <summary>
	/// The values of the character properties defined in the data files. The game keeps the at work property up to date.
	/// </summary>
	public CharacterTraits Traits => m_traits;

	/// <summary>
	/// Gets the id a condition matches for the value of one of the person's character properties, such as the age group
	/// their age falls in, or null if they have no value for it.
	/// </summary>
	public string? GetConditionValueId(string propertyId) => m_gameData.CharacterProperties.Find(propertyId)?.GetConditionValueId(m_traits.Values.GetValueOrDefault(propertyId));

	public string FullName => m_fullName;

	/// <summary>
	/// Describes the person in a few sentences, with what can be seen through their clothing and what they're wearing.
	/// </summary>
	public string Description => m_description ??= m_traits.GetDescription(m_gameData.CharacterTemplates, m_gameData.GetObscuring(Clothing.Pieces.Select(x => x.Clothing)), Clothing.Describe(m_gameData.ClothingSlots, m_gameData.ClothingLayers, m_gameData.ClothingSettings.HidesClothingAt));

	/// <summary>
	/// Called by the worn clothing when it changes, since it changes the description.
	/// </summary>
	internal void OnClothingChanged()
	{
		using (ScopedPropertyChange(nameof(Description)))
			m_description = null;
	}

	/// <summary>
	/// The id of the person's profession, or null if they have none.
	/// </summary>
	public string? Profession => m_traits.GetValueId(m_gameData.Families.Roles.Properties.Profession);

	/// <summary>
	/// The family the person lives with.
	/// </summary>
	public Family Family => m_family ?? throw new InvalidOperationException($"Person {Id} isn't in a family.");

	/// <summary>
	/// The business the person owns or helps in, or null if they have none.
	/// </summary>
	public Business? Business { get; internal set; }

	public bool IsBusinessOwner => Business?.Owner == this;

	/// <summary>
	/// The house the person lives in.
	/// </summary>
	public Location Home => Family.Home;

	/// <summary>
	/// Where the person works: their business's place, or their home if they have no business or it's run from home.
	/// </summary>
	public Location Workplace => Business?.Location ?? Home;

	/// <summary>
	/// Whether the person is working right now, as given by the at work property in <see cref="Traits"/>. Tasks start and
	/// stop work.
	/// </summary>
	public bool IsAtWork
	{
		get => m_traits.GetValueId(m_atWorkPropertyId) == m_atWork.Id;
		set
		{
			if (value != IsAtWork)
			{
				using (ScopedPropertyChange(nameof(IsAtWork), nameof(Traits), nameof(Description), nameof(PortraitPlace)))
				{
					m_traits = m_traits.With(m_atWorkPropertyId, value ? m_atWork : m_notAtWork);
					m_description = null;
				}
			}
		}
	}

	/// <summary>
	/// Where the person is, for drawing their portrait: a business's place is the workplace of its profession, and a
	/// house is a home, unless it's the person's own home and they're working from it. Anywhere else is its kind of
	/// quarter.
	/// </summary>
	public PortraitPlace? PortraitPlace
	{
		get
		{
			var location = Location;
			return location.BusinessProfession is { } businessProfession ? Data.PortraitPlace.InWorkplace(businessProfession) :
				location.IsHouse ? (location == Workplace && IsAtWork && Profession is { } profession ? Data.PortraitPlace.InWorkplace(profession) : Data.PortraitPlace.Home) :
				location.QuarterKind is { } quarterKind ? Data.PortraitPlace.InQuarter(quarterKind) : null;
		}
	}

	/// <summary>
	/// The location the person is in. Changing it moves the person between the locations' lists of people.
	/// </summary>
	public Location Location
	{
		get => m_location;
		set
		{
			ArgumentNullException.ThrowIfNull(value);

			var oldLocation = m_location;
			if (SetPropertyField([nameof(Location), nameof(PortraitPlace)], value, ref m_location))
			{
				oldLocation.RemovePerson(this);
				value.AddPerson(this);
			}
		}
	}

	/// <summary>
	/// The sequences of steps the person is in the middle of, in the order they were last current: the last one that has
	/// set off is <see cref="Current"/>, and the others are on hold, or for appointments they haven't set off for yet,
	/// waiting. See Docs/Steps.md.
	/// </summary>
	public IReadOnlyList<StepSequence> Sequences => m_sequences;

	/// <summary>
	/// The sequence of steps the person is carrying out, or null if they aren't carrying out any.
	/// </summary>
	public StepSequence? Current => m_sequences.LastOrDefault(x => x.HasSetOff);

	/// <summary>
	/// The need the person is trying to meet: that of the steps they're carrying out, or null if they're idle, have no goal,
	/// or are taking part in someone else's steps.
	/// </summary>
	public Need? Goal => Current is { Role: SequenceRole.Steps } current ? current.Need : null;

	/// <summary>
	/// How someone other than the player last chose their goal, for developers to see why, or null if they haven't chosen
	/// one since their plan was last set some other way.
	/// </summary>
	public GoalChoice? GoalChoice => m_goalChoice;

	/// <summary>
	/// Who the steps the person is carrying out last talked with or asked, or null if there's no one.
	/// </summary>
	public Person? Partner => Current?.Partner;

	/// <summary>
	/// Who the steps the person is carrying out are looking for, and where, or null if they aren't looking for anyone.
	/// </summary>
	public SeekChoice? Seeking => Current?.Seeking;

	/// <summary>
	/// Gets the sequence of a need's steps the person is in the middle of (rather than taking part in someone else's), or
	/// null if there's none.
	/// </summary>
	public StepSequence? GetSequence(Need need) => m_sequences.LastOrDefault(x => x.Need == need && x.Role == SequenceRole.Steps && x.HasSetOff);

	/// <summary>
	/// Starts a sequence of steps, which becomes the current one. Any other is put on hold.
	/// </summary>
	internal void AddSequence(StepSequence sequence)
	{
		m_sequences.Add(sequence);
		RaiseScheduleChanged();
	}

	/// <summary>
	/// Makes a sequence the person is in the middle of the current one, putting the one that was on hold.
	/// </summary>
	internal void MakeCurrent(StepSequence sequence)
	{
		if (Current != sequence && m_sequences.Remove(sequence))
		{
			m_sequences.Add(sequence);
			RaiseScheduleChanged();
		}
	}

	/// <summary>
	/// Has the person set off for the appointment a sequence of theirs is for: it becomes their current one, and any other
	/// is put on hold.
	/// </summary>
	internal void SetOff(StepSequence sequence)
	{
		if (m_sequences.Remove(sequence))
		{
			sequence.SetOff = SetOffState.Gone;
			m_sequences.Add(sequence);
			RaiseScheduleChanged();
		}
	}

	/// <summary>
	/// Marks the player's sequence for an appointment as due: it's time for them to set off, and they're asked whether to.
	/// </summary>
	internal void AskToSetOff(StepSequence sequence)
	{
		if (m_sequences.Contains(sequence))
		{
			sequence.SetOff = SetOffState.Due;
			RaiseScheduleChanged();
		}
	}

	/// <summary>
	/// Takes a sequence away, once it's done or given up. The one put on hold most recently becomes current.
	/// </summary>
	internal void RemoveSequence(StepSequence sequence)
	{
		if (m_sequences.Remove(sequence))
			RaiseScheduleChanged();
	}

	/// <summary>
	/// For each outfit the person changed into that replaced clothes they were wearing, the pieces it replaced, which they
	/// change back into when they change out of it. Kept until then, however long that is.
	/// </summary>
	public IReadOnlyDictionary<OwnedOutfit, IReadOnlyList<ClothingPiece>> ReplacedClothes => m_replacedClothes;

	/// <summary>
	/// Remembers the pieces an outfit replaced when the person changed into it, or forgets them if it replaced none.
	/// </summary>
	internal void SetReplacedClothes(OwnedOutfit outfit, IReadOnlyList<ClothingPiece> pieces)
	{
		if (pieces.Count == 0)
			m_replacedClothes.Remove(outfit);
		else
			m_replacedClothes[outfit] = pieces;
	}

	/// <summary>
	/// Forgets the pieces an outfit replaced, returning them.
	/// </summary>
	internal IReadOnlyList<ClothingPiece> RemoveReplacedClothes(OwnedOutfit outfit) => m_replacedClothes.Remove(outfit, out var pieces) ? pieces : [];

	/// <summary>
	/// Whether an item is in one of the bundles the person carries.
	/// </summary>
	public bool IsInBundle(Item item) => Inventory.Items.OfType<Bundle>().Any(x => x.Items.Contains(item));

	/// <summary>
	/// Adds tasks after the person's other tasks, such as the tasks for one of the goal's steps.
	/// </summary>
	internal void AddTasks(IEnumerable<PersonTask> tasks)
	{
		foreach (var task in tasks)
			m_tasks.Add(task);
		RaiseScheduleChanged();
	}

	/// <summary>
	/// Replaces the person's tasks, such as the walk and the wait of being idle, with how they chose what to do if they
	/// did. They aren't doing any of them yet.
	/// </summary>
	internal void SetTasks(IEnumerable<PersonTask> tasks, GoalChoice? choice = null)
	{
		m_goalChoice = choice;
		m_tasks.Clear();
		foreach (var task in tasks)
			m_tasks.Add(task);
		m_taskEnd = null;
		RaiseScheduleChanged();
	}

	/// <summary>
	/// Drops every task but the one the person is doing, which they finish before going on, such as when someone they were
	/// following lets them go while they're changing clothes.
	/// </summary>
	internal void DropTasksAfterCurrent()
	{
		var end = m_taskEnd;
		SetTasks(m_tasks.Take(1).ToList(), m_goalChoice);
		m_taskEnd = m_tasks.Count == 0 ? null : end;
	}

	/// <summary>
	/// Whether the person has napped since they last slept until rested. Someone only naps once between full sleeps.
	/// </summary>
	public bool HasNapped { get; internal set; }

	/// <summary>
	/// Someone who asked the player to chat and is waiting for an answer, or null if no one is. Only the player is asked.
	/// </summary>
	public Person? TalkRequest
	{
		get => m_talkRequest;
		internal set => SetPropertyField(value, ref m_talkRequest);
	}

	/// <summary>
	/// Someone who asked the player the question of an "ask" step they're on, and is waiting for an answer, or null if no one
	/// is. Only the player is asked.
	/// </summary>
	public Person? AskRequest
	{
		get => m_askRequest;
		internal set => SetPropertyField(value, ref m_askRequest);
	}

	/// <summary>
	/// The tasks the person has left to do: those of the step they're carrying out, or of being idle. The first one is the
	/// current task, if <see cref="TaskEnd"/> is set.
	/// </summary>
	public IReadOnlyList<PersonTask> Tasks => m_tasks;

	/// <summary>
	/// The task the person is doing right now, or null if they aren't doing anything.
	/// </summary>
	public PersonTask? CurrentTask => m_taskEnd is null ? null : m_tasks[0];

	/// <summary>
	/// When the current task ends, or null if the person isn't doing anything. The player has no task while they're
	/// deciding what to do.
	/// </summary>
	public TimePoint? TaskEnd => m_taskEnd;

	/// <summary>
	/// Gets how high a level need is, from 0 to <see cref="Need.MaximumLevel"/>, as of the last time the person's needs
	/// were brought up to date.
	/// </summary>
	public double GetLevel(Need need) => m_levels.GetValueOrDefault(need.Id) / (double) LevelUnits;

	/// <summary>
	/// Gets how much the person has done of a quota need this day or week: the minutes spent on it, or for a quota measured
	/// in runs, the runs finished.
	/// </summary>
	public long GetProgress(Need need) => m_progress.GetValueOrDefault(need.Id);

	/// <summary>
	/// Gets how many minutes the person has spent on a quota need this day or week, counting each run finished of a quota
	/// measured in runs by the minutes its "do" steps take.
	/// </summary>
	public long GetProgressMinutes(Need need) => need.IsRuns ? GetProgress(need) * need.GetRunMinutes(Family.Members.Count) : GetProgress(need);

	/// <summary>
	/// Gets how many more minutes the person plans to spend on a need, if they're in the middle of its steps. For a quota
	/// measured in time, it's the time left in their current task and the tasks after it, if it's their goal, and what's
	/// left of the time chosen for it while steps that could use it are left (a "do" step that meets it, or a "repeat"
	/// step). For a quota measured in runs, it's a whole run, until the run is counted.
	/// </summary>
	internal long GetPlannedMinutes(Need need, TimePoint time)
	{
		var minutes = 0L;
		if (GetSequence(need) is { } sequence)
		{
			if (need.IsRuns)
			{
				if (need.Steps.Skip(sequence.Step).Any(x => x.Kind == StepKind.CountRun))
					minutes = need.GetRunMinutes(Family.Members.Count);
			}
			else
			{
				if (sequence == Current)
				{
					for (var index = 0; index < m_tasks.Count; index++)
					{
						if (m_tasks[index] is BusyTask { Minutes: { } busy } task && ActiveNeedOf(task) == need)
							minutes += index == 0 && m_taskEnd is { } end ? Math.Max(m_gameData.Calendar.GetTotalMinutes(end - time), 0) : task.ResumeMinutes ?? busy;
					}
				}

				if (need.Steps.Skip(sequence.Step).Any(x => x is { Kind: StepKind.Do, MeetsQuota: true } or { Kind: StepKind.Repeat }))
					minutes += sequence.MinutesLeft ?? 0;
			}
		}

		return minutes;
	}

	/// <summary>
	/// The need being met right now: the goal, while the person is busy with a step that meets it (any step, for a level
	/// need), the need a talk meets, the need met for them by the one they follow, or for the worker waiting at an
	/// appointment whose "meet" step meets the quota, its need. Looking for a friend to socialize with doesn't meet the
	/// socializing need by itself.
	/// </summary>
	internal Need? ActiveNeed => m_taskEnd is null ? null : ActiveNeedOf(m_tasks[0]);

	private Need? ActiveNeedOf(PersonTask task)
	{
		var goal = Goal;
		return task switch
		{
			BusyTask { Step: { } waitStep } when goal is not null && waitStep < goal.Steps.Count && goal.Steps[waitStep] is { Kind: StepKind.Wait } or { Kind: StepKind.Seek, MeetsQuota: false } => null,
			BusyTask { IsAside: true } => null,
			BusyTask busy => goal is null ? null :
				goal.Kind == NeedKind.Level || (busy.Step is { } step ? step < goal.Steps.Count && goal.Steps[step].MeetsQuota : !goal.IsRuns) ? goal : null,
			TalkTask talk => talk.Need,
			FollowTask follow => GetNeedMetFollowing(follow.Leader),
			MeetTask { Appointment: { } appointment } when appointment.Worker == this && appointment.Need.GetAppointmentSteps(appointment.Step) is { } steps &&
				appointment.Need.Steps[steps.Meet].MeetsQuota && appointment.Need.Quota?.Time is not null => appointment.Need,
			_ => null,
		};
	}

	/// <summary>
	/// Gets the need of the person's met by following someone, while that person is spending time on a "do" step that meets
	/// a need for their partner (see <see cref="Step.PartnerMeets"/>), if the person is their partner and has it.
	/// </summary>
	private Need? GetNeedMetFollowing(Person leader) =>
		leader.CurrentTask is BusyTask { Step: { } step, IsAside: false } && leader.Current is { Partner: { } partner } current && partner == this &&
		step < current.Need.Steps.Count && current.Need.Steps[step] is { Kind: StepKind.Do, PartnerMeets: { } id }
			? m_gameData.Schedules.Needs.FirstOrDefault(x => x.Id == id && Scheduler.HasNeed(this, x))
			: null;

	public WorldObjectRecord CreateRecord() =>
		new PersonRecord(Id, IsPlayer, Role, m_location.Id, m_traits.Values.ToDictionary(x => x.Key, x => JsonSerializer.SerializeToElement(x.Value switch
		{
			CharacterPropertyOption option => option.Id,
			var value => value,
		}), StringComparer.Ordinal), m_tasks.Select(x => x.CreateRecord()).ToList(), m_taskEnd?.SaveToString(),
			m_levels.ToDictionary(x => x.Key, x => x.Value / (double) LevelUnits, StringComparer.Ordinal), new Dictionary<string, long>(m_progress, StringComparer.Ordinal),
			m_goalChoice?.CreateRecord(), m_sightings.Count == 0 ? null : m_sightings.Select(x => x.Value.CreateRecord(x.Key)).ToList(), m_talkRequest?.Id, HasNapped,
			Inventory.CreateRecord(), Clothing.CreateRecord(), m_sequences.Count == 0 ? null : m_sequences.Select(x => x.CreateRecord()).ToList(),
			m_replacedClothes.Count == 0 ? null : m_replacedClothes.Select(x => new ReplacedClothesRecord(x.Key.Id, x.Value.Select(y => y.Id).ToList())).ToList(),
			m_askRequest?.Id);

	internal void SetFamily(Family family) => m_family = family;

	/// <summary>
	/// The jobs the person has taken on and not yet finished, one for each of their needs at most.
	/// </summary>
	public IReadOnlyList<Job> Jobs => m_jobs;

	/// <summary>
	/// Gets the person's job for a need, or null if they have none.
	/// </summary>
	public Job? GetJob(Need need) => m_jobs.FirstOrDefault(x => x.Need == need);

	internal void AddJob(Job job) => m_jobs.Add(job);

	internal void RemoveJob(Job job) => m_jobs.Remove(job);

	/// <summary>
	/// The person's relationships with everyone else, by the other person.
	/// </summary>
	public IReadOnlyDictionary<Person, Relationship> Relationships => m_relationships;

	/// <summary>
	/// Gets the person's relationship with someone else.
	/// </summary>
	public Relationship GetRelationship(Person other) => m_relationships[other];

	internal void AddRelationship(Relationship relationship) => m_relationships.Add(relationship.GetOther(this), relationship);

	/// <summary>
	/// The last time the person saw each other person, or heard about them from someone else, in the last
	/// <see cref="ScheduleSettings.SightingHours"/> hours, by the person seen.
	/// </summary>
	public IReadOnlyDictionary<Person, Sighting> Sightings => m_sightings;

	/// <summary>
	/// Whether the person is asleep: spending time on the need people meet while asleep.
	/// </summary>
	public bool IsAsleep => CurrentTask is BusyTask { IsAside: false } && Goal is { } goal && goal.Id == m_gameData.Schedules.SleepNeed;

	/// <summary>
	/// Remembers seeing someone, replacing any earlier sighting of them. What they said they were doing is kept while
	/// their goal stays the same.
	/// </summary>
	internal void See(Person other, Sighting sighting)
	{
		if (other != this)
		{
			if (sighting.Plan is null && m_sightings.TryGetValue(other, out var known) && known is { Source: null, Plan: { } plan } && plan.Goal == other.Goal)
				sighting = sighting with { Plan = plan };
			m_sightings[other] = sighting;
		}
	}

	/// <summary>
	/// Sets a sighting as it was saved.
	/// </summary>
	internal void LoadSighting(Person other, Sighting sighting) => m_sightings[other] = sighting;

	/// <summary>
	/// Learns about a sighting of someone from someone else, and remembers it as secondhand if it's more recent than what
	/// the person already knows. Returns whether it was remembered.
	/// </summary>
	public bool LearnSighting(Person other, Sighting sighting, Person source)
	{
		var isNewer = other != this && other != source && (!m_sightings.TryGetValue(other, out var known) || known.Time < sighting.Time);
		if (isNewer)
			m_sightings[other] = sighting with { Source = source };
		return isNewer;
	}

	/// <summary>
	/// Forgets the sightings from before the given time.
	/// </summary>
	internal void ForgetSightingsBefore(TimePoint time)
	{
		foreach (var other in m_sightings.Where(x => x.Value.Time < time).Select(x => x.Key).ToList())
			m_sightings.Remove(other);
	}

	internal void SetLevel(Need need, double level) => m_levels[need.Id] = (long) Math.Round(Math.Clamp(level, 0, Need.MaximumLevel) * LevelUnits);

	internal void SetProgress(Need need, long minutes) => m_progress[need.Id] = minutes;

	/// <summary>
	/// Starts the first task, which ends at the given time.
	/// </summary>
	internal void StartTask(TimePoint end)
	{
		if (m_tasks.Count == 0)
			throw new InvalidOperationException($"Person {Id} has no task to start.");
		m_taskEnd = end;
		RaiseScheduleChanged();
	}

	/// <summary>
	/// Interrupts what the person is doing at the given time with another task, which ends at the given time. Their needs
	/// are brought up to date first. The task they were doing is kept after the interruption, with the minutes it had left,
	/// and their steps stay the same.
	/// </summary>
	internal void Interrupt(PersonTask interruption, TimePoint time, TimePoint end)
	{
		UpdateNeeds(time);
		if (m_taskEnd is { } taskEnd)
			m_tasks[0] = m_tasks[0] with { ResumeMinutes = (int) Math.Max(m_gameData.Calendar.GetTotalMinutes(taskEnd - time), 0) };
		m_tasks.Insert(0, interruption);
		m_taskEnd = end;
		RaiseScheduleChanged();
	}

	/// <summary>
	/// Removes the first task, which has finished, and returns it.
	/// </summary>
	internal PersonTask FinishTask()
	{
		var task = m_tasks[0];
		m_tasks.RemoveAt(0);
		m_taskEnd = null;
		RaiseScheduleChanged();
		return task;
	}

	/// <summary>
	/// Brings the person's needs up to the given time. Level needs grow, unless they're being met (when they drop) or
	/// paused by the need being met. Time spent meeting a quota need counts toward it. Quotas start again each day or
	/// week.
	/// </summary>
	internal void UpdateNeeds(TimePoint time)
	{
		if (time > m_needsTime)
		{
			var calendar = m_gameData.Calendar;
			StartPeriods(m_needsTime);

			// Levels change by whole units each minute, so they come out the same however often they're brought up to date.
			var minutes = calendar.GetTotalMinutes(time - m_needsTime);
			var active = ActiveNeed;
			foreach (var need in m_gameData.Schedules.Needs.Where(x => x.Kind == NeedKind.Level))
			{
				var units = m_levels.GetValueOrDefault(need.Id);
				if (need == active)
					m_levels[need.Id] = Math.Clamp(units - (GetUnitsPerMinute(GetSatisfyPerHour(need)) * minutes), 0, MaximumLevelUnits);
				else if (active is null || need.PausedBy?.Contains(active.Id, StringComparer.Ordinal) != true)
					m_levels[need.Id] = Math.Clamp(units + (GetUnitsPerMinute(need.PerHour!.Value) * minutes), 0, MaximumLevelUnits);
			}

			if (active is { Kind: NeedKind.Quota })
			{
				m_progress[active.Id] = GetProgress(active) + minutes;

				// Time spent at a job's site on its need counts toward the job.
				if (GetJob(active) is { IsDone: false } job && job.Place == m_location)
					job.MinutesDone += minutes;
			}

			m_needsTime = time;
			StartPeriods(time);
		}
	}

	/// <summary>
	/// Sets the person's schedule state when loading a save, at the time the save was made.
	/// </summary>
	internal void LoadSchedule(IReadOnlyList<StepSequence> sequences, IReadOnlyList<PersonTask> tasks, TimePoint? taskEnd, GoalChoice? choice)
	{
		m_sequences.Clear();
		foreach (var sequence in sequences)
			m_sequences.Add(sequence);
		SetTasks(tasks, choice);
		if (taskEnd is not null)
			StartTask(taskEnd);
	}

	/// <summary>
	/// Tells listeners that the person's goal or tasks changed, such as to show what they're doing.
	/// </summary>
	private void RaiseScheduleChanged() => RaisePropertyChanged(nameof(Goal), nameof(Current), nameof(Sequences), nameof(GoalChoice), nameof(Tasks), nameof(CurrentTask), nameof(TaskEnd));

	/// <summary>
	/// Starts the quotas again for the time's day and week, if they've changed.
	/// </summary>
	private void StartPeriods(TimePoint time)
	{
		var calendar = m_gameData.Calendar;
		var day = calendar.GetStartOfDay(time);
		var week = calendar.GetStartOfWeek(time);
		var isNewDay = day != m_progressDay;
		var isNewWeek = week != m_progressWeek;
		foreach (var need in m_gameData.Schedules.Needs.Where(x => x.Period == NeedPeriod.Day ? isNewDay : isNewWeek))
			m_progress.Remove(need.Id);
		m_progressDay = day;
		m_progressWeek = week;
	}

	private readonly GameData m_gameData;
	private readonly string m_atWorkPropertyId;
	private readonly CharacterPropertyOption m_atWork;
	private readonly CharacterPropertyOption m_notAtWork;
	private readonly string m_fullName;
	private readonly Collection<PersonTask> m_tasks;
	private readonly Collection<OwnedOutfit> m_outfits;
	/// <summary>
	/// Gets how many units a level changes by each minute, for a change of so much each hour.
	/// </summary>
	private static long GetUnitsPerMinute(double perHour) => (long) Math.Round(perHour * LevelUnits / 60);

	/// <summary>
	/// How many units each point of a level is kept in. Levels are kept in whole units, so they don't gather rounding errors
	/// as they change, and an hourly rate of up to six decimal places changes them by a whole number of units each minute.
	/// </summary>
	private const long LevelUnits = 60_000_000;

	private const long MaximumLevelUnits = (long) Need.MaximumLevel * LevelUnits;

	// Each level need's level, in units of LevelUnits.
	private readonly Dictionary<string, long> m_levels;
	private readonly Dictionary<string, long> m_progress;
	private CharacterTraits m_traits;
	private string? m_description;
	private Family? m_family;
	private readonly Dictionary<Person, Relationship> m_relationships = [];
	private readonly Dictionary<Person, Sighting> m_sightings = [];
	private Person? m_talkRequest;
	private Location m_location;
	private GoalChoice? m_goalChoice;
	private Person? m_askRequest;
	private readonly Collection<StepSequence> m_sequences = [];
	private readonly Collection<Job> m_jobs = [];
	private readonly Dictionary<OwnedOutfit, IReadOnlyList<ClothingPiece>> m_replacedClothes = [];
	private TimePoint? m_taskEnd;
	private TimePoint m_needsTime;
	private TimePoint m_progressDay;
	private TimePoint m_progressWeek;
}

/// <summary>
/// The saved form of a person: whether they're the player, their role in their family, where they are, their
/// character property values (with value list properties saved as the value's id), and their schedule: the tasks they
/// have left, when the current task ends, their level needs, what they've done of quota needs this day or week (minutes,
/// or runs for a quota measured in runs), how they last chose their goal, the items they carry and the clothing they
/// wear, and the sequences of steps they're in the middle of, the current one last of those that have set off. For the player, it also has someone who
/// asked them something, while they're waiting for an answer. Their home and workplace come from their
/// family and business, and their goal from their current sequence.
/// </summary>
public sealed record PersonRecord(WorldId Id, bool IsPlayer, FamilyRole Role, WorldId Location, IReadOnlyDictionary<string, JsonElement> Traits,
	IReadOnlyList<PersonTaskRecord> Tasks, string? TaskEnd, IReadOnlyDictionary<string, double> Needs, IReadOnlyDictionary<string, long> Progress,
	GoalChoiceRecord? GoalChoice = null, IReadOnlyList<SightingRecord>? Sightings = null, WorldId? TalkRequest = null, bool HasNapped = false,
	IReadOnlyList<WorldId>? Inventory = null, IReadOnlyList<WorldId>? Clothing = null, IReadOnlyList<StepSequenceRecord>? Sequences = null,
	IReadOnlyList<ReplacedClothesRecord>? ReplacedClothes = null, WorldId? AskRequest = null)
	: WorldObjectRecord(Id)
{
	internal override IWorldObject Create(WorldLoader loader)
	{
		var gameData = loader.GameData;
		var values = new Dictionary<string, object>(StringComparer.Ordinal);
		foreach (var (propertyId, element) in Traits)
		{
			var property = WorldLoader.GetData(gameData.CharacterProperties, propertyId, "character property");
			object? value = (property.Type, element.ValueKind) switch
			{
				(CharacterPropertyType.String, JsonValueKind.String) => element.GetString(),
				(CharacterPropertyType.Integer, JsonValueKind.Number) when element.TryGetInt32(out var number) => number,
				(CharacterPropertyType.ValueList, JsonValueKind.String) => property.Values?.FirstOrDefault(x => x.Id == element.GetString()),
				_ => null,
			};
			values.Add(propertyId, value ?? throw new SaveLoadException(OurResources.SaveTraitValueInvalid.FormatCurrentCulture(Id, element.GetRawText(), propertyId)));
		}

		var traits = new CharacterTraits(values);
		var atWorkProperty = gameData.Families.Roles.Properties.AtWork;
		if (!traits.HasValue(atWorkProperty))
			throw new SaveLoadException(OurResources.SaveTraitValueInvalid.FormatCurrentCulture(Id, "", atWorkProperty));

		var person = new Person(Id, gameData, traits, Role, IsPlayer, loader.Get<Location>(Location), loader.Time);
		person.HasNapped = HasNapped;
		foreach (var (needId, level) in Needs)
		{
			var need = GetNeed(gameData, needId);
			if (need.Kind != NeedKind.Level || !(level >= 0 && level <= Need.MaximumLevel))
				throw new SaveLoadException(OurResources.SaveNeedInvalid.FormatCurrentCulture(Id, needId));
			person.SetLevel(need, level);
		}

		foreach (var (needId, minutes) in Progress)
		{
			var need = GetNeed(gameData, needId);
			if (need.Kind != NeedKind.Quota || minutes < 0)
				throw new SaveLoadException(OurResources.SaveNeedInvalid.FormatCurrentCulture(Id, needId));
			person.SetProgress(need, minutes);
		}

		// Tasks and goal choices can refer to other people, such as someone they're talking with, who may not exist yet.
		var sequences = new List<StepSequence>();
		foreach (var sequenceRecord in Sequences ?? [])
		{
			if (sequenceRecord.Step < 0 || sequenceRecord.MinutesLeft is < 0)
				throw new SaveLoadException(OurResources.SaveStepsInvalid.FormatCurrentCulture(Id, sequenceRecord.Need));
			sequences.Add(sequenceRecord.Load(loader, x => GetNeed(gameData, x)));
		}

		loader.Defer(() => person.LoadSchedule(sequences, Tasks.Select(x => x.Create(loader)).ToList(), TaskEnd is null || Tasks.Count == 0 ? null : LoadTime(gameData, TaskEnd),
			GoalChoice is { } choiceRecord ? LoadGoalChoice(loader, choiceRecord) : null));
		if (ReplacedClothes is { } replaced)
		{
			loader.Defer(() =>
			{
				foreach (var record in replaced)
					person.SetReplacedClothes(loader.Get<OwnedOutfit>(record.Outfit), record.Pieces.Select(loader.Get<ClothingPiece>).ToList());
			});
		}

		// Sightings refer to other people, who may not exist yet.
		if (Sightings is { } sightings)
			loader.Defer(() => LoadSightings(loader, person, sightings));
		if (TalkRequest is { } talkRequest)
			loader.Defer(() => person.TalkRequest = loader.Get<Person>(talkRequest));
		if (AskRequest is { } asker)
			loader.Defer(() => person.AskRequest = loader.Get<Person>(asker));

		// Items refer to their owners, who may not exist yet.
		if (Inventory is { } inventory)
			loader.Defer(() => person.Inventory.Load(inventory.Select(loader.Get<Item>)));
		if (Clothing is { } clothing)
			loader.Defer(() => person.Clothing.Load(clothing.Select(loader.Get<ClothingPiece>)));

		return person;
	}

	private GoalChoice LoadGoalChoice(WorldLoader loader, GoalChoiceRecord record)
	{
		var gameData = loader.GameData;
		var urgencies = new List<NeedUrgency>();
		foreach (var urgency in record.Urgencies)
		{
			var need = GetNeed(gameData, urgency.Need);
			if (!(urgency.Urgency >= 0 && double.IsFinite(urgency.Urgency)))
				throw new SaveLoadException(OurResources.SaveNeedInvalid.FormatCurrentCulture(Id, urgency.Need));
			urgencies.Add(new NeedUrgency(need, urgency.Urgency));
		}

		var chosen = record.Need is null ? null : GetNeed(gameData, record.Need);
		return new GoalChoice(LoadTime(gameData, record.Time), urgencies, chosen, record.Result);
	}

	private void LoadSightings(WorldLoader loader, Person person, IReadOnlyList<SightingRecord> sightings)
	{
		foreach (var record in sightings)
		{
			var other = loader.Get<Person>(record.Person);
			var need = record.Need is null ? null : GetNeed(loader.GameData, record.Need);
			var hasDestination = record.Activity is SightingActivity.Walking or SightingActivity.Left;
			if (other == person || person.Sightings.ContainsKey(other) || !Enum.IsDefined(record.Activity) || (need is not null) != (record.Activity == SightingActivity.Busy) || (record.Destination is not null) != hasDestination)
				throw new SaveLoadException(OurResources.SaveSightingInvalid.FormatCurrentCulture(Id, record.Person));

			var plan = record.Plan is { } planRecord
				? new SightingPlan(planRecord.Goal is null ? null : GetNeed(loader.GameData, planRecord.Goal), planRecord.Destination is { } planDestination ? loader.Get<Location>(planDestination) : null,
					planRecord.Doing)
				: null;
			person.LoadSighting(other, new Sighting(loader.Get<Location>(record.Location), loader.LoadTime(record.Time), record.Activity, need,
				record.Destination is { } destination ? loader.Get<Location>(destination) : null, record.Source is { } source ? loader.Get<Person>(source) : null, plan, record.Doing));
		}
	}

	private static Need GetNeed(GameData gameData, string id) =>
		gameData.Schedules.Needs.FirstOrDefault(x => x.Id == id) ?? throw new SaveLoadException(OurResources.SaveDataNotFound.FormatCurrentCulture("need", id));

	private static TimePoint LoadTime(GameData gameData, string text) => WorldLoader.LoadTime(gameData, text);
}

/// <summary>
/// The saved form of the pieces an outfit replaced when someone changed into it, by id.
/// </summary>
public sealed record ReplacedClothesRecord(WorldId Outfit, IReadOnlyList<WorldId> Pieces);
