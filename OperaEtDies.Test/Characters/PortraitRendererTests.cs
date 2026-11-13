using OperaEtDies.Characters;

namespace OperaEtDies.Test.Characters;

[TestFixture]
public sealed class PortraitRendererTests
{
	[TestCase(true, true, null, null, ExpectedResult = true)]
	[TestCase(false, true, null, null, ExpectedResult = false)]
	[TestCase(true, false, null, null, ExpectedResult = false)]
	[TestCase(false, true, true, null, ExpectedResult = true)]
	[TestCase(false, false, true, null, ExpectedResult = false)]
	[TestCase(true, true, false, null, ExpectedResult = false)]
	[TestCase(false, false, null, true, ExpectedResult = true)]
	[TestCase(true, true, null, false, ExpectedResult = false)]
	[TestCase(true, true, true, false, ExpectedResult = false)]
	[TestCase(false, false, false, true, ExpectedResult = true)]
	public bool IsShapeDrawnFollowsConditionsAndOverrides(bool isLayerDrawn, bool isShapeConditionMet, bool? layerOverride, bool? shapeOverride) =>
		PortraitRenderer.IsShapeDrawn(isLayerDrawn, isShapeConditionMet, layerOverride, shapeOverride);
}
