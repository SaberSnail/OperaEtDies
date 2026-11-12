using System.IO;
using OperaEtDies.Data;
using OperaEtDies.Reports;

namespace OperaEtDies.Test.Reports;

[TestFixture]
public sealed class VillageReportTests
{
	[Test]
	public void SavingAndLoadingKeepsEverythingTheReportShows()
	{
		var gameData = GameData.Load(JsonDataLoader.DefaultDataDirectory).Value!;

		var report = WriteReport(gameData, isThroughSave: false);
		var throughSave = WriteReport(gameData, isThroughSave: true);

		Assert.That(report.Split('\n').Count(x => x.StartsWith("person\t", StringComparison.Ordinal)), Is.GreaterThan(20));
		Assert.That(throughSave, Is.EqualTo(report));
	}

	[Test]
	public void TestDataSavesAndLoadsForTheReport()
	{
		var gameData = TestData.CreateGameData(withFeatures: true);

		Assert.That(WriteReport(gameData, isThroughSave: true), Is.EqualTo(WriteReport(gameData, isThroughSave: false)));
	}

	private static string WriteReport(GameData gameData, bool isThroughSave)
	{
		using var output = new StringWriter();
		VillageReport.Write(gameData, count: 20, firstSeed: 0, isThroughSave, output);
		return output.ToString();
	}
}
