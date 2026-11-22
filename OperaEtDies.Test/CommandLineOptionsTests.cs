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
	public void ModArgumentsAreCollectedInOrder()
	{
		var options = CommandLineOptions.Parse(["--mod", @"C:\First", "--developer", "--MOD", @"C:\Second", "--mod"]);

		Assert.That(options.ModDirectories, Is.EqualTo(new[] { @"C:\First", @"C:\Second" }));
		Assert.That(options.AreModsEnabled, Is.True);
	}

	[Test]
	public void WithoutModArgumentsTheModListIsUsed()
	{
		Assert.That(CommandLineOptions.Parse(["--developer"]).ModDirectories, Is.Null);
	}
}
