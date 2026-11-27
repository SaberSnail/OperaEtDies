using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OperaEtDies.Controls;

namespace OperaEtDies.Test.Controls;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class DoubleRuleTests
{
	[Test]
	public void IsTwoLinesAndTheGapThick()
	{
		var horizontal = new DoubleRule { Gap = 2 };
		var vertical = new DoubleRule { Gap = 3, Orientation = Orientation.Vertical };

		horizontal.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		vertical.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

		Assert.That(horizontal.DesiredSize, Is.EqualTo(new Size(0, 4)));
		Assert.That(vertical.DesiredSize, Is.EqualTo(new Size(5, 0)));
	}

	[Test]
	public void MakesRoomForItsOrnament()
	{
		var rule = new DoubleRule { Ornament = Geometry.Parse("M12 6 L18 12 L12 18 L6 12 Z"), OrnamentSize = 12 };

		rule.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

		Assert.That(rule.DesiredSize, Is.EqualTo(new Size(12, 12)));
	}

	[Test]
	public void IgnoresAnOrnamentWhenVertical()
	{
		var rule = new DoubleRule { Orientation = Orientation.Vertical, Ornament = Geometry.Parse("M0 0 H24 V24 Z"), OrnamentSize = 12 };

		rule.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

		Assert.That(rule.DesiredSize, Is.EqualTo(new Size(4, 0)));
	}
}
