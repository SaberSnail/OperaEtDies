using OperaEtDies.Controls.Characters;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Test.GameState;

namespace OperaEtDies.Test.Controls.Characters;

[TestFixture]
public sealed class CharacterViewModelTests
{
	[Test]
	public void ThePlayerSeesTheirOwnRelationshipsFromTheClosestDown()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var settings = world.GameData.Families.Relationships;
		var player = world.Player;

		var viewModel = new CharacterViewModel(player, world, isDeveloper: false);

		var expected = player.Relationships.OrderByDescending(x => x.Value.Score).Select(x => x.Value.Score).ToList();
		Assert.That(viewModel.Relationships, Has.Count.EqualTo(world.People.Count - 1));
		Assert.That(viewModel.Relationships!.Select(x => x.IsDeveloper), Is.All.False);
		Assert.That(viewModel.Relationships.Select(x => x.Score), Is.EqualTo(expected));
		Assert.That(viewModel.Relationships.Select(x => (x.Level, x.IsFriend)), Is.EqualTo(expected.Select(x => (settings.GetLevel(x).Title, settings.IsFriend(x)))));
		Assert.That((viewModel.IsRelationshipsShown, viewModel.IsRelationshipsDeveloperOnly), Is.EqualTo((true, false)));

		// Developers also see the scores.
		var developer = new CharacterViewModel(player, world, isDeveloper: true);
		Assert.That(developer.Relationships!.Select(x => x.IsDeveloper), Is.All.True);

