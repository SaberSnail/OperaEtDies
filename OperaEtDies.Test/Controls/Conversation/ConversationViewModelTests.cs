using GoldenAnvil.Utility;
using OperaEtDies.Controls;
using OperaEtDies.Controls.Conversation;
using OperaEtDies.Data;
using OperaEtDies.GameState;
using OperaEtDies.Test.GameState;

namespace OperaEtDies.Test.Controls.Conversation;

[TestFixture]
public sealed class ConversationViewModelTests
{
	[Test]
	public void WhatIsSaidIsKept()
	{
		var (world, person) = TalkingTests.CreateWorldWithSomeoneHere();
		using var timeRunner = new TimeRunner(world, isPaced: false);
		var viewModel = new ConversationViewModel(person, world, timeRunner);

		Assert.That(viewModel.Title, Is.EqualTo(OurResources.ConversationHeader.FormatCurrentCulture(person.FullName)));
		Assert.That(viewModel.Unavailable, Is.Null);
		Assert.That(viewModel.Subjects.Any(x => x == person || x.IsPlayer), Is.False);
		Assert.That(viewModel.AskWhatDoingText, Does.Contain("5 minutes"));

		viewModel.AskWhatDoing();
		Assert.That(viewModel.Lines, Is.EqualTo(new[] { OurResources.ConversationAskedDoing.FormatCurrentCulture(person.FullName), OurResources.TalkPlanIdle }));

		var subject = viewModel.Subjects.First(x => !person.Sightings.ContainsKey(x));
		viewModel.SelectedSubject = subject;
		viewModel.AskAbout();
		Assert.That(viewModel.Lines[^1], Is.EqualTo(OurResources.TalkNotSeen.FormatCurrentCulture(subject.FullName)));

		viewModel.Socialize();
		Assert.That(viewModel.Lines[^1], Does.StartWith(OurResources.ConversationSocialized.FormatCurrentCulture(person.FullName, "")[..^2]));
	}

	[Test]
	public void TheNextAskStepCanBeAsked()
	{
		var (world, person) = TalkingTests.CreateWorldWithSomeoneHere();
		using var timeRunner = new TimeRunner(world, isPaced: false);
		var viewModel = new ConversationViewModel(person, world, timeRunner);
		Assert.That((viewModel.AskStepText, viewModel.CanAskStep()), Is.EqualTo(((string?) null, false)));

		// With an "ask" step next, its title is a button, and asking says its text and the answer.
		var need = new Need
		{
			Id = "errand", Title = "Errand", Level = new LevelNeedSettings { PerHour = 0, SatisfyPerHour = 60 },
			StepSettings =
			[
				new Step
				{
					Kind = StepKind.Ask, With = StepTarget.Anyone, Name = "agreed", Minutes = TestData.Amount(5), Chance = new StepChance { Base = 100 },
					Player = new StepPlayer { Title = "Offer help", Text = TestData.Text("Can I help?") },
				},
			],
		};
		need.StepLists = new Dictionary<string, StepList>(StringComparer.Ordinal);
		Scheduler.StartSequence(world.Player, need, 0, null, [], null);
		Assert.That(viewModel.AskStepText, Is.EqualTo(OurResources.ConversationAskStep.FormatCurrentCulture("Offer help", "5 minutes")));

		viewModel.AskStep();

		Assert.That(viewModel.Lines, Is.EqualTo(new[] { "Can I help?", OurResources.ConversationAgreed.FormatCurrentCulture(person.FullName) }));
		Assert.That(viewModel.AskStepText, Is.Null);
	}

	[Test]
	public void SomeoneWhoLeftCantBeTalkedWith()
	{
		var (world, person) = TalkingTests.CreateWorldWithSomeoneHere();
		using var timeRunner = new TimeRunner(world, isPaced: false);
		var viewModel = new ConversationViewModel(person, world, timeRunner);

		person.Location = world.Locations.First(x => x != world.Player.Location);

		Assert.That(viewModel.Unavailable, Is.EqualTo(OurResources.ConversationNotHere.FormatCurrentCulture(person.FullName)));
		Assert.That((viewModel.CanAskWhatDoing(), viewModel.CanAskAbout(), viewModel.CanSocialize()), Is.EqualTo((false, false, false)));
		viewModel.AskWhatDoing();
		Assert.That(viewModel.Lines, Is.Empty);
	}

	[Test]
	public void SightingsAreDescribedInWords()
	{
		var (world, person) = TalkingTests.CreateWorldWithSomeoneHere();
		using var timeRunner = new TimeRunner(world, isPaced: false);
		var viewModel = new ConversationViewModel(person, world, timeRunner);
		var subject = world.Npcs.First(x => x != person);
		var laundry = world.GameData.Schedules.Needs.Single(x => x.Id == "laundry");
		var quarter = world.Locations.First(x => x.Template is OperaEtDies.Data.QuarterTemplate);
		var clock = world.GameData.Calendar.FormatClock(world.Time);
		var name = subject.FullName;

		Assert.That(viewModel.DescribeSighting(subject, new Sighting(quarter, world.Time, SightingActivity.Busy, laundry)),
			Is.EqualTo($"“I saw {name} at {quarter.Title}, washing, around {clock}.”"));
		Assert.That(viewModel.DescribeSighting(subject, new Sighting(quarter, world.Time, SightingActivity.Left, Destination: subject.Home, Source: world.Player)),
			Is.EqualTo($"“You told me {name} was leaving {quarter.Title} for {subject.Home.Title}, around {clock}.”"));
		Assert.That(viewModel.DescribeSighting(subject, new Sighting(quarter, world.Time, SightingActivity.None, Plan: new SightingPlan(laundry, subject.Home))),
			Is.EqualTo($"“I saw {name} at {quarter.Title}, around {clock}. {name} said they were on their way home, where they'd be hanging out the washing.”"));
		Assert.That(ConversationViewModel.DescribePlan(subject, quarter, new SightingPlan(laundry, null)), Is.EqualTo("“I'm washing.”"));
		Assert.That(ConversationViewModel.DescribePlan(subject, subject.Home, new SightingPlan(laundry, null)), Is.EqualTo("“I'm hanging out the washing.”"));
		Assert.That(ConversationViewModel.DescribePlan(subject, quarter, new SightingPlan(laundry, quarter)), Is.EqualTo($"“I'm on my way to {quarter.Title}, where I'll be washing.”"));
		Assert.That(ConversationViewModel.DescribePlan(subject, quarter, new SightingPlan(null, subject.Home)), Is.EqualTo("“I'm just on my way home.”"));
		var workplace = world.Locations.First(x => x.Template is OperaEtDies.Data.BusinessTemplate);
		var work = world.GameData.Schedules.Needs.Single(x => x.Id == "work");
		Assert.That(ConversationViewModel.DescribePlan(subject, quarter, new SightingPlan(work, workplace)),
			Is.EqualTo($"“I'm on my way to {workplace.Quarter!.Title}, where I'll be {(work.Activity ?? work.Title).ToLowerInvariant()} at {workplace.Title}.”"));
		Assert.That(ConversationViewModel.DescribePlan(subject, quarter, new SightingPlan(null, null)), Is.EqualTo("“Nothing in particular.”"));
	}
}
