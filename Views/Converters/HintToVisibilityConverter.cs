using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HelloGstarCAD.Views.Converters
{
    public class HintToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && string.IsNullOrWhiteSpace(text))
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}