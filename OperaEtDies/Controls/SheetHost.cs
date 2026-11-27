using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace OperaEtDies.Controls;

/// <summary>
/// Draws the sheets over the center, stacked one on another, showing only the top one. The ones under it are only hidden,
/// not removed, so each comes back as it was (such as scrolled to the same place) when the sheets over it close. A sheet
/// slides in as it opens (see <see cref="Motion"/>), and closes at once.
/// </summary>
public sealed class SheetHost : ItemsControl
{
	static SheetHost()
	{
		var panel = new FrameworkElementFactory(typeof(Grid));
		ItemsPanelProperty.OverrideMetadata(typeof(SheetHost), new FrameworkPropertyMetadata(new ItemsPanelTemplate(panel)));
		FocusableProperty.OverrideMetadata(typeof(SheetHost), new FrameworkPropertyMetadata(false));
	}

	protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
	{
		base.PrepareContainerForItemOverride(element, item);
		if (element is UIElement container)
		{
			var isTop = Items.Count != 0 && ReferenceEquals(Items[^1], item);
			container.Visibility = isTop ? Visibility.Visible : Visibility.Collapsed;

			// A sheet opening slides in by a few characters' width.
			if (isTop && IsLoaded)
				Motion.SlideIn(container, FontSize * 3);
		}
	}

	protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
	{
		base.OnItemsChanged(e);
		for (var index = 0; index < Items.Count; index++)
		{
			if (ItemContainerGenerator.ContainerFromIndex(index) is UIElement container)
				container.Visibility = index == Items.Count - 1 ? Visibility.Visible : Visibility.Collapsed;
		}
	}
}
