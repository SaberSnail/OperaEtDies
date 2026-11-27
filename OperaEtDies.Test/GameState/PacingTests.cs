using System.Collections.ObjectModel;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Controls;
using OperaEtDies.GameState;
using OperaEtDies.Settings;
using OperaEtDies.Time;

namespace OperaEtDies.Test.GameState;

[TestFixture]
public sealed class PacingTests
{
	[Test]
	public async Task TimePassedInSlicesEndsTheSameAsTimePassedAtOnceAsync()
	{
		for (var seed = 0; seed < 10; seed++)
		{
			var (_, atOnce) = GameWorldTests.CreateWorld(seed, withSchedules: true);
			var (_, sliced) = GameWorldTests.CreateWorld(seed, withSchedules: true);
			var pacer = new SlicingPacer(sliced.GameData.Calendar, minutes: 7);
			sliced.Pacer = pacer;

			await atOnce.AdvanceTimeAsync(atOnce.GameData.Calendar.CreateTimeOffset(1, 0, 0));
			await sliced.AdvanceTimeAsync(sliced.GameData.Calendar.CreateTimeOffset(1, 0, 0));
			sliced.Pacer = null;

			Assert.That(pacer.Slices, Is.GreaterThan(100), $"Seed {seed}");
			Assert.That(Save(sliced), Is.EqualTo(Save(atOnce)), $"Seed {seed}");
		}
	}

