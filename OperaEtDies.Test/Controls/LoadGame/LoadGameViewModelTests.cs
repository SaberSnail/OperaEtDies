using System.IO;
using OperaEtDies.Controls.LoadGame;
using OperaEtDies.GameState;
using OperaEtDies.Test.GameState;
using OperaEtDies.Windows.Main;

namespace OperaEtDies.Test.Controls.LoadGame;

[TestFixture]
public sealed class LoadGameViewModelTests
{
	[SetUp]
	public void SetUp()
	{
		m_folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		m_settings = new SaveSettings(TestData.CreateGameInfo().Title, "hash", "", [], m_folder);
	}

	[TearDown]
	public void TearDown()
	{
		if (Directory.Exists(m_folder))
			Directory.Delete(m_folder, recursive: true);
	}

	[Test]
	public void ListsNoSavesWithoutTheFolder()
	{
		var viewModel = new LoadGameViewModel(TestData.CreateGameData(), m_settings, new Random(0));

		Assert.That(viewModel.Saves, Is.Empty);
		Assert.That(viewModel.HasSaves, Is.False);
	}

	[Test]
	public void ListsSavesNewestFirst()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		Write(world, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "old");
		Write(world, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), "new");
		File.WriteAllText(Path.Combine(m_folder, "broken" + SaveFile.Extension), "{ not json");

		var viewModel = new LoadGameViewModel(world.GameData, m_settings, new Random(0));
		var readable = viewModel.Saves.Where(x => x.Save is not null).ToList();
		var broken = viewModel.Saves.Single(x => x.Save is null);

		Assert.That(viewModel.HasSaves, Is.True);
		Assert.That(readable.Select(x => x.SavedAt.Month), Is.EqualTo(new[] { 2, 1 }));
		Assert.That(readable.Select(x => x.Title), Is.All.EqualTo("John Smith"));
		Assert.That(readable.Select(x => x.CanLoad()), Is.All.True);
		Assert.That(broken.Title, Is.EqualTo("broken"));
		Assert.That(broken.Details, Is.Not.Empty);
		Assert.That(broken.CanLoad(), Is.False);
	}

	[Test]
	public void LoadingASaveRaisesTheLoadedWorld()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		Write(world, DateTimeOffset.Now, "hash");
		var viewModel = new LoadGameViewModel(world.GameData, m_settings, new Random(0));
		GameWorld? loaded = null;
		viewModel.GameLoaded += (_, e) => loaded = e.Value;

		viewModel.Saves.Single().Load();

		Assert.That(viewModel.IsConfirming, Is.False);
		Assert.That(loaded, Is.Not.Null);
		Assert.That(loaded!.Player.Id, Is.EqualTo(world.Player.Id));
	}

	[Test]
	public void ChangedDataIsConfirmedBeforeLoading()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		Write(world, DateTimeOffset.Now, "other");
		var viewModel = new LoadGameViewModel(world.GameData, m_settings, new Random(0));
		var loadCount = 0;
		viewModel.GameLoaded += (_, _) => loadCount++;
		var save = viewModel.Saves.Single();

		save.Load();
		Assert.That(save.IsDataChanged, Is.True);
		Assert.That(viewModel.ConfirmingLoad, Is.SameAs(save));
		Assert.That(loadCount, Is.Zero);

		viewModel.Cancel();
		Assert.That(viewModel.IsConfirming, Is.False);
		Assert.That(loadCount, Is.Zero);

		save.Load();
		viewModel.ConfirmLoad();
		Assert.That(viewModel.IsConfirming, Is.False);
		Assert.That(loadCount, Is.EqualTo(1));
	}

	[Test]
	public void ShowsWhyASaveCouldNotLoad()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var save = SaveFile.Create(world, m_settings with { GameTitle = "Another Game" }, DateTimeOffset.Now);
		save.Write(Path.Combine(m_folder, save.GetFileName()));
		var viewModel = new LoadGameViewModel(world.GameData, m_settings, new Random(0));
		var loadCount = 0;
		viewModel.GameLoaded += (_, _) => loadCount++;

		viewModel.Saves.Single().Load();

		Assert.That(viewModel.HasError, Is.True);
		Assert.That(viewModel.Error, Does.Contain("Another Game"));
		Assert.That(loadCount, Is.Zero);
	}

	[Test]
	public void ChangedModsAreConfirmedBeforeLoading()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var save = SaveFile.Create(world, m_settings with { ModsHash = "mods" }, DateTimeOffset.Now);
		save.Write(Path.Combine(m_folder, save.GetFileName()));
		var viewModel = new LoadGameViewModel(world.GameData, m_settings, new Random(0));

		viewModel.Saves.Single().Load();

		Assert.That(viewModel.ConfirmingLoad, Is.SameAs(viewModel.Saves.Single()));
	}

	[Test]
	public void SaveTimesIncludeSeconds()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var savedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
		Write(world, savedAt, "hash");

		var viewModel = new LoadGameViewModel(world.GameData, m_settings, new Random(0));

		Assert.That(viewModel.Saves.Single().Details, Does.Contain(savedAt.ToLocalTime().ToString("G", System.Globalization.CultureInfo.CurrentCulture)));
		Assert.That(viewModel.Saves.Single().Details, Does.Contain("07"));
	}

	[Test]
	public void DeletingASaveIsConfirmedFirst()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		Write(world, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "hash");
		Write(world, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), "hash");
		var viewModel = new LoadGameViewModel(world.GameData, m_settings, new Random(0));
		var save = viewModel.Saves[0];

		save.Delete();
		Assert.That(viewModel.ConfirmingDelete, Is.SameAs(save));
		Assert.That(viewModel.IsConfirming, Is.True);
		Assert.That(viewModel.DeleteConfirmation, Does.Contain("John Smith"));

		viewModel.Cancel();
		Assert.That(viewModel.IsConfirming, Is.False);
		Assert.That(File.Exists(save.FilePath), Is.True);

		save.Delete();
		viewModel.ConfirmDelete();
		Assert.That(viewModel.IsConfirming, Is.False);
		Assert.That(File.Exists(save.FilePath), Is.False);
		Assert.That(viewModel.Saves, Has.Count.EqualTo(1).And.Not.Contains(save));

		viewModel.Saves[0].Delete();
		viewModel.ConfirmDelete();
		Assert.That(viewModel.HasSaves, Is.False);
		Assert.That(Directory.GetFiles(m_folder), Is.Empty);
	}

	[Test]
	public void UnreadableSavesCanBeDeleted()
	{
		Directory.CreateDirectory(m_folder);
		File.WriteAllText(Path.Combine(m_folder, "broken" + SaveFile.Extension), "{ not json");
		var viewModel = new LoadGameViewModel(TestData.CreateGameData(), m_settings, new Random(0));

		viewModel.Saves.Single().Delete();
		viewModel.ConfirmDelete();

		Assert.That(viewModel.Saves, Is.Empty);
		Assert.That(Directory.GetFiles(m_folder), Is.Empty);
	}

	[Test]
	public void MainWindowSavesTheGame()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		using var mainWindow = new MainWindowViewModel(TestData.CreateGameInfo().Title, world, saveSettings: m_settings);

		Assert.That(mainWindow.CanSaveGame(), Is.True);
		mainWindow.SaveGame();

		Assert.That(mainWindow.SaveStatus, Is.EqualTo(OurResources.GameSaved));
		var file = Directory.GetFiles(m_folder).Single();
		Assert.That(SaveFile.Read(file).DataHash, Is.EqualTo("hash"));

		mainWindow.TravelTo(world.Player.Home.Connections[0]);
		Assert.That(mainWindow.SaveStatus, Is.Null);

		using var withoutSettings = new MainWindowViewModel(TestData.CreateGameInfo().Title, world);
		Assert.That(withoutSettings.CanSaveGame(), Is.False);
	}

	private void Write(GameWorld world, DateTimeOffset savedAt, string dataHash)
	{
		var save = SaveFile.Create(world, m_settings with { DataHash = dataHash }, savedAt);
		save.Write(Path.Combine(m_folder, save.GetFileName()));
	}

	private string m_folder = "";
	private SaveSettings m_settings = null!;
}
