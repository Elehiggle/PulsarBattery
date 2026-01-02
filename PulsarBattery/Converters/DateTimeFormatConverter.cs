using Microsoft.UI.Xaml.Data;
using System;
using System.Globalization;

namespace PulsarBattery.Converters;

public sealed class DateTimeFormatConverter : IValueConverter
{
    public string Format { get; set; } = "G";

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DateTimeOffset dto)
        {
            return string.Empty;
        }

        var format = parameter as string ?? Format;
        var culture = TryGetCulture(language) ?? CultureInfo.CurrentCulture;
        return dto.ToString(format, culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is not string s)
        {
            return DateTimeOffset.MinValue;
        }

        var culture = TryGetCulture(language) ?? CultureInfo.CurrentCulture;
        return DateTimeOffset.TryParse(s, culture, DateTimeStyles.AssumeLocal, out var dto)
            ? dto
            : DateTimeOffset.MinValue;
    }

    private static CultureInfo? TryGetCulture(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}