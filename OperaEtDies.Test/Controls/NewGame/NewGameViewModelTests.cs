using OperaEtDies.Controls.NewGame;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Controls.NewGame;

[TestFixture]
public sealed class NewGameViewModelTests
{
	[Test]
	public void HasDefaults()
	{
		var viewModel = CreateViewModel();

		Assert.That(viewModel.Profession.Id, Is.EqualTo("smith"));
		Assert.That(viewModel.FirstName, Is.AnyOf("John", "Osric"));
		Assert.That(viewModel.LastName, Is.EqualTo("Smith"));
		Assert.That(viewModel.Height.Id, Is.EqualTo("tall"));
		Assert.That(viewModel.Weight.Id, Is.EqualTo("heavy"));
		Assert.That(viewModel.Age, Is.EqualTo(36));
		Assert.That(viewModel.CanStartGame(), Is.True);
	}

	[Test]
	public void LastNameFollowsProfessionUntilCustomized()
	{
		var viewModel = CreateViewModel();

		viewModel.Profession = viewModel.Professions[1];
		Assert.That(viewModel.LastName, Is.EqualTo("Miller"));

		viewModel.LastName = "Cooper";
		viewModel.Profession = viewModel.Professions[0];
		Assert.That(viewModel.LastName, Is.EqualTo("Cooper"));
	}

	[TestCase("")]
	[TestCase("  ")]
	[TestCase("Smith")]
	public void ClearingOrResettingLastNameFollowsProfessionAgain(string lastName)
	{
		var viewModel = CreateViewModel();

		viewModel.LastName = "Cooper";
		viewModel.LastName = lastName;
		Assert.That(viewModel.LastName, Is.EqualTo("Smith"));

		viewModel.Profession = viewModel.Professions[1];

		Assert.That(viewModel.LastName, Is.EqualTo("Miller"));
	}

	[Test]
	public void ClearingDefaultLastNameRestoresIt()
	{
		var viewModel = CreateViewModel();
		var changed = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		viewModel.LastName = "";

		// The value didn't change, but the view still needs to be told so it replaces its blank text.
		Assert.That(viewModel.LastName, Is.EqualTo("Smith"));
		Assert.That(changed, Does.Contain(nameof(NewGameViewModel.LastName)));
	}

	[Test]
	public void AgeCannotGoBelowMinimum()
	{
		var viewModel = CreateViewModel();

		viewModel.Age = 50;
		Assert.That(viewModel.Age, Is.EqualTo(50));

		viewModel.Age = 12;
		Assert.That(viewModel.Age, Is.EqualTo(36));
	}

	[Test]
	public void RandomizeFirstNameChangesName()
	{
		var viewModel = CreateViewModel();
		var original = viewModel.FirstName;

		viewModel.RandomizeFirstName();

		Assert.That(viewModel.FirstName, Is.Not.EqualTo(original));
	}

	[Test]
	public void BlankNamesBlockStart()
	{
		var viewModel = CreateViewModel();
		var raised = false;
		viewModel.StartGameRequested += (_, _) => raised = true;

		viewModel.FirstName = " ";
		Assert.That(viewModel.CanStartGame(), Is.False);
		viewModel.StartGame();
		Assert.That(raised, Is.False);
	}

	[Test]
	public void StartGameReportsSettings()
	{
		var viewModel = CreateViewModel();
		NewGameSettings? settings = null;
		viewModel.StartGameRequested += (_, e) => settings = e.Value;

		viewModel.Profession = viewModel.Professions[1];
		viewModel.FirstName = " Hugh ";
		viewModel.Height = viewModel.Heights[0];
		viewModel.Weight = viewModel.Weights[2];
		viewModel.Age = 40;
		viewModel.StartGame();

		Assert.That(settings, Is.Not.Null);
		Assert.That(settings!.Profession.Id, Is.EqualTo("miller"));
		Assert.That(settings.FirstName, Is.EqualTo("Hugh"));
		Assert.That(settings.LastName, Is.EqualTo("Miller"));
		Assert.That(settings.Height.Id, Is.EqualTo("tall"));
		Assert.That(settings.Weight.Id, Is.EqualTo("heavy"));
		Assert.That(settings.Age, Is.EqualTo(40));
	}

	private static NewGameViewModel CreateViewModel() => new(TestData.CreateGameData(), new Random(0));
}
