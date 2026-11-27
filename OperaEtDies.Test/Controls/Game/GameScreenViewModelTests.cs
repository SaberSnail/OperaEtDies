using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Controls;
using OperaEtDies.Controls.Characters;
using OperaEtDies.Controls.Log;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Controls.Map;
using OperaEtDies.Controls.Start;
using OperaEtDies.Controls.Status;
using OperaEtDies.Controls.Villagers;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Test.GameState;
using OperaEtDies.Town;
using OperaEtDies.Controls.Game;

namespace OperaEtDies.Test.Controls.Game;

[TestFixture]
public sealed class GameScreenViewModelTests
{
	[Test]
	public void StartsAtHome()
	{
		using var viewModel = CreateViewModel();
		var home = viewModel.World.Player.Home;

		Assert.That(viewModel.CurrentLocation.Location, Is.SameAs(viewModel.World.Player.Location));
		Assert.That(viewModel.CurrentLocation.Location, Is.SameAs(home));
		Assert.That(viewModel.CurrentLocation.Title, Is.EqualTo(home.Title));
		Assert.That(viewModel.CurrentLocation.Description, Is.EqualTo(home.MediumDescription));
		Assert.That(viewModel.CurrentLocation.Connections.Select(x => x.Title), Is.EqualTo(home.Connections.Select(x => x.Title)));
		Assert.That(viewModel.CurrentLocation.Connections.Select(x => x.Description), Is.EqualTo(home.Connections.Select(x => x.ShortDescription)));
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
		Assert.That(location.LongDescription, Is.EqualTo(viewModel.World.Player.Home.LongDescription));
	}

	[Test]
	public void TravelMovesPlayer()
	{
		using var viewModel = CreateViewModel();
		var home = viewModel.World.Player.Home;
		var changed = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
		viewModel.CurrentLocation.Examine();

		viewModel.CurrentLocation.Connections[0].Travel();

		var quarter = home.Connections[0];
		Assert.That(viewModel.World.Player.Location, Is.SameAs(quarter));
		Assert.That(viewModel.CurrentLocation.Title, Is.EqualTo(quarter.Title));
		Assert.That(viewModel.CurrentLocation.IsExamined, Is.False);
		Assert.That(viewModel.CurrentLocation.Connections.Select(x => x.Location), Does.Contain(home));
		Assert.That(changed, Does.Contain(nameof(GameScreenViewModel.CurrentLocation)));
	}

	[Test]
	public void WalkingBetweenQuartersTakesTime()
	{
		using var viewModel = CreateViewModel();
		var world = viewModel.World;
		var calendar = world.GameData.Calendar;
		var start = world.Time;

		// Stepping out of the house into its quarter takes a minute.
		viewModel.TravelTo(world.Player.Home.Quarter!);
		Assert.That(world.Time, Is.EqualTo(start + calendar.CreateTimeOffset(0, 0, 1)));
		start = world.Time;

		var quarter = world.Player.Location;
		var next = quarter.Exits.First(x => x.Direction is not null).Location;
		viewModel.TravelTo(next);

		Assert.That(world.Time, Is.EqualTo(start + world.GetTravelTime(quarter, next)));
		Assert.That(world.Time, Is.GreaterThan(start));
		Assert.That(calendar.GetParts(world.Time - start).Minutes, Is.EqualTo(2).Or.EqualTo(5));
	}

	[Test]
	public void ConnectionsShowHowLongTheyTake()
	{
		using var viewModel = CreateViewModel();
		var world = viewModel.World;

		// From the house, the only way out is into its quarter.
		Assert.That(viewModel.CurrentLocation.Places.Select(x => x.TravelTime), Is.EqualTo(new[] { "1 minute" }));

		viewModel.TravelTo(world.Player.Home.Quarter!);
		var quarter = world.Player.Location;
		Assert.Multiple(() =>
		{
			foreach (var path in viewModel.CurrentLocation.Paths)
			{
				var minutes = world.GameData.Calendar.GetParts(world.GetTravelTime(quarter, path.Location)).Minutes;
				Assert.That(path.TravelTime, Is.EqualTo($"{minutes} minutes"));
				Assert.That(path.TravelTimeText, Is.EqualTo($"({minutes} minutes)"));
			}
			Assert.That(viewModel.CurrentLocation.Places.Select(x => x.TravelTime), Is.All.EqualTo("1 minute"));
		});
	}

	[Test]
	public void LocationShowsPeopleInIt()
	{
		using var viewModel = CreateViewModel();
		var gameState = viewModel.World;

		foreach (var location in gameState.Locations)
		{
			viewModel.TravelTo(location);

			var expected = gameState.Npcs.Where(x => x.Location == location).Select(x => x.FullName);
			Assert.That(viewModel.CurrentLocation.People.Select(x => x.Name), Is.EquivalentTo(expected));
			Assert.That(viewModel.CurrentLocation.HasPeople, Is.EqualTo(expected.Any()));
		}
	}

