using OperaEtDies.Controls;

namespace OperaEtDies.Test.Controls;

[TestFixture]
public sealed class ColumnLayoutTests
{
	[TestCase(1100, 16, false, false)]
	[TestCase(1100, 19.2, true, false)]
	[TestCase(1100, 24, true, true)]
	[TestCase(1920, 24, false, false)]
	public void SideColumnsGoCompactWhenTheCenterIsTooNarrow(double width, double fontSize, bool left, bool right)
	{
		Assert.That(ColumnLayout.GetCompactColumns(width, fontSize, hasLeft: true, hasRight: true, isLeftCompact: false, isRightCompact: false), Is.EqualTo((left, right)));
	}

	[Test]
	public void CompactColumnsOnlySwitchBackWithRoomToSpare()
	{
		// With normal columns, the center would have half an em more than the narrowest it can be.
		const double fontSize = 16;
		var width = ((ColumnLayout.MinimumCenterEms + 0.5 + ColumnLayout.LeftEms + ColumnLayout.RightEms) * fontSize) + ColumnLayout.CenterPadding;

		Assert.That(ColumnLayout.GetCompactColumns(width, fontSize, true, true, false, false), Is.EqualTo((false, false)));
		Assert.That(ColumnLayout.GetCompactColumns(width, fontSize, true, true, true, false), Is.EqualTo((true, false)));

		width += ColumnLayout.HysteresisEms * fontSize;
		Assert.That(ColumnLayout.GetCompactColumns(width, fontSize, true, true, true, false), Is.EqualTo((false, false)));
	}

	[Test]
	public void TheRightColumnOnlyGoesCompactOnceTheLeftIs()
	{
		// Too narrow even with the left column compact.
		const double fontSize = 16;
		var width = ((ColumnLayout.MinimumCenterEms - 1 + ColumnLayout.LeftCompactEms + ColumnLayout.RightEms) * fontSize) + ColumnLayout.CenterPadding;
		Assert.That(ColumnLayout.GetCompactColumns(width, fontSize, true, true, false, false), Is.EqualTo((true, true)));

		// Without a left column, the right one goes compact on its own.
		width = ((ColumnLayout.MinimumCenterEms - 1 + ColumnLayout.RightEms) * fontSize) + ColumnLayout.CenterPadding;
		Assert.That(ColumnLayout.GetCompactColumns(width, fontSize, false, true, false, false), Is.EqualTo((false, true)));

		// A column that isn't there is never compact.
		Assert.That(ColumnLayout.GetCompactColumns(400, fontSize, false, false, false, false), Is.EqualTo((false, false)));
	}

	[Test]
	public void NothingChangesBeforeTheLayoutHasASize()
	{
		Assert.That(ColumnLayout.GetCompactColumns(0, 16, true, true, true, false), Is.EqualTo((true, false)));
	}
}
