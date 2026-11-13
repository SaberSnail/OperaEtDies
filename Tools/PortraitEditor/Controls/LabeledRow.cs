using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PortraitEditor.Controls;

/// <summary>
/// Lays out a row of a form: a label (the first child), a field (the second child), and anything after the field
/// (such as buttons) at the right end, at their own widths. The label is as wide as the widest label of the rows in
/// the same alignment scope, so the fields line up, but gives up width when the row is too narrow for the field to
/// have <see cref="MinFieldWidth"/>. A text block label that's cut short is shown in full in a tooltip, so labels
/// should cut their text off with an ellipsis.
/// </summary>
public sealed class LabeledRow : Panel
{
	public static readonly DependencyProperty MinFieldWidthProperty = DependencyProperty.Register(nameof(MinFieldWidth), typeof(double), typeof(LabeledRow),
		new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

	/// <summary>
	/// Marks an element whose rows line up their labels with each other.
	/// </summary>
	public static readonly DependencyProperty IsAlignmentScopeProperty = DependencyProperty.RegisterAttached("IsAlignmentScope", typeof(bool), typeof(LabeledRow), new PropertyMetadata(false));

	public static bool GetIsAlignmentScope(DependencyObject element) => (bool) element.GetValue(IsAlignmentScopeProperty);

	public static void SetIsAlignmentScope(DependencyObject element, bool value) => element.SetValue(IsAlignmentScopeProperty, value);

	public LabeledRow()
	{
		IsVisibleChanged += OnIsVisibleChanged;
	}

	/// <summary>
	/// The narrowest the field gets before the label starts to shrink.
	/// </summary>
	public double MinFieldWidth
	{
		get => (double) GetValue(MinFieldWidthProperty);
		set => SetValue(MinFieldWidthProperty, value);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		var height = 0.0;
		var trailingWidth = 0.0;
		foreach (var child in Children.OfType<UIElement>().Skip(2))
		{
			child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
			trailingWidth += child.DesiredSize.Width;
			height = Math.Max(height, child.DesiredSize.Height);
		}

		var labelWidth = 0.0;
		if (Label is { } label)
		{
			label.Measure(new Size(double.PositiveInfinity, availableSize.Height));
			m_fullLabelWidth = label.DesiredSize.Width;
			m_scope?.Report(this, m_fullLabelWidth);
			labelWidth = GetLabelWidth(availableSize.Width, trailingWidth);
			label.Measure(new Size(labelWidth, availableSize.Height));
			height = Math.Max(height, label.DesiredSize.Height);
		}

		var fieldWidth = 0.0;
		if (Field is { } field)
		{
			field.Measure(new Size(double.IsPositiveInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, availableSize.Width - labelWidth - trailingWidth), availableSize.Height));
			fieldWidth = field.DesiredSize.Width;
			height = Math.Max(height, field.DesiredSize.Height);
		}

		return new Size(labelWidth + fieldWidth + trailingWidth, height);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		var right = finalSize.Width;
		foreach (var child in Children.OfType<UIElement>().Skip(2).Reverse())
		{
			right -= child.DesiredSize.Width;
			child.Arrange(new Rect(Math.Max(0, right), 0, child.DesiredSize.Width, finalSize.Height));
		}

		var labelWidth = 0.0;
		if (Label is { } label)
		{
			labelWidth = Math.Min(GetLabelWidth(finalSize.Width, finalSize.Width - right), Math.Max(0, right));
			label.Arrange(new Rect(0, 0, labelWidth, finalSize.Height));
			if (label is TextBlock text)
				text.ToolTip = m_fullLabelWidth > labelWidth + 0.5 ? text.Text : null;
		}

		Field?.Arrange(new Rect(labelWidth, 0, Math.Max(0, right - labelWidth), finalSize.Height));
		return finalSize;
	}

	/// <summary>
	/// Gets the label's width: as wide as the widest label in the scope, unless that leaves the field less than its
	/// smallest width.
	/// </summary>
	private double GetLabelWidth(double rowWidth, double trailingWidth)
	{
		var alignedWidth = Math.Max(m_scope?.LabelWidth ?? 0, m_fullLabelWidth);
		return double.IsPositiveInfinity(rowWidth) ? alignedWidth : Math.Clamp(rowWidth - trailingWidth - MinFieldWidth, 0, alignedWidth);
	}

	private UIElement? Label => Children.Count > 0 ? Children[0] : null;

	private UIElement? Field => Children.Count > 1 ? Children[1] : null;

	/// <summary>
	/// Joins the nearest alignment scope while the row is shown, and leaves it while hidden, so hidden rows don't
	/// make the labels wider.
	/// </summary>
	private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		m_scope?.Remove(this);
		m_scope = null;
		if (IsVisible)
		{
			m_scope = FindScope();
			InvalidateMeasure();
		}
	}

	private AlignmentScope? FindScope()
	{
		for (var element = VisualTreeHelper.GetParent(this); element is not null; element = VisualTreeHelper.GetParent(element))
		{
			if (GetIsAlignmentScope(element))
			{
				if (element.GetValue(s_scopeProperty) is not AlignmentScope scope)
				{
					scope = new AlignmentScope();
					element.SetValue(s_scopeProperty, scope);
				}

				return scope;
			}
		}

		return null;
	}

	/// <summary>
	/// The rows whose labels line up, with the full width of each one's label.
	/// </summary>
	private sealed class AlignmentScope
	{
		public double LabelWidth { get; private set; }

		public void Report(LabeledRow row, double width)
		{
			if (!m_widths.TryGetValue(row, out var oldWidth) || oldWidth != width)
			{
				m_widths[row] = width;
				Update();
			}
		}

		public void Remove(LabeledRow row)
		{
			if (m_widths.Remove(row))
				Update();
		}

		private void Update()
		{
			var labelWidth = m_widths.Count == 0 ? 0 : m_widths.Values.Max();
			if (labelWidth != LabelWidth)
			{
				LabelWidth = labelWidth;
				foreach (var row in m_widths.Keys)
					row.InvalidateMeasure();
			}
		}

		private readonly Dictionary<LabeledRow, double> m_widths = [];
	}

	private static readonly DependencyProperty s_scopeProperty = DependencyProperty.RegisterAttached("Scope", typeof(AlignmentScope), typeof(LabeledRow));

	private AlignmentScope? m_scope;
	private double m_fullLabelWidth;
}
