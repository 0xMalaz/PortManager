using System.Globalization;
using System.Windows.Data;
using PortManager.Models;

namespace PortManager.Infrastructure;

/// <summary>
/// Formats a timestamp relative to a reference time, so rows can refresh their "5m ago" text
/// without being rebuilt: only the shared reference time changes.
/// </summary>
public sealed class RelativeTimeConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values is [DateTimeOffset timestamp, DateTimeOffset now]
            ? PortActivityFormatter.Format(timestamp, now)
            : string.Empty;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
