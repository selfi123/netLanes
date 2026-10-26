using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NetLanes.Core.Models;
using NetLanes.Core.Utilities;

namespace NetLanes.App.Converters;

public class RateFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return UnitConverter.FormatAdaptiveRate(d);
        }
        if (value is long l)
        {
            return UnitConverter.FormatAdaptiveRate(l);
        }
        return "0 B/s";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ByteCountConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long l)
        {
            return UnitConverter.FormatBytes(l);
        }
        return "0 B";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BooleanToVisibilityInvertedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        return b ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class NullToVisibilityInvertedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class EnforcementStateToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is EnforcementState state)
        {
            return state switch
            {
                EnforcementState.ActiveShaping => new SolidColorBrush(Color.FromRgb(0, 230, 118)),       // Green
                EnforcementState.MonitorOnly => new SolidColorBrush(Color.FromRgb(0, 210, 255)),         // Cyan
                EnforcementState.Paused => new SolidColorBrush(Color.FromRgb(255, 179, 0)),             // Amber
                EnforcementState.ElevationRequired => new SolidColorBrush(Color.FromRgb(255, 82, 82)),  // Coral Red
                EnforcementState.DriverUnavailable => new SolidColorBrush(Color.FromRgb(255, 82, 82)),  // Coral Red
                EnforcementState.SafeOverflowBypass => new SolidColorBrush(Color.FromRgb(255, 145, 0)), // Orange
                _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))                                  // Slate Gray
            };
        }
        return new SolidColorBrush(Color.FromRgb(148, 163, 184));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
