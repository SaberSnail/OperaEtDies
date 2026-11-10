using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class ModPathTests
{
	[Test]
	public void ParsesNamesIdsAndIndexes()
	{
		Assert.That(ModPath.TryParse("properties[hair].values[red].frequencyWhen[0]", out var segments, out var error), Is.True, error);

		Assert.That(segments!.Select(x => x.ToString()), Is.EqualTo(new[] { "properties", "[hair]", "values", "[red]", "frequencyWhen", "[0]" }));
		Assert.That(segments![1].GetIndex(), Is.Null);
		Assert.That(segments![5].GetIndex(), Is.EqualTo(0));
	}

	[Test]
	public void QuotedBracketsAreIds()
	{
		Assert.That(ModPath.TryParse("hides['*'].x['4']['a.b]c']", out var segments, out var error), Is.True, error);

		Assert.That(segments!.Select(x => x.Text), Is.EqualTo(new[] { "hides", "*", "x", "4", "a.b]c" }));
		Assert.That(segments![3].GetIndex(), Is.Null);
	}

	[TestCase("")]
	[TestCase(".a")]
	[TestCase("a.")]
	[TestCase("a..b")]
	[TestCase("a[")]
	[TestCase("a[]")]
	[TestCase("a[b")]
	[TestCase("a]")]
	[TestCase("a[b]c")]
	[TestCase("a['b]")]
	public void RejectsMalformedPaths(string path)
	{
		Assert.That(ModPath.TryParse(path, out _, out var error), Is.False);
		Assert.That(error, Is.Not.Empty);
	}

	[Test]
	public void FormatsPaths()
	{
		ModPath.TryParse("a[b].c[0]", out var segments, out _);

		Assert.That(ModPath.Format(segments!), Is.EqualTo("a[b].c[0]"));
	}
}