	[Test]
	public void ShowVillagersListsEveryoneByFamily()
	{
		using var viewModel = CreateViewModel();
		var changed = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		viewModel.ShowVillagers();

		var villagers = viewModel.Top<VillagersViewModel>()!;
		var families = viewModel.World.Families.OrderBy(x => x.LastName, StringComparer.CurrentCulture).ToList();
		Assert.That(viewModel.Sheets, Is.EqualTo(new[] { villagers }));
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.CurrentLocation));
		Assert.That(changed, Does.Contain(nameof(GameScreenViewModel.TopSheet)).And.Contain(nameof(GameScreenViewModel.IsVillagersOpen)));
		Assert.That(viewModel.IsVillagersOpen, Is.True);
		// Families are sorted by last name.
		Assert.That(villagers.Families.Select(x => x.Family), Is.EqualTo(families));
		Assert.That(villagers.Families.Select(x => x.Title), Is.EqualTo(families.Select(x => $"The {x.LastName} Family")));
		foreach (var family in villagers.Families)
			Assert.That(family.People.Select(x => x.Person), Is.EqualTo(family.Family.Members));

		// Showing them again replaces them with a new sheet.
		viewModel.ShowVillagers();
		Assert.That(viewModel.Sheets, Has.Count.EqualTo(1));
	}

	[Test]
	public void ShowStatusListsThePlayersNeeds()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		viewModel.ShowMap();
		viewModel.ShowStatus();

		var status = viewModel.Top<StatusViewModel>()!;
		var world = viewModel.World;
		Assert.That(viewModel.Sheets, Is.EqualTo(new[] { status }));
		var fatigue = status.Needs.Single(x => x.Title == "Fatigue");
		Assert.That(fatigue.Value, Does.StartWith("Rested (").And.EndsWith(" of 100)"));
		var upkeep = status.Needs.Single(x => x.Title == "Upkeep");
		var quota = 30 + (15 * world.Player.Family.Members.Count);
		Assert.That(upkeep.Value, Does.EndWith($" of {world.GameData.Calendar.FormatOffsetFrom(world.Time, world.GameData.Calendar.CreateTimeOffset(0, 0, quota), TimeFormat.Long)} today"));
		Assert.That(upkeep.Note, Is.EqualTo(OurResources.NeedStatusShared));
		Assert.That(status.Needs.Any(x => x.Title == "Work"), Is.EqualTo(world.Player.Business is not null));

		// Trips are counted in visits, with how long each takes.
		var members = world.Player.Family.Members;
		var water = world.GameData.Schedules.Needs.Single(x => x.Id == "water");
		var visits = members.Sum(x => x.GetProgress(water)) / 5;
		Assert.That(status.Needs.Single(x => x.Title == "Water").Value, Is.EqualTo($"{Math.Min(visits, members.Count)} of {members.Count} visits today (5 minutes each)"));
		Assert.That(status.Needs.Single(x => x.Title == "Water").IsDone, Is.EqualTo(visits >= members.Count));
		Assert.That(fatigue.IsDone, Is.False);

		// Opening the villagers replaces it, and closing it shows the location.
		viewModel.ShowVillagers();
		Assert.That(viewModel.Top<StatusViewModel>(), Is.Null);
		viewModel.ShowStatus();
		viewModel.CloseTopSheet();
		Assert.That((viewModel.TopSheet, viewModel.HasSheets), Is.EqualTo(((object?) null, false)));
	}

	[Test]
	public void DoneChoresAreMarkedAndNoLongerOffered()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var upkeep = world.GameData.Schedules.Needs.Single(x => x.Id == "upkeep");
		foreach (var member in world.Player.Family.Members)
		{
			member.UpdateNeeds(world.Time);
			member.SetProgress(upkeep, 0);
			if (!member.IsPlayer)
				member.SetPlan(null, []);
		}

		// With less time left than a shift, it takes what's left.
		var quota = 30 + (15 * world.Player.Family.Members.Count);
		world.Player.SetProgress(upkeep, quota - 10);
		var tidy = GetStart(viewModel, "upkeep");
		Assert.That(tidy.Action!.Text, Is.EqualTo("Until done (10 minutes)"));

		// Started at home, tidying up is the next step, and takes the time left.
		tidy.Action!.Do();
		var steps = viewModel.CurrentLocation.Steps!;
		Assert.That(steps.Action!.Text, Does.StartWith("Tidy up until ").And.EndWith(" (10 minutes)"));
		steps.Action.Do();

		// Once it's done, it can't be started again today, and the status page says so.
		var done = GetStart(viewModel, "upkeep");
		Assert.That((done.Action, done.Note), Is.EqualTo(((SplitActionViewModel?) null, "Done for today")));
		viewModel.ShowStatus();
		Assert.That(viewModel.Top<StatusViewModel>()!.Needs.Single(x => x.Title == "Upkeep").IsDone, Is.True);
	}

	[Test]
	public void StartingSomethingShowsItsStepsUntilStopped()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var player = world.Player;
		var square = world.Locations.First(x => x.Template is QuarterTemplate && x.QuarterKind == "square");
		var water = world.GameData.Schedules.Needs.Single(x => x.Id == "water");
		player.SetProgress(water, 0);
		viewModel.TravelTo(square);

		// Started at the square, fetching water is next, and the start page says it's in progress.
		GetStart(viewModel, "water").Action!.Do();
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.CurrentLocation));
		Assert.That(player.Current?.Need, Is.EqualTo(water));
		viewModel.ShowStart();
		Assert.That(viewModel.StartScreen!.InProgress, Is.EqualTo("In progress: Water. Starting something else puts it on hold."));
		viewModel.CloseStart();

		// Stopping gives it up.
		viewModel.CurrentLocation.Steps!.Stop();
		Assert.That((player.Current, viewModel.CurrentLocation.Steps), Is.EqualTo(((StepSequence?) null, (PlayerStepsViewModel?) null)));
		Assert.That(viewModel.CurrentLocation.WaitResult, Does.StartWith("You stopped "));
	}

	[Test]
	public void PathsToThePlayersHomeAreMarked()
	{
		using var viewModel = CreateViewModel();
		var player = viewModel.World.Player;
		Assert.That(viewModel.CurrentLocation.Connections.All(x => x.PlaceMarker is null), Is.True);

		// Home is marked with an icon, which the button's tooltip explains.
		viewModel.TravelTo(player.Home.Quarter!);
		var home = viewModel.CurrentLocation.Connections.Single(x => x.Location == player.Home);
		Assert.That((home.PlaceMarker, home.Markers.Single(), home.Action.ToolTip), Is.EqualTo((ExitMarker.Home, ExitMarker.Home, "Your home")));
		Assert.That(viewModel.CurrentLocation.Connections.Where(x => x.PlaceMarker is not null).Select(x => x.Location),
			Is.SubsetOf(new[] { player.Home, player.Workplace }));
	}

	[Test]
	public void MapsMarkThePlayersHome()
	{
		using var viewModel = CreateViewModel();
		var player = viewModel.World.Player;
		viewModel.ShowMap();

		Assert.That(viewModel.Top<TownMapViewModel>()!.Quarters.Where(x => x.HasHome).Select(x => x.Quarter), Is.EqualTo(new[] { player.Home.Quarter }));
		Assert.That(viewModel.Top<TownMapViewModel>()!.Quarters.Where(x => x.HasWorkplace).Select(x => x.Quarter),
			Is.EqualTo(player.Business is null || player.Workplace == player.Home ? Array.Empty<Location>() : new[] { player.Workplace.Quarter }));
		Assert.That(viewModel.CharacterPanel.MiniMap.Quarters.Where(x => x.HasHome).Select(x => x.Quarter), Is.EqualTo(new[] { player.Home.Quarter }));
	}

	[Test]
	public void FetchingWaterTellsThePlayerToTakeItHome()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var square = world.Locations.First(x => x.Template is QuarterTemplate && x.QuarterKind == "square");
		var water = world.GameData.Schedules.Needs.Single(x => x.Id == "water");
		foreach (var member in world.Player.Family.Members)
		{
			member.UpdateNeeds(world.Time);
			member.SetProgress(water, 0);
			if (!member.IsPlayer)
				member.SetPlan(null, []);
		}
		viewModel.TravelTo(square);

		var start = GetStart(viewModel, "water");
		Assert.That(start.Action!.Text, Is.EqualTo("Start"));
		start.Action!.Do();
		var fetch = viewModel.CurrentLocation.Steps!.Action!;
		Assert.That(fetch.Text, Does.StartWith("Fetch water until ").And.EndWith(" (5 minutes)"));
		fetch.Do();

		Assert.That(viewModel.CurrentLocation.WaitResult, Is.EqualTo("You fetched water for 5 minutes. Take it home to finish."));
		// Taking it home is next, which the player does themselves, so there's no button for it.
		var steps = viewModel.CurrentLocation.Steps!;
		Assert.That((steps.Action, steps.Next), Is.EqualTo(((PlayerStepActionViewModel?) null, "Next: Take it home to finish.")));
	}

	[Test]
	public void StepsChooseTheDestinationAndReplacingItAsksFirst()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var player = world.Player;
		var square = world.Locations.First(x => x.Template is QuarterTemplate && x.QuarterKind == "square");
		var water = world.GameData.Schedules.Needs.Single(x => x.Id == "water");
		foreach (var member in player.Family.Members)
		{
			member.UpdateNeeds(world.Time);
			member.SetProgress(water, 0);
			if (!member.IsPlayer)
				member.SetPlan(null, []);
		}
		viewModel.TravelTo(square);
		GetStart(viewModel, "water").Action!.Do();
		viewModel.CurrentLocation.Steps!.Action!.Do();

		// Taking the water home makes home the destination, and the way there is marked.
		Assert.That(player.Destination, Is.SameAs(player.Home));
		Assert.That(world.IsPlayerDestinationFromSteps, Is.True);
		Assert.That(viewModel.CurrentLocation.Connections.Where(x => x.DestinationMarker is not null).Select(x => x.Location), Is.EqualTo(new[] { world.GetPlayerWayToDestination() }));
		Assert.That(viewModel.Concerns.Destination, Does.StartWith(player.Home.Title));

		// Choosing somewhere else from a sheet asks first, and keeping going changes nothing.
		var elsewhere = square.Connections.First(x => x != player.Home && x.Bounds is not null);
		viewModel.ShowMap();
		viewModel.Top<TownMapViewModel>()!.Quarters.Single(x => x.Quarter == elsewhere).ChooseDestination();
		Assert.That(viewModel.Concerns.DestinationQuestion, Does.Contain(elsewhere.Title));
		viewModel.Concerns.CancelDestination();
		Assert.That((player.Destination, viewModel.Concerns.IsAskingDestination), Is.EqualTo((player.Home, false)));

		// Confirming gives up the steps, which carry on once the sheets close.
		viewModel.Top<TownMapViewModel>()!.Quarters.Single(x => x.Quarter == elsewhere).ChooseDestination();
		viewModel.Concerns.ConfirmDestination();
		Assert.That(player.Destination, Is.SameAs(elsewhere));
		Assert.That(viewModel.Top<TownMapViewModel>()!.Quarters.Single(x => x.HasDestination).Quarter, Is.SameAs(elsewhere));
		Assert.That(player.Current, Is.Null);
		viewModel.CloseSheets();
		Assert.That(viewModel.CurrentLocation.Steps, Is.Null);
		Assert.That(world.IsPlayerDestinationFromSteps, Is.False);

		// Walking there takes the player all the way, which clears it.
		var time = world.Time;
		viewModel.CurrentLocation.WalkToDestination();
		Assert.That(player.Location, Is.SameAs(elsewhere));
		Assert.That(viewModel.CurrentLocation.Location, Is.SameAs(elsewhere));
		Assert.That(world.Time, Is.GreaterThan(time));
		Assert.That(player.Destination, Is.Null);
		Assert.That(viewModel.CurrentLocation.CanWalkToDestination(), Is.False);
	}

	[Test]
	public void TheWayToTheDestinationOffersWalkingAllTheWay()
	{
		using var viewModel = CreateViewModel();
		var world = viewModel.World;
		var player = world.Player;
		var square = world.Locations.First(x => x.Template is QuarterTemplate && x.QuarterKind == "square");
		viewModel.TravelTo(square);

		// A house that isn't next to the square is reached through another place, whose exit is marked as the way there.
		var target = world.Families.Select(x => x.Home).First(x => !square.Connections.Contains(x));
		world.SetPlayerDestination(target);
		var location = viewModel.CurrentLocation;
		var index = location.Connections.ToList().FindIndex(x => x.DestinationMarker is not null);
		var way = location.Connections[index];
		Assert.That((way.DestinationMarker, way.Location), Is.EqualTo((ExitMarker.TowardDestination, world.GetPlayerWayToDestination())));
		Assert.That(way.Action.ToolTip, Does.Contain(target.Title));
		Assert.That(location.Connections.Where(x => x != way).Select(x => x.Action.HasMenu), Is.All.False);

		// Shift and its number open its menu, which walks all the way there.
		Assert.That(viewModel.OpenChoiceMenu(index + 1), Is.True);
		Assert.That(way.Action.IsMenuOpen, Is.True);
		Assert.That(way.Action.Alternatives.Single().Text, Does.StartWith($"Walk all the way to {target.Title} ("));
		Assert.That(way.Action.ChooseAlternative(1), Is.True);
		Assert.That((player.Location, player.Destination), Is.EqualTo((target, (Location?) null)));
	}

	[Test]
	public void DestinationsCanBeChosenAndCleared()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var player = world.Player;
		var family = world.Families.First(x => x != player.Family);

		viewModel.ShowVillagers();
		viewModel.Top<VillagersViewModel>()!.Families.Single(x => x.Family == family).HeadHome();

		// Choosing it doesn't close the sheet, and it's marked on the mini-map and under Concerns.
		Assert.That(player.Destination, Is.SameAs(family.Home));
		Assert.That(viewModel.Top<VillagersViewModel>(), Is.Not.Null);
		Assert.That(viewModel.Concerns.HasDestination, Is.True);
		Assert.That(viewModel.CharacterPanel.MiniMap.Quarters.Where(x => x.HasDestination).Select(x => x.Quarter), Is.SubsetOf(new[] { family.Home.Quarter }));
		viewModel.CloseSheets();
		Assert.That(viewModel.CurrentLocation.CanWalkToDestination(), Is.True);

		// The exit that's the way there is marked, and unless it leads straight there, offers walking all the way there.
		var way = viewModel.CurrentLocation.Connections.Single(x => x.DestinationMarker is not null);
		Assert.That(way.Location, Is.SameAs(world.GetPlayerWayToDestination()));
		if (way.DestinationMarker == ExitMarker.TowardDestination)
		{
			Assert.That(way.Action.Alternatives.Single().Text, Does.StartWith($"Walk all the way to {family.Home.Title} ("));
			Assert.That(way.Action.ToolTip, Does.Contain(family.Home.Title));
		}
		else
		{
			Assert.That((way.Location, way.Action.HasMenu), Is.EqualTo((family.Home, false)));
		}

		viewModel.Concerns.ClearDestination();
		Assert.That(player.Destination, Is.Null);
		Assert.That(viewModel.Concerns.Destination, Is.EqualTo(OurResources.ConcernsNoDestination));
		Assert.That(viewModel.CurrentLocation.Connections.All(x => x.DestinationMarker is null), Is.True);
	}

	[Test]
	public async Task ThePlayerIsAskedWhetherToKeepGoingAsync()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var player = world.Player;
		var work = world.GameData.Schedules.Needs.Single(x => x.Id == "work");
		var loop = work.Steps.ToList().FindIndex(x => x.Kind == StepKind.Do);
		player.SetProgress(work, 0);
		player.SetPlan(work, [], startStep: loop + 1, goalMinutes: 60);
		await world.UpdatePlayerStepsAsync();

		var prompt = viewModel.Overlay as RepeatPromptViewModel;
		Assert.That(prompt, Is.Not.Null);
		Assert.That(prompt!.ContinueText, Does.StartWith("Keep going ("));

		// Stopping closes the prompt and says what the player stopped doing.
		prompt.Stop();
		Assert.That((viewModel.Overlay, player.Current), Is.EqualTo(((object?) null, (StepSequence?) null)));
		Assert.That(viewModel.CurrentLocation.WaitResult, Is.EqualTo("You stopped work."));
	}

	[Test]
	public async Task TheKeepGoingPromptSaysWhyThePassEndedAsync()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var player = world.Player;
		var work = world.GameData.Schedules.Needs.Single(x => x.Id == "work");
		var loop = work.Steps.ToList().FindIndex(x => x.Kind == StepKind.Do);
		player.SetProgress(work, 0);
		var sequence = player.SetPlan(work, [], startStep: loop + 1, goalMinutes: 60)!;

		// A step the steps carried on from just failed, ending the pass, so the player isn't interrupted with it, and is
		// told why when they're asked whether to keep going.
		var npc = world.Npcs[0];
		world.OnStepFailed(player, sequence, loop, FailCause.No, world.Time, npc, isEnding: false);
		await world.UpdatePlayerStepsAsync();

		var prompt = viewModel.Overlay as RepeatPromptViewModel;
		Assert.That(prompt?.Text, Does.StartWith($"{npc.FullName} said no. "));
	}

	[Test]
	public void ClosedMarketShowsWhenItIsOpen()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var square = world.Locations.First(x => x.Template is QuarterTemplate && x.QuarterKind == "square");
		viewModel.TravelTo(square);

		// The game starts on the first day, and the market is only open on the sixth.
		var market = GetStart(viewModel, "market");
		Assert.That(market.Title, Is.EqualTo("Market"));
		Assert.That(market.Action, Is.Null);
		Assert.That(market.Note, Is.EqualTo("Open on Day 6 from 7:00 to 14:00"));
		Assert.That(GetStart(viewModel, "water").Note, Is.Null);
	}

	[Test]
	public void ToolbarButtonsReplaceTheSheetsOrCloseTheirOwn()
	{
		using var viewModel = CreateViewModel(withSchedules: true);

		viewModel.ToggleVillagers();
		Assert.That(viewModel.Sheets, Is.EqualTo(new[] { viewModel.Top<VillagersViewModel>() }));
		Assert.That(viewModel.TimeRunner.IsHeld, Is.True);
		viewModel.ToggleVillagers();
		Assert.That(viewModel.HasSheets, Is.False);
		Assert.That(viewModel.TimeRunner.IsHeld, Is.False);
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.CurrentLocation));

		viewModel.ToggleStatus();
		Assert.That(viewModel.IsStatusOpen, Is.True);
		viewModel.ToggleStatus();
		Assert.That((viewModel.HasSheets, viewModel.IsStatusOpen), Is.EqualTo((false, false)));

		// Another sheet's button replaces the sheets rather than closing or stacking.
		viewModel.ToggleMap();
		viewModel.ToggleStatus();
		Assert.That(viewModel.Sheets, Is.EqualTo(new[] { viewModel.Top<StatusViewModel>() }));
		Assert.That((viewModel.IsMapOpen, viewModel.IsStatusOpen), Is.EqualTo((false, true)));

		// A button closes only its own sheet on top, showing the one under it.
		viewModel.ToggleVillagers();
		var villagers = viewModel.Top<VillagersViewModel>()!;
		viewModel.ShowCharacter(viewModel.World.Player);
		Assert.That(viewModel.IsPlayerCharacterOpen, Is.True);
		Assert.That(viewModel.Sheets, Has.Count.EqualTo(2));
		viewModel.TogglePlayerCharacter();
		Assert.That(viewModel.Sheets, Is.EqualTo(new[] { villagers }));

		// The button for returning to the center closes them all.
		viewModel.ShowCharacter(viewModel.World.Npcs[0]);
		Assert.That(viewModel.CanCloseSheets(), Is.True);
		viewModel.CloseSheets();
		Assert.That((viewModel.HasSheets, viewModel.CanCloseSheets()), Is.EqualTo((false, false)));
	}

	[Test]
	public void TimeIsHeldWhileASheetIsOpen()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var time = viewModel.World.Time;
		viewModel.ShowMap();

		// Nothing can pass time under a sheet.
		viewModel.CurrentLocation.Connections[0].Travel();
		Assert.That(viewModel.World.Time, Is.EqualTo(time));
		Assert.That(viewModel.TimeRunner.IsRunning, Is.False);

		viewModel.CloseTopSheet();
		viewModel.CurrentLocation.Connections[0].Travel();
		Assert.That(viewModel.World.Time, Is.GreaterThan(time));
	}

	[Test]
	public void TheWardrobeAndInventoryOpenInTheCenterClosingTheSheets()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		Assert.That(viewModel.World.Player.Location, Is.SameAs(viewModel.World.Player.Home));
		viewModel.ShowVillagers();

		viewModel.ToggleWardrobe();

		Assert.That(viewModel.HasSheets, Is.False);
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.Clothing));
		Assert.That((viewModel.IsWardrobeOpen, viewModel.IsInventoryOpen), Is.EqualTo((true, false)));

		// A sheet can open over it, and its button then closes the sheets rather than the wardrobe.
		viewModel.ToggleMap();
		viewModel.ToggleWardrobe();
		Assert.That(viewModel.HasSheets, Is.False);
		Assert.That(viewModel.IsWardrobeOpen, Is.True);
		viewModel.ToggleWardrobe();
		Assert.That(viewModel.Clothing, Is.Null);
	}

	[Test]
	public void TheLogOpensInASheet()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		viewModel.EventLog.OpenSheet();

		var sheet = viewModel.Top<LogSheetViewModel>()!;
		Assert.That(sheet.Log, Is.SameAs(viewModel.EventLog));
		viewModel.EventLog.OpenSheet();
		Assert.That(viewModel.HasSheets, Is.False);
	}

	[Test]
	public void EscClosesTheTopSheetAndTwiceClosesThemAll()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		viewModel.ShowVillagers();
		viewModel.ShowCharacter(viewModel.World.Npcs[0]);
		viewModel.ShowCharacter(viewModel.World.Npcs[1]);

		// Pressed slowly, each Esc closes the top sheet, then there's nothing for it to do.
		viewModel.EscapeRepeatTime = TimeSpan.Zero;
		Assert.That(viewModel.HandleEscape(), Is.True);
		Assert.That(viewModel.Sheets, Has.Count.EqualTo(2));
		Assert.That(viewModel.HandleEscape(), Is.True);
		Assert.That(viewModel.HandleEscape(), Is.True);
		Assert.That(viewModel.HasSheets, Is.False);
		Assert.That(viewModel.HandleEscape(), Is.False);

		// Pressed twice quickly, the second closes the rest, and a third does nothing rather than opening the main menu.
		viewModel.EscapeRepeatTime = TimeSpan.FromMinutes(1);
		viewModel.ShowVillagers();
		viewModel.ShowCharacter(viewModel.World.Npcs[0]);
		viewModel.ShowCharacter(viewModel.World.Npcs[1]);
		Assert.That(viewModel.HandleEscape(), Is.True);
		Assert.That(viewModel.Sheets, Has.Count.EqualTo(2));
		Assert.That(viewModel.HandleEscape(), Is.True);
		Assert.That(viewModel.HasSheets, Is.False);
		Assert.That(viewModel.HandleEscape(), Is.True);
	}

	[Test]
	public void LetterHotkeysOpenAndCloseSheetsAndViews()
	{
		using var viewModel = CreateViewModel(withSchedules: true);

		Assert.That(viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Villagers)), Is.True);
		Assert.That(viewModel.IsVillagersOpen, Is.True);
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Villagers));
		Assert.That(viewModel.HasSheets, Is.False);
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Character));
		Assert.That(viewModel.IsPlayerCharacterOpen, Is.True);
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Map));
		Assert.That(viewModel.Sheets, Is.EqualTo(new[] { viewModel.Top<TownMapViewModel>() }));
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Status));
		Assert.That(viewModel.Sheets, Is.EqualTo(new[] { viewModel.Top<StatusViewModel>() }));

		// What the player can start is in the center, so it doesn't open under a sheet.
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Start));
		Assert.That(viewModel.StartScreen, Is.Null);
		viewModel.CloseSheets();
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Start));
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.StartScreen));
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Start));
		Assert.That(viewModel.StartScreen, Is.Null);

		// The player starts at home, where the wardrobe is.
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Wardrobe));
		Assert.That(viewModel.IsWardrobeOpen, Is.True);
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Wardrobe));
		Assert.That(viewModel.Clothing, Is.Null);

		// J asks the view to move keyboard focus to Concerns, every time it's pressed.
		Assert.That(viewModel.ConcernsFocusRequest, Is.Null);
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Concerns));
		var request = viewModel.ConcernsFocusRequest;
		viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Concerns));
		Assert.That(viewModel.ConcernsFocusRequest, Is.Not.Null.And.Not.SameAs(request));

		// The window handles the rest.
		Assert.That(viewModel.HandleHotkey(new Hotkey(HotkeyCommand.QuickLoad)), Is.False);
	}

	[Test]
	public void NumberKeysTakeTheExitsAndAreOffUnderASheet()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var location = viewModel.CurrentLocation;
		Assert.That(location.Connections.Select(x => x.Badge), Is.EqualTo(Enumerable.Range(1, location.Connections.Count).Select(Hotkeys.GetChoiceKeyText)));

		viewModel.ShowMap();
		Assert.That(viewModel.ChooseNumber(1), Is.False);
		Assert.That(viewModel.World.Player.Location, Is.SameAs(location.Location));

		viewModel.CloseSheets();
		var target = location.Connections[0].Location;
		Assert.That(viewModel.HandleHotkey(new Hotkey(HotkeyCommand.Choice, 1)), Is.True);
		Assert.That(viewModel.World.Player.Location, Is.SameAs(target));
	}

	[Test]
	public void StepButtonsTakeTheNumbersLeftAfterTheExits()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var square = world.Locations.First(x => x.Template is QuarterTemplate && x.QuarterKind == "square");
		var water = world.GameData.Schedules.Needs.Single(x => x.Id == "water");
		foreach (var member in world.Player.Family.Members)
		{
			member.UpdateNeeds(world.Time);
			member.SetProgress(water, 0);
			if (!member.IsPlayer)
				member.SetPlan(null, []);
		}
		viewModel.TravelTo(square);

		// The start page numbers its buttons.
		var start = GetStart(viewModel, "water");
		Assert.That(viewModel.StartScreen!.Choices.Select(x => x.Action).OfType<SplitActionViewModel>().Select(x => x.Badge).First(), Is.EqualTo("1"));
		start.Action!.Do();

		var exits = viewModel.CurrentLocation.Connections.Count;
		var steps = viewModel.CurrentLocation.Steps!;
		Assert.That(steps.Primary.Badge, Is.EqualTo(Hotkeys.GetChoiceKeyText(exits + 1)));
		Assert.That(steps.Primary.Text, Is.EqualTo(steps.Action!.Text));

		// Shift with the step's number opens its menu, of having it or all of it done for the player.
		Assert.That(exits + 1, Is.LessThanOrEqualTo(Hotkeys.ChoiceCount));
		Assert.That(viewModel.OpenChoiceMenu(exits + 1), Is.True);
		Assert.That((steps.Primary.IsMenuOpen, steps.Primary.Alternatives.Count), Is.EqualTo((true, 2)));

		// Pressing the step's number fetches the water.
		var time = world.Time;
		Assert.That(viewModel.ChooseNumber(exits + 1), Is.True);
		Assert.That(world.Time, Is.GreaterThan(time));
		Assert.That(viewModel.CurrentLocation.WaitResult, Is.EqualTo("You fetched water for 5 minutes. Take it home to finish."));
	}

	[Test]
	public void PromptsNumberTheirButtons()
	{
		var notice = new NoticePromptViewModel("Something happened.");
		var dismissed = 0;
		notice.Dismissed += (_, _) => dismissed++;
		Assert.That(notice.ChooseNumber(2), Is.False);
		Assert.That(notice.ChooseNumber(1), Is.True);
		Assert.That(dismissed, Is.EqualTo(1));
	}

	[Test]
	public void PeopleAtTheLocationAreLabeledWithTheirRelationToThePlayer()
	{
		using var viewModel = CreateViewModel();
		var world = viewModel.World;
		var player = world.Player;
		var relatives = viewModel.CurrentLocation.People.Where(x => x.Person.Family == player.Family).ToList();

		// The game starts at home, with the family there.
		Assert.That(relatives, Is.Not.Empty);
		Assert.That(relatives.Select(x => x.PlayerLabel), Has.All.StartsWith("This is your "));
		Assert.That(viewModel.CurrentLocation.People.Where(x => x.Person.Family != player.Family).Select(x => x.PlayerLabel), Has.All.Null);
		Assert.That(viewModel.CurrentLocation.People.Any(x => x.IsYou), Is.False);
	}

	[Test]
	public void PlayerCanSleepAtHome()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		Assert.That(viewModel.CurrentLocation.Location, Is.SameAs(world.Player.Home));
		var sleep = GetStart(viewModel, "fatigue");
		Assert.That(sleep.Title, Is.EqualTo("Fatigue"));
		Assert.That(sleep.Action!.Text, Is.EqualTo("Start"));

		// Started at home, changing into night clothes is next, which can be done for them, and then sleeping until rested.
		sleep.Action!.Do();
		viewModel.CurrentLocation.Steps!.DoStep();
		var location = viewModel.CurrentLocation;
		var action = location.Steps!.Action!;
		Assert.That(action.Text, Is.EqualTo("Sleep"));
		action.Do();

		Assert.That(location.WaitResult, Does.StartWith("You slept for "));
		Assert.That(world.Player.GetLevel(world.GameData.Schedules.Needs.Single(x => x.Id == "fatigue")), Is.Zero);
	}

	[Test]
	public void StartingAChoreFromConcernsCarriesOnOnceTheSheetsClose()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var player = world.Player;
		var water = world.GameData.Schedules.Needs.Single(x => x.Id == "water");
		player.SetProgress(water, 0);
		viewModel.Concerns.Update();
		var meter = viewModel.Concerns.Household.Single(x => x.Need == water);
		Assume.That(meter.IsStartable, Is.True);

		// Starting it takes no time, so it can be done with a sheet open, which stays open.
		viewModel.ShowMap();
		var time = world.Time;
		meter.Start();
		Assert.That((player.Current?.Need, viewModel.HasSheets, world.Time), Is.EqualTo((water, true, time)));
		Assert.That(viewModel.CurrentLocation.Steps, Is.Null);

		// The player carries on with it once the sheets close.
		viewModel.CloseSheets();
		Assert.That(viewModel.CurrentLocation.Steps, Is.Not.Null);
	}

	/// <summary>
	/// Opens the start page and gets what it offers for a need.
	/// </summary>
	private static StartChoiceViewModel GetStart(GameScreenViewModel viewModel, string needId)
	{
		viewModel.CloseStart();
		viewModel.ShowStart();
		return viewModel.StartScreen!.Choices.Single(x => x.Choice.Need.Id == needId);
	}

	[Test]
	public void VillagersLabelThePlayer()
	{
		using var viewModel = CreateViewModel();
		viewModel.ShowVillagers();
		var people = viewModel.Top<VillagersViewModel>()!.Families.SelectMany(x => x.People).ToList();

		Assert.That(people.Where(x => x.IsYou).Select(x => x.Person), Is.EqualTo(new[] { viewModel.World.Player }));
		var player = viewModel.World.Player;
		foreach (var person in people)
		{
			var expected = person.Person.IsPlayer ? "This is you" :
				person.Person.Family != player.Family ? null :
				person.Person.Role == FamilyRole.Wife ? "This is your wife" :
				person.Person.Role == FamilyRole.Child ? (person.Person.Traits.GetValueId("gender") == "male" ? "This is your son" : "This is your daughter") :
				person.Person.Traits.GetValueId("gender") == "male" ? "This is your father" : "This is your mother";
			Assert.That(person.PlayerLabel, Is.EqualTo(expected), person.Name);
		}
		Assert.That(people.Count(x => x.PlayerLabel is not null), Is.GreaterThan(1));
		Assert.That(people.Any(x => x.CanGoTo), Is.False);
	}

	[Test]
	public void DevelopersCanGoStraightToAVillager()
	{
		using var viewModel = CreateViewModel(isDeveloper: true, withSchedules: true);
		var start = viewModel.World.Time;
		viewModel.World.AdvanceTime(viewModel.World.GameData.Calendar.CreateTimeOffset(0, 3, 0));
		viewModel.ShowVillagers();
		var people = viewModel.Top<VillagersViewModel>()!.Families.SelectMany(x => x.People).ToList();
		var target = people.First(x => !x.Person.IsPlayer && x.Person.Location != viewModel.World.Player.Location);
		var time = viewModel.World.Time;

		Assert.That(people.Where(x => x.CanGoTo).Select(x => x.Person), Is.EquivalentTo(viewModel.World.Npcs));

		target.GoTo();

		Assert.That(viewModel.World.Player.Location, Is.SameAs(target.Person.Location));
		Assert.That(viewModel.CurrentLocation.Location, Is.SameAs(target.Person.Location));
		Assert.That(viewModel.Top<VillagersViewModel>(), Is.Null);
		Assert.That(viewModel.World.Time, Is.EqualTo(time));
		Assert.That(time, Is.GreaterThan(start));
	}

	[Test]
	public void FollowingSomeoneTakesThePlayerWithThemWhenTheyLeave()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var target = viewModel.CurrentLocation.People[0];
		var person = target.Person;

		target.ToggleFollow();
		Assert.That((viewModel.CurrentLocation.Following, target.IsFollowed), Is.EqualTo((person, true)));

		// While they stay put, waiting stops each time they start spending time on a different goal (or when the part of the day
		// ends), not when they carry on with the same one, and leaves the player where they are.
		// Once they leave, the player goes with them.
		var start = viewModel.CurrentLocation.Location;
		var calendar = world.GameData.Calendar;
		for (var wait = 0; wait < 200 && viewModel.CurrentLocation.Location == start; wait++)
		{
			var (time, goal) = (world.Time, person.Goal);
			viewModel.CurrentLocation.Wait();
			if (viewModel.CurrentLocation.Location == start)
				Assert.That(person.Goal != goal || world.Time == calendar.GetNextDayPartStart(time), Is.True);
		}

		var screen = viewModel.CurrentLocation;
		Assert.That(screen.Location, Is.Not.SameAs(start));
		Assert.That(world.Player.Location, Is.SameAs(person.Location));
		Assert.That(screen.Location, Is.SameAs(person.Location));
		Assert.That(screen.Following, Is.SameAs(person));
		Assert.That(screen.People.Single(x => x.Person == person).IsFollowed, Is.True);
		Assert.That(screen.WaitResult, Is.EqualTo(OurResources.WaitResultFollowed.FormatCurrentCulture(person.FullName, screen.Location.Title)));

		// Following can be stopped, and walking somewhere stops it too.
		screen.People.Single(x => x.Person == person).ToggleFollow();
		Assert.That(screen.Following, Is.Null);
		screen.People.Single(x => x.Person == person).ToggleFollow();
		screen.Connections[0].Travel();
		Assert.That(viewModel.CurrentLocation.Following, Is.Null);
	}

	[Test]
	public void WaitingStopsWhenSomeoneArrivesOrLeaves()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var calendar = world.GameData.Calendar;
		var screen = viewModel.CurrentLocation;
		var changes = 0;
		var dayParts = new HashSet<string>();

		Assert.That(screen.WaitResult, Is.Null);
		for (var wait = 0; wait < 30; wait++)
		{
			var before = screen.People.Select(x => x.Person).ToList();
			var start = world.Time;

			screen.Wait();

			var after = screen.People.Select(x => x.Person).ToList();
			Assert.That(after, Is.EquivalentTo(screen.Location.People.Where(x => !x.IsPlayer)));
			Assert.That(world.Time, Is.GreaterThan(start).And.LessThanOrEqualTo(calendar.GetNextDayPartStart(start)));
			if (!after.SequenceEqual(before))
				changes++;
			else if (viewModel.TalkPrompt is { } prompt)
				prompt.Decline();
			else
			{
				Assert.That(world.Time, Is.EqualTo(calendar.GetNextDayPartStart(start)));
				var dayPart = calendar.GetDayPart(world.Time);
				Assert.That(screen.WaitResult, Does.EndWith(dayPart.Announcement?.Text ?? OurResources.DayPartStarted.FormatCurrentCulture(dayPart.Title)));
				dayParts.Add(dayPart.Id);
			}
			Assert.That(screen.WaitResult, Is.Not.Null);
		}

		Assert.That(changes, Is.GreaterThan(0));
		Assert.That(dayParts, Has.Count.GreaterThan(1));
	}

	[Test]
	public void ExamineVillagerOpensTheirCharacterViewAndClosingItReturnsToVillagers()
	{
		using var viewModel = CreateViewModel();
		viewModel.ShowVillagers();
		var villagers = viewModel.Top<VillagersViewModel>();
		var person = villagers!.Families[1].People[0];

		person.Examine();

		Assert.That(viewModel.Top<CharacterViewModel>()!.Person.Person, Is.SameAs(person.Person));
		Assert.That(viewModel.Top<CharacterViewModel>()!.Person.Person.Description, Is.Not.Empty);
		Assert.That(viewModel.Sheets, Has.Count.EqualTo(2));

		viewModel.CloseTopSheet();

		Assert.That(viewModel.Top<CharacterViewModel>(), Is.Null);
		Assert.That(viewModel.Top<VillagersViewModel>(), Is.SameAs(villagers));
	}

	[Test]
	public void ExaminePersonAtTheLocationOpensTheirCharacterView()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var person = viewModel.CurrentLocation.People[0];

		person.Examine();

		Assert.That(viewModel.Top<CharacterViewModel>()!.Person.Person, Is.SameAs(person.Person));
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.CurrentLocation));

		// Opening another sheet from the toolbar replaces the character's.
		viewModel.ToggleStatus();
		Assert.That(viewModel.Sheets, Is.EqualTo(new[] { viewModel.Top<StatusViewModel>() }));
	}

	[Test]
	public void VillagersShowDeveloperInformationOnlyForDevelopers()
	{
		using var player = CreateViewModel();
		using var developer = CreateViewModel(isDeveloper: true);

		player.ShowVillagers();
		developer.ShowVillagers();

		Assert.That(player.Top<VillagersViewModel>()!.Families.Select(x => x.IsDeveloper), Has.All.False);
		Assert.That(developer.Top<VillagersViewModel>()!.Families.Select(x => x.IsDeveloper), Has.All.True);
		foreach (var family in developer.Top<VillagersViewModel>()!.Families)
		{
			Assert.That(family.Wealth, Is.EqualTo($"Wealth: {family.Family.WealthLevel.GetDataName()}"));
			Assert.That(family.HomeTitle, Is.EqualTo(family.Family.Home.Title));
			Assert.That(family.Family.Members.Select(x => x.Home), Has.All.SameAs(family.Family.Home));
		}
	}

	[Test]
	public void VisitHomeClosesVillagersAndGoesToTheHouse()
	{
		using var viewModel = CreateViewModel(isDeveloper: true);
		viewModel.ShowVillagers();
		var family = viewModel.Top<VillagersViewModel>()!.Families[1];

		family.VisitHome();

		Assert.That(viewModel.Top<VillagersViewModel>(), Is.Null);
		Assert.That(viewModel.World.Player.Location, Is.SameAs(family.Family.Home));
		Assert.That(viewModel.CurrentLocation.Location, Is.SameAs(family.Family.Home));
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.CurrentLocation));
	}

	[Test]
	public void QuartersListPathsInCompassOrderAndPlacesSeparately()
	{
		using var viewModel = CreateViewModel();
		var quarter = viewModel.World.Player.Home.Connections.Single();

		viewModel.TravelTo(quarter);
		var screen = viewModel.CurrentLocation;

		// Paths lead to other quarters, in compass order, and say which way they go. Places are the houses and businesses.
		Assert.That(screen.Paths, Is.Not.Empty);
		Assert.That(screen.Paths.Select(x => x.Direction), Is.Ordered);
		Assert.That(screen.Paths.Select(x => x.Location.Bounds), Has.All.Not.Null);
		foreach (var path in screen.Paths)
		{
			var exit = quarter.Exits.Single(x => x.Location == path.Location);
			Assert.That(path.Route, Is.EqualTo($"{exit.Direction!.Value.GetTitle()} along the {exit.Path}"));
		}

		Assert.That(screen.Places.Select(x => x.Location), Does.Contain(viewModel.World.Player.Home));
		Assert.That(screen.Places.Select(x => x.Route), Has.All.Null);
		Assert.That(screen.PlacesHeader, Is.EqualTo(OurResources.PlacesHeader));
		Assert.That(screen.Connections, Is.EqualTo(screen.Paths.Concat(screen.Places)));

		// A house only leads back to its quarter.
		viewModel.TravelTo(viewModel.World.Player.Home);
		Assert.That(viewModel.CurrentLocation.HasPaths, Is.False);
		Assert.That(viewModel.CurrentLocation.PlacesHeader, Is.EqualTo(OurResources.ConnectionsHeader));
	}

	[Test]
	public void OnlyDevelopersCanTravelByTheMap()
	{
		using var player = CreateViewModel();
		using var developer = CreateViewModel(isDeveloper: true);
		var home = player.World.Player.Home;

		player.ShowMap();
		developer.ShowMap();

		// Everyone can open the map.
		Assert.That(player.Top<TownMapViewModel>(), Is.Not.Null);
		Assert.That(developer.Top<TownMapViewModel>(), Is.Not.Null);
		Assert.That(player.Top<TownMapViewModel>()!.CanTravel, Is.False);
		Assert.That(developer.Top<TownMapViewModel>()!.CanTravel, Is.True);

		// Choosing a quarter does nothing for players.
		var quarter = player.Top<TownMapViewModel>()!.Quarters.First(x => !x.IsCurrent);
		Assert.That(quarter.CanVisit(), Is.False);
		quarter.Visit();
		Assert.That(player.Top<TownMapViewModel>(), Is.Not.Null);
		Assert.That(player.World.Player.Location, Is.SameAs(home));
	}

	[Test]
	public void MiniMapShowsThePlayersQuarterAndItsNeighbors()
	{
		using var viewModel = CreateViewModel();
		var home = viewModel.World.Player.Home;
		var quarter = home.Connections.Single();
		var neighbors = quarter.Exits.Where(x => x.Direction is not null).Select(x => x.Location).ToList();

		// At home, the mini-map shows the quarter the house is in.
		var miniMap = viewModel.CharacterPanel.MiniMap;
		Assert.That(miniMap.CanTravel, Is.False);
		Assert.That(miniMap.Quarters.Select(x => x.Quarter), Is.EquivalentTo(neighbors.Prepend(quarter)));
		Assert.That(miniMap.Quarters.Where(x => x.IsCurrent).Select(x => x.Quarter), Is.EqualTo(new[] { quarter }));
		Assert.That(miniMap.Width, Is.LessThanOrEqualTo(196.001));
		Assert.That(miniMap.Height, Is.LessThanOrEqualTo(196.001));

		// Moving updates it.
		var changed = new List<string?>();
		viewModel.CharacterPanel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
		var next = neighbors[0];
		viewModel.TravelTo(next);

		Assert.That(changed, Does.Contain(nameof(CharacterPanelViewModel.MiniMap)));
		Assert.That(viewModel.CharacterPanel.MiniMap.Quarters.Where(x => x.IsCurrent).Select(x => x.Quarter), Is.EqualTo(new[] { next }));
		Assert.That(viewModel.CharacterPanel.MiniMap.Quarters.Select(x => x.Quarter), Does.Contain(quarter));
	}

	[Test]
	public void MapShowsEveryQuarterAndMarksThePlayersQuarter()
	{
		using var viewModel = CreateViewModel(isDeveloper: true);
		var quarters = viewModel.World.Locations.Where(x => x.Bounds is not null).ToList();
		var home = viewModel.World.Player.Home;

		viewModel.ShowMap();
		var map = viewModel.Top<TownMapViewModel>()!;

		// The player is at home, so their quarter is the one the house is in.
		Assert.That(map.Quarters.Select(x => x.Quarter), Is.EquivalentTo(quarters));
		Assert.That(map.Quarters.Where(x => x.IsCurrent).Select(x => x.Quarter), Is.EqualTo(new[] { home.Connections.Single() }));
		Assert.That(map.Quarters.Select(x => x.Left), Has.All.GreaterThanOrEqualTo(0));
		Assert.That(map.Quarters.Select(x => x.Top), Has.All.GreaterThanOrEqualTo(0));
		Assert.That(map.Quarters.Max(x => x.Left + x.Width), Is.EqualTo(map.Width));
		Assert.That(map.Quarters.Max(x => x.Top + x.Height), Is.EqualTo(map.Height));

		// Each path between quarters is drawn as two segments.
		var pathCount = quarters.Sum(x => x.Exits.Count(exit => exit.Direction is not null)) / 2;
		Assert.That(map.Paths, Has.Count.EqualTo(pathCount * 2));
	}

	[Test]
	public void ChoosingAQuarterOnTheMapGoesThere()
	{
		using var viewModel = CreateViewModel(isDeveloper: true);
		viewModel.ShowMap();
		var quarter = viewModel.Top<TownMapViewModel>()!.Quarters.First(x => !x.IsCurrent);

		var time = viewModel.World.Time;

		quarter.Visit();

		Assert.That(viewModel.World.Time, Is.EqualTo(time), "Developers' jumps take no time.");
		Assert.That(viewModel.Top<TownMapViewModel>(), Is.Null);
		Assert.That(viewModel.World.Player.Location, Is.SameAs(quarter.Quarter));
		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.CurrentLocation));
	}

	[Test]
	public void TheLogCollapsesToItsHeading()
	{
		using var viewModel = CreateViewModel();
		var log = viewModel.EventLog;
		Assert.That((log.IsCollapsed, log.CollapseToolTip), Is.EqualTo((false, OurResources.LogCollapseToolTip)));

		log.ToggleCollapsed();
		Assert.That((log.IsCollapsed, log.CollapseToolTip), Is.EqualTo((true, OurResources.LogExpandToolTip)));
		log.ToggleCollapsed();
		Assert.That(log.IsCollapsed, Is.False);
	}

	[Test]
	public void TheLocationOpensWhatThePlayerCanStart()
	{
		using var viewModel = CreateViewModel(withSchedules: true);

		viewModel.CurrentLocation.ChooseStart();

		Assert.That(viewModel.CurrentView, Is.SameAs(viewModel.StartScreen));
	}

	[Test]
	public void ShowsTheDateAndTimeAsTimePasses()
	{
		using var viewModel = CreateViewModel();
		var world = viewModel.World;
		var calendar = world.GameData.Calendar;
		var panel = viewModel.CharacterPanel;
		var changes = new List<string?>();
		panel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

		// Move to midsummer afternoon of the next year, which is always later than the start.
		var midsummer = calendar.CreateTimePoint(calendar.StartYear + 1, 2, 2, 4, 13, 5);
		world.AdvanceTime(midsummer - world.Time);

		Assert.Multiple(() =>
		{
			Assert.That(panel.Date, Is.EqualTo("Day 4, Mid Summer, year 2"));
			Assert.That(panel.TimeOfDay, Is.EqualTo("Afternoon, 13:05"));
			Assert.That(panel.SpecialDay, Is.EqualTo("Midsummer"));
			Assert.That(changes, Is.SupersetOf(new[] { nameof(CharacterPanelViewModel.Date), nameof(CharacterPanelViewModel.TimeOfDay), nameof(CharacterPanelViewModel.SpecialDay), nameof(CharacterPanelViewModel.DayDialPosition) }));

			// The day runs from the start of the morning (6:00) to the start of the night (21:00), with marks where the
			// afternoon and evening start.
			Assert.That((panel.DayDialPosition, panel.IsNight), Is.EqualTo((425.0 / 900, false)));
			Assert.That(panel.DayDialMarks, Is.EqualTo(new[] { 0.4, 0.8 }).Within(1e-9));
		});

		world.AdvanceTime(calendar.CreateTimeOffset(1, 9, 0));

		Assert.Multiple(() =>
		{
			Assert.That(panel.Date, Is.EqualTo("Day 5, Mid Summer, year 2"));
			Assert.That(panel.TimeOfDay, Is.EqualTo("Night, 22:05"));
			Assert.That(panel.SpecialDay, Is.Null);

			// The night runs from 21:00 to 6:00.
			Assert.That((panel.DayDialPosition, panel.IsNight), Is.EqualTo((65.0 / 540, true)));
		});
	}

	[Test]
	public void TalkingToSomeoneOpensAConversationThatEndsWhenThePlayerLeaves()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var target = viewModel.CurrentLocation.People[0];

		Assert.That(target.CanTalk, Is.True);
		target.Talk();
		Assert.That(viewModel.Conversation!.Person.Person, Is.SameAs(target.Person));
		Assert.That(viewModel.Overlay, Is.SameAs(viewModel.Conversation));

		viewModel.Conversation.Close();
		Assert.That((viewModel.Conversation, viewModel.Overlay), Is.EqualTo(((object?) null, (object?) null)));

		target.Talk();
		viewModel.CurrentLocation.Connections[0].Travel();
		Assert.That(viewModel.Conversation, Is.Null);

		// The player can't talk with themselves.
		Assert.That(viewModel.CurrentLocation.People.Any(x => x.Person.IsPlayer && x.CanTalk), Is.False);
	}

	[Test]
	public void SheetsOpenOverAConversation()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		viewModel.CurrentLocation.People[0].Talk();

		var conversation = viewModel.Conversation;
		viewModel.ToggleMap();

		// The conversation stays under the sheet, and is there again once it closes.
		Assert.That(viewModel.Top<TownMapViewModel>(), Is.Not.Null);
		Assert.That(viewModel.Conversation, Is.SameAs(conversation));
		viewModel.ToggleMap();
		Assert.That(viewModel.Overlay, Is.SameAs(conversation));
	}

	[Test]
	public void SomeoneAskingThePlayerToChatShowsAPromptOnTop()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var asker = viewModel.CurrentLocation.People[0].Person;
		viewModel.CurrentLocation.People[0].Talk();

		world.Player.TalkRequest = asker;
		Assert.That(viewModel.TalkPrompt!.Text, Is.EqualTo(OurResources.TalkPromptText.FormatCurrentCulture(asker.FullName)));
		Assert.That(viewModel.Overlay, Is.SameAs(viewModel.TalkPrompt));

		// Turning them down closes the prompt, and they don't ask again for a while.
		viewModel.TalkPrompt.Decline();
		Assert.That((viewModel.TalkPrompt, world.Player.TalkRequest), Is.EqualTo(((object?) null, (object?) null)));
		Assert.That(asker.GetRelationship(world.Player).LastDeclined, Is.EqualTo(world.Time));
		Assert.That(viewModel.Overlay, Is.SameAs(viewModel.Conversation));

		// Agreeing chats with them, and says so.
		var start = world.Time;
		world.Player.TalkRequest = asker;
		viewModel.TalkPrompt!.Accept();
		Assert.That(viewModel.TalkPrompt, Is.Null);
		Assert.That(world.Time, Is.GreaterThan(start));
		Assert.That(viewModel.CurrentLocation.WaitResult, Does.StartWith(OurResources.TalkPromptChatted.FormatCurrentCulture(asker.FullName, "")[..^2]));
	}

	[Test]
	public void ThePlayerIsRemindedOfAppointmentsAndToldWhenTheyMissThem()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var client = world.Npcs.First(x => x.Family != world.Player.Family && x.Home != world.Player.Location);
		var need = new Need
		{
			Id = "errand", Title = "Errand", Icon = "icon", Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 },
			StepSettings =
			[
				new Step
				{
					Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "agreed", Minutes = TestData.Amount(5), Chance = new StepChance { Base = 100 },
					Player = new StepPlayer { Title = "Ask" },
					Appointment = TestData.AtPartnerHome([TestData.Do(TestData.Amount(30))]),
				},
			],
		};
		need.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		var time = world.GameData.Calendar.AddMinutes(world.Time, 120);
		var appointment = world.AddAppointment(world.Player, client, need, 0, client.Home, time);

		// The next one is in the sidebar.
		Assert.That(viewModel.CharacterPanel.NextAppointment,
			Is.EqualTo(OurResources.AppointmentNext.FormatCurrentCulture(client.FullName, AppointmentText.FormatWhen(world, time), AppointmentText.FormatPlace(world, appointment))));

		// When it's time, they're asked whether to set off, and setting off has them go there first.
		world.Player.AskToSetOff(appointment.GetSequence(world.Player)!);
		Assert.That(viewModel.Overlay, Is.SameAs(viewModel.AppointmentPrompt));
		Assert.That(viewModel.AppointmentPrompt!.Text, Does.Contain(client.FullName));
		viewModel.AppointmentPrompt.SetOffNow();
		Assert.That(viewModel.AppointmentPrompt, Is.Null);
		Assert.That(viewModel.CurrentLocation.Steps!.Next,
			Is.EqualTo(OurResources.PlayerStepsNext.FormatCurrentCulture(OurResources.PlayerStepGoToAppointment.FormatCurrentCulture(
				OurResources.AppointmentPlaceOffQuarter.FormatCurrentCulture(OurResources.AppointmentPlaceNamedHome.FormatCurrentCulture(client.FullName), client.Home.Quarter!.Title)))));

		// Missing it is a notice, which closes.
		world.OnAppointmentMissed(appointment, isOtherWaiting: true, world.Time);
		Assert.That(viewModel.NoticePrompt!.Text, Is.EqualTo(OurResources.AppointmentGaveUp.FormatCurrentCulture(client.FullName)));
		Assert.That(viewModel.Overlay, Is.SameAs(viewModel.NoticePrompt));
		viewModel.NoticePrompt.Dismiss();
		Assert.That(viewModel.NoticePrompt, Is.Null);
	}

	[Test]
	public void AppointmentsCanBeCalledOffWhenReminded()
	{
		using var viewModel = CreateViewModel(withSchedules: true);
		var world = viewModel.World;
		var client = world.Npcs.First(x => x.Family != world.Player.Family);
		var need = new Need
		{
			Id = "errand", Title = "Errand", Icon = "icon", Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 },
			StepSettings =
			[
				new Step
				{
					Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "agreed", Minutes = TestData.Amount(5), Chance = new StepChance { Base = 100 },
					Player = new StepPlayer { Title = "Ask" },
					Appointment = TestData.AtPartnerHome([TestData.Do(TestData.Amount(30))]),
				},
			],
		};
		need.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		var appointment = world.AddAppointment(world.Player, client, need, 0, client.Home, world.GameData.Calendar.AddMinutes(world.Time, 120));
		world.Player.AskToSetOff(appointment.GetSequence(world.Player)!);

		viewModel.AppointmentPrompt!.CallOff();

		Assert.That((viewModel.AppointmentPrompt, viewModel.CharacterPanel.NextAppointment, world.Appointments.Count), Is.EqualTo(((object?) null, (string?) null, 0)));
	}

	[Test]
	public void DevelopersCanSeeSomeonesStatusFromTheirCharacterView()
	{
		using var viewModel = CreateViewModel(isDeveloper: true, withSchedules: true);
		var person = viewModel.World.Npcs[0];
		viewModel.ShowCharacter(person);
		var character = viewModel.Top<CharacterViewModel>()!;

		Assert.That(character.CanViewStatus, Is.True);
		character.ViewStatus();
		Assert.That(viewModel.Top<StatusViewModel>()!.Person, Is.SameAs(person));

		// Closing it without changing anything shows the character view again, and leaves the person's plan alone.
		var choice = person.GoalChoice;
		viewModel.CloseTopSheet();
		Assert.That(viewModel.TopSheet, Is.SameAs(character));
		Assert.That(person.GoalChoice, Is.SameAs(choice));

		// After a change, the person chooses a new goal now.
		character.ViewStatus();
		var fatigue = viewModel.Top<StatusViewModel>()!.Needs.Single(x => x.Title == "Fatigue");
		Assert.That(viewModel.Top<StatusViewModel>()!.IsChanged, Is.False);
		fatigue.Amount = fatigue.Amount > 50 ? 0 : 100;
		Assert.That(viewModel.Top<StatusViewModel>()!.IsChanged, Is.True);
		viewModel.CloseTopSheet();
		Assert.That(person.GoalChoice, Is.Not.SameAs(choice));
		Assert.That(person.GoalChoice!.Time, Is.EqualTo(viewModel.World.Time));

		// The player's own character view has no status button, and neither does anyone's for non-developers.
		viewModel.ShowCharacter(viewModel.World.Player);
		Assert.That(viewModel.Top<CharacterViewModel>()!.CanViewStatus, Is.False);
		using var player = CreateViewModel(withSchedules: true);
		player.ShowCharacter(player.World.Npcs[0]);
		Assert.That(player.Top<CharacterViewModel>()!.CanViewStatus, Is.False);
	}

	private static GameScreenViewModel CreateViewModel(int seed = 0, bool isDeveloper = false, bool withSchedules = false)
	{
		var gameData = TestData.CreateGameData(withSchedules: withSchedules);
		var world = GameWorld.CreateNewGame(gameData, TestData.CreatePlayerTraits(gameData), new Random(seed));
		return new GameScreenViewModel(world, isDeveloper);
	}
}

internal static class GameScreenTestExtensions
{
	/// <summary>
	/// Gets the sheet on top, if it's of the given type, or else null.
	/// </summary>
	public static T? Top<T>(this GameScreenViewModel viewModel) where T : class => viewModel.TopSheet as T;
}
