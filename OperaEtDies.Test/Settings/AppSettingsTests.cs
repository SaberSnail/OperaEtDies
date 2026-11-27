using OperaEtDies.Settings;

namespace OperaEtDies.Test.Settings;

[TestFixture]
public sealed class AppSettingsTests
{
	[Test]
	public void WithoutAStoreTheDefaultsAreUsed()
	{
		var settings = new AppSettings();

		Assert.That(settings.TextSize, Is.EqualTo(100));
		Assert.That(settings.ReduceMotion, Is.EqualTo(ReduceMotion.FollowWindows));
		Assert.That(settings.TimeSpeed, Is.EqualTo(TimeSpeed.Normal));
		Assert.That(settings.IsFullScreen, Is.False);
	}

	[Test]
	public void SettingsLoadFromTheStore()
	{
		var store = new FakeAppSettingsStore(new AppSettingsValues { TextSize = 130, ReduceMotion = ReduceMotion.On, TimeSpeed = TimeSpeed.Instant, IsFullScreen = true });

		var settings = new AppSettings(store);

		Assert.That(settings.TextSize, Is.EqualTo(130));
		Assert.That(settings.ReduceMotion, Is.EqualTo(ReduceMotion.On));
		Assert.That(settings.TimeSpeed, Is.EqualTo(TimeSpeed.Instant));
		Assert.That(settings.IsFullScreen, Is.True);
	}

	[Test]
	public void ValuesOutOfRangeAreBroughtIntoIt()
	{
		var store = new FakeAppSettingsStore(new AppSettingsValues { TextSize = 500, ReduceMotion = (ReduceMotion) 42, TimeSpeed = (TimeSpeed) 42 });

		var settings = new AppSettings(store);

		Assert.That(settings.TextSize, Is.EqualTo(AppSettings.MaxTextSize));
		Assert.That(settings.ReduceMotion, Is.EqualTo(ReduceMotion.FollowWindows));
		Assert.That(settings.TimeSpeed, Is.EqualTo(TimeSpeed.Normal));
	}

	[TestCase(0, 90)]
	[TestCase(94, 90)]
	[TestCase(95, 100)]
	[TestCase(123, 120)]
	[TestCase(150, 150)]
	[TestCase(1000, 150)]
	public void TextSizesAreRoundedToAStep(int textSize, int expected)
	{
		Assert.That(new AppSettings { TextSize = textSize }.TextSize, Is.EqualTo(expected));
	}

	[Test]
	public void EachChangeIsKept()
	{
		var store = new FakeAppSettingsStore(new AppSettingsValues());
		var settings = new AppSettings(store);

		settings.TextSize = 110;
		Assert.That(store.Saved?.TextSize, Is.EqualTo(110));
		settings.ReduceMotion = ReduceMotion.Off;
		Assert.That(store.Saved?.ReduceMotion, Is.EqualTo(ReduceMotion.Off));
		settings.TimeSpeed = TimeSpeed.Fast;
		Assert.That(store.Saved?.TimeSpeed, Is.EqualTo(TimeSpeed.Fast));
		settings.IsFullScreen = true;
		Assert.That(store.Saved, Is.EqualTo(new AppSettingsValues { TextSize = 110, ReduceMotion = ReduceMotion.Off, TimeSpeed = TimeSpeed.Fast, IsFullScreen = true }));
		Assert.That(store.SaveCount, Is.EqualTo(4));

		// Setting what's already there changes nothing.
		settings.TimeSpeed = TimeSpeed.Fast;
		Assert.That(store.SaveCount, Is.EqualTo(4));
	}

	[TestCase(ReduceMotion.On, true)]
	[TestCase(ReduceMotion.Off, false)]
	public void ReduceMotionOverridesWindows(ReduceMotion reduceMotion, bool isReduced)
	{
		Assert.That(new AppSettings { ReduceMotion = reduceMotion }.IsMotionReduced, Is.EqualTo(isReduced));
	}

	[Test]
	public void ChangingReduceMotionChangesWhetherMotionIsReduced()
	{
		var settings = new AppSettings();
		var changes = new List<string?>();
		settings.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

		settings.ReduceMotion = ReduceMotion.On;

		Assert.That(changes, Is.EquivalentTo(new[] { nameof(AppSettings.ReduceMotion), nameof(AppSettings.IsMotionReduced) }));
	}

	[TestCase(TimeSpeed.Slow, 2.0)]
	[TestCase(TimeSpeed.Normal, 1.0)]
	[TestCase(TimeSpeed.Fast, 0.5)]
	[TestCase(TimeSpeed.VeryFast, 0.25)]
	[TestCase(TimeSpeed.Instant, null)]
	public void FasterSpeedsTakeLessTime(TimeSpeed speed, double? scale)
	{
		Assert.That(speed.GetPaceScale(), Is.EqualTo(scale));
	}

	internal sealed class FakeAppSettingsStore(AppSettingsValues values) : IAppSettingsStore
	{
		public AppSettingsValues? Saved { get; private set; }

		public int SaveCount { get; private set; }

		public AppSettingsValues Load() => values;

		public void Save(AppSettingsValues values)
		{
			Saved = values;
			SaveCount++;
		}

		public void Reset() => Saved = null;
	}
}
