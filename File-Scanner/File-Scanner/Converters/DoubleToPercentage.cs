using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace File_Scanner.Converters
{
    public class DoubleToPercentage : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                double toConvert = (double)value;
                return toConvert.ToString("#0.00000*");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double result = 0.0;
            return double.TryParse((string)value, out result) ? result : 0.0;
        }
    }
}