		// Only developers can change a score, which changes what the relationship is called.
		var readOnlyRow = viewModel.Relationships![0];
		readOnlyRow.Score = 0;
		Assert.That(readOnlyRow.Score, Is.EqualTo(expected[0]));
		var row = developer.Relationships![0];
		var changed = new List<string?>();
		row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
		row.Score = 150;
		Assert.That((row.Score, row.Level, row.IsFriend), Is.EqualTo((100, settings.GetLevel(100).Title, true)));
		row.Score = 0;
		Assert.That((row.Score, row.Level, row.IsFriend), Is.EqualTo((0, settings.GetLevel(0).Title, false)));
		Assert.That(player.Relationships.Values.Count(x => x.Score == 0), Is.GreaterThanOrEqualTo(1));
		Assert.That(changed, Does.Contain(nameof(row.Level)).And.Contain(nameof(row.IsFriend)));
		Assert.That((developer.IsRelationshipsShown, developer.IsRelationshipsDeveloperOnly), Is.EqualTo((true, false)));
	}

	[Test]
	public void OnlyDevelopersSeeOtherPeoplesRelationships()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		var person = world.Npcs[0];

		var viewModel = new CharacterViewModel(person, world, isDeveloper: false);
		Assert.That(viewModel.Relationships, Is.Null);
		Assert.That((viewModel.IsRelationshipsShown, viewModel.IsRelationshipsDeveloperOnly), Is.EqualTo((false, false)));

		var developer = new CharacterViewModel(person, world, isDeveloper: true);
		Assert.That(developer.Relationships!.Select(x => x.Score), Is.Ordered.Descending);
		Assert.That(developer.Relationships, Has.Count.EqualTo(world.People.Count - 1));
		Assert.That((developer.IsRelationshipsShown, developer.IsRelationshipsDeveloperOnly), Is.EqualTo((false, true)));
	}

	[Test]
	public void DevelopersSeeWhoSomeoneHasSeen()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var (schedules, calendar) = (world.GameData.Schedules, world.GameData.Calendar);
		var (person, other, teller) = (world.Npcs[0], world.Npcs[1], world.Npcs[2]);
		var quarter = world.Locations.First(x => x.Template is QuarterTemplate);
		var laundry = schedules.Needs.Single(x => x.Id == "laundry");
		var (name, place, clock) = (other.FullName, quarter.Title, calendar.FormatClock(world.Time));

		Assert.That(new CharacterViewModel(person, world, isDeveloper: false).Sightings, Is.Null);
		var developer = new CharacterViewModel(person, world, isDeveloper: true);
		Assert.That(developer.Sightings, Has.Count.EqualTo(person.Sightings.Count));
		Assert.That(developer.IsSightingsEmpty, Is.EqualTo(person.Sightings.Count == 0));

		Assert.That(Format(new Sighting(quarter, world.Time, SightingActivity.Busy, laundry)), Is.EqualTo($"{name}: Washing at {place}, {clock}"));
		Assert.That(Format(new Sighting(other.Home, world.Time, SightingActivity.Busy, laundry)), Is.EqualTo($"{name}: Hanging out the washing at {other.Home.Title}, {clock}"));
		Assert.That(Format(new Sighting(quarter, world.Time, SightingActivity.None)), Is.EqualTo($"{name}: at {place}, {clock}"));
		Assert.That(Format(new Sighting(quarter, world.Time, SightingActivity.Walking, Destination: other.Home)), Is.EqualTo($"{name}: walking from {place} to {other.Home.Title}, {clock}"));
		Assert.That(Format(new Sighting(quarter, world.Time, SightingActivity.Left, Destination: other.Home, Source: teller)), Is.EqualTo($"{name}: left {place} for {other.Home.Title}, {clock} (told by {teller.FullName})"));

		string Format(Sighting sighting) => CharacterViewModel.FormatSighting(other, sighting, schedules, calendar);
	}

	[Test]
	public void FamilyAndNeighborsGetABonus()
	{
		var settings = new RelationshipSettings
		{
			Score = new IntegerRange { Minimum = 50, Maximum = 50, Shape = new WeightShape { Peak = ShapePeak.Middle, MostWithin = 1 } },
			FamilyBonus = 20,
			SpouseBonus = 30,
			ElderBonus = 15,
			ElderInLawBonus = 10,
			NeighborBonus = 10,
			FriendScore = 75,
			Levels = [new RelationshipLevel { Minimum = 0, Title = "Cold" }, new RelationshipLevel { Minimum = 75, Title = "Friend" }],
		};
		var random = new Random(0);

		Assert.That(settings.ChooseScore(random, null, isNeighbor: false), Is.EqualTo(50));
		Assert.That(settings.ChooseScore(random, FamilyTie.Other, isNeighbor: false), Is.EqualTo(70));
		Assert.That(settings.ChooseScore(random, null, isNeighbor: true), Is.EqualTo(60));
		Assert.That(settings.ChooseScore(random, FamilyTie.Other, isNeighbor: true), Is.EqualTo(70));
		Assert.That(settings.ChooseScore(random, FamilyTie.Spouse, isNeighbor: false), Is.EqualTo(80));
		Assert.That(settings.ChooseScore(random, FamilyTie.Elder, isNeighbor: false), Is.EqualTo(65));
		Assert.That(settings.ChooseScore(random, FamilyTie.ElderInLaw, isNeighbor: true), Is.EqualTo(60));
		Assert.That(settings.ChooseScore(random, FamilyTie.Spouse, isNeighbor: true), Is.EqualTo(80));
		Assert.That((settings.IsFriend(74), settings.IsFriend(75)), Is.EqualTo((false, true)));
		Assert.That((settings.GetLevel(0).Title, settings.GetLevel(74).Title, settings.GetLevel(100).Title), Is.EqualTo(("Cold", "Cold", "Friend")));
	}

	[Test]
	public void HouseholdTiesComeFromRelationsToTheHead()
	{
		var relations = TestData.CreateFamilySettings().HeadRelations;

		Assert.That(relations.GetFamilyTie("self", "husband"), Is.EqualTo(FamilyTie.Spouse));
		Assert.That(relations.GetFamilyTie("son", "son"), Is.EqualTo(FamilyTie.Spouse));
		Assert.That(relations.GetFamilyTie("self", "son"), Is.EqualTo(FamilyTie.Elder));
		Assert.That(relations.GetFamilyTie("sonInLaw", "husband"), Is.EqualTo(FamilyTie.Elder));
		Assert.That(relations.GetFamilyTie("father", "daughterInLaw"), Is.EqualTo(FamilyTie.Elder));
		Assert.That(relations.GetFamilyTie("self", "sonInLaw"), Is.EqualTo(FamilyTie.ElderInLaw));
		Assert.That(relations.GetFamilyTie("husband", "daughter"), Is.EqualTo(FamilyTie.ElderInLaw));
		Assert.That(relations.GetFamilyTie("self", "father"), Is.EqualTo(FamilyTie.Other));
		Assert.That(relations.GetFamilyTie("mother", "mother"), Is.EqualTo(FamilyTie.Other));
	}
}
