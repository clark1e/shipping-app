using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace BoardmanShipping
{
    public class ContainsCompleteConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = (value as string) ?? string.Empty;
            return status.IndexOf("complete", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("pass", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class GroupSummaryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable items && parameter is string propName)
            {
                try
                {
                    var sum = items
                        .Cast<object>()
                        .Select(o =>
                        {
                            var pi = o.GetType().GetProperty(propName);
                            return pi == null
                                ? 0d
                                : System.Convert.ToDouble(pi.GetValue(o) ?? 0);
                        })
                        .Sum();
                    return sum == Math.Floor(sum)
                        ? ((int)sum).ToString()
                        : sum.ToString("0.0");
                }
                catch { }
            }
            return "0";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class TotalCellBrushConverter : IValueConverter
    {
        private static readonly Brush Purple = new SolidColorBrush(Color.FromRgb(191, 95, 255));
        private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0, 153, 0));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable items)
            {
                try
                {
                    int palletSum = items
                        .Cast<object>()
                        .Select(o =>
                        {
                            var pi = o.GetType().GetProperty("Pallet");
                            return pi != null
                                ? System.Convert.ToInt32(pi.GetValue(o) ?? 0)
                                : 0;
                        })
                        .Sum();
                    return palletSum > 0 ? Purple : Green;
                }
                catch { }
            }
            return Green;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