	[Test]
	public async Task AskingTimeToStopStopsItAtTheEndOfASliceAsync()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true);
		var calendar = world.GameData.Calendar;
		var start = world.Time;
		world.Pacer = new SlicingPacer(calendar, minutes: 10, stopAfter: 3);

		var passed = await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 6, 0));

		Assert.That(calendar.GetTotalMinutes(passed), Is.EqualTo(30));
		Assert.That(world.Time, Is.EqualTo(calendar.AddMinutes(start, 30)));

		// Once asked to stop, no more time passes, unless it can't be stopped.
		Assert.That(calendar.GetTotalMinutes(await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 1, 0))), Is.Zero);
		Assert.That(calendar.GetTotalMinutes(await world.AdvanceTimeAsync(calendar.CreateTimeOffset(0, 1, 0), canStop: false)), Is.EqualTo(60));
	}

	[Test]
	public async Task AskingTimeToStopStopsWhatThePlayerIsDoingAsync()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true, isHeadless: false);
		var calendar = world.GameData.Calendar;
		var player = world.Player;
		var sleep = world.GameData.Schedules.Needs.First(x => x.Id == world.GameData.Schedules.SleepNeed);
		world.Pacer = new SlicingPacer(calendar, minutes: 10, stopAfter: 2);

		var passed = await world.StartPlayerStepsAsync(sleep, isAutomatic: true);

		Assert.That(calendar.GetTotalMinutes(passed), Is.LessThanOrEqualTo(20));
		Assert.That(player.Tasks.Where(x => x is not TalkTask), Is.Empty);
	}

	[Test]
	public async Task WalkingPassesAtOnceAsync()
	{
		var (_, world) = GameWorldTests.CreateWorld(withSchedules: true, isHeadless: false);
		var calendar = world.GameData.Calendar;
		var player = world.Player;
		var (here, next) = (player.Location, player.Location.Connections[0]);
		var walking = world.GetTravelMinutes(here, next) * 2;
		var walked = calendar.AddMinutes(world.Time, walking);
		player.SetPlan(null, [new TravelTask(next), new TravelTask(here), new BusyTask(30)]);
		player.StartTask(calendar.AddMinutes(world.Time, world.GetTravelMinutes(here, next)));
		var pacer = new SlicingPacer(calendar, minutes: 1);
		world.Pacer = pacer;

		await world.AdvanceTimeAsync(calendar.CreateTimeOffset(1, 0, 0), () => player.Tasks.Count == 0);

		// The walk doesn't pause, or count toward the pacing, but the rest is paced.
		Assert.That(calendar.GetTotalMinutes(pacer.Skipped), Is.EqualTo(walking));
		Assert.That(pacer.Waits, Is.Not.Empty);
		Assert.That(pacer.Waits, Has.All.GreaterThan(walked));
	}

	[Test]
	public void PacedTimeCanOnlyPassAsynchronously()
	{
		var (_, world) = GameWorldTests.CreateWorld();
		world.Pacer = new SlicingPacer(world.GameData.Calendar, minutes: 10);

		Assert.That(() => world.AdvanceTime(world.GameData.Calendar.CreateTimeOffset(0, 1, 0)), Throws.InvalidOperationException);
	}

	[TestCase(0, 0.0)]
	[TestCase(1, 0.668)]
	[TestCase(60, 1.799)]
	[TestCase(240, 3.098)]
	[TestCase(720, 5.0)]
	[TestCase(5000, 5.0)]
	public void LongerTimesTakeLongerButEachMinuteTakesLess(long minutes, double seconds)
	{
		Assert.That(TimePacer.GetSeconds(minutes), Is.EqualTo(seconds).Within(0.001));
		if (minutes is > 0 and <= 720)
			Assert.That(TimePacer.GetMinutesFor(seconds), Is.EqualTo((double) minutes).Within(0.5));
	}

	[TestCase(1)]
	[TestCase(60)]
	[TestCase(720)]
	public void TheScaleStretchesTheTimesTaken(long minutes)
	{
		Assert.That(TimePacer.GetSeconds(minutes, 2), Is.EqualTo(TimePacer.GetSeconds(minutes) * 2).Within(0.001));
		Assert.That(TimePacer.GetSeconds(minutes, 0.25), Is.EqualTo(TimePacer.GetSeconds(minutes) * 0.25).Within(0.001));
		Assert.That(TimePacer.GetMinutesFor(TimePacer.GetSeconds(minutes, 0.5), 0.5), Is.EqualTo((double) minutes).Within(0.5));
	}

	[TestCase(TimeSpeed.Slow, false)]
	[TestCase(TimeSpeed.Normal, false)]
	[TestCase(TimeSpeed.Instant, true)]
	public void OnlyTheInstantSpeedPassesTimeAtOnce(TimeSpeed speed, bool isInstant)
	{
		var world = CreateRunnerWorld();
		var settings = new AppSettings { TimeSpeed = speed };
		using var runner = new TimeRunner(world, settings);

		using var withoutSettings = new TimeRunner(world, settings: null);

		Assert.That(runner.IsInstant, Is.EqualTo(isInstant));
		Assert.That(withoutSettings.IsInstant, Is.True);
	}

	[Test]
	public void ChangingTheSpeedChangesWhetherTimeIsInstant()
	{
		var settings = new AppSettings();
		using var runner = new TimeRunner(CreateRunnerWorld(), settings);
		var changes = new List<string?>();
		runner.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

		settings.TimeSpeed = TimeSpeed.Instant;

		Assert.That(runner.IsInstant, Is.True);
		Assert.That(changes, Does.Contain(nameof(TimeRunner.IsInstant)));
	}

	private static GameWorld CreateRunnerWorld()
	{
		var gameData = TestData.CreateGameData();
		return GameWorld.CreateNewGame(gameData, TestData.CreatePlayerTraits(gameData), new Random(0));
	}

	private static string Save(GameWorld world) => SaveFile.Create(world, new SaveSettings("Test", "hash", "", [], ""), DateTimeOffset.UnixEpoch).Serialize();

	/// <summary>
	/// Paces time in slices of so many minutes, without pausing, and asks it to stop after so many slices.
	/// </summary>
	private sealed class SlicingPacer(GameCalendar calendar, int minutes, int stopAfter = int.MaxValue) : ITimePacer
	{
		public int Slices { get; private set; }

		public Collection<TimePoint> Waits { get; } = [];

		public TimeOffset Skipped { get; private set; } = calendar.CreateTimeOffset(0, 0, 0);

		public bool IsStopRequested => Slices >= stopAfter;

		public TimePoint GetSliceEnd(TimePoint time, TimePoint end)
		{
			var next = calendar.AddMinutes(time, minutes);
			return next < end ? next : end;
		}

		public Task WaitUntilAsync(TimePoint time)
		{
			Slices++;
			Waits.Add(time);
			return Task.CompletedTask;
		}

		public void Skip(TimeOffset offset) => Skipped += offset;
	}
}
