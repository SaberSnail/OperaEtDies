using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace OperaEtDies.Controls.Log;

/// <summary>
/// Attached properties for showing the event log: a text block filled with an entry's pieces, with the people (with their
/// tooltip) and places in it as links, and a scroll viewer that stays scrolled to the end as entries are added, which a
/// conversation also uses.
/// </summary>
public static class LogViewBehaviors
{
	public static readonly DependencyProperty PartsProperty = DependencyProperty.RegisterAttached("Parts", typeof(IReadOnlyList<LogTextPartViewModel>), typeof(LogViewBehaviors),
		new PropertyMetadata(null, OnPartsChanged));

	public static IReadOnlyList<LogTextPartViewModel>? GetParts(DependencyObject target) => (IReadOnlyList<LogTextPartViewModel>?) target.GetValue(PartsProperty);

	public static void SetParts(DependencyObject target, IReadOnlyList<LogTextPartViewModel>? value) => target.SetValue(PartsProperty, value);

	/// <summary>
	/// Whether a scroll viewer stays scrolled to the end as its content grows, as long as it was at the end already.
	/// </summary>
	public static readonly DependencyProperty IsScrolledToEndProperty = DependencyProperty.RegisterAttached("IsScrolledToEnd", typeof(bool), typeof(LogViewBehaviors),
		new PropertyMetadata(false, OnIsScrolledToEndChanged));

	public static bool GetIsScrolledToEnd(DependencyObject target) => (bool) target.GetValue(IsScrolledToEndProperty);

	public static void SetIsScrolledToEnd(DependencyObject target, bool value) => target.SetValue(IsScrolledToEndProperty, value);

	private static void OnPartsChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
	{
		if (target is not TextBlock textBlock)
			throw new InvalidOperationException($"{nameof(PartsProperty)} can only be set on a {nameof(TextBlock)}.");

		textBlock.Inlines.Clear();
		foreach (var part in (IReadOnlyList<LogTextPartViewModel>?) e.NewValue ?? [])
		{
			if (part.IsLink)
			{
				var link = new Hyperlink(new Run(part.Text));
				link.SetResourceReference(FrameworkContentElement.StyleProperty, "LogLinkStyle");
				link.Click += (_, _) => part.Click();
				if (part.Person is not null)
				{
					// The person's tooltip is only made once it's about to show.
					var toolTip = new ToolTip();
					toolTip.SetResourceReference(ContentControl.ContentTemplateProperty, "PersonToolTipTemplate");
					link.ToolTip = toolTip;
					link.ToolTipOpening += (_, _) => toolTip.Content ??= part.GetPersonToolTip();
				}
				textBlock.Inlines.Add(link);
			}
			else
			{
				textBlock.Inlines.Add(new Run(part.Text));
			}
		}
	}

	private static void OnIsScrolledToEndChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
	{
		if (target is not ScrollViewer scrollViewer)
			throw new InvalidOperationException($"{nameof(IsScrolledToEndProperty)} can only be set on a {nameof(ScrollViewer)}.");

		scrollViewer.ScrollChanged -= OnScrollChanged;
		if ((bool) e.NewValue)
			scrollViewer.ScrollChanged += OnScrollChanged;
	}

	/// <summary>
	/// Follows the content as it grows, unless the player has scrolled back to read older content.
	/// </summary>
	private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (e.ExtentHeightChange > 0 && e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - e.ExtentHeightChange - 1)
			((ScrollViewer) sender).ScrollToEnd();
	}
}
