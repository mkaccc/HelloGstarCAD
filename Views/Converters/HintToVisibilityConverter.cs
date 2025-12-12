using System;
using System.Windows;
using System.Windows.Data;
using System.Globalization;

namespace HelloGstarCAD.Views.Converters
{
    public class HintToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string hint = value as string;
            string targetHint = parameter as string;
            
            return (hint == targetHint) ? Visibility.Visible : Visibility.Collapsed;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}