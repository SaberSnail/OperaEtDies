using System.IO;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class SaveFileTests
{
	[Test]
	public void LoadedGameMatchesTheSavedGame()
	{
		for (var seed = 0; seed < 10; seed++)
		{
			var (_, world) = GameWorldTests.CreateWorld(seed, withFeatures: true);
			MoveSomePeople(world);

			var json = CreateSave(world).Serialize();
			var loaded = SaveFile.Deserialize(json).Load(world.GameData, GameTitle, new Random(seed));

			AssertSameWorld(loaded, world);
			Assert.That(CreateSave(loaded).Serialize(), Is.EqualTo(json), $"Seed {seed}");
		}
	}

	[Test]
	public void SchedulesSaveAndLoad()
	{
		for (var seed = 0; seed < 10; seed++)
		{
			var (_, world) = GameWorldTests.CreateWorld(seed, withSchedules: true);
			world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, seed + 3, seed * 7));

			var json = CreateSave(world).Serialize();
			var loaded = SaveFile.Deserialize(json).Load(world.GameData, GameTitle, new Random(seed));

			AssertSameWorld(loaded, world);
			Assert.That(CreateSave(loaded).Serialize(), Is.EqualTo(json), $"Seed {seed}");
			Assert.That(loaded.Npcs.Where(x => x.Goal is not null), Is.Not.Empty);
			Assert.That(loaded.Npcs.Where(x => x.GoalChoice is { Urgencies.Count: > 0 }), Is.Not.Empty);
			Assert.That(loaded.People.Where(x => x.Sightings.Count > 0), Is.Not.Empty);
		}
	}

	[Test]
	public void TalkingSavesAndLoads()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 3, 0));
		var (first, second) = (world.Npcs.First(x => x.CurrentTask is not null), world.Npcs.Last(x => x.CurrentTask is not null));
		first.GetRelationship(second).Score = 100;
		world.StartTalking(first, second, 15, world.Time, isSocializing: true);
		first.See(second, new Sighting(second.Location, world.Time, SightingActivity.Talking, Plan: new SightingPlan(second.Goal, world.Locations[0])));

		var json = CreateSave(world).Serialize();
		var loaded = SaveFile.Deserialize(json).Load(world.GameData, GameTitle, new Random(0));

		AssertSameWorld(loaded, world);
		Assert.That(CreateSave(loaded).Serialize(), Is.EqualTo(json));
		var talk = (TalkTask) loaded.Objects.Get<Person>(first.Id).CurrentTask!;
		Assert.That((talk.With.Id, talk.Minutes), Is.EqualTo((second.Id, 15)));
		Assert.That(loaded.Objects.Get<Person>(first.Id).Tasks[1].ResumeMinutes, Is.Not.Null);
		Assert.That(talk.Need?.Id, Is.EqualTo(world.GameData.Schedules.SocializeNeed));
		Assert.That(loaded.Objects.Get<Person>(first.Id).Sightings.Values.Count(x => x.Plan is not null), Is.EqualTo(1));
	}

	[Test]
	public void JobsAndWhatPeopleWereDoingSaveAndLoad()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var (worker, client) = (world.Npcs[0], world.Npcs[^1]);
		var work = world.GameData.Schedules.Needs.First(x => x.Id == "work");
		var job = world.AddJob(worker, client, work, client.Home, 600);
		job.MinutesDone = 120;
		worker.See(client, new Sighting(client.Location, world.Time, SightingActivity.Busy, work, Plan: new SightingPlan(work, null, "Mending"), Doing: "Hammering"));

		var json = CreateSave(world).Serialize();
		var loaded = SaveFile.Deserialize(json).Load(world.GameData, GameTitle, new Random(0));

		AssertSameWorld(loaded, world);
		Assert.That(CreateSave(loaded).Serialize(), Is.EqualTo(json));
		var loadedJob = loaded.Jobs.Single();
		Assert.That((loadedJob.Worker.Id, loadedJob.Client.Id, loadedJob.Need, loadedJob.Place.Id, loadedJob.Minutes, loadedJob.MinutesDone), Is.EqualTo((worker.Id, client.Id, work, client.Home.Id, 600, 120L)));
		Assert.That(loaded.Objects.Get<Person>(worker.Id).GetJob(work), Is.SameAs(loadedJob));
		var sighting = loaded.Objects.Get<Person>(worker.Id).Sightings[loaded.Objects.Get<Person>(client.Id)];
		Assert.That((sighting.Doing, sighting.Plan?.Doing), Is.EqualTo(("Hammering", "Mending")));
	}

	[Test]
	public void LoadFailsForInvalidJobs()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var (worker, client) = (world.Npcs[0], world.Npcs[^1]);
		world.AddJob(worker, client, world.GameData.Schedules.Needs.First(x => x.Id == "work"), client.Home, 600);
		var save = CreateSave(world);
		var job = save.Objects.OfType<JobRecord>().Single();
		var index = save.Objects.ToList().IndexOf(job);

		SaveFile With(JobRecord record) => save with { Objects = [.. save.Objects.Take(index), record, .. save.Objects.Skip(index + 1)] };
		AssertLoadFails(With(job with { Need = "flying" }), world.GameData, "flying");
		AssertLoadFails(With(job with { Client = job.Worker }), world.GameData, "job");
		AssertLoadFails(With(job with { Minutes = 0 }), world.GameData, "job");
		AssertLoadFails(With(job with { MinutesDone = -1 }), world.GameData, "job");
	}

	[Test]
	public void EventLogSavesAndLoads()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 6, 0));
		var npc = world.Npcs.First(x => x.Location != world.Player.Location);
		var from = npc.Location;
		world.MovePerson(npc, world.Player.Location, world.Time);
		world.MovePerson(npc, from, world.Time);

		var json = CreateSave(world).Serialize();
		var loaded = SaveFile.Deserialize(json).Load(world.GameData, GameTitle, new Random(0));

		AssertSameWorld(loaded, world);
		Assert.That(CreateSave(loaded).Serialize(), Is.EqualTo(json));
		Assert.That(loaded.EventLog.Entries.Select(x => x.Kind), Is.SupersetOf(new[] { LogEntryKind.Arrived, LogEntryKind.Left }));
	}

	[Test]
	public void GameSavedWithoutAnEventLogLoadsWithAnEmptyOne()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var save = CreateSave(world);
		save = save with { Objects = save.Objects.Where(x => x is not EventLogRecord).ToList() };

		var loaded = SaveFile.Deserialize(save.Serialize()).Load(world.GameData, GameTitle, new Random(0));

		Assert.That(loaded.EventLog.Entries, Is.Empty);
	}

	[Test]
	public void LoadFailsForInvalidEventLogs()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var npc = world.Npcs.First(x => x.Location != world.Player.Location);
		world.MovePerson(npc, world.Player.Location, world.Time);
		var save = CreateSave(world);
		var log = save.Objects.OfType<EventLogRecord>().Single();
		var entry = log.Entries[^1];

		SaveFile With(LogEntryRecord record) => Replace(save, log, log with { Entries = [record] });
		AssertLoadFails(With(entry with { Need = "flying" }), world.GameData, "flying");
		AssertLoadFails(With(entry with { Need = "work", Step = 999 }), world.GameData, "step");
		AssertLoadFails(With(entry with { Step = 0 }), world.GameData, "step");
		AssertLoadFails(With(entry with { Person = entry.Location }), world.GameData, nameof(Person));
		AssertLoadFails(save with { Objects = [.. save.Objects, log with { Id = new WorldId(save.NextId) }] } with { NextId = save.NextId + 1 }, world.GameData, "event log");
	}

	[Test]
	public void FollowingSavesAndLoads()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 3, 0));
		var (leader, follower) = (world.Npcs.First(x => x.CurrentTask is not null), world.Npcs.Last(x => x.CurrentTask is not null));
		follower.Interrupt(new FollowTask(leader, 30), world.Time, world.GameData.Calendar.AddMinutes(world.Time, 30));
		if (leader.Current is { } leading)
			leading.Seeking = new SeekChoice(null, leader.Location, SeekClue.People);

		var json = CreateSave(world).Serialize();
		var loaded = SaveFile.Deserialize(json).Load(world.GameData, GameTitle, new Random(0));

		AssertSameWorld(loaded, world);
		Assert.That(CreateSave(loaded).Serialize(), Is.EqualTo(json));
		var follow = (FollowTask) loaded.Objects.Get<Person>(follower.Id).CurrentTask!;
		Assert.That((follow.Leader.Id, follow.Minutes), Is.EqualTo((leader.Id, 30)));
		Assert.That(loaded.Objects.Get<Person>(leader.Id).Seeking, Is.EqualTo(new SeekChoice(null, loaded.Objects.Get<Location>(leader.Location.Id), SeekClue.People)));
	}

	[Test]
	public void SeekingSavesAndLoads()
	{
		var (world, person) = TalkingTests.CreateWorldWithSomeoneHere();
		var socializing = world.GameData.Schedules.Needs.Single(x => x.Id == world.GameData.Schedules.SocializeNeed);
		foreach (var need in world.GameData.Schedules.Needs.Where(x => x.Kind == NeedKind.Level))
			person.SetLevel(need, 0);
		person.SetLevel(socializing, OperaEtDies.Data.Need.MaximumLevel);
		world.CancelGoal(person);
		Assert.That(person.Seeking, Is.Not.Null);

		var json = CreateSave(world).Serialize();
		var loaded = SaveFile.Deserialize(json).Load(world.GameData, GameTitle, new Random(0));

		AssertSameWorld(loaded, world);
		Assert.That(CreateSave(loaded).Serialize(), Is.EqualTo(json));
	}

	[Test]
	public void StepsSaveAndLoad()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var laundry = world.GameData.Schedules.Needs.Single(x => x.Id == "laundry");
		var work = world.GameData.Schedules.Needs.Single(x => x.Id == "work");
		var player = world.Player;
		var held = player.SetPlan(laundry, [], startStep: 4)!;
		held.SetAnswer("asked", true);
		var playing = new StepSequence(work, 6, 60) { Takeover = StepTakeover.All, IsCarriedOut = true, Place = world.Locations[1], Partner = world.Npcs[1] };
		player.AddSequence(playing);
		var person = world.Npcs[0];
		var item = world.Objects.OfType<Item>().FirstOrDefault(x => x.Container is not null);
		var sequence = person.SetPlan(work, [new BusyTask(60, 4)], startStep: 5, goalMinutes: 60)!;
		if (item is not null)
		{
			item.Container!.Remove(item);
			person.Inventory.Add(item);
			sequence.AddBundle("workClothes", world.CreateBundle(person, "Work clothes", [item]));
		}

		sequence.RememberPlace("spot", world.Locations[0]);
		sequence.Until = world.Time;
		var night = person.GetOutfits(OutfitKind.Night).FirstOrDefault();
		var replaced = person.GetOutfits(OutfitKind.Everyday).FirstOrDefault()?.Pieces ?? [];
		if (night is not null)
			person.SetReplacedClothes(night, replaced);

		var json = CreateSave(world).Serialize();
		var loaded = SaveFile.Deserialize(json).Load(world.GameData, GameTitle, new Random(0));

		var loadedPlayer = loaded.Player;
		Assert.That(loadedPlayer.Sequences.Select(x => (x.Need.Id, x.Step)), Is.EqualTo(new[] { ("laundry", 4), ("work", 6) }));
		Assert.That(loadedPlayer.Sequences[0].Answers["asked"], Is.True);
		var loadedPlaying = loadedPlayer.Current!;
		Assert.That((loadedPlaying.MinutesLeft, loadedPlaying.Takeover, loadedPlaying.IsCarriedOut, loadedPlaying.Place?.Id, loadedPlaying.Partner?.Id),
			Is.EqualTo(((int?) 60, StepTakeover.All, true, world.Locations[1].Id, world.Npcs[1].Id)));
		var loadedPerson = loaded.Npcs[0];
		var loadedSequence = loadedPerson.Current!;
		Assert.That((loadedSequence.Step, loadedSequence.MinutesLeft, loadedPerson.Tasks[0], loadedSequence.Until), Is.EqualTo((5, (int?) 60, (PersonTask) new BusyTask(60, 4), world.Time)));
		Assert.That(loadedSequence.Bundles.Values.SelectMany(x => x.Items).Select(x => x.Id), Is.EqualTo(item is null ? Array.Empty<WorldId>() : new[] { item.Id }));
		Assert.That(loadedSequence.RememberedPlaces["spot"].Id, Is.EqualTo(world.Locations[0].Id));
		Assert.That(loadedPerson.ReplacedClothes.Select(x => (x.Key.Id, x.Value.Select(y => y.Id).ToList())), Is.EqualTo(person.ReplacedClothes.Select(x => (x.Key.Id, x.Value.Select(y => y.Id).ToList()))));
		Assert.That(CreateSave(loaded).Serialize(), Is.EqualTo(json));
	}

	[Test]
	public void LoadFailsForUnknownOrInvalidNeeds()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var save = CreateSave(world);
		var person = save.Objects.OfType<PersonRecord>().First();
		var index = save.Objects.ToList().IndexOf(person);

		SaveFile With(PersonRecord record) => save with { Objects = [.. save.Objects.Take(index), record, .. save.Objects.Skip(index + 1)] };
		AssertLoadFails(With(person with { Sequences = [new StepSequenceRecord("flying", 0)] }), world.GameData, "flying");
		AssertLoadFails(With(person with { Needs = new Dictionary<string, double> { ["flying"] = 5 } }), world.GameData, "flying");
		AssertLoadFails(With(person with { Needs = new Dictionary<string, double> { ["work"] = 5 } }), world.GameData, "work");
		AssertLoadFails(With(person with { Needs = new Dictionary<string, double> { ["fatigue"] = 500 } }), world.GameData, "fatigue");
		AssertLoadFails(With(person with { Progress = new Dictionary<string, long> { ["fatigue"] = 5 } }), world.GameData, "fatigue");
		AssertLoadFails(With(person with { Sequences = [new StepSequenceRecord("work", 20)] }), world.GameData, "work");
		AssertLoadFails(With(person with { Sequences = [new StepSequenceRecord("work", -1)] }), world.GameData, "work");
		AssertLoadFails(With(person with { Sequences = [new StepSequenceRecord("work", 1, MinutesLeft: -1)] }), world.GameData, "work");

		var choice = new GoalChoiceRecord(save.Time, [new NeedUrgencyRecord("fatigue", 50)], "fatigue", GoalChoiceResult.Chosen);
		AssertLoadFails(With(person with { GoalChoice = choice with { Need = "flying" } }), world.GameData, "flying");
		AssertLoadFails(With(person with { GoalChoice = choice with { Urgencies = [new NeedUrgencyRecord("flying", 50)] } }), world.GameData, "flying");
		AssertLoadFails(With(person with { GoalChoice = choice with { Urgencies = [new NeedUrgencyRecord("hunger", -1)] } }), world.GameData, "hunger");
		AssertLoadFails(With(person with { GoalChoice = choice with { Time = "never" } }), world.GameData, "never");

		var other = save.Objects.OfType<PersonRecord>().Last();
		var location = save.Objects.OfType<LocationRecord>().First();
		var sighting = new SightingRecord(other.Id, location.Id, save.Time, SightingActivity.Busy, "fatigue", null, null);
		AssertLoadFails(With(person with { Sightings = [sighting with { Need = "flying" }] }), world.GameData, "flying");
		AssertLoadFails(With(person with { Sightings = [sighting with { Need = null }] }), world.GameData, "sighting");
		AssertLoadFails(With(person with { Sightings = [sighting with { Activity = SightingActivity.Left }] }), world.GameData, "sighting");
		AssertLoadFails(With(person with { Sightings = [sighting with { Person = person.Id }] }), world.GameData, "sighting");
		AssertLoadFails(With(person with { Sightings = [sighting, sighting] }), world.GameData, "sighting");
		AssertLoadFails(With(person with { Sightings = [sighting with { Time = "never" }] }), world.GameData, "never");
		AssertLoadFails(With(person with { Sightings = [sighting with { Plan = new SightingPlanRecord("flying", null) }] }), world.GameData, "flying");
	}

	[Test]
	public void ShippedDataSavesAndLoads()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		for (var seed = 0; seed < 5; seed++)
		{
			var random = new Random(seed);
			var world = GameWorld.CreateNewGame(gameData, CharacterRandomizer.CreateRandomPlayer(gameData, random), random);
			MoveSomePeople(world);

			var json = CreateSave(world).Serialize();
			var loaded = SaveFile.Deserialize(json).Load(gameData, GameTitle, new Random(seed));

			AssertSameWorld(loaded, world);
			Assert.That(CreateSave(loaded).Serialize(), Is.EqualTo(json), $"Seed {seed}");
		}
	}

	[Test]
	public void ClothingSavesAndLoads()
	{
		var (world, person, worn, carried, stored) = CreateWorldWithClothing();
		carried.ReadyAt = world.GameData.Calendar.AddMinutes(world.Time, 60);
		carried.DelayActivity = "Drying";
		var outfit = person.Outfits[0];
		person.SetPlan(null, [new AtWorkTask(true), new ChangeClothesTask(outfit, IsTakingOff: true) { ResumeMinutes = 3 }]);

		var json = CreateSave(world).Serialize();
		var loaded = SaveFile.Deserialize(json).Load(world.GameData, GameTitle, new Random(0));

		Assert.That(CreateSave(loaded).Serialize(), Is.EqualTo(json));
		var loadedOutfit = loaded.Objects.Get<OwnedOutfit>(outfit.Id);
		Assert.That(loaded.Objects.Get<Person>(person.Id).Tasks, Is.EqualTo(new PersonTask[] { new AtWorkTask(true), new ChangeClothesTask(loadedOutfit, IsTakingOff: true) { ResumeMinutes = 3 } }));
		var loadedPerson = loaded.Objects.Get<Person>(person.Id);
		var loadedWorn = loaded.Objects.Get<ClothingPiece>(worn.Id);
		var loadedCarried = loaded.Objects.Get<ClothingPiece>(carried.Id);
		var loadedStored = loaded.Objects.Get<ClothingPiece>(stored.Id);
		Assert.That(loadedPerson.Clothing.Pieces, Is.EqualTo(new[] { loadedWorn }));
		Assert.That(loadedWorn.Wearer, Is.SameAs(loadedPerson));
		Assert.That(loadedPerson.Inventory.Items, Is.EqualTo(new[] { loadedCarried }));
		Assert.That(loadedCarried.Container, Is.SameAs(loadedPerson.Inventory));
		Assert.That(loadedPerson.Home.Items.GetOwnedBy(loadedPerson), Does.Contain(loadedStored));
		Assert.That(loadedStored.Owner, Is.SameAs(loadedPerson));
		Assert.That(loadedWorn.Description, Is.EqualTo(worn.Description));
		Assert.That(loadedStored.Colors.Select(x => (x.Key, x.Value.Id)), Is.EqualTo(stored.Colors.Select(x => (x.Key, x.Value.Id))));
		Assert.That(loadedCarried.ReadyAt, Is.EqualTo(carried.ReadyAt));
		Assert.That(loadedCarried.DelayActivity, Is.EqualTo("Drying"));
		Assert.That(loadedWorn.ReadyAt, Is.Null);
	}

	[Test]
	public void LoadFailsForInvalidClothing()
	{
		var (world, person, worn, carried, _) = CreateWorldWithClothing();
		var save = CreateSave(world);
		var personRecord = save.Objects.OfType<PersonRecord>().Single(x => x.Id == person.Id);
		var wornRecord = save.Objects.OfType<ClothingPieceRecord>().Single(x => x.Id == worn.Id);
		var otherPerson = save.Objects.OfType<PersonRecord>().First(x => x.Id != person.Id);

		AssertLoadFails(Replace(save, wornRecord, wornRecord with { Clothing = "chainmail" }), world.GameData, "chainmail");
		AssertLoadFails(Replace(save, wornRecord, wornRecord with { Colors = new Dictionary<string, string> { ["main"] = "plaid" } }), world.GameData, "plaid");
		AssertLoadFails(Replace(save, wornRecord, wornRecord with { Colors = new Dictionary<string, string>() }), world.GameData, worn.Id.ToString().TrimStart('#'));
		AssertLoadFails(Replace(save, personRecord, personRecord with { Inventory = [carried.Id, worn.Id] }), world.GameData, worn.Id.ToString().TrimStart('#'));
		AssertLoadFails(Replace(save, otherPerson, otherPerson with { Clothing = [worn.Id] }), world.GameData, worn.Id.ToString().TrimStart('#'));
		AssertLoadFails(Replace(save, personRecord, personRecord with { Clothing = [worn.Id, carried.Id] }), world.GameData, carried.Id.ToString().TrimStart('#'));
		AssertLoadFails(Replace(save, personRecord, personRecord with { Clothing = [personRecord.Id] }), world.GameData, nameof(ClothingPiece));
		var outfit = save.Objects.OfType<OwnedOutfitRecord>().First();
		AssertLoadFails(Replace(save, outfit, outfit with { Pieces = [outfit.Pieces[0], outfit.Pieces[0]] }), world.GameData, outfit.Id.ToString().TrimStart('#'));
		AssertLoadFails(Replace(save, outfit, outfit with { Template = "ballgown" }), world.GameData, "ballgown");
	}

	/// <summary>
	/// Creates a world from the shipped data in which one person wears only a tunic, carries another, and keeps a coif at home.
	/// </summary>
	private static (GameWorld World, Person Person, ClothingPiece Worn, ClothingPiece Carried, ClothingPiece Stored) CreateWorldWithClothing()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		var random = new Random(0);
		var world = GameWorld.CreateNewGame(gameData, CharacterRandomizer.CreateRandomPlayer(gameData, random), random);
		var person = world.Npcs[0];
		ClothingPiece Create(string clothingId, string colorId)
		{
			var clothing = gameData.Clothing.Get(clothingId);
			return world.Objects.Create(id => new ClothingPiece(id, clothing, clothing.Colors.ToDictionary(x => x, _ => gameData.ClothingColors.Get(colorId)), person));
		}

		var worn = Create("tunic", "russet");
		var carried = Create("tunic", "woad");
		var stored = Create("coif", "undyed");
		person.Clothing.TakeOffAll(person.Home.Items);
		person.Clothing.Wear(worn);
		person.Inventory.Add(carried);
		person.Home.Items.Add(stored);
		return (world, person, worn, carried, stored);
	}

	[Test]
	public void LoadedGameKeepsCreatingNewIds()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var loaded = SaveFile.Deserialize(CreateSave(world).Serialize()).Load(world.GameData, GameTitle, new Random(0));

		var location = loaded.Objects.Create(id => new Location(id, loaded.GameData, loaded.GameData.QuarterTemplates[0]));

		Assert.That(world.Objects.Find(location.Id), Is.Null);
		Assert.That(loaded.Objects.Count, Is.EqualTo(world.Objects.Count + 1));
	}

	[Test]
	public void WritesAndReadsFiles()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var save = CreateSave(world);
		var folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		try
		{
			var filePath = Path.Combine(folder, "Saves", save.GetFileName());
			save.Write(filePath);

			var read = SaveFile.Read(filePath);

			Assert.That(read.Serialize(), Is.EqualTo(save.Serialize()));
			Assert.That(read.PlayerName, Is.EqualTo("John Smith"));
			Assert.That(Path.GetFileName(filePath), Does.StartWith("John Smith ").And.EndWith(SaveFile.Extension));
		}
		finally
		{
			if (Directory.Exists(folder))
				Directory.Delete(folder, recursive: true);
		}
	}

	[Test]
	public void SaveDirectoryIsUnderTheGameTitle()
	{
		var directory = SaveFile.GetSaveDirectory("Sub Corona");

		Assert.That(directory, Is.EqualTo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Golden Anvil", "Sub Corona", "Saves")));
		Assert.That(Path.GetFileName(Path.GetDirectoryName(SaveFile.GetSaveDirectory("A/B:C"))), Is.EqualTo("A_B_C"));
	}

	[Test]
	public void SaveRecordsTheGameAndData()
	{
		var (_, world) = GameWorldTests.CreateWorld();

		var settings = new SaveSettings(GameTitle, "hash", "modsHash", ["mod"], "");

		var save = SaveFile.Deserialize(SaveFile.Create(world, settings, s_savedAt).Serialize());

		Assert.That(save.Version, Is.EqualTo(SaveFile.CurrentVersion));
		Assert.That(save.GameTitle, Is.EqualTo(GameTitle));
		Assert.That(save.DataHash, Is.EqualTo("hash"));
		Assert.That(save.ModsHash, Is.EqualTo("modsHash"));
		Assert.That(save.Mods, Is.EqualTo(new[] { "mod" }));
		Assert.That(save.Objects.Select(x => x.Id), Is.EqualTo(world.Objects.All.Select(x => x.Id)));

		// A change to either hash means the data or mods have changed.
		Assert.That(save.IsDataChanged(settings), Is.False);
		Assert.That(save.IsDataChanged(settings with { DataHash = "other" }), Is.True);
		Assert.That(save.IsDataChanged(settings with { ModsHash = "" }), Is.True);
	}

	[Test]
	public void SavesWithoutAModsHashWereSavedWithoutMods()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var node = System.Text.Json.Nodes.JsonNode.Parse(CreateSave(world).Serialize())!.AsObject();
		node.Remove("modsHash");
		var json = node.ToJsonString();

		var save = SaveFile.Deserialize(json);

		Assert.That(json, Does.Not.Contain("modsHash"));
		Assert.That(save.ModsHash, Is.Empty);
	}

	[Test]
	public void LoadFailsForAnotherVersionOrGame()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var save = CreateSave(world);

		AssertLoadFails(save with { Version = SaveFile.CurrentVersion + 1 }, world.GameData, "version");
		Assert.That(() => save.Load(world.GameData, "Another Game", new Random(0)), Throws.TypeOf<SaveLoadException>().With.Message.Contains("Another Game"));
	}

	[Test]
	public void LoadFailsForUnknownIds()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var save = CreateSave(world);
		var business = save.Objects.OfType<BusinessRecord>().First();
		var person = save.Objects.OfType<PersonRecord>().First();
		var location = save.Objects.OfType<LocationRecord>().First();

		AssertLoadFails(Replace(save, business, business with { Profession = "astronaut" }), world.GameData, "astronaut");
		AssertLoadFails(Replace(save, location, location with { Template = "castle" }), world.GameData, "castle");
		AssertLoadFails(Replace(save, person, person with { Location = new WorldId(9999) }), world.GameData, "9999");
		AssertLoadFails(Replace(save, person, person with { Location = save.Objects.OfType<PersonRecord>().Last().Id }), world.GameData, nameof(Location));
		AssertLoadFails(Replace(save, person, person with { Traits = new Dictionary<string, System.Text.Json.JsonElement>(person.Traits) { ["height"] = System.Text.Json.JsonSerializer.SerializeToElement("giant") } }), world.GameData, "giant");
		AssertLoadFails(Replace(save, person, person with { Traits = new Dictionary<string, System.Text.Json.JsonElement>(person.Traits) { ["wings"] = System.Text.Json.JsonSerializer.SerializeToElement("feathered") } }), world.GameData, "wings");
	}

	[Test]
	public void LoadFailsForInconsistentObjects()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var save = CreateSave(world);
		var family = save.Objects.OfType<FamilyRecord>().First(x => x.Members.Count > 1);

		AssertLoadFails(save with { Objects = [.. save.Objects, save.Objects[0]] }, world.GameData, save.Objects[0].Id.ToString().TrimStart('#'));
		AssertLoadFails(save with { NextId = 1 }, world.GameData, "next id");
		AssertLoadFails(Replace(save, family, family with { Members = family.Members.Skip(1).ToList() }), world.GameData, "family");
		var player = save.Objects.OfType<PersonRecord>().Single(x => x.IsPlayer);
		AssertLoadFails(Replace(save, player, player with { IsPlayer = false }), world.GameData, "player");
	}

	[Test]
	public void LoadFailsForInvalidRelationships()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var save = CreateSave(world);
		var relationship = save.Objects.OfType<RelationshipRecord>().First();
		var other = save.Objects.OfType<RelationshipRecord>().Last();

		AssertLoadFails(save with { Objects = save.Objects.Where(x => x != relationship).ToList() }, world.GameData, "relationships");
		AssertLoadFails(Replace(save, relationship, relationship with { Score = 101 }), world.GameData, relationship.Id.ToString().TrimStart('#'));
		AssertLoadFails(Replace(save, relationship, relationship with { Second = relationship.First }), world.GameData, relationship.Id.ToString().TrimStart('#'));
		AssertLoadFails(Replace(save, other, other with { First = relationship.First, Second = relationship.Second }), world.GameData, other.Id.ToString().TrimStart('#'));
		AssertLoadFails(Replace(save, relationship, relationship with { LastTalked = "never" }), world.GameData, "never");
	}

	[TestCase("tuesday")]
	[TestCase("")]
	[TestCase("-60")]
	public void LoadFailsForInvalidTime(string time)
	{
		var (_, world) = GameWorldTests.CreateWorld();
		AssertLoadFails(CreateSave(world) with { Time = time }, world.GameData, $"\"{time}\"");
	}

	[Test]
	public void ReadFailsForInvalidJson()
	{
		Assert.That(() => SaveFile.Deserialize("{ not json"), Throws.TypeOf<SaveLoadException>());
		Assert.That(() => SaveFile.Deserialize("""{ "version": 1 }"""), Throws.TypeOf<SaveLoadException>());
	}

	private static SaveFile CreateSave(GameWorld world) => SaveFile.Create(world, new SaveSettings(GameTitle, "hash", "", [], ""), s_savedAt);

	private static SaveFile Replace(SaveFile save, WorldObjectRecord oldRecord, WorldObjectRecord newRecord) =>
		save with { Objects = save.Objects.Select(x => x == oldRecord ? newRecord : x).ToList() };

	private static void AssertLoadFails(SaveFile save, GameData gameData, string messagePart) =>
		Assert.That(() => SaveFile.Deserialize(save.Serialize()).Load(gameData, GameTitle, new Random(0)),
			Throws.TypeOf<SaveLoadException>().With.Message.Contains(messagePart).IgnoreCase);

	/// <summary>
	/// Moves some people around and passes some time, so the save holds more than how the game started.
	/// </summary>
	private static void MoveSomePeople(GameWorld world)
	{
		var random = new Random(1);
		foreach (var person in world.People.Where((_, index) => index % 3 == 0))
			person.Location = world.Locations[random.Next(world.Locations.Count)];
		foreach (var relationship in world.Relationships.Where((_, index) => index % 5 == 0))
			relationship.LastTalked = world.Time;
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(9, 7, 45));
	}

	private static void AssertSameWorld(GameWorld actual, GameWorld expected)
	{
		Assert.That(actual.Objects.All.Select(x => (x.Id, x.GetType())), Is.EqualTo(expected.Objects.All.Select(x => (x.Id, x.GetType()))));
		Assert.That(actual.Time, Is.EqualTo(expected.Time));
		Assert.That(actual.Player.Id, Is.EqualTo(expected.Player.Id));
		Assert.That(actual.EventLog.Entries.Select(x => x.CreateRecord()), Is.EqualTo(expected.EventLog.Entries.Select(x => x.CreateRecord())));
		Assert.That(actual.Families.Select(x => x.Id), Is.EqualTo(expected.Families.Select(x => x.Id)));
		Assert.That(actual.Npcs.Select(x => x.Id), Is.EqualTo(expected.Npcs.Select(x => x.Id)));

		foreach (var (location, other) in actual.Locations.Zip(expected.Locations))
		{
			Assert.That(location.Template, Is.SameAs(other.Template));
			Assert.That(location.Title, Is.EqualTo(other.Title));
			Assert.That(location.MediumDescription, Is.EqualTo(other.MediumDescription));
			Assert.That(location.Bounds, Is.EqualTo(other.Bounds));
			Assert.That(location.Area, Is.EqualTo(other.Area));
			Assert.That(location.QuarterKind, Is.EqualTo(other.QuarterKind));
			Assert.That(location.Family?.Id, Is.EqualTo(other.Family?.Id));
			Assert.That(location.Exits.Select(x => (x.Location.Id, x.Direction, x.Path, x.IsCrossing)), Is.EqualTo(other.Exits.Select(x => (x.Location.Id, x.Direction, x.Path, x.IsCrossing))));
			Assert.That(location.Surroundings.Select(x => (x.Feature.Id, x.Direction, x.Text)), Is.EqualTo(other.Surroundings.Select(x => (x.Feature.Id, x.Direction, x.Text))));
			Assert.That(location.People.Select(x => x.Id), Is.EquivalentTo(other.People.Select(x => x.Id)));
		}

		foreach (var (person, other) in actual.People.Zip(expected.People))
		{
			Assert.That(person.Id, Is.EqualTo(other.Id));
			Assert.That(person.FullName, Is.EqualTo(other.FullName));
			Assert.That(person.Description, Is.EqualTo(other.Description));
			Assert.That(person.Traits.Values, Is.EquivalentTo(other.Traits.Values));
			Assert.That(person.Role, Is.EqualTo(other.Role));
			Assert.That(person.IsAtWork, Is.EqualTo(other.IsAtWork));
			Assert.That(person.Location.Id, Is.EqualTo(other.Location.Id));
			Assert.That(person.Home.Id, Is.EqualTo(other.Home.Id));
			Assert.That(person.Workplace.Id, Is.EqualTo(other.Workplace.Id));
			Assert.That(person.Business?.Id, Is.EqualTo(other.Business?.Id));
			Assert.That(person.PortraitPlace, Is.EqualTo(other.PortraitPlace));
			Assert.That(person.Goal, Is.SameAs(other.Goal));
			Assert.That(person.Tasks.Select(x => x.CreateRecord()), Is.EqualTo(other.Tasks.Select(x => x.CreateRecord())));
			Assert.That(person.TaskEnd, Is.EqualTo(other.TaskEnd));
			Assert.That((person.GoalChoice?.Time, person.GoalChoice?.Need, person.GoalChoice?.Result), Is.EqualTo((other.GoalChoice?.Time, other.GoalChoice?.Need, other.GoalChoice?.Result)));
			Assert.That(person.GoalChoice?.Urgencies, Is.EqualTo(other.GoalChoice?.Urgencies));
			Assert.That((person.Seeking?.Friend?.Id, person.Seeking?.Place.Id, person.Seeking?.Clue), Is.EqualTo((other.Seeking?.Friend?.Id, other.Seeking?.Place.Id, other.Seeking?.Clue)));
			Assert.That(person.Sightings.Select(x => (x.Key.Id, x.Value.Location.Id, x.Value.Time, x.Value.Activity, x.Value.Need, x.Value.Destination?.Id, x.Value.Source?.Id, x.Value.Plan?.Goal, x.Value.Plan?.Destination?.Id)),
				Is.EqualTo(other.Sightings.Select(x => (x.Key.Id, x.Value.Location.Id, x.Value.Time, x.Value.Activity, x.Value.Need, x.Value.Destination?.Id, x.Value.Source?.Id, x.Value.Plan?.Goal, x.Value.Plan?.Destination?.Id))));
			foreach (var need in actual.GameData.Schedules.Needs)
			{
				Assert.That(person.GetLevel(need), Is.EqualTo(other.GetLevel(need)));
				Assert.That(person.GetProgress(need), Is.EqualTo(other.GetProgress(need)));
			}
		}

		Assert.That(actual.Relationships.Select(x => (x.First.Id, x.Second.Id, x.Score, x.LastTalked)), Is.EqualTo(expected.Relationships.Select(x => (x.First.Id, x.Second.Id, x.Score, x.LastTalked))));
		foreach (var person in actual.People)
			Assert.That(person.Relationships.Keys.Select(x => x.Id), Is.EquivalentTo(actual.People.Where(x => x != person).Select(x => x.Id)));

		foreach (var (business, other) in actual.Businesses.Zip(expected.Businesses))
		{
			Assert.That(business.Profession, Is.SameAs(other.Profession));
			Assert.That(business.Owner.Id, Is.EqualTo(other.Owner.Id));
			Assert.That(business.Helpers.Select(x => x.Id), Is.EqualTo(other.Helpers.Select(x => x.Id)));
			Assert.That(business.Location?.Id, Is.EqualTo(other.Location?.Id));
		}

		foreach (var (feature, other) in actual.Features.Zip(expected.Features))
		{
			Assert.That(feature.Template, Is.SameAs(other.Template));
			Assert.That(feature.Bounds, Is.EqualTo(other.Bounds));
		}
	}

	private const string GameTitle = "Title";

	private static readonly DateTimeOffset s_savedAt = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
}
