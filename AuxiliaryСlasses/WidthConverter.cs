using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ApartmentPlans.AuxiliaryСlasses
{
    public class WidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double windowWidth)
            {
                // Уменьшить ширину окна на 555 пикселей
                return windowWidth - 555;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
