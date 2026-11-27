using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OperaEtDies.Controls;

namespace OperaEtDies.Test.Controls;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class DropCapTextTests
{
	[TestCase("Soot blackens the beams.", "S", "oot blackens the beams.")]
	[TestCase("  A small house.", "A", " small house.")]
	[TestCase("“Welcome,” she says.", "“W", "elcome,” she says.")]
	[TestCase("", "", "")]
	public void SplitsTheCapFromTheRest(string text, string cap, string rest)
	{
		Assert.That(DropCapText.SplitCap(text), Is.EqualTo((cap, rest)));
	}

	[Test]
	public void IndentsOnlyTheLinesBesideTheCap()
	{
		var element = CreateElement(LongText, capLines: 2);

		element.Measure(new Size(300, double.PositiveInfinity));

		Assert.That(element.CapIndent, Is.GreaterThan(0));
		Assert.That(element.Lines, Has.Count.GreaterThan(3));
		Assert.That(element.Lines.Take(2).Select(x => x.X), Is.All.EqualTo(element.CapIndent));
		Assert.That(element.Lines.Skip(2).Select(x => x.X), Is.All.EqualTo(0));
	}

	[Test]
	public void WrapsEveryLineWithinTheWidth()
	{
		var element = CreateElement(LongText, capLines: 2);

		element.Measure(new Size(300, double.PositiveInfinity));

		Assert.That(element.Lines.Select(x => x.X + x.Width), Is.All.LessThanOrEqualTo(300 + 1));
		// The last line holds the end of the paragraph too.
		Assert.That(element.Lines.Sum(x => x.Length), Is.EqualTo(DropCapText.SplitCap(LongText).Remainder.Length + 1));
		Assert.That(element.DesiredSize.Width, Is.LessThanOrEqualTo(300));
	}

	[Test]
	public void ShortTextIsAsTallAsTheCap()
	{
		var shortText = CreateElement("A cottage.", capLines: 3);
		var plain = new TextBlock { Text = "A cottage.", FontFamily = shortText.FontFamily, FontSize = shortText.FontSize };

		shortText.Measure(new Size(300, double.PositiveInfinity));
		plain.Measure(new Size(300, double.PositiveInfinity));

		Assert.That(shortText.Lines, Has.Count.EqualTo(1));
		Assert.That(shortText.DesiredSize.Height, Is.GreaterThan(plain.DesiredSize.Height * 2.5));
	}

	[Test]
	public void EmptyTextTakesNoRoom()
	{
		var element = CreateElement("", capLines: 2);

		element.Measure(new Size(300, double.PositiveInfinity));

		Assert.That(element.Cap, Is.Empty);
		Assert.That(element.Lines, Is.Empty);
		Assert.That(element.DesiredSize, Is.EqualTo(new Size(0, 0)));
	}

	[Test]
	public void LaysOutAgainWhenTheWidthChanges()
	{
		var element = CreateElement(LongText, capLines: 2);

		element.Measure(new Size(300, double.PositiveInfinity));
		var narrowLines = element.Lines.Count;
		element.Measure(new Size(600, double.PositiveInfinity));

		Assert.That(element.Lines, Has.Count.LessThan(narrowLines));
	}

	[Test]
	public void PutsTheTrailerAfterTheLastLineWhenItFits()
	{
		var element = CreateElement("A small house.", capLines: 2);
		element.Trailer = new Border { Width = 30, Height = 10 };

		element.Measure(new Size(300, double.PositiveInfinity));
		element.Arrange(new Rect(element.DesiredSize));

		var last = element.Lines[^1];
		Assert.That(element.TrailerBounds.X, Is.GreaterThan(last.X + last.Width));
		Assert.That(element.TrailerBounds.Right, Is.LessThanOrEqualTo(300));
		Assert.That(element.TrailerBounds.Top, Is.GreaterThan(0));
	}

	[Test]
	public void PutsTheTrailerOnANewLineWhenItDoesntFit()
	{
		var element = CreateElement(LongText, capLines: 2);
		var withoutTrailer = CreateElement(LongText, capLines: 2);
		element.Trailer = new Border { Width = 290, Height = 10 };

		element.Measure(new Size(300, double.PositiveInfinity));
		withoutTrailer.Measure(new Size(300, double.PositiveInfinity));

		Assert.That(element.TrailerBounds.X, Is.EqualTo(0));
		Assert.That(element.TrailerBounds.Top, Is.GreaterThanOrEqualTo(withoutTrailer.DesiredSize.Height - 1));
		Assert.That(element.DesiredSize.Height, Is.GreaterThan(withoutTrailer.DesiredSize.Height));
	}

	[Test]
	public void ACollapsedTrailerTakesNoRoom()
	{
		var element = CreateElement(LongText, capLines: 2);
		var withoutTrailer = CreateElement(LongText, capLines: 2);
		element.Trailer = new Border { Width = 290, Height = 10, Visibility = Visibility.Collapsed };

		element.Measure(new Size(300, double.PositiveInfinity));
		withoutTrailer.Measure(new Size(300, double.PositiveInfinity));

		Assert.That((element.TrailerBounds.IsEmpty, element.DesiredSize), Is.EqualTo((true, withoutTrailer.DesiredSize)));
	}

	private static DropCapText CreateElement(string text, int capLines) =>
		new() { Text = text, CapLines = capLines, FontFamily = new FontFamily("Georgia"), FontSize = 16 };

	private const string LongText = "Soot blackens the beams above the forge, and the anvil rings out across the square whenever the door stands open. " +
		"Racks of half-finished nails and hinges line the back wall, and a barrel of quenching water stands by the hearth.";
}
