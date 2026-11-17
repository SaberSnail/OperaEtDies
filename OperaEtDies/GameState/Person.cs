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
		m_levels = new Dictionary<string, double>(StringComparer.Ordinal);
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
	public bool IsInDayClothes => Clothing.Pieces.Any(piece => m_outfits.Any(x => x.Kind != OutfitKind.Night && x.Pieces.Contains(piece)));

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
	/// The need the person is trying to meet, or null if they're idle or have no goal.
	/// </summary>
	public Need? Goal => m_goal;

	/// <summary>
	/// How someone other than the player last chose their goal, for developers to see why, or null if they haven't chosen
	/// one since their plan was last set some other way.
	/// </summary>
	public GoalChoice? GoalChoice => m_goalChoice;

	/// <summary>
	/// A trip the person has been to the place for but hasn't finished at home yet, or null if there isn't one. Only the
	/// player does trips in parts, since they choose each part themselves.
	/// </summary>
	public Need? PendingTrip
	{
		get => m_pendingTrip;
		internal set => SetPropertyField(value, ref m_pendingTrip);
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
	/// The tasks left toward the person's goal. The first one is the current task, if <see cref="TaskEnd"/> is set.
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
	public double GetLevel(Need need) => m_levels.GetValueOrDefault(need.Id);

	/// <summary>
	/// Gets how many minutes the person has spent on a quota need this day or week (counting the minutes spent at the
	/// place for a quota of trips).
	/// </summary>
	public long GetProgress(Need need) => m_progress.GetValueOrDefault(need.Id);

	/// <summary>
	/// Gets how many more minutes the person plans to spend on a need: the time left in their current task and the tasks
	/// after it, if the need is their goal, and the time at home to finish a trip they've been to the place for.
	/// </summary>
	internal long GetPlannedMinutes(Need need, TimePoint time)
	{
		var minutes = 0L;
		if (m_pendingTrip == need)
			minutes += need.GetHomeMinutes(Family.Members.Count);
		if (m_goal == need)
		{
			for (var index = 0; index < m_tasks.Count; index++)
			{
				if (m_tasks[index] is BusyTask { Minutes: { } busy } task)
					minutes += index == 0 && m_taskEnd is { } end ? Math.Max(m_gameData.Calendar.GetTotalMinutes(end - time), 0) : task.ResumeMinutes ?? busy;
			}
		}

		return minutes;
	}

	/// <summary>
	/// The need being met right now: the goal, while the person is busy with it, or the need a talk meets. Looking for a
	/// friend to socialize with doesn't meet the socializing need by itself.
	/// </summary>
	internal Need? ActiveNeed => CurrentTask switch
	{
		BusyTask => m_goal?.Id == m_gameData.Schedules.SocializeNeed ? null : m_goal,
		TalkTask talk => talk.Need,
		_ => null,
	};

	public WorldObjectRecord CreateRecord() =>
		new PersonRecord(Id, IsPlayer, Role, m_location.Id, m_traits.Values.ToDictionary(x => x.Key, x => JsonSerializer.SerializeToElement(x.Value switch
		{
			CharacterPropertyOption option => option.Id,
			var value => value,
		}), StringComparer.Ordinal), m_goal?.Id, m_tasks.Select(x => x.CreateRecord()).ToList(), m_taskEnd?.SaveToString(),
			new Dictionary<string, double>(m_levels, StringComparer.Ordinal), new Dictionary<string, long>(m_progress, StringComparer.Ordinal), m_pendingTrip?.Id,
			m_goalChoice?.CreateRecord(), m_sightings.Count == 0 ? null : m_sightings.Select(x => x.Value.CreateRecord(x.Key)).ToList(), m_talkRequest?.Id, HasNapped,
			Inventory.CreateRecord(), Clothing.CreateRecord());

	internal void SetFamily(Family family) => m_family = family;

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
	public bool IsAsleep => CurrentTask is BusyTask && m_goal is { } goal && goal.Id == m_gameData.Schedules.SleepNeed;

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

	internal void SetLevel(Need need, double level) => m_levels[need.Id] = Math.Clamp(level, 0, Need.MaximumLevel);

	internal void SetProgress(Need need, long minutes) => m_progress[need.Id] = minutes;

	/// <summary>
	/// Gives the person a new goal and the tasks to reach it, replacing any they had, with how they chose it if they did.
	/// They aren't doing any of them yet.
	/// </summary>
	internal void SetPlan(Need? goal, IEnumerable<PersonTask> tasks, GoalChoice? choice = null)
	{
		m_goal = goal;
		m_goalChoice = choice;
		m_tasks.Clear();
		foreach (var task in tasks)
			m_tasks.Add(task);
		m_taskEnd = null;
		RaiseScheduleChanged();
	}

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
	/// and their goal stays the same.
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
		if (m_tasks.Count == 0)
			m_goal = null;
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

			var minutes = calendar.GetTotalMinutes(time - m_needsTime);
			var hours = minutes / 60.0;
			var active = ActiveNeed;
			foreach (var need in m_gameData.Schedules.Needs.Where(x => x.Kind == NeedKind.Level))
			{
				var level = GetLevel(need);
				if (need == active)
					SetLevel(need, level - (GetSatisfyPerHour(need) * hours));
				else if (active is null || need.PausedBy?.Contains(active.Id, StringComparer.Ordinal) != true)
					SetLevel(need, level + (need.PerHour!.Value * hours));
			}

			if (active is { Kind: NeedKind.Quota })
				m_progress[active.Id] = GetProgress(active) + minutes;

			m_needsTime = time;
			StartPeriods(time);
		}
	}

	/// <summary>
	/// Sets the person's schedule state when loading a save, at the time the save was made.
	/// </summary>
	internal void LoadSchedule(Need? goal, IReadOnlyList<PersonTask> tasks, TimePoint? taskEnd, GoalChoice? choice)
	{
		SetPlan(goal, tasks, choice);
		if (taskEnd is not null)
			StartTask(taskEnd);
	}

	/// <summary>
	/// Tells listeners that the person's goal or tasks changed, such as to show what they're doing.
	/// </summary>
	private void RaiseScheduleChanged() => RaisePropertyChanged(nameof(Goal), nameof(GoalChoice), nameof(Tasks), nameof(CurrentTask), nameof(TaskEnd));

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
	private readonly Dictionary<string, double> m_levels;
	private readonly Dictionary<string, long> m_progress;
	private CharacterTraits m_traits;
	private string? m_description;
	private Family? m_family;
	private readonly Dictionary<Person, Relationship> m_relationships = [];
	private readonly Dictionary<Person, Sighting> m_sightings = [];
	private Person? m_talkRequest;
	private Location m_location;
	private Need? m_goal;
	private GoalChoice? m_goalChoice;
	private Need? m_pendingTrip;
	private TimePoint? m_taskEnd;
	private TimePoint m_needsTime;
	private TimePoint m_progressDay;
	private TimePoint m_progressWeek;
}

