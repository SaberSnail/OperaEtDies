using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using OperaEtDies.Controls;

namespace OperaEtDies.Test.Controls;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class SplitButtonTests
{
	[Test]
	public void ItsTemplateFindsTheSharedResources()
	{
		var button = CreateButton(new SplitActionViewModel("Wait", () => { }, alternatives: [new MenuActionViewModel("Wait an hour", () => { })]));

		// The template loads, with the chevron shown for the menu.
		Assert.That(button.Template.FindName("PART_Chevron", button), Is.TypeOf<ToggleButton>().With.Property(nameof(UIElement.Visibility)).EqualTo(Visibility.Visible));
	}

	[Test]
	public void TheChevronIsDisabledWhileTheDefaultIs()
	{
		var button = CreateButton(new SplitActionViewModel("Walk there", () => { }, () => false, [new MenuActionViewModel("Walk home", () => { })]));

		var chevron = (ToggleButton) button.Template.FindName("PART_Chevron", button);
		Assert.That((chevron.Visibility, chevron.IsEnabled), Is.EqualTo((Visibility.Visible, false)));
	}

	[Test]
	public void TheChevronIsHiddenWithoutAMenu()
	{
		var button = CreateButton(new SplitActionViewModel("Wait", () => { }));

		Assert.That(((ToggleButton) button.Template.FindName("PART_Chevron", button)).Visibility, Is.EqualTo(Visibility.Collapsed));
	}

	/// <summary>
	/// Creates a split button for an action, with the theme and the shared styles the game uses, and lays it out.
	/// </summary>
	private static SplitButton CreateButton(SplitActionViewModel action)
	{
		// The pack scheme needs an application.
		_ = Application.Current ?? new Application();
		var resources = new ResourceDictionary();
		resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/OperaEtDies;component/Themes/Default/Default.xaml") });
		resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/OperaEtDies;component/Themes/Shared/Shared.xaml") });
		var button = new SplitButton { Action = action };
		var host = new Border { Resources = resources, Child = button };
		host.Measure(new Size(400, 100));
		host.Arrange(new Rect(0, 0, 400, 100));
		return button;
	}
}
