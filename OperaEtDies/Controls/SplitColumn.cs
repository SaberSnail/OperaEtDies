using System.Windows;
using System.Windows.Controls;

namespace OperaEtDies.Controls;

/// <summary>
/// A column split in two by a splitter the player can drag, such as Concerns over the log in the right column. The bottom
/// part can be collapsed down to its own heading (its content shows only that while <see cref="IsBottomCollapsed"/>),
/// giving the top part the rest of the room. Expanding it again restores the split as it was.
/// </summary>
[TemplatePart(Name = GridPartName, Type = typeof(Grid))]
public sealed class SplitColumn : ContentControl
{
	public static readonly DependencyProperty TopContentProperty = DependencyProperty.Register(nameof(TopContent), typeof(object), typeof(SplitColumn));

	public static readonly DependencyProperty IsBottomCollapsedProperty = DependencyProperty.Register(nameof(IsBottomCollapsed), typeof(bool), typeof(SplitColumn),
		new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((SplitColumn) d).UpdateRows()));


	/// <summary>
	/// What's in the top part. The content is the bottom part.
	/// </summary>
	public object? TopContent
	{
		get => GetValue(TopContentProperty);
		set => SetValue(TopContentProperty, value);
	}

	/// <summary>
	/// Whether the bottom part is collapsed down to its heading.
	/// </summary>
	public bool IsBottomCollapsed
	{
		get => (bool) GetValue(IsBottomCollapsedProperty);
		set => SetValue(IsBottomCollapsedProperty, value);
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		if (e.Property == FontSizeProperty)
			UpdateRows();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		m_grid = GetTemplateChild(GridPartName) as Grid;
		m_expandedHeights = null;
		UpdateRows();
	}

	/// <summary>
	/// Sizes the rows for whether the bottom part is collapsed: the top part takes the room the bottom part's heading
	/// leaves, or the split goes back to where it was. Each part has a smallest height in ems, so the splitter can't be
	/// dragged to hide either one, except that a collapsed bottom part is only as tall as its heading.
	/// </summary>
	private void UpdateRows()
	{
		if (m_grid is { RowDefinitions: [var top, _, var bottom] })
		{
			top.MinHeight = FontSize * TopMinimumEms;
			if (IsBottomCollapsed)
			{
				m_expandedHeights ??= (top.Height, bottom.Height);
				top.Height = new GridLength(1, GridUnitType.Star);
				bottom.Height = GridLength.Auto;
				bottom.MinHeight = 0;
			}
			else
			{
				if (m_expandedHeights is { } heights)
				{
					top.Height = heights.Top;
					bottom.Height = heights.Bottom;
					m_expandedHeights = null;
				}
				bottom.MinHeight = FontSize * BottomMinimumEms;
			}
		}
	}

	private const string GridPartName = "PART_Grid";
	private const double TopMinimumEms = 4;
	private const double BottomMinimumEms = 5;

	private Grid? m_grid;
	private (GridLength Top, GridLength Bottom)? m_expandedHeights;
}
