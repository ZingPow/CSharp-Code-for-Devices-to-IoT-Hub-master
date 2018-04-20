using System;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace RPDevice.Converters
{
    public class FillColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var c = (int)value;
            switch (c)
            {
                case 0:
                    return new SolidColorBrush(Windows.UI.Colors.LightGray);
                case 1:
                    return new SolidColorBrush(Windows.UI.Colors.Red);
                default:
                    return new SolidColorBrush(Windows.UI.Colors.LightGray);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
