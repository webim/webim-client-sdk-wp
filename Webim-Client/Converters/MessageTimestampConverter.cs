using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Webim_Client.Converters
{
    public class MessageTimestampConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(value is DateTime))
            {
                return null;
            }
            DateTime dateTime = (DateTime)value;
            return dateTime.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
