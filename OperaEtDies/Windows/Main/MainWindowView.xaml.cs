using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using OperaEtDies.Controls;
using OperaEtDies.Settings;

namespace OperaEtDies.Windows.Main;

public partial class MainWindowView : Window
{
	/// <summary>
	/// Creates the window, where it was when it was last closed if that was kept under the given registry key, and full
	/// screen if the settings say so.
	/// </summary>
	public MainWindowView(MainWindowViewModel? viewModel, string? placementKeyPath)
	{
		ViewModel = viewModel;
		m_placementKeyPath = placementKeyPath;
		InitializeComponent();
		RestorePlacement();
		TitleBar.SizeChanged += (_, _) => UpdateCaptionHeight();
		Loaded += OnLoaded;
	}

	public MainWindowViewModel? ViewModel { get; }

	/// <summary>
	/// Closing the window exits the game, unless there's a game with unsaved changes, in which case it stays open and asks
	/// whether to save first. Where the window was is kept once it closes.
	/// </summary>
	protected override void OnClosing(CancelEventArgs e)
	{
		base.OnClosing(e);
		if (!e.Cancel && ViewModel is not null && !ViewModel.RequestClose())
			e.Cancel = true;
		if (!e.Cancel)
			SavePlacement();
	}

	protected override void OnClosed(EventArgs e)
	{
		if (ViewModel is not null)
			ViewModel.Settings.PropertyChanged -= OnSettingsPropertyChanged;
		SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
		base.OnClosed(e);
	}

	/// <summary>
	/// A maximized window drawn without the system's frame would hang over the edges of the screen by the width of the
	/// frame, so its content is kept inside by that much, and the thin border around it is dropped.
	/// </summary>
	protected override void OnStateChanged(EventArgs e)
	{
		base.OnStateChanged(e);
		if (IsFullScreen)
		{
			// A full screen window has no border either way.
		}
		else if (WindowState == WindowState.Maximized)
		{
			var frame = GetMaximizedFrame();
			WindowFrame.BorderThickness = new Thickness(0);
			WindowFrame.Padding = frame;
		}
		else
		{
			WindowFrame.BorderThickness = new Thickness(1);
			WindowFrame.Padding = new Thickness(0);
		}
	}

	/// <summary>
	/// A window that's moved or resized while it's full screen, such as moved to another monitor, or resized for a change
	/// in the screen's scaling, fills the screen it's on again.
	/// </summary>
	protected override void OnLocationChanged(EventArgs e)
	{
		base.OnLocationChanged(e);
		FitToScreen();
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		base.OnRenderSizeChanged(sizeInfo);
		FitToScreen();
	}

