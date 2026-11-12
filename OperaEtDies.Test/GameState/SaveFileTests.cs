using System.IO;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class SaveFileTests
{
	[Test]
	public void SavingALoadedGameGivesTheSameFile()
	{
		for (var seed = 0; seed < 10; seed++)
		{
			var (_, world) = GameWorldTests.CreateWorld(seed, withFeatures: true);
			MoveSomePeople(world);

			var json = CreateSave(world).Serialize();
			var loaded = SaveFile.Deserialize(json).Load(world.GameData, GameTitle, new Random(seed));

			Assert.That(CreateSave(loaded).Serialize(), Is.EqualTo(json), $"Seed {seed}");
		}
	}

	[Test]
	public void LoadedGameMatchesTheSavedGame()
	{
		for (var seed = 0; seed < 10; seed++)
		{
			var (_, world) = GameWorldTests.CreateWorld(seed, withFeatures: true);
			MoveSomePeople(world);

			var loaded = SaveFile.Deserialize(CreateSave(world).Serialize()).Load(world.GameData, GameTitle, new Random(seed));

			AssertSameWorld(loaded, world);
		}
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
		world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(9, 7, 45));
	}

	private static void AssertSameWorld(GameWorld actual, GameWorld expected)
	{
		Assert.That(actual.Objects.All.Select(x => (x.Id, x.GetType())), Is.EqualTo(expected.Objects.All.Select(x => (x.Id, x.GetType()))));
		Assert.That(actual.Time, Is.EqualTo(expected.Time));
		Assert.That(actual.Player.Id, Is.EqualTo(expected.Player.Id));
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
		}

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
