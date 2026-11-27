using OperaEtDies.Controls.Settings;
using OperaEtDies.Settings;

namespace OperaEtDies.Test.Controls.Settings;

[TestFixture]
public sealed class SettingsViewModelTests
{
	[Test]
	public void TheChoicesShowTheSettings()
	{
		var settings = new AppSettings { TextSize = 120, ReduceMotion = ReduceMotion.Off, TimeSpeed = TimeSpeed.Fast, IsFullScreen = true };
		using var viewModel = new SettingsViewModel(settings);

		Assert.That(viewModel.TextSizes.Select(x => x.Label), Is.EqualTo(new[] { "90%", "100%", "110%", "120%", "130%", "140%", "150%" }));
		Assert.That(viewModel.TextSizes.Single(x => x.IsSelected).Label, Is.EqualTo("120%"));
		Assert.That(viewModel.DisplayModes.Single(x => x.IsSelected).Label, Is.EqualTo(OurResources.DisplayFullScreen));
		Assert.That(viewModel.ReduceMotionChoices.Single(x => x.IsSelected).Label, Is.EqualTo(OurResources.ReduceMotionOff));
		Assert.That(viewModel.TimeSpeeds.Single(x => x.IsSelected).Label, Is.EqualTo(OurResources.TimeSpeedFast));
	}

	[Test]
	public void ChoosingChangesTheSettingAtOnce()
	{
		var settings = new AppSettings();
		using var viewModel = new SettingsViewModel(settings);

		viewModel.TextSizes[^1].IsSelected = true;
		viewModel.DisplayModes[1].IsSelected = true;
		viewModel.ReduceMotionChoices[1].IsSelected = true;
		viewModel.TimeSpeeds[^1].IsSelected = true;

		Assert.That(settings.TextSize, Is.EqualTo(150));
		Assert.That(settings.IsFullScreen, Is.True);
		Assert.That(settings.ReduceMotion, Is.EqualTo(ReduceMotion.On));
		Assert.That(settings.TimeSpeed, Is.EqualTo(TimeSpeed.Instant));
	}

	[Test]
	public void UnselectingAChoiceKeepsIt()
	{
		var settings = new AppSettings();
		using var viewModel = new SettingsViewModel(settings);

		viewModel.TimeSpeeds[1].IsSelected = false;

		Assert.That(settings.TimeSpeed, Is.EqualTo(TimeSpeed.Normal));
		Assert.That(viewModel.TimeSpeeds[1].IsSelected, Is.True);
	}

	[Test]
	public void ChangesFromElsewhereShowInTheChoices()
	{
		var settings = new AppSettings();
		using var viewModel = new SettingsViewModel(settings);
		var changed = false;
		viewModel.DisplayModes[1].PropertyChanged += (_, _) => changed = true;

		settings.IsFullScreen = true;

		Assert.That(changed, Is.True);
		Assert.That(viewModel.DisplayModes[1].IsSelected, Is.True);
		Assert.That(viewModel.DisplayModes[0].IsSelected, Is.False);
	}

	[Test]
	public void BackAsksToGoBack()
	{
		using var viewModel = new SettingsViewModel(new AppSettings());
		var count = 0;
		viewModel.BackRequested += (_, _) => count++;

		viewModel.Back();

		Assert.That(count, Is.EqualTo(1));
	}
}
