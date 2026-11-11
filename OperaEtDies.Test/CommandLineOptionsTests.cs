namespace OperaEtDies.Test;

[TestFixture]
public sealed class CommandLineOptionsTests
{
	[Test]
	public void NoArgumentsStartTheGameNormally()
	{
		var options = CommandLineOptions.Parse([]);

		Assert.That(options.ShowHelp, Is.False);
		Assert.That(options.AreModsEnabled, Is.True);
		Assert.That(options.IsDeveloper, Is.False);
		Assert.That(options.IsVillageReport, Is.False);
	}

	[Test]
	public void ArgumentsTurnOnTheirOptions()
	{
		var options = CommandLineOptions.Parse(["--NO-MODS", "--developer", "--help", "--generate-villages", "5", "--unknown"]);

		Assert.That(options.ShowHelp, Is.True);
		Assert.That(options.AreModsEnabled, Is.False);
		Assert.That(options.IsDeveloper, Is.True);
		Assert.That(options.IsVillageReport, Is.True);
	}

	[Test]
	public void HelpListsEveryOption()
	{
		var help = CommandLineOptions.GetHelpText();

		Assert.That(help, Does.Contain("--help").And.Contain("--no-mods").And.Contain("--developer").And.Contain("--generate-villages <count>").And.Contain("--seed <number>"));
	}
}
