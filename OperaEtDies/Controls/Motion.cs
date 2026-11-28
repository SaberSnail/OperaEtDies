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
	/// How long something in the center takes to unfold or change size, such as a conversation opening in a person's line.
	/// </summary>
	public static TimeSpan UnfoldDuration { get; } = TimeSpan.FromMilliseconds(150);

	/// <summary>
	/// Unfolds an element downward from nothing while it fades in, easing out, pushing what's below it down as it grows.
	/// </summary>
	public static void UnfoldIn(FrameworkElement element)
	{
		ArgumentNullException.ThrowIfNull(element);
		if (IsReduced)
			return;

		if (element.LayoutTransform is not ScaleTransform transform || transform.IsFrozen)
		{
			transform = new ScaleTransform();
			element.LayoutTransform = transform;
		}

		var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
		Start(transform, ScaleTransform.ScaleYProperty, new DoubleAnimation(0, 1, UnfoldDuration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
		Start(element, UIElement.OpacityProperty, new DoubleAnimation(0, 1, UnfoldDuration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
	}

	/// <summary>
	/// How long something waits before it appears (see <see cref="AppearAfterDelay"/>), so it doesn't flash past if
	/// it's only shown for a moment.
	/// </summary>
	public static TimeSpan AppearDelay { get; } = TimeSpan.FromMilliseconds(300);

	/// <summary>
	/// How long something takes to fade in once its delay is over.
	/// </summary>
	public static TimeSpan FadeDuration { get; } = TimeSpan.FromMilliseconds(200);

	/// <summary>
	/// Keeps an element hidden for a moment, then fades it in, such as the loading screen, which is often only shown for
	/// an instant. With reduced motion, it still waits, but then appears at once.
	/// </summary>
	public static void AppearAfterDelay(UIElement element)
	{
		ArgumentNullException.ThrowIfNull(element);
		var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
		animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
		animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(AppearDelay)));

		// Once the animation ends, the element is drawn at its own opacity, so with reduced motion it appears at once.
		if (!IsReduced)
			animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(AppearDelay + FadeDuration)));
		Start(element, UIElement.OpacityProperty, animation);
	}

	/// <summary>
	/// Whether an element waits a moment before it appears (see <see cref="AppearAfterDelay"/>) each time it's loaded:
	/// <c>controls:Motion.AppearsAfterDelay="True"</c>.
	/// </summary>
	public static readonly DependencyProperty AppearsAfterDelayProperty =
		DependencyProperty.RegisterAttached("AppearsAfterDelay", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnAppearsAfterDelayChanged));

	public static bool GetAppearsAfterDelay(DependencyObject d) => (bool) d.GetValue(AppearsAfterDelayProperty);

	public static void SetAppearsAfterDelay(DependencyObject d, bool value) => d.SetValue(AppearsAfterDelayProperty, value);

	private static void OnAppearsAfterDelayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not FrameworkElement element)
			throw new InvalidOperationException($"{nameof(AppearsAfterDelayProperty)} can only be set on a {nameof(FrameworkElement)}.");

		element.Loaded -= OnAppearingElementLoaded;
		if ((bool) e.NewValue)
			element.Loaded += OnAppearingElementLoaded;
	}

	private static void OnAppearingElementLoaded(object sender, RoutedEventArgs e) => AppearAfterDelay((UIElement) sender);

	/// <summary>
	/// Whether an element unfolds (see <see cref="UnfoldIn"/>) each time it's shown, such as the conversation in a person's
	/// line: <c>controls:Motion.UnfoldsWhenShown="True"</c>.
	/// </summary>
	public static readonly DependencyProperty UnfoldsWhenShownProperty =
		DependencyProperty.RegisterAttached("UnfoldsWhenShown", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnUnfoldsWhenShownChanged));

	public static bool GetUnfoldsWhenShown(DependencyObject d) => (bool) d.GetValue(UnfoldsWhenShownProperty);

	public static void SetUnfoldsWhenShown(DependencyObject d, bool value) => d.SetValue(UnfoldsWhenShownProperty, value);

	/// <summary>
	/// Whether an element's width and height change gradually, easing out, when they're changed, such as by a style
	/// trigger: <c>controls:Motion.AnimatesSize="True"</c>. Only sizes set as numbers are animated.
	/// </summary>
	public static readonly DependencyProperty AnimatesSizeProperty =
		DependencyProperty.RegisterAttached("AnimatesSize", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnAnimatesSizeChanged));

	public static bool GetAnimatesSize(DependencyObject d) => (bool) d.GetValue(AnimatesSizeProperty);

	public static void SetAnimatesSize(DependencyObject d, bool value) => d.SetValue(AnimatesSizeProperty, value);

	private static void OnUnfoldsWhenShownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not FrameworkElement element)
			throw new InvalidOperationException($"{nameof(UnfoldsWhenShownProperty)} can only be set on a {nameof(FrameworkElement)}.");

		element.IsVisibleChanged -= OnUnfoldingElementVisibleChanged;
		if ((bool) e.NewValue)
			element.IsVisibleChanged += OnUnfoldingElementVisibleChanged;
	}

	private static void OnUnfoldingElementVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if ((bool) e.NewValue)
			UnfoldIn((FrameworkElement) sender);
	}

	private static void OnAnimatesSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not FrameworkElement element)
			throw new InvalidOperationException($"{nameof(AnimatesSizeProperty)} can only be set on a {nameof(FrameworkElement)}.");

		// A change of size always leads to a layout, so the size is compared with the last one after each.
		element.LayoutUpdated -= GetSizeWatcher(element).OnLayoutUpdated;
		if ((bool) e.NewValue)
			element.LayoutUpdated += GetSizeWatcher(element).OnLayoutUpdated;
	}

	private static SizeWatcher GetSizeWatcher(FrameworkElement element)
	{
		if (element.GetValue(SizeWatcherProperty) is not SizeWatcher watcher)
		{
			watcher = new SizeWatcher(element);
			element.SetValue(SizeWatcherProperty, watcher);
		}
		return watcher;
	}

	private static readonly DependencyProperty SizeWatcherProperty =
		DependencyProperty.RegisterAttached("SizeWatcher", typeof(SizeWatcher), typeof(Motion));

	/// <summary>
	/// Follows an element's width and height as they're set, and animates each change from the size it had.
	/// </summary>
	private sealed class SizeWatcher(FrameworkElement element)
	{
		public void OnLayoutUpdated(object? sender, EventArgs e)
		{
			// The size it's being animated to, not the size it's drawn at while it animates.
			var (width, height) = ((double) element.GetAnimationBaseValue(FrameworkElement.WidthProperty), (double) element.GetAnimationBaseValue(FrameworkElement.HeightProperty));
			if (m_width is { } oldWidth && m_height is { } oldHeight && (!width.Equals(oldWidth) || !height.Equals(oldHeight)) && !IsReduced)
			{
				var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
				if (!double.IsNaN(oldWidth) && !double.IsNaN(width))
					Start(element, FrameworkElement.WidthProperty, new DoubleAnimation(oldWidth, width, UnfoldDuration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
				if (!double.IsNaN(oldHeight) && !double.IsNaN(height))
					Start(element, FrameworkElement.HeightProperty, new DoubleAnimation(oldHeight, height, UnfoldDuration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
			}
			(m_width, m_height) = (width, height);
		}

		private double? m_width;
		private double? m_height;
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
