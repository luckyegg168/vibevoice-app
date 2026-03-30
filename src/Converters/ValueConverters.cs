using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VibeVoice.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public string? Parameter { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool bval = value is bool b && b;
        bool invert = (parameter is string s && s == "Invert") || Parameter == "Invert";
        if (invert) bval = !bval;
        return bval ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => parameter;
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNull = value == null || (value is string s && string.IsNullOrEmpty(s));
        bool invert = parameter is string p && p == "Invert";
        if (invert) isNull = !isNull;
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class AudioLevelToWidthConverter : System.Windows.Data.IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2
            && values[0] is float level
            && values[1] is double containerWidth && containerWidth > 0)
        {
            return Math.Max(4, level * containerWidth);
        }
        return 4.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
