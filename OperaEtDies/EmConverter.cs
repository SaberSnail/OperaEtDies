using System.Globalization;
using System.Windows.Data;

namespace OperaEtDies;

/// <summary>
/// Converts a font size to a length in ems: the font size times the parameter, so sizes such as the side columns' widths
/// scale with the text. Bound to an element's FontSize, it follows the theme's text size.
/// </summary>
public sealed class EmConverter : IValueConverter
{
	public static EmConverter Instance { get; } = new();

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
		value is double fontSize ? fontSize * GetEms(parameter) : 0.0;

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();

	private static double GetEms(object parameter) => parameter switch
	{
		double ems => ems,
		string text => double.Parse(text, CultureInfo.InvariantCulture),
		_ => 1,
	};
}
