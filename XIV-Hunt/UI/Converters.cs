using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace FFXIV_GameSense.UI
{
    class SystemDateTimeFormatConverter : IValueConverter
    {
        private static readonly string SystemDateTimeFormat = GetSysDateTimeFormat();
        private static string GetSysDateTimeFormat()
        {
            CultureInfo ci = NativeMethods.GetSystemDefaultCultureInfo();
            return ci.DateTimeFormat.ShortDatePattern + " " + ci.DateTimeFormat.ShortTimePattern;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime)
            {
                return ((DateTime)value).ToString(SystemDateTimeFormat);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    class ByteToPercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte)
                return (byte)value/100f;
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double)
                return (byte)((double)value * 100);
            return null;
        }
    }
}