/// <summary>
/// The saved form of a person: whether they're the player, their role in their family, where they are, their
/// character property values (with value list properties saved as the value's id), and their schedule: their goal, the
/// tasks left toward it, when the current task ends, their level needs, and the minutes spent on quota needs this day or week, and any trip
/// they still have to finish at home, and how they last chose their goal, and the items they carry and the clothing they wear. Their home and workplace come from their family and business.
/// </summary>
public sealed record PersonRecord(WorldId Id, bool IsPlayer, FamilyRole Role, WorldId Location, IReadOnlyDictionary<string, JsonElement> Traits,
	string? Goal, IReadOnlyList<PersonTaskRecord> Tasks, string? TaskEnd, IReadOnlyDictionary<string, double> Needs, IReadOnlyDictionary<string, long> Progress,
	string? PendingTrip = null, GoalChoiceRecord? GoalChoice = null, IReadOnlyList<SightingRecord>? Sightings = null, WorldId? TalkRequest = null, bool HasNapped = false,
	IReadOnlyList<WorldId>? Inventory = null, IReadOnlyList<WorldId>? Clothing = null)
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

		var goal = Goal is null ? null : GetNeed(gameData, Goal);
		// Tasks and goal choices can refer to other people, such as someone they're talking with, who may not exist yet.
		loader.Defer(() => person.LoadSchedule(goal, Tasks.Select(x => x.Create(loader)).ToList(), TaskEnd is null || Tasks.Count == 0 ? null : LoadTime(gameData, TaskEnd),
			GoalChoice is { } choiceRecord ? LoadGoalChoice(loader, choiceRecord) : null));
		if (PendingTrip is not null)
		{
			var trip = GetNeed(gameData, PendingTrip);
			if (!trip.IsTrips || !trip.ReturnHome)
				throw new SaveLoadException(OurResources.SaveNeedInvalid.FormatCurrentCulture(Id, PendingTrip));
			person.PendingTrip = trip;
		}

		// Sightings refer to other people, who may not exist yet.
		if (Sightings is { } sightings)
			loader.Defer(() => LoadSightings(loader, person, sightings));
		if (TalkRequest is { } talkRequest)
			loader.Defer(() => person.TalkRequest = loader.Get<Person>(talkRequest));

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
		var seeking = record.Seeking is { } seek ? new SeekChoice(loader.Get<Person>(seek.Friend), loader.Get<Location>(seek.Place), seek.Clue) : null;
		return new GoalChoice(LoadTime(gameData, record.Time), urgencies, chosen, record.Result, seeking);
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
				? new SightingPlan(planRecord.Goal is null ? null : GetNeed(loader.GameData, planRecord.Goal), planRecord.Destination is { } planDestination ? loader.Get<Location>(planDestination) : null)
				: null;
			person.LoadSighting(other, new Sighting(loader.Get<Location>(record.Location), loader.LoadTime(record.Time), record.Activity, need,
				record.Destination is { } destination ? loader.Get<Location>(destination) : null, record.Source is { } source ? loader.Get<Person>(source) : null, plan));
		}
	}

	private static Need GetNeed(GameData gameData, string id) =>
		gameData.Schedules.Needs.FirstOrDefault(x => x.Id == id) ?? throw new SaveLoadException(OurResources.SaveDataNotFound.FormatCurrentCulture("need", id));

	private static TimePoint LoadTime(GameData gameData, string text) => WorldLoader.LoadTime(gameData, text);
}
