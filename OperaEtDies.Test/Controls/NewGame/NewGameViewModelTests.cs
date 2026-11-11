using System.Globalization;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Controls.NewGame;

[TestFixture]
public sealed class NewGameViewModelTests
{
	[Test]
	public void ShowsEveryProperty()
	{
		var viewModel = CreateViewModel();

		Assert.That(viewModel.Properties.Select(x => x.Id), Is.EqualTo(new[] { "gender", "profession", "firstName", "lastName", "height", "weight", "age" }));
		Assert.That(viewModel.Properties.Select(x => x.GetType()), Is.EqualTo(new[]
		{
			typeof(ValueListPropertyEditorViewModel),
			typeof(ValueListPropertyEditorViewModel),
			typeof(StringPropertyEditorViewModel),
			typeof(StringPropertyEditorViewModel),
			typeof(ValueListPropertyEditorViewModel),
			typeof(ValueListPropertyEditorViewModel),
			typeof(IntegerPropertyEditorViewModel),
		}));
	}

	[Test]
	public void PropertiesComeAfterTheirDependencies()
	{
		CharacterProperty Property(string id, CharacterCondition? when = null) => new()
		{
			Id = id,
			Title = id,
			Type = CharacterPropertyType.ValueList,
			When = when,
			Values = [new CharacterPropertyOption { Id = "x", Title = "X" }, new CharacterPropertyOption { Id = "y", Title = "Y" }],
		};
		var gameData = new GameData(
			new CharacterData
			{
				Templates = TestData.CreateTemplates("{a}", "{a}"),
				Properties = [Property("style", TestData.Condition("length", "x")), Property("a"), Property("length"), Property("b")],
			},
			TestData.CreateLocationList(), TestData.CreateProfessionList(), TestData.CreateFamilySettings());

		var viewModel = new NewGameViewModel(gameData, new Random(0));

		// "length" moves up so it comes before "style", which depends on it. The rest keep their order.
		Assert.That(viewModel.Properties.Select(x => x.Id), Is.EqualTo(new[] { "a", "length", "style", "b" }));
	}

	[Test]
	public void PropertiesThePlayerCanNeverHaveAreLeftOut()
	{
		CharacterPropertyOption Option(string id, CharacterCondition? when = null) => new() { Id = id, Title = id, When = when };
		var gameData = new GameData(
			new CharacterData
			{
				Templates = TestData.CreateTemplates("{gender}", "{gender}"),
				Properties =
				[
					new CharacterProperty
					{
						Id = "gender",
						Title = "Gender",
						Type = CharacterPropertyType.ValueList,
						Values = [Option("male"), Option("female")],
						Player = new CharacterRoleSettings { Values = ["male"] },
					},
					new CharacterProperty { Id = "beard", Title = "Beard", Type = CharacterPropertyType.ValueList, When = TestData.Condition("gender", "male"), Values = [Option("full"), Option("none")] },
					new CharacterProperty { Id = "bosom", Title = "Bosom", Type = CharacterPropertyType.ValueList, When = TestData.Condition("gender", "female"), Values = [Option("small"), Option("full")] },
					new CharacterProperty
					{
						Id = "build",
						Title = "Build",
						Type = CharacterPropertyType.ValueList,
						Values = [Option("curvy", TestData.Condition("gender", "female")), Option("average")],
					},
				],
			},
			TestData.CreateLocationList(), TestData.CreateProfessionList(), TestData.CreateFamilySettings());

		var viewModel = new NewGameViewModel(gameData, new Random(0));

		// The player is always male, so the bosom never applies. Build still applies, with one value the player can have.
		Assert.That(viewModel.Properties.Select(x => x.Id), Is.EqualTo(new[] { "gender", "beard", "build" }));
		Assert.That(viewModel.FindProperty("build")!.IsFixed, Is.True);
	}

	[Test]
	public void PropertiesWithOneValueAreFixed()
	{
		var viewModel = CreateViewModel();
		var gender = viewModel.FindProperty("gender")!;

		// Gender has only one value for the player.
		Assert.That(gender.IsFixed, Is.True);
		Assert.That(gender.IsAvailable, Is.True);
		Assert.That(gender.ValueText, Is.EqualTo("male title"));
		Assert.That(gender.CanRandomize(), Is.False);
		Assert.That(viewModel.Properties.Where(x => x.Id != "gender"), Has.All.Property(nameof(CharacterPropertyEditorViewModel.IsFixed)).False);
	}