	/// <summary>
	/// Any key or mouse button finishes running animations at once, so the player never waits on one.
	/// </summary>
	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		Motion.FinishAll();
		base.OnPreviewKeyDown(e);
	}

	/// <summary>
	/// Hotkeys (see <see cref="Hotkeys"/>) go to the view model once the control with keyboard focus has had its chance,
	/// so an open drop-down still closes on Esc. Keys that type are off while a text box has focus, and a key held down
	/// only counts once.
	/// </summary>
	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		var key = e.Key == Key.System ? e.SystemKey : e.Key;
		if (!e.Handled && !e.IsRepeat && ViewModel is not null && Hotkeys.Find(key, Keyboard.Modifiers) is { } hotkey && !(Hotkeys.IsTypingKey(hotkey) && IsTyping()))
			e.Handled = ViewModel.HandleHotkey(hotkey);
	}

	/// <summary>
	/// Whether keyboard focus is somewhere the player types, such as a text box.
	/// </summary>
	private static bool IsTyping() => Keyboard.FocusedElement is TextBoxBase or PasswordBox or ComboBox { IsEditable: true };

	protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
	{
		Motion.FinishAll();
		base.OnPreviewMouseDown(e);
	}

	private void OnLeaveFullScreenClick(object sender, RoutedEventArgs e) => ViewModel?.ToggleFullScreen();

	private void OnMinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

	private void OnMaximizeClick(object sender, RoutedEventArgs e)
	{
		if (WindowState == WindowState.Maximized)
			SystemCommands.RestoreWindow(this);
		else
			SystemCommands.MaximizeWindow(this);
	}

	private void OnCloseClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

	/// <summary>
	/// Gets how far a maximized window hangs over each edge of the screen: the width of the sizing frame and its padding, in
	/// device-independent units.
	/// </summary>
	private Thickness GetMaximizedFrame()
	{
		var dpi = (uint) Math.Round(VisualTreeHelper.GetDpi(this).PixelsPerInchX);
		var scale = dpi / 96.0;
		var width = (NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CXFRAME, dpi) + NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CXPADDEDBORDER, dpi)) / scale;
		var height = (NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CYFRAME, dpi) + NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CXPADDEDBORDER, dpi)) / scale;
		return new Thickness(width, height, width, height);
	}

	/// <summary>
	/// Whether the window fills the screen. It's kept with the settings, so the window starts full screen if it was left
	/// that way.
	/// </summary>
	private bool IsFullScreen => m_fullScreenRestore is not null;

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		Loaded -= OnLoaded;
		SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
		if (ViewModel is not null)
		{
			ViewModel.Settings.PropertyChanged += OnSettingsPropertyChanged;
			UpdateFullScreen();
		}
	}

	private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AppSettings.IsFullScreen))
			UpdateFullScreen();
	}

	private void OnDisplaySettingsChanged(object? sender, EventArgs e) => FitToScreen();

	private void UpdateFullScreen()
	{
		if (ViewModel?.Settings.IsFullScreen == true)
			EnterFullScreen();
		else
			LeaveFullScreen();
	}

	/// <summary>
	/// Fills the screen the window is mostly on, without a frame or the parts of the title bar that only matter for a
	/// window (the toolbar stays), remembering where the window was so it can go back.
	/// </summary>
	private void EnterFullScreen()
	{
		if (IsFullScreen)
			return;

		var restore = new WindowPlacement(GetNormalBounds(), WindowState == WindowState.Maximized);
		WindowState = WindowState.Normal;
		m_fullScreenRestore = restore;

		// Without a caption or a sizing frame, Windows treats the window as full screen and hides the taskbar while it's in
		// front, and the window can't be dragged, resized, snapped, or maximized.
		WindowStyle = WindowStyle.None;
		ResizeMode = ResizeMode.NoResize;
		Chrome.ResizeBorderThickness = new Thickness(0);
		UpdateCaptionHeight();
		WindowFrame.BorderThickness = new Thickness(0);
		WindowFrame.Padding = new Thickness(0);
		FitToScreen();
	}

	/// <summary>
	/// Goes back to a window where it was before it filled the screen, or keeps its size in the middle of the screen if
	/// that place is no longer on any screen.
	/// </summary>
	private void LeaveFullScreen()
	{
		if (m_fullScreenRestore is not { } restore)
			return;

		m_fullScreenRestore = null;
		WindowStyle = WindowStyle.SingleBorderWindow;
		ResizeMode = ResizeMode.CanResize;
		Chrome.ResizeBorderThickness = new Thickness(ResizeBorderThickness);
		UpdateCaptionHeight();
		WindowFrame.BorderThickness = new Thickness(1);
		WindowFrame.Padding = new Thickness(0);

		var bounds = restore.Bounds;
		if (!restore.IsOnScreen())
		{
			var workArea = SystemParameters.WorkArea;
			bounds = new Rect(workArea.Left + ((workArea.Width - bounds.Width) / 2), workArea.Top + ((workArea.Height - bounds.Height) / 2), bounds.Width, bounds.Height);
		}

		Left = bounds.Left;
		Top = bounds.Top;
		Width = bounds.Width;
		Height = bounds.Height;
		if (restore.IsMaximized)
			WindowState = WindowState.Maximized;
	}

	/// <summary>
	/// The title bar is where the window is dragged from, except in full screen, where it can't be dragged.
	/// </summary>
	private void UpdateCaptionHeight() => Chrome.CaptionHeight = IsFullScreen ? 0 : TitleBar.ActualHeight;

	/// <summary>
	/// Makes a full screen window cover the whole of the screen it's mostly on, taskbar included. Moving it there doesn't
	/// fit it again, since it's already in place.
	/// </summary>
	private void FitToScreen()
	{
		var handle = new WindowInteropHelper(this).Handle;
		if (IsFullScreen && handle != IntPtr.Zero && !m_isFittingToScreen)
		{
			m_isFittingToScreen = true;
			var monitor = NativeMethods.MonitorFromWindow(handle, NativeMethods.MONITOR_DEFAULTTONEAREST);
			var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
			if (NativeMethods.GetMonitorInfo(monitor, ref info))
			{
				var screen = info.rcMonitor;
				NativeMethods.SetWindowPos(handle, IntPtr.Zero, screen.Left, screen.Top, screen.Right - screen.Left, screen.Bottom - screen.Top, NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
			}

			m_isFittingToScreen = false;
		}
	}

	/// <summary>
	/// Gets where the window is when it's neither maximized nor full screen, or where it goes back to when it is.
	/// </summary>
	private Rect GetNormalBounds() =>
		m_fullScreenRestore is { } restore ? restore.Bounds :
		WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) :
		RestoreBounds;

	private void RestorePlacement()
	{
		if (m_placementKeyPath is not null && WindowPlacement.Load(m_placementKeyPath) is { } placement)
		{
			if (placement.IsOnScreen())
			{
				WindowStartupLocation = WindowStartupLocation.Manual;
				Left = placement.Bounds.Left;
				Top = placement.Bounds.Top;
				Width = placement.Bounds.Width;
				Height = placement.Bounds.Height;
			}

			if (placement.IsMaximized)
				WindowState = WindowState.Maximized;
		}
	}

	/// <summary>
	/// Saves where the window is. A maximized window keeps the place it goes back to when it's no longer maximized, and a
	/// full screen window where it was before it filled the screen, so leaving full screen in a later session goes back
	/// there too.
	/// </summary>
	private void SavePlacement()
	{
		if (m_placementKeyPath is not null)
		{
			var placement = m_fullScreenRestore ?? new WindowPlacement(GetNormalBounds(), WindowState == WindowState.Maximized);
			placement.Save(m_placementKeyPath);
		}
	}

	private const double ResizeBorderThickness = 6;

	private readonly string? m_placementKeyPath;

	// Where the window was before it filled the screen, or null if it isn't full screen.
	private WindowPlacement? m_fullScreenRestore;
	private bool m_isFittingToScreen;

	private static class NativeMethods
	{
		public const int SM_CXFRAME = 32;
		public const int SM_CYFRAME = 33;
		public const int SM_CXPADDEDBORDER = 92;
		public const uint MONITOR_DEFAULTTONEAREST = 2;
		public const uint SWP_NOZORDER = 0x0004;
		public const uint SWP_NOACTIVATE = 0x0010;

		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct MONITORINFO
		{
			public int cbSize;
			public RECT rcMonitor;
			public RECT rcWork;
			public uint dwFlags;
		}

		[DllImport("user32.dll")]
		public static extern int GetSystemMetricsForDpi(int index, uint dpi);

		[DllImport("user32.dll")]
		public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
	}
}
