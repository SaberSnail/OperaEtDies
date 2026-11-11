using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class ProfessionWealthTests
{
	[TestCase("veryLow", 1.0)]
	[TestCase("low", 2.0)]
	[TestCase("Medium", 4.0)]
	[TestCase("high", 8.0)]
	[TestCase("low+", 2.4)]
	[TestCase("low++", 2.8)]
	[TestCase("medium-", 3.6)]
	[TestCase("medium--", 3.2)]
	[TestCase("high-", 7.2)]
	public void StepsMoveAFifthOfTheWayToTheNextLevel(string text, double expected)
	{
		Assert.That(ProfessionWealth.TryParse(text, out var wealth), Is.True);
		Assert.That(wealth!.Value.GetAmount(), Is.EqualTo(expected).Within(0.0001));
	}

	[Test]
	public void StepsNeverReachTheNextLevel()
	{
		Assert.That(new ProfessionWealth(WealthLevel.Low, 2).GetAmount(), Is.LessThan(new ProfessionWealth(WealthLevel.Medium, -2).GetAmount()));
	}

	[TestCase("")]
	[TestCase("rich")]
	[TestCase("low+++")]
	[TestCase("high-+")]
	public void InvalidWealthDoesntParse(string text)
	{
		Assert.That(ProfessionWealth.TryParse(text, out _), Is.False);
	}
}
