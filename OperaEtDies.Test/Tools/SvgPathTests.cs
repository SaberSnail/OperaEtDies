using System.Windows;
using OperaEtDies.Data;
using PortraitEditor.Model;

namespace OperaEtDies.Test.Tools;

[TestFixture]
public sealed class SvgPathTests
{
	[TestCase("M0,0 H400 V480 H0 Z")]
	[TestCase("M22,64 h76 v100 h-76 Z")]
	[TestCase("F1 M8,130 a50,50 0 1 1 100,0 a50,50 0 1 1 -100,0 Z")]
	[TestCase("F0 M141,204 C141,236 149,250 151,264 S160,280 171.6,288 Q200,312 228.4,288 T250,260")]
	[TestCase("M1,2 3,4 5,6 z m1,1 l2,2 2,-2")]
	[TestCase("  M1-2L3.5.5  ")]
	public void UnchangedPathKeepsItsText(string text)
	{
		Assert.That(SvgPath.TryParse(text, out var path, out var error), Is.True, error);
		Assert.That(path!.ToString(), Is.EqualTo(text));
	}

	[Test]
	public void ShippedPathsCanBeEdited()
	{
		var result = PortraitData.Load(JsonDataLoader.DefaultDataDirectory, []);
		Assert.That(result?.Value, Is.Not.Null);

		var paths = result!.Value!.Layers.SelectMany(x => x.Shapes).Select(x => x.Path).ToList();
		var failures = paths.Where(x => !SvgPath.TryParse(x, out var path, out _) || path.ToString() != x).ToList();
		Assert.That(failures, Is.Empty);
	}

	[TestCase("M0,0 X10,10")]
	[TestCase("M0,0 L10")]
	[TestCase("10,10")]
	[TestCase("M0,0 Z 10,10")]
	public void InvalidPathIsRejected(string text)
	{
		Assert.That(SvgPath.TryParse(text, out _, out var error), Is.False);
		Assert.That(error, Is.Not.Empty);
	}

	[Test]
	public void MovingAPointLeavesTheOthersInPlace()
	{
		Assert.That(Move("M10,10 L20,20 L30,10", 1, new Point(25, 25)), Is.EqualTo("M10,10 L25,25 L30,10"));
		Assert.That(Move("M10,10 l10,10 l10,-10", 1, new Point(25, 25)), Is.EqualTo("M10,10 l15,15 l5,-15"));
	}

	[Test]
	public void LinesAcrossFollowTheirStart()
	{
		Assert.That(Move("M10,10 h20 v10", 0, new Point(0, 0)), Is.EqualTo("M0,0 h30 v20"));
	}

	[Test]
	public void MovingTheEndOfACurveMovesItsHandles()
	{
		Assert.That(Move("M0,0 C0,10 10,10 10,0 C10,-10 20,-10 20,0", 1, new Point(12, 2)), Is.EqualTo("M0,0 C0,10 12,12 12,2 C12,-8 20,-10 20,0"));
	}

	[Test]
	public void MovingTheStartOfAClosedFigureMovesItsEnd()
	{
		Assert.That(Move("M0,0 L10,0 L0,0 Z", 0, new Point(1, 1)), Is.EqualTo("M1,1 L10,0 L1,1 Z"));
	}

	[Test]
	public void MovingTheEndOfAClosedFigureMovesItsStart()
	{
		Assert.That(Move("M0,0 C0,5 5,10 10,10 C10,5 5,0 0,0 Z", 2, new Point(0, -2)), Is.EqualTo("M0,-2 C0,3 5,10 10,10 C10,5 5,-2 0,-2 Z"));
	}

	[Test]
	public void MovingAControlPoint()
	{
		Assert.That(SvgPath.TryParse("M0,0 Q5,10 10,0", out var path, out _), Is.True);
		var control = path!.GetPoints().Single(x => x.Role == SvgPointRole.Control1);
		path.MovePoint(control, new Point(5, 20));
		Assert.That(path.ToString(), Is.EqualTo("M0,0 Q5,20 10,0"));
	}

	[Test]
	public void TransformStretchesAndMoves()
	{
		Assert.That(SvgPath.TryParse("M0,0 L10,10 a5,5 0 0 1 10,0", out var path, out _), Is.True);
		path!.Transform(new Point(0, 0), 2, 2, new Vector(5, 5));
		Assert.That(path.ToString(), Is.EqualTo("M5,5 L25,25 a10,10 0 0 1 20,0"));
	}

	[Test]
	public void MovedPointsAreRounded()
	{
		Assert.That(Move("M0,0 L10,10", 1, new Point(1.234, 5.678)), Is.EqualTo("M0,0 L1.2,5.7"));
	}

	/// <summary>
	/// Moves the end of the segment at the index.
	/// </summary>
	private static string Move(string text, int segmentIndex, Point position)
	{
		Assert.That(SvgPath.TryParse(text, out var path, out var error), Is.True, error);
		var point = path!.GetPoints().Single(x => x.SegmentIndex == segmentIndex && x.Role == SvgPointRole.End);
		path.MovePoint(point, position);
		return path.ToString();
	}
}
