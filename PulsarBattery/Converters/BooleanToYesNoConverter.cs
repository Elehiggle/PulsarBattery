using Microsoft.UI.Xaml.Data;
using System;

namespace PulsarBattery.Converters;

public sealed class BooleanToYesNoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? "Yes" : "No";
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is not string s)
        {
            return false;
        }

        return s.Equals("Yes", StringComparison.OrdinalIgnoreCase)
            || s.Equals("Ja", StringComparison.OrdinalIgnoreCase);
    }
}