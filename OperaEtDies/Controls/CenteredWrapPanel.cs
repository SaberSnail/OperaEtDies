using System.Windows;
using System.Windows.Controls;

namespace OperaEtDies.Controls;

/// <summary>
/// Lays out its children left to right, wrapping onto as many rows as its width needs, like a horizontal
/// <see cref="WrapPanel"/>, but with each row centered on its own, so a short last row is centered too.
/// </summary>
public sealed class CenteredWrapPanel : Panel
{
	protected override Size MeasureOverride(Size availableSize)
	{
		var childAvailable = new Size(availableSize.Width, double.PositiveInfinity);
		double width = 0;
		double height = 0;
		double rowWidth = 0;
		double rowHeight = 0;
		foreach (UIElement child in InternalChildren)
		{
			child.Measure(childAvailable);
			var size = child.DesiredSize;
			if (rowWidth > 0 && rowWidth + size.Width > availableSize.Width)
			{
				width = Math.Max(width, rowWidth);
				height += rowHeight;
				rowWidth = 0;
				rowHeight = 0;
			}
			rowWidth += size.Width;
			rowHeight = Math.Max(rowHeight, size.Height);
		}
		return new Size(Math.Max(width, rowWidth), height + rowHeight);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		var children = InternalChildren;
		double top = 0;
		var rowStart = 0;
		double rowWidth = 0;
		double rowHeight = 0;
		for (var index = 0; index < children.Count; index++)
		{
			var size = children[index].DesiredSize;
			if (rowWidth > 0 && rowWidth + size.Width > finalSize.Width)
			{
				ArrangeRow(rowStart, index, top, (finalSize.Width - rowWidth) / 2, rowHeight);
				top += rowHeight;
				rowStart = index;
				rowWidth = 0;
				rowHeight = 0;
			}
			rowWidth += size.Width;
			rowHeight = Math.Max(rowHeight, size.Height);
		}
		ArrangeRow(rowStart, children.Count, top, (finalSize.Width - rowWidth) / 2, rowHeight);
		return finalSize;
	}

	private void ArrangeRow(int start, int end, double top, double left, double height)
	{
		var x = Math.Max(0, left);
		for (var index = start; index < end; index++)
		{
			var child = InternalChildren[index];
			child.Arrange(new Rect(x, top, child.DesiredSize.Width, height));
			x += child.DesiredSize.Width;
		}
	}
}
