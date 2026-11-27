using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OperaEtDies.Controls;

/// <summary>
/// Every animation in the game goes through here, so reducing motion turns them all off in one place, and any input can
/// finish them all at once (see <see cref="FinishAll"/>), so the player never waits on one. Animations only ever change how
/// something looks on its way to where it already is: the element's own values are left at the end state, and the
/// animation is drawn over them until it's done or finished early.
/// </summary>
public static class Motion
{
	/// <summary>
	/// Whether animations are turned off, so everything appears at once. It follows Windows' own setting for showing
	/// animations until the settings screen can change it.
	/// </summary>
	public static bool IsReduced { get; set; } = !SystemParameters.ClientAreaAnimation;

	/// <summary>
	/// How long a sheet takes to slide in.
	/// </summary>
	public static TimeSpan SheetDuration { get; } = TimeSpan.FromMilliseconds(150);

	/// <summary>
	/// Slides an element in from a little to its right while it fades in, easing out, as a sheet opening.
	/// </summary>
	public static void SlideIn(UIElement element, double offset)
	{
		ArgumentNullException.ThrowIfNull(element);
		if (IsReduced)
			return;

		if (element.RenderTransform is not TranslateTransform transform || transform.IsFrozen)
		{
			transform = new TranslateTransform();
			element.RenderTransform = transform;
		}

		var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
		Start(transform, TranslateTransform.XProperty, new DoubleAnimation(offset, 0, SheetDuration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
		Start(element, UIElement.OpacityProperty, new DoubleAnimation(0, 1, SheetDuration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
	}

	/// <summary>
	/// Finishes every running animation at once, such as when the player presses a key or a mouse button.
	/// </summary>
	public static void FinishAll()
	{
		var running = s_running.ToList();
		s_running.Clear();
		foreach (var clock in running)
			clock.Controller?.Remove();
	}

	private static void Start(IAnimatable target, DependencyProperty property, AnimationTimeline animation)
	{
		var clock = animation.CreateClock();
		clock.Completed += (_, _) => s_running.Remove(clock);
		s_running.Add(clock);
		target.ApplyAnimationClock(property, clock);
	}

	private static readonly HashSet<AnimationClock> s_running = [];
}
