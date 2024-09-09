using System;
using System.Globalization;
using System.Windows.Data;

namespace ApartmentPlans.AuxiliaryСlasses
{
    public class ColumnWidthToComboBoxWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double columnWidth && parameter is string param && double.TryParse(param, out double adjustment))
            {
                return columnWidth - adjustment;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
