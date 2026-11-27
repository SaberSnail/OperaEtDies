using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OperaEtDies.Controls;

/// <summary>
/// Keeps only one side of a corner radius, "Left" or "Right" (the parameter), for the two halves of a split button.
/// </summary>
public sealed class CornerRadiusSideConverter : IValueConverter
{
	public static CornerRadiusSideConverter Instance { get; } = new();

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is CornerRadius radius
		? parameter as string == "Right" ? new CornerRadius(0, radius.TopRight, radius.BottomRight, 0) : new CornerRadius(radius.TopLeft, 0, 0, radius.BottomLeft)
		: new CornerRadius();

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
