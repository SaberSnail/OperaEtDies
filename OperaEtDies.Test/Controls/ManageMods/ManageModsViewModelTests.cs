using OperaEtDies.Controls.ManageMods;
using OperaEtDies.Mods;

namespace OperaEtDies.Test.Controls.ManageMods;

[TestFixture]
public sealed class ManageModsViewModelTests
{
	[Test]
	public void ShowsSavedMods()
	{
		var store = new FakeModListStore(@"C:\A", @"C:\B");

		var viewModel = new ManageModsViewModel(store, new FakeFolderPicker(), areModsEnabled: true);

		Assert.That(viewModel.Mods, Is.EqualTo(new[] { @"C:\A", @"C:\B" }));
		Assert.That(viewModel.AreModsEnabled, Is.True);
	}

	[Test]
	public void AddsAndSavesChosenFolder()
	{
		var store = new FakeModListStore(@"C:\A");
		var viewModel = new ManageModsViewModel(store, new FakeFolderPicker(@"C:\B"), areModsEnabled: true);

		viewModel.AddMod();

		Assert.That(store.Saved, Is.EqualTo(new[] { @"C:\A", @"C:\B" }));
		Assert.That(viewModel.SelectedMod, Is.EqualTo(@"C:\B"));
	}

	[Test]
	public void AddingExistingFolderSelectsIt()
	{
		var store = new FakeModListStore(@"C:\A", @"C:\B");
		var viewModel = new ManageModsViewModel(store, new FakeFolderPicker(@"c:\a\"), areModsEnabled: true);

		viewModel.AddMod();

		Assert.That(viewModel.Mods, Has.Count.EqualTo(2));
		Assert.That(store.Saved, Is.Null);
		Assert.That(viewModel.SelectedMod, Is.EqualTo(@"C:\A"));
	}

	[Test]
	public void CancellingAddChangesNothing()
	{
		var store = new FakeModListStore(@"C:\A");
		var viewModel = new ManageModsViewModel(store, new FakeFolderPicker(null), areModsEnabled: true);

		viewModel.AddMod();

		Assert.That(viewModel.Mods, Has.Count.EqualTo(1));
		Assert.That(store.Saved, Is.Null);
	}

	[Test]
	public void RemovesSelectedModAndSelectsNext()
	{
		var store = new FakeModListStore(@"C:\A", @"C:\B", @"C:\C");
		var viewModel = new ManageModsViewModel(store, new FakeFolderPicker(), areModsEnabled: true);
		Assert.That(viewModel.CanRemoveMod(), Is.False);

		viewModel.SelectedMod = @"C:\B";
		viewModel.RemoveMod();

		Assert.That(store.Saved, Is.EqualTo(new[] { @"C:\A", @"C:\C" }));
		Assert.That(viewModel.SelectedMod, Is.EqualTo(@"C:\C"));

		viewModel.RemoveMod();
		Assert.That(viewModel.SelectedMod, Is.EqualTo(@"C:\A"));

		viewModel.RemoveMod();
		Assert.That(viewModel.SelectedMod, Is.Null);
		Assert.That(store.Saved, Is.Empty);
	}

	[Test]
	public void ReordersSelectedMod()
	{
		var store = new FakeModListStore(@"C:\A", @"C:\B", @"C:\C");
		var viewModel = new ManageModsViewModel(store, new FakeFolderPicker(), areModsEnabled: true) { SelectedMod = @"C:\A" };
		Assert.That(viewModel.CanMoveModUp(), Is.False);

		viewModel.MoveModDown();
		Assert.That(store.Saved, Is.EqualTo(new[] { @"C:\B", @"C:\A", @"C:\C" }));

		viewModel.MoveModDown();
		Assert.That(store.Saved, Is.EqualTo(new[] { @"C:\B", @"C:\C", @"C:\A" }));
		Assert.That(viewModel.CanMoveModDown(), Is.False);

		viewModel.MoveModUp();
		Assert.That(store.Saved, Is.EqualTo(new[] { @"C:\B", @"C:\A", @"C:\C" }));
		Assert.That(viewModel.SelectedMod, Is.EqualTo(@"C:\A"));
	}

	[Test]
	public void BackRaisesRequest()
	{
		var viewModel = new ManageModsViewModel(new FakeModListStore(), new FakeFolderPicker(), areModsEnabled: false);
		var raised = false;
		viewModel.BackRequested += (_, _) => raised = true;

		viewModel.Back();

		Assert.That(raised, Is.True);
	}

	internal sealed class FakeModListStore(params string[] mods) : IModListStore
	{
		public IReadOnlyList<string>? Saved { get; private set; }

		public IReadOnlyList<string> Load() => Saved ?? mods;

		public void Save(IReadOnlyList<string> modDirectories) => Saved = modDirectories.ToList();
	}

	internal sealed class FakeFolderPicker(string? folder = null) : IFolderPicker
	{
		public string? PickFolder(string title, string? initialDirectory) => folder;
	}
}
