using System.IO;
using System.Windows.Threading;
using ModEditor.Model;
using ModEditor.ViewModels;
using ModEditor.ViewModels.Form;

namespace OperaEtDies.Test.Tools;

/// <summary>
/// Runs the mod editor's view model on a real mod folder, with its background checks, as the editor does.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class ModEditorViewModelTests
{
	[Test]
	public void OpeningAModShowsWhatItChangesWithoutAnyEdits()
	{
		var directory = Path.Combine(Path.GetTempPath(), "OperaEtDiesModEditorTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		File.WriteAllText(Path.Combine(directory, "Ops.json"), """[ { "op": "set", "path": "families.head.femaleChance", "value": 35 } ]""");
		var previousContext = SynchronizationContext.Current;
		SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
		using var viewModel = new MainWindowViewModel(new NoDialogs(), new EditorSettings([], false, null, false, 0), _ => { });
		try
		{
			viewModel.OpenRecentFolder(directory);
			WaitFor(() => viewModel.ProblemSummary != "Checking…");

			var head = viewModel.GameDataRoots.Single().Children.Single(x => x.Title == "families").Children.Single(x => x.Title == "head");
			head.IsSelected = true;

			Assert.That(head.Change, Is.EqualTo(ChangeKind.Changed));
			Assert.That(viewModel.ChangeRows.Select(x => $"{x.Path}: {x.Before} -> {x.After}"), Is.EqualTo(new[] { "families.head.femaleChance: 8 -> 35" }));
			Assert.That(viewModel.ChangeRows.Single().Sources.Single().Title, Does.Contain("set"));

			// The form marks the changed field, and goes from it to the operation that changed it.
			var femaleChance = ((ObjectFieldViewModel) viewModel.Form!.Root).Fields.Single(x => x.Label == "femaleChance");
			var unchanged = ((ObjectFieldViewModel) viewModel.Form.Root).Fields.First(x => x.Label != "femaleChance");
			Assert.That(femaleChance.Change, Is.EqualTo(ChangeKind.Changed));
			Assert.That(femaleChance.ToolTipText, Does.StartWith("The mod changes this"));
			Assert.That(unchanged.Change, Is.Null);
			Assert.That(unchanged.CanGoToModChange(), Is.False);

			femaleChance.GoToModChange();
			Assert.That(viewModel.TreeTab, Is.EqualTo(0));
			Assert.That(viewModel.SelectedNode, Is.InstanceOf<ModNodeViewModel>().With.Property(nameof(ModNodeViewModel.Location)).EqualTo(new OperationLocation("Ops.json", 0)));
		}
		finally
		{
			SynchronizationContext.SetSynchronizationContext(previousContext);
			viewModel.Dispose();
			Directory.Delete(directory, recursive: true);
		}
	}

	/// <summary>
	/// Lets the dispatcher run what's waiting for it (such as the end of a background check) until the condition is met.
	/// </summary>
	private static void WaitFor(Func<bool> condition)
	{
		var deadline = DateTime.UtcNow.AddSeconds(30);
		while (!condition())
		{
			Assert.That(DateTime.UtcNow, Is.LessThan(deadline), "Timed out waiting for the editor.");
			var frame = new DispatcherFrame();
#pragma warning disable VSTHRD001 // Running the dispatcher until it's idle is what this is for.
			_ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
#pragma warning restore VSTHRD001
			Dispatcher.PushFrame(frame);
			Thread.Sleep(10);
		}
	}

	private sealed class NoDialogs : IEditorDialogs
	{
		public string? PickFolder(string? initialDirectory) => null;

		public string? PromptText(string title, string message, string initialText, IReadOnlyList<string>? suggestions = null) => null;

		public bool? AskToSave(string modName) => false;

		public void ShowError(string message) => Assert.Fail(message);
	}
}