	[Test]
	public void HasDefaults()
	{
		var viewModel = CreateViewModel();

		Assert.That(FirstName(viewModel).Text, Is.AnyOf("John", "Osric"));
		Assert.That(LastName(viewModel).Text, Is.EqualTo(Profession(viewModel).SelectedOption!.Id == "smith" ? "Smith" : "Miller"));
		Assert.That(Height(viewModel).SelectedOption, Is.AnyOf(Height(viewModel).Options.ToArray()));
		Assert.That(Age(viewModel).Value, Is.InRange(32, 60));
		Assert.That(Age(viewModel).Minimum, Is.EqualTo(32));
		Assert.That(Age(viewModel).Maximum, Is.EqualTo(60));
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
			heights.Add(Height(viewModel).SelectedOption!.Id);
			weights.Add(((ValueListPropertyEditorViewModel) viewModel.FindProperty("weight")!).SelectedOption!.Id);
			ages.Add(Age(viewModel).Value);
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
		var editor = FirstName(viewModel);
		var changed = new List<string?>();
		editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		editor.Text = firstName;

		Assert.That(editor.Text, Is.AnyOf("John", "Osric"));
		Assert.That(changed, Does.Contain(nameof(StringPropertyEditorViewModel.Text)));
		Assert.That(viewModel.CanStartGame(), Is.True);
	}

	[TestCase("")]
	[TestCase("  ")]
	public void BlankAgeIsRandomized(string ageText)
	{
		var editor = Age(CreateViewModel());
		var changed = new List<string?>();
		editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		editor.Text = ageText;

		Assert.That(editor.Value, Is.InRange(32, 60));
		Assert.That(editor.Text, Is.EqualTo(editor.Value.ToString(CultureInfo.CurrentCulture)));
		Assert.That(changed, Does.Contain(nameof(IntegerPropertyEditorViewModel.Text)));
	}

	[Test]
	public void AgeTextSetsAge()
	{
		var editor = Age(CreateViewModel());
		editor.Value = 40;

		editor.Text = "45";
		Assert.That(editor.Value, Is.EqualTo(45));

		editor.Text = "abc";
		Assert.That(editor.Value, Is.EqualTo(45));
		Assert.That(editor.Text, Is.EqualTo("45"));

		editor.Text = "90";
		Assert.That(editor.Value, Is.EqualTo(60));
	}

	[Test]
	public void AgeStaysWithinRange()
	{
		var editor = Age(CreateViewModel());

		editor.Value = 50;
		Assert.That(editor.Value, Is.EqualTo(50));

		editor.Value = 12;
		Assert.That(editor.Value, Is.EqualTo(32));

		editor.Value = 90;
		Assert.That(editor.Value, Is.EqualTo(60));
	}

