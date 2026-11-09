using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Windows.Main;

namespace OperaEtDies.Test.Windows.Main;

[TestFixture]
public sealed class MainWindowViewModelTests
{
	[Test]
	public void StartsInStartingLocation()
	{
		using var viewModel = CreateViewModel();

		Assert.That(viewModel.PlayerLocation.Id, Is.EqualTo("home"));
		Assert.That(viewModel.CurrentLocation.Title, Is.EqualTo("home title"));
		Assert.That(viewModel.CurrentLocation.Description, Is.EqualTo("home medium"));
		Assert.That(viewModel.CurrentLocation.Connections.Select(x => x.Title), Is.EqualTo(new[] { "lane title" }));
		Assert.That(viewModel.CurrentLocation.Connections.Select(x => x.Description), Is.EqualTo(new[] { "lane short" }));
	}

	[Test]
	public void ExamineShowsLongDescription()
	{
		using var viewModel = CreateViewModel();
		var location = viewModel.CurrentLocation;

		Assert.That(location.IsExamined, Is.False);
		Assert.That(location.CanExamine(), Is.True);

		location.Examine();

		Assert.That(location.IsExamined, Is.True);
		Assert.That(location.CanExamine(), Is.False);
		Assert.That(location.LongDescription, Is.EqualTo(new[] { "home long 1", "home long 2", "home long 3" }));
	}

	[Test]
	public void TravelMovesPlayer()
	{
		using var viewModel = CreateViewModel();
		var changed = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
		viewModel.CurrentLocation.Examine();

		viewModel.CurrentLocation.Connections[0].Travel();

		Assert.That(viewModel.PlayerLocation.Id, Is.EqualTo("lane"));
		Assert.That(viewModel.CurrentLocation.Title, Is.EqualTo("lane title"));
		Assert.That(viewModel.CurrentLocation.IsExamined, Is.False);
		Assert.That(viewModel.CurrentLocation.Connections.Select(x => x.Title), Is.EqualTo(new[] { "home title", "cottage title" }));
		Assert.That(changed, Does.Contain(nameof(MainWindowViewModel.CurrentLocation)));

		viewModel.CurrentLocation.Connections[1].Travel();

		Assert.That(viewModel.PlayerLocation.Id, Is.EqualTo("cottage"));
	}

	[Test]
	public void GeneratesThreeNpcs()
	{
		var locations = new HashSet<string>();
		for (var seed = 0; seed < 50; seed++)
		{
			using var viewModel = CreateViewModel(seed);

			Assert.That(viewModel.Npcs, Has.Count.EqualTo(3));
			foreach (var npc in viewModel.Npcs)
			{
				Assert.That(npc.Traits.Age, Is.InRange(16, 70));
				locations.Add(npc.Location.Id);
			}
		}

		Assert.That(locations, Is.EquivalentTo(new[] { "home", "lane", "cottage" }));
	}

	[Test]
	public void LocationShowsCharactersInIt()
	{
		using var viewModel = CreateViewModel();

		foreach (var id in new[] { "lane", "cottage", "lane", "home" })
		{
			viewModel.TravelTo(viewModel.GameData.Locations.GetLocation(id)!);

			var expected = viewModel.Npcs.Where(x => x.Location.Id == id).Select(x => x.Traits.FullName);
			Assert.That(viewModel.CurrentLocation.Characters.Select(x => x.Name), Is.EqualTo(expected));
			Assert.That(viewModel.CurrentLocation.HasCharacters, Is.EqualTo(expected.Any()));
		}
	}

	[Test]
	public void ExamineCharacterShowsDescription()
	{
		var gameData = TestData.CreateGameData();
		var traits = new CharacterTraits(Gender.Female, "Alice", "Miller", gameData.Professions.Professions[1], gameData.CharacterOptions.Heights[0], gameData.CharacterOptions.Weights[2], 34);
		var character = new CharacterViewModel(new Character(traits, gameData.Locations.Locations[0]));

		Assert.That(character.Name, Is.EqualTo("Alice Miller"));
		Assert.That(character.IsExamined, Is.False);
		Assert.That(character.CanExamine(), Is.True);

		character.Examine();

		Assert.That(character.IsExamined, Is.True);
		Assert.That(character.CanExamine(), Is.False);
		Assert.That(character.Description, Is.EqualTo(OurResources.CharacterDescription.FormatCurrentCulture("Alice Miller", OurResources.GenderNounFemale, 34, OurResources.GenderPronounFemale, "tall", "heavy", "a miller")));
	}

	private static MainWindowViewModel CreateViewModel(int seed = 0)
	{
		var gameData = TestData.CreateGameData();
		var player = new CharacterTraits(Gender.Male, "John", "Smith", gameData.Professions.Professions[0], gameData.CharacterOptions.Heights[0], gameData.CharacterOptions.Weights[0], 40);
		var settings = new NewGameSettings(player);
		return new MainWindowViewModel(TestData.CreateGameInfo(), gameData, settings, new Random(seed));
	}
}
