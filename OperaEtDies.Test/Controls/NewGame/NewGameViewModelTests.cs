using OperaEtDies.Characters;
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
		Assert.That(viewModel.Height, Is.AnyOf(viewModel.Heights.ToArray()));
		Assert.That(viewModel.Weight, Is.AnyOf(viewModel.Weights.ToArray()));
		Assert.That(viewModel.Age, Is.InRange(36, 60));
		Assert.That(viewModel.CanStartGame(), Is.True);
	}

	[Test]
	public void StartingValuesAreRandom()
	{
		var heights = new HashSet<string>();
		var weights = new HashSet<string>();
		var ages = new HashSet<int>();

		for (var seed = 0; seed < 100; seed++)
		{
			var viewModel = new NewGameViewModel(TestData.CreateGameData(), new Random(seed));
			heights.Add(viewModel.Height.Id);
			weights.Add(viewModel.Weight.Id);
			ages.Add(viewModel.Age);
		}

		Assert.That(heights, Has.Count.EqualTo(3));
		Assert.That(weights, Has.Count.EqualTo(3));
		Assert.That(ages, Has.Count.GreaterThan(1));
	}

	[TestCase("")]
	[TestCase("  ")]
	public void BlankFirstNameIsRandomized(string firstName)
	{
		var viewModel = CreateViewModel();
		var changed = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		viewModel.FirstName = firstName;

		Assert.That(viewModel.FirstName, Is.AnyOf("John", "Osric"));
		Assert.That(changed, Does.Contain(nameof(NewGameViewModel.FirstName)));
		Assert.That(viewModel.CanStartGame(), Is.True);
	}

	[TestCase("")]
	[TestCase("  ")]
	public void BlankAgeIsRandomized(string ageText)
	{
		var viewModel = CreateViewModel();
		var changed = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		viewModel.AgeText = ageText;

		Assert.That(viewModel.Age, Is.InRange(36, 60));
		Assert.That(viewModel.AgeText, Is.EqualTo(viewModel.Age.ToString(System.Globalization.CultureInfo.CurrentCulture)));
		Assert.That(changed, Does.Contain(nameof(NewGameViewModel.AgeText)));
	}

	[Test]
	public void AgeTextSetsAge()
	{
		var viewModel = CreateViewModel();
		viewModel.Age = 40;

		viewModel.AgeText = "45";
		Assert.That(viewModel.Age, Is.EqualTo(45));

		viewModel.AgeText = "abc";
		Assert.That(viewModel.Age, Is.EqualTo(45));
		Assert.That(viewModel.AgeText, Is.EqualTo("45"));

		viewModel.AgeText = "90";
		Assert.That(viewModel.Age, Is.EqualTo(60));
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
	public void AgeStaysWithinRange()
	{
		var viewModel = CreateViewModel();

		viewModel.Age = 50;
		Assert.That(viewModel.Age, Is.EqualTo(50));

		viewModel.Age = 12;
		Assert.That(viewModel.Age, Is.EqualTo(36));

		viewModel.Age = 90;
		Assert.That(viewModel.Age, Is.EqualTo(60));
	}

	[Test]
	public void RandomizeFieldsCanSelectEveryValue()
	{
		var viewModel = CreateViewModel();
		var professions = new HashSet<Profession>();
		var firstNames = new HashSet<string>();
		var heights = new HashSet<CharacterOption>();
		var weights = new HashSet<CharacterOption>();

		// Enough rolls that even the rarest test value is all but certain to come up.
		for (var i = 0; i < 500; i++)
		{
			viewModel.RandomizeProfession();
			viewModel.RandomizeFirstName();
			viewModel.RandomizeHeight();
			viewModel.RandomizeWeight();
			viewModel.RandomizeAge();

			professions.Add(viewModel.Profession);
			firstNames.Add(viewModel.FirstName);
			heights.Add(viewModel.Height);
			weights.Add(viewModel.Weight);
			Assert.That(viewModel.Age, Is.InRange(viewModel.MinimumAge, viewModel.MaximumAge));
		}

		Assert.That(professions, Is.EquivalentTo(viewModel.Professions));
		Assert.That(firstNames, Is.EquivalentTo(new[] { "John", "Osric" }));
		Assert.That(heights, Is.EquivalentTo(viewModel.Heights));
		Assert.That(weights, Is.EquivalentTo(viewModel.Weights));
	}

	[Test]
	public void RandomizeProfessionUpdatesDefaultLastName()
	{
		var viewModel = CreateViewModel();

		viewModel.RandomizeProfession();

		Assert.That(viewModel.LastName, Is.EqualTo(viewModel.Profession.DefaultLastName));
	}

	[Test]
	public void RandomizeAllKeepsCustomLastName()
	{
		var viewModel = CreateViewModel();
		viewModel.LastName = "Atwood";

		for (var i = 0; i < 20; i++)
		{
			viewModel.RandomizeAll();

			Assert.That(viewModel.LastName, Is.EqualTo("Atwood"));
			Assert.That(viewModel.Age, Is.InRange(viewModel.MinimumAge, viewModel.MaximumAge));
			Assert.That(viewModel.CanStartGame(), Is.True);
		}
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
		Assert.That(settings!.Player.Profession.Id, Is.EqualTo("miller"));
		Assert.That(settings.Player.FirstName, Is.EqualTo("Hugh"));
		Assert.That(settings.Player.LastName, Is.EqualTo("Miller"));
		Assert.That(settings.Player.Height.Id, Is.EqualTo("tall"));
		Assert.That(settings.Player.Weight.Id, Is.EqualTo("heavy"));
		Assert.That(settings.Player.Age, Is.EqualTo(40));
		Assert.That(settings.Player.Gender, Is.EqualTo(Gender.Male));
	}

	private static NewGameViewModel CreateViewModel() => new(TestData.CreateGameData(), new Random(0));
}