	[Test]
	public void LastNameFollowsProfessionUntilCustomized()
	{
		var viewModel = CreateViewModel();
		SetProfession(viewModel, "smith");
		var changed = new List<string?>();
		LastName(viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		SetProfession(viewModel, "miller");
		Assert.That(LastName(viewModel).Text, Is.EqualTo("Miller"));
		Assert.That(changed, Does.Contain(nameof(StringPropertyEditorViewModel.Text)));

		LastName(viewModel).Text = "Cooper";
		SetProfession(viewModel, "smith");
		Assert.That(LastName(viewModel).Text, Is.EqualTo("Cooper"));
	}

	[TestCase("")]
	[TestCase("  ")]
	[TestCase("Smith")]
	public void ClearingOrResettingLastNameFollowsProfessionAgain(string lastName)
	{
		var viewModel = CreateViewModel();
		SetProfession(viewModel, "smith");

		LastName(viewModel).Text = "Cooper";
		LastName(viewModel).Text = lastName;
		Assert.That(LastName(viewModel).Text, Is.EqualTo("Smith"));

		SetProfession(viewModel, "miller");

		Assert.That(LastName(viewModel).Text, Is.EqualTo("Miller"));
	}

	[Test]
	public void ClearingDefaultLastNameRestoresIt()
	{
		var viewModel = CreateViewModel();
		SetProfession(viewModel, "smith");
		var changed = new List<string?>();
		LastName(viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		LastName(viewModel).Text = "";

		// The value didn't change, but the view still needs to be told so it replaces its blank text.
		Assert.That(LastName(viewModel).Text, Is.EqualTo("Smith"));
		Assert.That(changed, Does.Contain(nameof(StringPropertyEditorViewModel.Text)));
	}

	[Test]
	public void RandomizeFieldsCanSelectEveryValue()
	{
		var viewModel = CreateViewModel();
		var professions = new HashSet<CharacterPropertyOption>();
		var firstNames = new HashSet<string>();
		var lastNames = new HashSet<string>();
		var heights = new HashSet<CharacterPropertyOption>();

		// Enough rolls that even the rarest test value is all but certain to come up.
		for (var i = 0; i < 500; i++)
		{
			foreach (var property in viewModel.Properties)
				property.Randomize();

			professions.Add(Profession(viewModel).SelectedOption!);
			firstNames.Add(FirstName(viewModel).Text);
			heights.Add(Height(viewModel).SelectedOption!);
			Assert.That(Age(viewModel).Value, Is.InRange(32, 60));
			lastNames.Add(LastName(viewModel).Text);
		}

		Assert.That(professions, Is.EquivalentTo(Profession(viewModel).Options));
		Assert.That(firstNames, Is.EquivalentTo(new[] { "John", "Osric" }));
		Assert.That(lastNames, Is.EquivalentTo(new[] { "Green", "Hill", "Wood", "Brook", "Field", "Stone", "Marsh", "Reed", "Ford", "Lane" }));
		Assert.That(heights, Is.EquivalentTo(Height(viewModel).Options));
	}

	[Test]
	public void RandomizingTheLastNamePicksAnOtherName()
	{
		var viewModel = CreateViewModel();
		var usual = LastName(viewModel).Text;
		Assert.That(usual, Is.EqualTo(Profession(viewModel).SelectedOption!.Id == "smith" ? "Smith" : "Miller"));

		for (var i = 0; i < 20; i++)
		{
			LastName(viewModel).Randomize();

			Assert.That(LastName(viewModel).Text, Is.AnyOf("Green", "Hill", "Wood", "Brook", "Field", "Stone", "Marsh", "Reed", "Ford", "Lane"));
		}

		// A randomized name isn't a custom one, so randomizing everything goes back to the usual name.
		viewModel.RandomizeAll();
		Assert.That(LastName(viewModel).Text, Is.EqualTo(Profession(viewModel).SelectedOption!.Id == "smith" ? "Smith" : "Miller"));
	}

	[Test]
	public void RandomizeAllKeepsCustomStrings()
	{
		var viewModel = CreateViewModel();
		FirstName(viewModel).Text = "Bob";
		LastName(viewModel).Text = "Atwood";

		for (var i = 0; i < 20; i++)
		{
			viewModel.RandomizeAll();

			Assert.That(FirstName(viewModel).Text, Is.EqualTo("Bob"));
			Assert.That(LastName(viewModel).Text, Is.EqualTo("Atwood"));
			Assert.That(Age(viewModel).Value, Is.InRange(32, 60));
			Assert.That(viewModel.CanStartGame(), Is.True);
		}
	}

	[Test]
	public void RandomizingAFieldReplacesACustomString()
	{
		var viewModel = CreateViewModel();
		FirstName(viewModel).Text = "Bob";

		FirstName(viewModel).Randomize();

		Assert.That(FirstName(viewModel).Text, Is.AnyOf("John", "Osric"));
	}

	[Test]
	public void StringWithoutDefaultsMustBeFilledIn()
	{
		var gameData = new GameData(
			new CharacterData
			{
				Templates = TestData.CreateTemplates("{name}", "{name}"),
				Properties = [new CharacterProperty { Id = "name", Title = "Name", Type = CharacterPropertyType.String }],
			},
			TestData.CreateLocationList(), TestData.CreateProfessionList(), TestData.CreateFamilySettings());
		var viewModel = new NewGameViewModel(gameData, new Random(0));
		var editor = (StringPropertyEditorViewModel) viewModel.Properties.Single();

		Assert.That(editor.HasDefaults, Is.False);
		Assert.That(editor.CanRandomize(), Is.False);
		Assert.That(editor.Text, Is.Empty);
		Assert.That(viewModel.CanStartGame(), Is.False);

		editor.Text = "Hugh";
		Assert.That(viewModel.CanStartGame(), Is.True);

		editor.Text = " ";
		Assert.That(editor.Text, Is.Empty);
		Assert.That(viewModel.CanStartGame(), Is.False);
	}

	[Test]
	public void StartGameReportsSettings()
	{
		var viewModel = CreateViewModel();
		NewGameSettings? settings = null;
		viewModel.StartGameRequested += (_, e) => settings = e.Value;

		SetProfession(viewModel, "miller");
		FirstName(viewModel).Text = " Hugh ";
		Height(viewModel).SelectedOption = Height(viewModel).Options[0];
		Age(viewModel).Value = 40;
		viewModel.StartGame();

		Assert.That(settings, Is.Not.Null);
		var player = settings!.Player;
		Assert.That(player.GetOption("profession").Id, Is.EqualTo("miller"));
		Assert.That(player.GetString("firstName"), Is.EqualTo("Hugh"));
		Assert.That(player.GetString("lastName"), Is.EqualTo("Miller"));
		Assert.That(player.GetOption("height").Id, Is.EqualTo("tall"));
		Assert.That(player.GetInteger("age"), Is.EqualTo(40));
		Assert.That(player.GetOption("gender").Id, Is.EqualTo("male"));
	}

	[Test]
	public void ConditionalPropertiesAreAvailableWhenTheyApply()
	{
		var viewModel = new NewGameViewModel(TestData.CreateConditionalGameData(), new Random(0));
		var gender = (ValueListPropertyEditorViewModel) viewModel.FindProperty("gender")!;
		var build = (ValueListPropertyEditorViewModel) viewModel.FindProperty("build")!;
		var beard = viewModel.FindProperty("beard")!;
		var buildChanges = new List<string?>();
		var beardChanges = new List<string?>();
		build.PropertyChanged += (_, e) => buildChanges.Add(e.PropertyName);
		beard.PropertyChanged += (_, e) => beardChanges.Add(e.PropertyName);

		gender.SelectedOption = gender.Options.Single(x => x.Id == "male");
		Assert.That(beard.IsAvailable, Is.True);
		Assert.That(beard.CanRandomize(), Is.True);
		Assert.That(build.Options.Select(x => x.Id), Is.EqualTo(new[] { "brawny", "wiry" }));

		buildChanges.Clear();
		beardChanges.Clear();
		gender.SelectedOption = gender.Options.Single(x => x.Id == "female");
		Assert.That(beard.IsAvailable, Is.False);
		Assert.That(beard.IsFixed, Is.False);
		Assert.That(beard.ValueText, Is.Empty);
		Assert.That(beard.CanRandomize(), Is.False);
		Assert.That(((ValueListPropertyEditorViewModel) beard).SelectedOption, Is.Null);
		Assert.That(beardChanges, Does.Contain(nameof(CharacterPropertyEditorViewModel.IsAvailable)));
		Assert.That(build.Options.Select(x => x.Id), Is.EqualTo(new[] { "slender", "wiry" }));
		Assert.That(build.SelectedOption, Is.AnyOf(build.Options.ToArray()));
		Assert.That(buildChanges, Does.Contain(nameof(ValueListPropertyEditorViewModel.Options)));

		NewGameSettings? settings = null;
		viewModel.StartGameRequested += (_, e) => settings = e.Value;
		viewModel.StartGame();
		Assert.That(settings!.Player.HasValue("beard"), Is.False);
		Assert.That(settings.Player.GetOption("build").Id, Is.AnyOf("slender", "wiry"));
	}

	private static void SetProfession(NewGameViewModel viewModel, string id)
	{
		var editor = Profession(viewModel);
		editor.SelectedOption = editor.Options.Single(x => x.Id == id);
	}

	private static ValueListPropertyEditorViewModel Profession(NewGameViewModel viewModel) => (ValueListPropertyEditorViewModel) viewModel.FindProperty("profession")!;

	private static StringPropertyEditorViewModel FirstName(NewGameViewModel viewModel) => (StringPropertyEditorViewModel) viewModel.FindProperty("firstName")!;

	private static StringPropertyEditorViewModel LastName(NewGameViewModel viewModel) => (StringPropertyEditorViewModel) viewModel.FindProperty("lastName")!;

	private static ValueListPropertyEditorViewModel Height(NewGameViewModel viewModel) => (ValueListPropertyEditorViewModel) viewModel.FindProperty("height")!;

	private static IntegerPropertyEditorViewModel Age(NewGameViewModel viewModel) => (IntegerPropertyEditorViewModel) viewModel.FindProperty("age")!;

	private static NewGameViewModel CreateViewModel() => new(TestData.CreateGameData(), new Random(0));
}
